namespace Cortside.DomainEvent.EntityFramework {
    public class OutboxHostedServiceConfiguration {
        public bool Enabled { get; set; }
        public int Interval { get; set; }
    }
}
