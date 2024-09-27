using System.Threading.Tasks;
using Amqp;
using Cortside.DomainEvent.EntityFramework;
using Microsoft.Extensions.Logging;

namespace Cortside.DomainEvent.Stub {
    public class DomainEventOutboxPublisherStub : BaseDomainEventPublisher, IDomainEventOutboxPublisher {
        private readonly IStubBroker queue;

        public DomainEventOutboxPublisherStub(DomainEventPublisherSettings settings, ILogger<DomainEventOutboxPublisherStub> logger, IStubBroker queue) : base(settings, logger) {
            this.queue = queue;
        }

        protected override Task SendAsync(Message message, EventProperties properties) {
            queue.Enqueue(message);
            Logger.LogInformation($"Published message {message.Properties.MessageId}");
            return Task.CompletedTask;
        }
    }
}
