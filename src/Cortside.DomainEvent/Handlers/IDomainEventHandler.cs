using System.Threading.Tasks;

namespace Cortside.DomainEvent.Handlers {
    public interface IDomainEventHandler<T> where T : class {
        Task<HandlerResult> HandleAsync(DomainEventMessage<T> @event);
    }
}
