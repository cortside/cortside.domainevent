namespace Cortside.DomainEvent {
    public class KeyedDomainEventReceiverSettings : DomainEventReceiverSettings {
        public string Key { get; set; }
        public string Server { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string ConnectionString => $"{Protocol}://{Username}:{Password}@{Server}/";
    }
}
