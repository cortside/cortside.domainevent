using System;

namespace Cortside.DomainEvent {
    public class EventMapping {
        public EventMapping() { }

        public EventMapping(Type @event, Type asType, Type handler) {
            Event = @event;
            AsType = asType;
            Handler = handler;
        }

        public Type Event { get; set; }
        public Type AsType { get; set; }
        public Type Handler { get; set; }
    }
}
