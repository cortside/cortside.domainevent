namespace Cortside.DomainEvent {
    public class DomainEventReceiverSettings : DomainEventSettings {
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
        public string Queue { set; get; }
        /// <summary>
        /// Gets or sets a type name for all events expected to be received.
        /// </summary>
        /// <value>
        /// The name of the type registered when adding a handler.
        /// </value>
        /// <remarks>
        /// This is only useful and necessary when receiving messages from an
        /// amqp instance where the message publisher is not using this library
        /// and is not adding an ApplicationProperty to the message with the type name.
        /// </remarks>
        public string SingleTypeName { get; set; }
    }
}
