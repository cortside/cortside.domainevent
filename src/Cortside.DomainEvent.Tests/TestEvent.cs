using System.Collections.Generic;

namespace Cortside.DomainEvent.Tests {


    public enum StatusEnum {
        New,
        PendingWork,
        Closed
    }

    public class TestEvent {
        public static Dictionary<string, TestEvent> Instances { get; } = new Dictionary<string, TestEvent>();

        public int IntValue { set; get; }
        public string StringValue { set; get; }
        public StatusEnum Status { set; get; }
    }
}
