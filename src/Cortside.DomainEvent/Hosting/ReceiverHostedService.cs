using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cortside.DomainEvent.Hosting {
    /// <summary>
    /// Message receiver hosted service
    /// </summary>
    public class ReceiverHostedService : BackgroundService {
        private readonly ILogger logger;
        private readonly IServiceProvider services;
        private readonly ReceiverHostedServiceSettings settings;
        private readonly string serviceKey = "";
        private IDomainEventReceiver receiver;

        /// <summary>
        /// Message receiver hosted service
        /// </summary>
        public ReceiverHostedService(ILogger<ReceiverHostedService> logger, IServiceProvider services, ReceiverHostedServiceSettings settings) {
            this.logger = logger;
            this.services = services;
            this.settings = settings;
        }

        public ReceiverHostedService(ILogger<ReceiverHostedService> logger, IServiceProvider services, ReceiverHostedServiceSettings settings, KeyedDomainEventReceiverSettings receiverSettings) {
            this.logger = logger;
            this.services = services;
            this.settings = settings;
            this.serviceKey = receiverSettings.Key;
            this.receiver = services.GetKeyedService<IDomainEventReceiver>(receiverSettings.Key);
        }

        public override Task StartAsync(CancellationToken cancellationToken) {
            logger.LogInformation("{ServiceKey} ReceiverHostedService StartAsync() entered.", serviceKey);
            return base.StartAsync(cancellationToken);
        }

        /// <summary>
        /// Interface method to start service
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            stoppingToken.ThrowIfCancellationRequested();

            await Task.Yield();

            if (!settings.Enabled) {
                logger.LogInformation("[{ServiceKey}] ReceiverHostedService is not enabled", serviceKey);
            } else if (settings.MessageTypes == null) {
                logger.LogError("Configuration error:  No event types have been configured for the [{ServiceKey}] ReceiverHostedService", serviceKey);
            } else {
                while (!stoppingToken.IsCancellationRequested) {
                    if (receiver is not { Link.IsClosed: false }) {
                        LogLocalState("receiver is null or link is NOT closed");
                        DisposeReceiver();
                        if (receiver is null) {
                            logger.LogDebug("Receiver is null, creating a new instance.");
                            receiver ??= services.GetService<IDomainEventReceiver>();
                            receiver.Closed += OnReceiverClosed;
                        }

                        logger.LogInformation("Starting receiver... {ServiceKey}", serviceKey);
                        try {
                            receiver.StartAndListen(settings.MessageTypes);
                            logger.LogInformation("{ServiceKey} Receiver started", serviceKey);
                        } catch (Exception e) {
                            logger.LogCritical(e, "Unable to start receiver {ServiceKey}. \n {E}", serviceKey, e);
                        }

                    }
                    await Task.Delay(TimeSpan.FromSeconds(settings.TimedInterval), stoppingToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Interface method to stop service
        /// </summary>
        public override Task StopAsync(CancellationToken cancellationToken) {
            logger.LogInformation("{ServiceKey} Receiver Hosted Service is stopping.", serviceKey);
            DisposeReceiver();
            return Task.CompletedTask;
        }

        private void LogLocalState(string prefix = "") {
            logger?.LogDebug("{Prefix} ReceiverId: {ReceiverId}, LinkId: {LinkId}, LinkState: {LinkState}, Link.IsClosed: {IsClosed}",
                prefix, receiver?.GetHashCode() ?? -1, receiver?.Link?.GetHashCode() ?? -1, receiver?.Link?.LinkState, receiver?.Link?.IsClosed);
        }

        private void OnReceiverClosed(IDomainEventReceiver closingReceiver, DomainEventError error) {
            if (error == null) {
                logger.LogError("{ServiceKey} Handling OnReceiverClosed event with no error information", serviceKey);
            } else {
                logger.LogError("{ServiceKey} Handling OnReceiverClosed event with error: {Condition} - {Description}", serviceKey, error.Condition, error.Description);
            }

            if (closingReceiver == null) {
                logger.LogDebug("closingReceiver is null");
                return;
            }

            //*jwS* not yet - closingReceiver.Closed -= OnReceiverClosed
            LogLocalState("calling closingReceiver.Close");
            closingReceiver?.Close();
        }

        private void DisposeReceiver() {
            LogLocalState("DisposeReceiver called.");
            receiver?.Close();
            //*jwS* not yet - receiver = null
        }

        public override void Dispose() {
            DisposeReceiver();
            GC.SuppressFinalize(this);
            base.Dispose();
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~ReceiverHostedService() {
            Dispose();
        }
    }
}
