using System;
using System.Threading.Tasks;

namespace Cortside.DomainEvent {

    public delegate void PublisherClosedCallback(IDomainEventPublisher publisher, DomainEventError error);

    public interface IDomainEventPublisher {
        event PublisherClosedCallback Closed;
        DomainEventError Error { get; set; }

        Task PublishAsync<T>(T @event) where T : class;
        Task PublishAsync<T>(T @event, string correlationId) where T : class;
        Task PublishAsync<T>(T @event, EventProperties properties) where T : class;
        Task PublishAsync(string body, EventProperties properties);

        Task ScheduleAsync<T>(T @event, DateTime scheduledEnqueueTimeUtc) where T : class;
        Task ScheduleAsync<T>(T @event, DateTime scheduledEnqueueTimeUtc, string correlationId) where T : class;
        Task ScheduleAsync<T>(T @event, DateTime scheduledEnqueueTimeUtc, EventProperties properties) where T : class;
        Task ScheduleAsync(string body, DateTime scheduledEnqueueTimeUtc, EventProperties properties);
    }
}
