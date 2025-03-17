namespace Cortside.DomainEvent.Handlers {
    /// <summary>
    /// Enum representing the result of a handler operation.
    /// </summary>
    public enum HandlerResult {
        /// <summary>
        /// Handling of the event was successful and the event will be consumed
        /// </summary>
        Success,
        /// <summary>
        /// Handling of the event was unsuccessful and the event will be rejected.
        /// Rejected events will be moved to the dead letter queue based on broker configuration.
        /// </summary>
        Failed,
        /// <summary>
        /// Handling of the event was unsuccessful and the event should be retried again.
        /// This result will currently operate the same as Failed.
        /// </summary>
        /// <remarks>
        /// Transactioal handling in Amqp.Net Lite is not yet supported consistently.
        /// </remarks>
        Retry,
        /// <summary>
        /// The handler has chosen to release the event back to the queue where it will be delivered to another consumer.
        /// </summary>
        Release
    }
}
