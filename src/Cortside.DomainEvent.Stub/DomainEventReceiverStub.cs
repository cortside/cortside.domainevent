using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amqp;
using Cortside.Common.Correlation;
using Cortside.Common.Threading;
using Cortside.DomainEvent.Handlers;
using Microsoft.Extensions.Logging;

namespace Cortside.DomainEvent.Stub {
    public class DomainEventReceiverStub : IDomainEventReceiver {
        public ReceiverLink Link { get; protected set; }
        public DomainEventError Error => null;

        private readonly IServiceProvider provider;
        private readonly DomainEventReceiverSettings settings;
        private readonly ILogger<DomainEventReceiverStub> logger;
        private readonly IStubBroker receiver;
        private IDictionary<string, Type> eventTypeLookup;

        public DomainEventReceiverStub(DomainEventReceiverSettings settings, IServiceProvider provider, ILogger<DomainEventReceiverStub> logger, IStubBroker queue) {
            this.provider = provider;
            this.settings = settings;
            this.logger = logger;
            receiver = queue;
        }

        public event ReceiverClosedCallback Closed;

        public void Close(TimeSpan? timeout = null) {
            // do nothing
        }

        public void StartAndListen(IDictionary<string, Type> eventTypeLookup) {
            InternalStart(eventTypeLookup);

            var thread = new Thread(Listen);
            thread.Start();
        }

        private void Listen() {
            while (true) {
                while (receiver.HasItems) {
                    var message = receiver.Peek();

                    // not sure how this ever happens but it does
                    if (message == null) {
                        receiver.Dequeue();
                        continue;
                    }
                    var messageTypeName = message.ApplicationProperties[Constants.MESSAGE_TYPE_KEY] as string;
                    if (eventTypeLookup?.ContainsKey(messageTypeName) == false) {
                        receiver.EnqueueUnmapped(message);
                        receiver.Dequeue();
                    } else {
                        AsyncUtil.RunSync(() => OnMessageCallbackAsync(message));
                    }
                }
                Thread.Sleep(500);
            }
        }

        private void InternalStart(IDictionary<string, Type> eventTypeLookup) {
            logger.LogInformation($"Starting {GetType().Name} for {settings.Service}");

            this.eventTypeLookup = eventTypeLookup;
            logger.LogInformation($"Registering {eventTypeLookup.Count} event types:");
            foreach (var pair in eventTypeLookup) {
                logger.LogInformation($"{pair.Key} = {pair.Value}");
            }
        }

        private async Task OnMessageCallbackAsync(Message message) {
            Statistics.Instance.Receive();

            var messageTypeName = message.ApplicationProperties[Constants.MESSAGE_TYPE_KEY] as string;
            var properties = new Dictionary<string, object> {
                ["CorrelationId"] = message.Properties.CorrelationId,
                ["MessageId"] = message.Properties.MessageId,
                ["MessageType"] = messageTypeName
            };

            // if message has correlationId, set it so that handling can be found by initial correlation
            if (!string.IsNullOrWhiteSpace(message.Properties.CorrelationId)) {
                CorrelationContext.SetCorrelationId(message.Properties.CorrelationId);
            }

            using (logger.BeginScope(properties)) {
                logger.LogInformation($"Received message {message.Properties.MessageId}");

                try {
                    string body = DomainEventMessage.GetBody(message);
                    logger.LogTrace("Received message {MessageId} with body: {MessageBody}", message.Properties.MessageId, body);

                    logger.LogDebug($"Event type key: {messageTypeName}");
                    if (!eventTypeLookup.ContainsKey(messageTypeName)) {
                        logger.LogError($"Message {message.Properties.MessageId} rejected because message type was not registered for type {messageTypeName}");
                        receiver.Reject(message);
                        Statistics.Instance.Reject();
                        return;
                    }

                    var dataType = eventTypeLookup[messageTypeName];
                    logger.LogDebug($"Event type: {dataType}");
                    var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(dataType);
                    logger.LogDebug($"Event type handler interface: {handlerType}");
                    var handler = provider.GetService(handlerType);
                    if (handler == null) {
                        logger.LogError($"Message {message.Properties.MessageId} rejected because handler was not found for type {messageTypeName}");
                        receiver.Reject(message);
                        Statistics.Instance.Reject();
                        return;
                    }
                    logger.LogDebug($"Event type handler: {handler.GetType()}");

                    dynamic domainEvent;
                    try {
                        domainEvent = DomainEventMessage.CreateGenericInstance(dataType, message);
                        logger.LogDebug($"Successfully deserialized body to {dataType}");
                    } catch (Exception ex) {
                        logger.LogError(ex, ex.Message);
                        receiver.Reject(message);
                        Statistics.Instance.Reject();
                        return;
                    }

                    HandlerResult result;
                    dynamic dhandler = handler;
                    try {
                        result = await dhandler.HandleAsync(domainEvent).ConfigureAwait(false);
                    } catch (Exception ex) {
                        logger.LogError(ex, $"Message {message.Properties.MessageId} caught unhandled exception {ex.Message}");
                        result = HandlerResult.Failed;
                    }
                    logger.LogInformation($"Handler executed for message {message.Properties.MessageId} and returned result of {result}");

                    switch (result) {
                        case HandlerResult.Success:
                            receiver.Accept(message);
                            Statistics.Instance.Accept();
                            logger.LogInformation($"Message {message.Properties.MessageId} accepted");
                            break;
                        case HandlerResult.Retry:
                            var deliveryCount = message.Header.DeliveryCount;
                            var delay = 10 * deliveryCount;
                            var scheduleTime = DateTime.UtcNow.AddSeconds(delay);

                            // create a new message to be queued with scheduled delivery time
                            var retry = new Message(body) {
                                Header = message.Header,
                                Footer = message.Footer,
                                Properties = message.Properties,
                                ApplicationProperties = message.ApplicationProperties
                            };
                            retry.ApplicationProperties[Constants.SCHEDULED_ENQUEUE_TIME_UTC] = scheduleTime;
                            receiver.Enqueue(retry);
                            receiver.Accept(message);
                            Statistics.Instance.Retry();
                            logger.LogInformation($"Message {message.Properties.MessageId} requeued with delay of {delay} seconds for {scheduleTime}");
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
                            throw new NotImplementedException($"Unknown HandlerResult value of {result}");
                    }
                } catch (Exception ex) {
                    logger.LogError(ex, $"Message {message.Properties.MessageId} rejected because of unhandled exception {ex.Message}");
                    receiver.Reject(message);
                    Statistics.Instance.Reject();
                }
            }
        }
    }
}
