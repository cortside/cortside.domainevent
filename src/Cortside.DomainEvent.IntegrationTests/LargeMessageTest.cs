using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Cortside.DomainEvent.Tests;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Cortside.DomainEvent.IntegrationTests {
    public class LargeMessageTest : E2EBase {
        [Theory,
         InlineData(10000, true),
         InlineData(1000000, true),
         InlineData(1200000, false),
         InlineData(30000000, false)
        ]
        public async Task LargeMessage(int size, bool pass) {
            //The received message(delivery - id:0, size: 1113762 bytes) exceeds the limit(1048576 bytes) currently allowed on the link.TrackingId:bf3067d1 - d4e2 - 447a - b0cd - fe8a04b4c94b_B2, SystemTracker: NoSystemTracker, Timestamp: 2024 - 07 - 23T20: 37:56
            if (enabled) {
                var @event = new TestEvent {
                    IntValue = r.Next() + 1,
                    StringValue = string.Concat(Enumerable.Repeat("x", size))
                };

                var correlationId = Guid.NewGuid().ToString();
                try {
                    await publisher.PublishAsync(@event, correlationId);
                } catch (Exception ex) {
                    Assert.Contains(publisher.Error.Description, ex.InnerException?.Message);
                } finally {
                    if (pass) {
                        Assert.Null(publisher.Error);
                        Assert.DoesNotContain(publisherLogger.LogEvents, x => x.LogLevel == LogLevel.Error);
                    } else {
                        Assert.NotNull(publisher.Error);
                        Assert.Contains("exceeds the limit", publisher.Error.Description);
                    }
                }

                if (pass) {
                    var received = await ReceiveAndWaitAsync(correlationId, 40);

                    Assert.NotNull(received);
                    Assert.Equal(@event.StringValue, received.Data.StringValue);
                    Assert.Equal(@event.IntValue, received.Data.IntValue);
                    Assert.Equal(size, @event.StringValue.Length);
                }
            }
        }

        private async Task<DomainEventMessage<TestEvent>> ReceiveAndWaitAsync(string correlationId, int timeout) {
            var stopWatch = Stopwatch.StartNew();

            using (var receiver = new DomainEventReceiver(receiverSettings, serviceProvider, receiverLogger)) {
                receiver.StartAndListen(eventTypes);

                while (TestEvent.GetByCorrelationId(correlationId) == null && stopWatch.Elapsed.TotalSeconds < timeout) {
                    Assert.DoesNotContain(receiverLogger.LogEvents, x => x.LogLevel == LogLevel.Error);
                    Assert.True(receiver.Error == null);
                    await Task.Delay(1000);
                }
            }

            stopWatch.Stop();
            Assert.DoesNotContain(receiverLogger.LogEvents, x => x.LogLevel == LogLevel.Error);
            return TestEvent.GetByCorrelationId(correlationId);
        }
    }
}
