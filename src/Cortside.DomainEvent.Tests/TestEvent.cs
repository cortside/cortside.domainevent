using System.Collections.Generic;

namespace Cortside.DomainEvent.Tests
{
    public enum Status
    {
        New,
        PendingWork,
        Closed
    }

    public class TestEvent
    {
        public static Dictionary<string, DomainEventMessage<TestEvent>> Instances { get; } = new Dictionary<string, DomainEventMessage<TestEvent>>();

        public int IntValue { set; get; }
        public string StringValue { set; get; }
        public Status Status { set; get; }
    }
}
