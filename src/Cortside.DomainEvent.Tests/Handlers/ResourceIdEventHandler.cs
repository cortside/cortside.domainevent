using System.Collections.Generic;
using System.Threading.Tasks;
using Cortside.DomainEvent.Handlers;
using Cortside.DomainEvent.Tests.Events;
using Microsoft.Extensions.Logging;

namespace Cortside.DomainEvent.Tests.Handlers {
    public class ResourceIdEventHandler : IDomainEventHandler<ResourceIdEvent> {
        private readonly ILogger<ResourceIdEventHandler> logger;

        public ResourceIdEventHandler(ILogger<ResourceIdEventHandler> logger) {
            this.logger = logger;
        }

        public async Task<HandlerResult> HandleAsync(DomainEventMessage<ResourceIdEvent> @event) {
            var properties = new Dictionary<string, object> {
                ["CorrelationId"] = @event.CorrelationId,
                ["MessageId"] = @event.MessageId,
                ["MessageType"] = @event.MessageTypeName,
                ["ResourceId"] = @event.Data.ResourceId
            };

            using (logger.BeginScope(properties)) {
                // TODO: something

                logger.LogInformation("The message body was {body}", @event.Data);

                return HandlerResult.Success;
            }
        }
    }
}
