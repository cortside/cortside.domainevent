using System;

namespace Cortside.DomainEvent.EntityFramework.IntegrationTests.Events {
    public class WidgetStateChangedEvent {
        public int WidgetId { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
