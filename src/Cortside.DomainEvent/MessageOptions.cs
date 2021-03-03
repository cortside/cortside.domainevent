namespace Cortside.DomainEvent {
    public class MessageOptions {
        public string CorrelationId { get; set; }
        public string MessageId { get; set; }
        public string EventType { get; set; }
        public string Address { get; set; }
    }
}
