using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Amqp;
using Amqp.Framing;
using Cortside.Common.Correlation;
using Cortside.DomainEvent.Handlers;
using Microsoft.Extensions.Logging;

namespace Cortside.DomainEvent {
    public class DomainEventReceiver : IDomainEventReceiver, IDisposable {
        public event ReceiverClosedCallback Closed;

        public IServiceProvider Provider { get; protected set; }
        public IDictionary<string, Type> EventTypeLookup { get; protected set; }
        public ReceiverLink Link { get; protected set; }
        public DomainEventError Error { get; set; }

        protected DomainEventReceiverSettings Settings { get; }

        protected ILogger<DomainEventReceiver> Logger { get; }

        private readonly string connectionString;

        protected virtual Session CreateSession() {
            var conn = new Connection(new Address(connectionString));
            return new Session(conn);
        }

        public DomainEventReceiver(DomainEventReceiverSettings settings, IServiceProvider provider, ILogger<DomainEventReceiver> logger) {
            Provider = provider;
            Settings = settings;
            Logger = logger;
            connectionString = settings.ConnectionString;
        }

        public DomainEventReceiver(KeyedDomainEventReceiverSettings settings, IServiceProvider provider, ILogger<DomainEventReceiver> logger) {
            Provider = provider;
            Settings = settings;
            Logger = logger;
            connectionString = settings.ConnectionString;
        }

        public void Start(IDictionary<string, Type> eventTypeLookup) {
            InternalStart(eventTypeLookup);
        }

        public void StartAndListen(IDictionary<string, Type> eventTypeLookup) {
            InternalStart(eventTypeLookup);

            Link.Start(Settings.Credits, (link, msg) => {
                // fire and forget
                _ = OnMessageCallbackAsync(link, msg);
            });
        }

        private void InternalStart(IDictionary<string, Type> eventTypeLookup) {
            if (Link != null) {
                throw new InvalidOperationException("Already receiving.");
            }

            Logger.LogInformation($"Starting {GetType().Name} for {Settings.Service}");

            EventTypeLookup = eventTypeLookup;
            Logger.LogInformation($"Registering {eventTypeLookup.Count} event types:");
            foreach (var pair in eventTypeLookup) {
                Logger.LogInformation($"{pair.Key} = {pair.Value}");
            }

            Error = null;
            var session = CreateSession();
            var attach = new Attach() {
                Source = new Source() {
                    Address = Settings.Queue,
                    Durable = Settings.Durable
                },
                Target = new Target() {
                    Address = null
                }
            };
            Link = new ReceiverLink(session, Settings.Service, attach, null);
            Link.Closed += OnClosed;
        }

        protected void OnClosed(IAmqpObject sender, Error error) {
            if (sender.Error != null) {
                Error = new DomainEventError {
                    Condition = sender.Error.Condition.ToString(),
                    Description = sender.Error.Description
                };
            }
            Closed?.Invoke(this, Error);
        }

        public Task<EventMessage> ReceiveAsync() {
            return ReceiveAsync(TimeSpan.FromSeconds(60));
        }

        public async Task<EventMessage> ReceiveAsync(TimeSpan timeout) {
            Message message = await Link.ReceiveAsync(timeout).ConfigureAwait(false);
            if (message == null) {
                return null;
            }

            Statistics.Instance.Receive();

            var messageTypeName = GetMessageTypeName(message);
            if (!EventTypeLookup.ContainsKey(messageTypeName)) {
                Logger.LogError($"Message {message.Properties.MessageId} rejected because message type was not registered for type {messageTypeName}");
                Link.Reject(message);
                Statistics.Instance.Reject();
                return null;
            }

            var dataType = EventTypeLookup[messageTypeName];
            Logger.LogDebug($"Event type: {dataType}");

            dynamic domainEvent;
            try {
                domainEvent = DomainEventMessage.CreateGenericInstance(dataType, message);
                Logger.LogDebug($"Successfully deserialized body to {dataType}");
            } catch (Exception ex) {
                Logger.LogError(ex, ex.Message);
                Link.Reject(message);
                Statistics.Instance.Reject();
                return null;
            }

            return new EventMessage(domainEvent, message, Link);
        }

