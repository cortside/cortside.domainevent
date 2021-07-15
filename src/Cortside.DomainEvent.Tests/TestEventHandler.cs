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
                TestEvent.Instances.Add(@event.MessageId, @event);

                // intentionally cause exception
                if (@event.Data.IntValue == int.MinValue) {
                    var x = 0;
                    _ = 1 / x;
                }

                var result = HandlerResult.Failed;

                if (@event.Data.IntValue > 0) {
                    result = HandlerResult.Success;
                } else if (@event.Data.IntValue == 0 && @event.DeliveryCount > 1) {
                    result = HandlerResult.Success;
                } else if (@event.Data.IntValue == 0) {
                    result = HandlerResult.Retry;
                }

                return result;
            }
        }
    }
}
