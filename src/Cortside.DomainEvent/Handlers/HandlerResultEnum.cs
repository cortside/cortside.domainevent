namespace Cortside.DomainEvent.Handlers {

    public enum HandlerResult {
        Success,
        Failed,
        Retry,
        Release
    };
}
