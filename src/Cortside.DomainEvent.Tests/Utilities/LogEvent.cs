using Microsoft.Extensions.Logging;

namespace Cortside.DomainEvent.Tests.Utilities {
    public class LogEvent {
        public LogLevel LogLevel { get; internal set; }
        public string Message { get; internal set; }
    }
}
