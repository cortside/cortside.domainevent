using System.Threading.Tasks;
using Amqp;
using Microsoft.Extensions.Logging;

namespace Cortside.DomainEvent.Stub {
    public class DomainEventPublisherStub : BaseDomainEventPublisher, IDomainEventPublisherSession {
        private readonly IStubBroker queue;

        public DomainEventPublisherStub(DomainEventPublisherSettings settings, ILogger<DomainEventPublisherStub> logger, IStubBroker queue) : base(settings, logger) {
            this.queue = queue;
        }

        public override IDomainEventPublisherSession BeginSession() {
            return this;
        }

        protected override Task SendAsync(Message message, EventProperties properties) {
            queue.Enqueue(message);
            Statistics.Instance.Publish();
            Logger.LogInformation($"Published message {message.Properties.MessageId}");
            return Task.CompletedTask;
        }

        public void Dispose() {
            // nothing to do
        }
    }
}
