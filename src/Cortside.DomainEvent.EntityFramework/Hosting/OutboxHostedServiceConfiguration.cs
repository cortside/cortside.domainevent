namespace Cortside.DomainEvent.EntityFramework.Hosting {
    public class OutboxHostedServiceConfiguration {
        public bool Enabled { get; set; }
        public int Interval { get; set; }
    }
}
