using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Transactions;
using Cortside.DomainEvent.Tests;
using Cortside.DomainEvent.Tests.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cortside.DomainEvent.IntegrationTests
{
    public class E2ETransactionTest : E2EBase
    {
        [Fact]
        public async Task ShouldBeAbleToSendAndReceiveAsync()
        {
            if (enabled)
            {
                var @event = NewTestEvent();
                var correlationId = Guid.NewGuid().ToString();
                try
                {
                    await publisher.PublishAsync(@event, correlationId).ConfigureAwait(false);
                }
                finally
                {
                    Assert.Null(publisher.Error);
                }

                EventMessage message;
                var logger = new MockLogger<DomainEventReceiver>();
                using (var receiver = new DomainEventReceiver(receiverSettings, serviceProvider, logger))
                {
                    receiver.Start(eventTypes);
                    message = await receiver.ReceiveAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                    message?.Accept();
                }
                Assert.DoesNotContain(logger.LogEvents, x => x.LogLevel == LogLevel.Error);

                Assert.NotNull(message);
                Assert.Equal(correlationId, message.Message.CorrelationId);
                Assert.NotNull(message.Message.MessageId);
                Assert.NotNull(message.Message.EventType);

                Assert.Equal(@event.StringValue, ((TestEvent)message.Message.Data).StringValue);
                Assert.Equal(@event.IntValue, ((TestEvent)message.Message.Data).IntValue);
            }
        }

        /// <summary>
        /// domainevent version of amqptransaction test TransactedRetiringAndPosting
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ShouldUseTransactionScopeAsync()
        {
            var s = Guid.NewGuid().ToString();
            if (enabled)
            {
                const int nMsgs = 10;
                var ids = new List<int>();

                for (int i = 0; i < nMsgs; i++)
                {
                    var @event = new TestEvent()
                    {
                        IntValue = i,
                        StringValue = s
                    };
                    ids.Add(i);
                    await publisher.PublishAsync(@event).ConfigureAwait(false);
                }

                var receiver = new DomainEventReceiver(receiverSettings, serviceProvider, new NullLogger<DomainEventReceiver>());
                receiver.Start(eventTypes);

                Assert.NotNull(receiver.Link?.Session);

                // create a new publisher that will share the same session
                publisher = new DomainEventPublisher(publisherSettings, new NullLogger<DomainEventPublisher>(), receiver.Link.Session);

                receiver.Link.SetCredit(2, false);
                var message1 = await receiver.ReceiveAsync().ConfigureAwait(false);
                var message2 = await receiver.ReceiveAsync().ConfigureAwait(false);

                await Console.Out.WriteLineAsync($"message1: {message1.GetData<TestEvent>().IntValue}").ConfigureAwait(false);
                await Console.Out.WriteLineAsync($"message2: {message2.GetData<TestEvent>().IntValue}").ConfigureAwait(false);

                // ack message1 and send a new message in a txn
                using (var ts = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    message1.Accept();
                    ids.Remove(message1.GetData<TestEvent>().IntValue);

                    var @event = new TestEvent() { IntValue = nMsgs + 1, StringValue = s };
                    await publisher.PublishAsync(@event).ConfigureAwait(false);
                    ids.Add(@event.IntValue);

                    ts.Complete();
                }

                Assert.Equal(nMsgs, ids.Count);

                // ack message2 and send a new message in a txn but abort the txn
                using (var ts = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    message2.Accept();
                    await publisher.PublishAsync(message2.GetData<TestEvent>()).ConfigureAwait(false);
                }

                // release the message, since it shouldn't have been accepted above
                message2.Release();
                await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

                Assert.Equal(nMsgs, ids.Count);

                // receive all messages. should see the effect of the first txn
                receiver.Link.SetCredit(nMsgs, false);
                for (int i = 0; i < nMsgs; i++)
                {
                    var message = await receiver.ReceiveAsync().ConfigureAwait(false);
                    message.Accept();

                    Assert.Contains(message.GetData<TestEvent>().IntValue, ids);
                    ids.Remove(message.GetData<TestEvent>().IntValue);
                }

                // at this point, the queue should have zero messages.
                // If there are messages, it is a bug in the broker.
                Assert.Empty(ids);

                // shouldn't be any messages left
                var empty = await receiver.ReceiveAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                if (empty != null)
                {
                    empty.Accept();
                    Assert.Equal(-1, empty.GetData<TestEvent>().IntValue);
                }
                Assert.Null(empty);

                receiver.Close();
            }
        }
    }
}
