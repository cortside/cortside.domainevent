namespace Cortside.DomainEvent {
    public class DomainEventPublisherSettings : DomainEventSettings {
        /// <summary>
        /// Gets or sets the address. Topic (azure SB) or exchange/queue (RabbitMQ)
        /// </summary>
        /// <value>
        /// The address.
        /// </value>
        /// <remarks>
        /// When used in the Publisher, this is used as a prefix
        /// where the type name of the event class is appended to the Address.
        /// Topics and exchange keys should be named appropriately.
        /// When used in the Receiver
        /// Azure SB {topic}/Subscriptions/{subscription}
        /// RabbitMQ {queue}
        /// </remarks>
        public string Topic { set; get; }
    }
}
