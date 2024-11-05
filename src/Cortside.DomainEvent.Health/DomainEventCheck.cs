using Cortside.Health;
using Cortside.Health.Checks;
using Cortside.Health.Enums;
using Cortside.Health.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Cortside.DomainEvent.Health {
    public class DomainEventCheck : Check {
        private readonly IDomainEventReceiver receiver;

        public DomainEventCheck(IMemoryCache cache, ILogger<Check> logger, IAvailabilityRecorder recorder, IDomainEventReceiver receiver) : base(cache, logger, recorder) {
            this.receiver = receiver;
        }

        public override async Task<ServiceStatusModel> ExecuteAsync() {
            var serviceStatusModel = new ServiceStatusModel() {
                Healthy = false,
                Required = check.Required,
                Timestamp = DateTime.UtcNow,
                Statistics = Statistics.Instance
            };

            if (receiver.Error != null) {
                serviceStatusModel.Status = ServiceStatus.Failure;
                serviceStatusModel.StatusDetail = $"{receiver.Error.Condition}:{receiver.Error.Description} with exception {receiver.Error.Exception.Message}";
                return serviceStatusModel;
            }

            serviceStatusModel.Healthy = true;
            serviceStatusModel.Status = ServiceStatus.Ok;
            serviceStatusModel.StatusDetail = "Successful";
            return serviceStatusModel;
        }
    }
}
