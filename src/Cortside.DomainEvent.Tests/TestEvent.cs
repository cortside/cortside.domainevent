using System.Collections.Generic;
using System.Linq;

namespace Cortside.DomainEvent.Tests {
    public enum Status {
        New,
        PendingWork,
        Closed
    }

    public class TestEvent {
        public static Dictionary<string, DomainEventMessage<TestEvent>> Instances { get; } = new Dictionary<string, DomainEventMessage<TestEvent>>();

        public int IntValue { set; get; }
        public string StringValue { set; get; }
        public Status Status { set; get; }

        public static DomainEventMessage<TestEvent> GetByCorrelationId(string correlationId) {
            return Instances.SingleOrDefault(x => x.Value.CorrelationId == correlationId).Value;
        }
    }
}
