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
            if (stoppingToken.IsCancellationRequested) {
                throw new OperationCanceledException(stoppingToken);
            }

            await Task.Yield();

            if (!settings.Enabled) {
                logger.LogInformation("{ServiceKey} ReceiverHostedService is not enabled", serviceKey);
            } else if (settings.MessageTypes == null) {
                logger.LogError("Configuration error:  No event types have been configured for the {ServiceKey} receiverhostedeservice", serviceKey);
            } else {
                while (!stoppingToken.IsCancellationRequested) {
                    if (receiver == null || receiver.Link?.IsClosed != false) {
                        DisposeReceiver();
                        receiver ??= services.GetService<IDomainEventReceiver>();
                        logger.LogInformation("Starting receiver... {ServiceKey}", serviceKey);
                        try {
                            receiver.StartAndListen(settings.MessageTypes);
                            logger.LogInformation("{ServiceKey} Receiver started", serviceKey);
                        } catch (Exception e) {
                            logger.LogCritical(e, "Unable to start receiver {ServiceKey}. \n {E}", serviceKey, e);
                        }
                        receiver.Closed += OnReceiverClosed;
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

        private void OnReceiverClosed(IDomainEventReceiver receiver, DomainEventError error) {
            if (error == null) {
                logger.LogError("{ServiceKey} Handling OnReceiverClosed event with no error information", serviceKey);
            } else {
                logger.LogError("{ServiceKey} Handling OnReceiverClosed event with error: {Condition} - {Description}", serviceKey, error.Condition, error.Description);
            }
        }

        private void DisposeReceiver() {
            receiver?.Close();
        }

        public override void Dispose() {
            DisposeReceiver();
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~ReceiverHostedService() {
            Dispose();
        }
    }
}
