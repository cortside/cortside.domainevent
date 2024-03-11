namespace Cortside.DomainEvent.Tests.Events {
    public class ActionEvent : ResourceIdEvent {
        public int Quantity { set; get; }
    }
}
