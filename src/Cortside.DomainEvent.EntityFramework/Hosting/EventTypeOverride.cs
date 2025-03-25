namespace Cortside.DomainEvent.EntityFramework.Hosting {
    public class EventTypeOverride {
        public string EventType { get; set; }
        public int MaximumPublishCount { get; set; }
    }
}
