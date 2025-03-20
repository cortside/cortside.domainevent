using System;

namespace Cortside.DomainEvent {
    public interface IDomainEventPublisherSession : IDomainEventPublisher, IDisposable {
    }
}
