using System;
using System.Collections.Generic;

namespace Cortside.DomainEvent.Hosting {
    /// <summary>
    /// Settings for the ReceiverHostedService
    /// </summary>
    public class ReceiverHostedServiceSettings {
        /// <summary>
        /// Message types for the Domain Event Receiver to handle
        /// </summary>
        public Dictionary<string, Type> MessageTypes { get; set; }

        /// <summary>
        /// Controls whether receiver hosted service is enabled
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Frequency which the receiver attempts to connect to the message broker in seconds
        /// </summary>
        public int TimedInterval { get; set; }
    }
}
