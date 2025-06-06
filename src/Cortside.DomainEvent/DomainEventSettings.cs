using System;

namespace Cortside.DomainEvent {
    public abstract class DomainEventSettings {
        /// <summary>
        /// Gets or sets the name of the service.
        /// </summary>
        /// <value>
        /// The name of the service, used in logging and for constructing session name.
        /// </value>
        public string Service { set; get; }

        [Obsolete("Use Service instead")]
        public string AppName {
            set { Service = value; }
            get { return Service; }
        }

        /// <summary>
        /// Gets or sets the protocol, amqp or ampqs.
        /// </summary>
        /// <value>
        /// The protocol.
        /// </value>
        public string Protocol { set; get; }

        /// <summary>
        /// Gets or sets the name of the shared access policy (Azure SB) or username (RabbitMQ).
        /// </summary>
        /// <value>
        /// The name of the policy.
        /// </value>
        public string Policy { set; get; }

        [Obsolete("Use Policy instead")]
        public string PolicyName {
            set { Policy = value; }
            get { return Policy; }
        }

        /// <summary>
        /// Gets or sets the key (Azure SB) or password (RabbitMQ).
        /// </summary>
        /// <value>
        /// The key.
        /// </value>
        /// <remarks>
        /// The key for Azure SB is from the shared access policy.
        /// Amqpnetlite will not accept a '/' in the key
        /// </remarks>
        public string Key { set; get; }

        /// <summary>
        /// Gets or sets the namespace url (Azure SB) or host (RabbitMQ)
        /// </summary>
        /// <value>
        /// The namespace.
        /// </value>
        public string Namespace { set; get; }

        /// <summary>
        /// Gets or sets the credits 
        /// </summary>
        /// <value>
        /// The credits.
        /// </value>
        /// <remarks>
        /// This is only used in the Receiver, to limit the number of simultaneous retrievals of messages.
        /// </remarks>
        public int Credits { set; get; } = 10;

        /// <summary>
        /// Set durability of queues and messages
        /// </summary>
        /// <value>
        /// 0 = Transient, 1 = Durable
        /// </value>
        /// <remarks>
        /// Default value is 1.  Rabbit MQ will reject connection if these
        /// settings do not match.  Azure SB does not seem to care what this
        /// setting is.
        /// </remarks>
        public uint Durable { set; get; } = 1;

        public string ConnectionString => $"{Protocol}://{Policy}:{Key}@{Namespace}/";
    }
}
