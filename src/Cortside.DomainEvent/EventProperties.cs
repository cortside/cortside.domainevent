using System;
using System.Collections.Generic;

namespace Cortside.DomainEvent
{
    public class EventProperties
    {
        public string CorrelationId { get; set; }
        public string MessageId { get; set; }
        public string EventType { get; set; }
        public string Topic { get; set; }
        public string RoutingKey { get; set; }

        public string Address => Topic + RoutingKey;

        public DateTime? CreationTime { get; set; }
        public byte Priority { get; set; }
        public Dictionary<string, object> ApplicationProperties { get; set; }
    }
}
