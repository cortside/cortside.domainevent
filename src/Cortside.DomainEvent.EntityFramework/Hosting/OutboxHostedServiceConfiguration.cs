namespace Cortside.DomainEvent.EntityFramework.Hosting {
    public class OutboxHostedServiceConfiguration {
        public int BatchSize { get; set; }
        public bool Enabled { get; set; }
        public int Interval { get; set; }
        public bool PurgePublished { get; set; }
        public int MaximumPublishCount { get; set; } = 10;
        public int PublishRetryInterval { get; set; } = 60;
    }
}
