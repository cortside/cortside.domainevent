using System.Collections.Generic;
using System.Threading.Tasks;
using Cortside.DomainEvent.Handlers;
using Microsoft.Extensions.Logging;

namespace Cortside.DomainEvent.Tests {
    public class TestEventHandler : IDomainEventHandler<TestEvent> {
        private readonly ILogger<TestEventHandler> logger;

        public TestEventHandler(ILogger<TestEventHandler> logger) {
            this.logger = logger;
        }

        public async Task<HandlerResult> HandleAsync(DomainEventMessage<TestEvent> @event) {
            var properties = new Dictionary<string, object> {
                ["CorrelationId"] = @event.CorrelationId,
                ["MessageId"] = @event.MessageId,
                ["MessageType"] = @event.MessageTypeName
            };

            using (logger.BeginScope(properties)) {
                TestEvent.Instances.Add(@event.MessageId, @event.Data);
                TestEvent.Instances.Add(@event.CorrelationId, @event.Data);

                if (@event.Data.IntValue == int.MinValue) {
                    var x = 0;
                    _ = 1 / x;
                }

                if (@event.Data.IntValue > 0) {
                    return HandlerResult.Success;
                } else if (@event.Data.IntValue == 0) {
                    return HandlerResult.Retry;
                } else {
                    return HandlerResult.Failed;
                }
            }
        }
    }
}
