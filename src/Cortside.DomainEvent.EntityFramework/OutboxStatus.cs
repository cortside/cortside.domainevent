namespace Cortside.DomainEvent.EntityFramework {
    public enum OutboxStatus {
        Queued,
        Publishing,
        Published,
        Failed
    }
}
