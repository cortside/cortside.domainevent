using System.Threading.Tasks;

namespace Cortside.DomainEvent {
    public interface IDomainEventHandler<T> where T : class {
        Task<HandlerResult> HandleAsync(DomainEventMessage<T> @event);
    }
}