        protected async Task OnMessageCallbackAsync(IReceiverLink receiver, Message message) {
            Statistics.Instance.Receive();

            var messageTypeName = GetMessageTypeName(message);
            var properties = new Dictionary<string, object> {
                ["CorrelationId"] = message.Properties.CorrelationId,
                ["MessageId"] = message.Properties.MessageId,
                ["MessageType"] = messageTypeName
            };

            // if message has correlationId, set it so that handling can be found by initial correlation
            if (!string.IsNullOrWhiteSpace(message.Properties.CorrelationId)) {
                CorrelationContext.SetCorrelationId(message.Properties.CorrelationId);
            }

            var timer = new Stopwatch();
            timer.Start();

            using (Logger.BeginScope(properties)) {
                Logger.LogInformation($"Received message {message.Properties.MessageId}");

                try {
                    string body = DomainEventMessage.GetBody(message);
                    Logger.LogTrace("Received message {MessageId} with body: {MessageBody}", message.Properties.MessageId, body);

                    Logger.LogDebug($"Event type key: {messageTypeName}");
                    if (!EventTypeLookup.ContainsKey(messageTypeName)) {
                        Logger.LogError($"Message {message.Properties.MessageId} rejected because message type was not registered for type {messageTypeName}");
                        receiver.Reject(message);
                        Statistics.Instance.Reject();
                        return;
                    }

                    var dataType = EventTypeLookup[messageTypeName];
                    Logger.LogDebug($"Event type: {dataType}");
                    var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(dataType);
                    Logger.LogDebug($"Event type handler interface: {handlerType}");
                    var handler = Provider.GetService(handlerType);
                    if (handler == null) {
                        Logger.LogError($"Message {message.Properties.MessageId} rejected because handler was not found for type {messageTypeName}");
                        receiver.Reject(message);
                        Statistics.Instance.Reject();
                        return;
                    }
                    Logger.LogDebug($"Event type handler: {handler.GetType()}");

                    dynamic domainEvent;
                    try {
                        domainEvent = DomainEventMessage.CreateGenericInstance(dataType, message);
                        Logger.LogDebug($"Successfully deserialized body to {dataType}");
                    } catch (Exception ex) {
                        Logger.LogError(ex, ex.Message);
                        receiver.Reject(message);
                        Statistics.Instance.Reject();
                        return;
                    }

                    HandlerResult result;
                    dynamic dhandler = handler;
                    try {
                        result = await dhandler.HandleAsync(domainEvent).ConfigureAwait(false);
                    } catch (Exception ex) {
                        Logger.LogError(ex, $"Message {message.Properties.MessageId} caught unhandled exception {ex.Message}");
                        result = HandlerResult.Failed;
                    }

                    timer.Stop();
                    var duration = timer.ElapsedMilliseconds;

                    var dp = new Dictionary<string, object> {
                        ["duration"] = duration
                    };

                    using (Logger.BeginScope(dp)) {
                        Logger.LogInformation($"Handler executed for message {message.Properties.MessageId} and returned result of {result}");

                        switch (result) {
                            case HandlerResult.Success:
                                receiver.Accept(message);
                                Statistics.Instance.Accept();
                                Logger.LogInformation($"Message {message.Properties.MessageId} accepted");
                                break;

                            case HandlerResult.Retry:
                                // until i can figure out how to have a similar safe way to handle accept and requeue
                                // without transactions, disabling this functionality.
                                // https://github.com/cortside/cortside.domainevent/issues/21
                                Logger.LogInformation($"Message {message.Properties.MessageId} being failed instead of expected retry.  See issue https://github.com/cortside/cortside.domainevent/issues/21");
                                receiver.Reject(message);
                                Statistics.Instance.Reject();
                                break;
                            case HandlerResult.Failed:
                                receiver.Reject(message);
                                Statistics.Instance.Reject();
                                break;
                            case HandlerResult.Release:
                                receiver.Release(message);
                                Statistics.Instance.Release();
                                break;
                            default:
                                throw new ArgumentOutOfRangeException($"Unknown HandlerResult value of {result}");
                        }
                    }
                } catch (Exception ex) {
                    timer.Stop();
                    var duration = timer.ElapsedMilliseconds;

                    var dp = new Dictionary<string, object> {
                        ["duration"] = duration
                    };

                    using (Logger.BeginScope(dp)) {
                        Logger.LogError(ex, $"Message {message.Properties.MessageId} rejected because of unhandled exception {ex.Message}");
                        receiver.Reject(message);
                        Statistics.Instance.Reject();
                    }
                }
            }
        }

        private string GetMessageTypeName(Message message) {
            return string.IsNullOrEmpty(Settings.UndefinedTypeName) ? message.ApplicationProperties[Constants.MESSAGE_TYPE_KEY] as string : Settings.UndefinedTypeName;
        }

        public void Close(TimeSpan? timeout = null) {
            timeout ??= TimeSpan.Zero;
            Link?.Session.Close(timeout.Value);
            Link?.Session.Connection.Close(timeout.Value);
            Link?.Close(timeout.Value);
            Link = null;
            Error = null;
            EventTypeLookup = null;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            Close();
        }
    }
}
