using System;

namespace Cortside.DomainEvent {
    /// <summary>
    /// Global statistics for the domain event system
    /// </summary>
    public sealed class Statistics {
        private static readonly Lazy<Statistics> lazy =
            new Lazy<Statistics>(() => new Statistics());

        public static Statistics Instance { get { return lazy.Value; } }

        public int Received { get; private set; }
        public int Accepted { get; private set; }
        public int Rejected { get; private set; }
        public int Released { get; private set; }
        public int Retries { get; private set; }


        public DateTime? LastReceived { get; private set; }
        public DateTime? LastAccepted { get; private set; }
        public DateTime? LastRejected { get; private set; }
        public DateTime? LastReleased { get; private set; }
        public DateTime? LastRetried { get; private set; }

        public int Queued { get; private set; }
        public DateTime? LastQueued { get; private set; }

        public int Published { get; private set; }
        public int PublishFailed { get; private set; }
        public DateTime? LastPublished { get; private set; }

        public void Receive() {
            Received++;
            LastReceived = DateTime.UtcNow;
        }

        public void Accept() {
            Accepted++;
            LastAccepted = DateTime.UtcNow;
        }
        public void Reject() {
            Rejected++;
            LastRejected = DateTime.UtcNow;
        }
        public void Release() {
            Released++;
            LastReleased = DateTime.UtcNow;
        }

        public void Retry() {
            Retries++;
            LastRetried = DateTime.UtcNow;
        }

        public void Queue() {
            Queued++;
            LastQueued = DateTime.UtcNow;
        }

        public void Publish(bool success = true) {
            Published++;
            LastPublished = DateTime.UtcNow;

            if (!success) {
                PublishFailed++;
            }
        }
    }
}
