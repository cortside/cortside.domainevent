using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cortside.DomainEvent.Tests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cortside.DomainEvent.IntegrationTests {
    public class E2E : E2EBase {

        [Fact]
        public async Task ShouldBeAbleToSendAndReceive() {
            if (enabled) {
                var @event = new TestEvent {
                    IntValue = r.Next(),
                    StringValue = Guid.NewGuid().ToString()
                };

                var correlationId = Guid.NewGuid().ToString();
                try {
                    await publisher.PublishAsync(@event, correlationId);
                } finally {
                    Assert.Null(publisher.Error);
                }

                ReceiveAndWait(correlationId);

                Assert.DoesNotContain(mockLogger.LogEvents, x => x.LogLevel == LogLevel.Error);

                Assert.True(TestEvent.Instances.Any());
                Assert.True(TestEvent.Instances.ContainsKey(correlationId));
                Assert.NotNull(TestEvent.Instances[correlationId]);
                Assert.Equal(@event.StringValue, TestEvent.Instances[correlationId].StringValue);
                Assert.Equal(@event.IntValue, TestEvent.Instances[correlationId].IntValue);
            }
        }

        [Fact]
        public async Task ShouldBeAbleToScheduleAndReceive() {
            if (enabled) {
                var @event = new TestEvent {
                    IntValue = r.Next(),
                    StringValue = Guid.NewGuid().ToString()
                };

                var correlationId = Guid.NewGuid().ToString();
                try {
                    await publisher.ScheduleAsync(@event, DateTime.UtcNow.AddSeconds(20), correlationId);
                } finally {
                    Assert.Null(publisher.Error);
                }

                var elapsed = ReceiveAndWait(correlationId);

                Assert.DoesNotContain(mockLogger.LogEvents, x => x.LogLevel == LogLevel.Error);

                Assert.True(elapsed.TotalSeconds >= 17, $"{elapsed.TotalSeconds} >= 17");
                Assert.True(TestEvent.Instances.Any());
                Assert.True(TestEvent.Instances.ContainsKey(correlationId));
                Assert.NotNull(TestEvent.Instances[correlationId]);
                Assert.Equal(@event.StringValue, TestEvent.Instances[correlationId].StringValue);
                Assert.Equal(@event.IntValue, TestEvent.Instances[correlationId].IntValue);
            }
        }

        private TimeSpan ReceiveAndWait(string correlationId) {
            var tokenSource = new CancellationTokenSource();
            var start = DateTime.Now;

            using (var receiver = new DomainEventReceiver(receiverSettings, serviceProvider, new NullLogger<DomainEventReceiver>())) {
                receiver.Closed += (r, e) => tokenSource.Cancel();
                receiver.StartAndListen(eventTypes);

                while (!TestEvent.Instances.ContainsKey(correlationId) && (DateTime.Now - start) < new TimeSpan(0, 0, 30)) {
                    if (tokenSource.Token.IsCancellationRequested) {
                        if (receiver.Error != null) {
                            Assert.Equal(string.Empty, receiver.Error.Description);
                            Assert.Equal(string.Empty, receiver.Error.Condition);
                        }
                        Assert.True(receiver.Error == null);
                    }
                    Thread.Sleep(1000);
                } // run for 30 seconds
            }

            return DateTime.Now.Subtract(start);
        }
    }
}
