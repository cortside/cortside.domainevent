using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cortside.Common.Correlation;
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

            var correlationId = CorrelationContext.GetCorrelationId();
            if (@event.Data.IntValue == Int32.MaxValue && correlationId != @event.CorrelationId) {
                throw new ArgumentException($"CorrelationId {@event.CorrelationId} should equal {correlationId}");
            }

            using (logger.BeginScope(properties)) {
                TestEvent.Instances.Add(@event.MessageId, @event);

                // intentionally cause exception, used to assert unhandled exception handling
                if (@event.Data.IntValue == int.MinValue) {
                    throw new ArgumentException("IntValue is int.MinValue");
                }

                await Task.Delay(10).ConfigureAwait(false);

                if (@event.Data.IntValue > 0) {
                    return HandlerResult.Success;
                } else if (@event.Data.IntValue == 0 && @event.DeliveryCount > 1) {
                    return HandlerResult.Success;
                } else if (@event.Data.IntValue == 0) {
                    return HandlerResult.Retry;
                }

                return HandlerResult.Failed;
            }
        }
    }
}
