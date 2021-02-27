namespace Cortside.DomainEvent {

    public enum HandlerResult {
        Success,
        Failed,
        Retry,
        Release
    };
}
