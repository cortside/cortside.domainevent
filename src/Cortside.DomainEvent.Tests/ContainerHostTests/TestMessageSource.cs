using System.Collections.Generic;
using System.Threading.Tasks;
using Amqp;
using Amqp.Framing;
using Amqp.Listener;

namespace Cortside.DomainEvent.Tests.ContainerHostTests {
    public class TestMessageSource : IMessageSource {
        readonly Queue<Message> messages;
        readonly List<Message> deadletterMessage;

        public TestMessageSource(Queue<Message> messages) {
            this.messages = messages;
            this.deadletterMessage = new List<Message>();
        }

        public int Count {
            get {
                lock (this.messages) {
                    return this.messages.Count;
                }
            }
        }

        public int DeadLetterCount {
            get {
                lock (this.deadletterMessage) {
                    return this.deadletterMessage.Count;
                }
            }
        }

        // Not thread safe
        public IList<Message> DeadletterMessage {
            get { return this.deadletterMessage; }
        }

        public Task<ReceiveContext> GetMessageAsync(ListenerLink link) {
            lock (this.messages) {
                ReceiveContext context = null;
                if (this.messages.Count > 0) {
                    context = new ReceiveContext(link, this.messages.Dequeue());
                }

                var tcs = new TaskCompletionSource<ReceiveContext>();
                tcs.SetResult(context);
                return tcs.Task;
            }
        }

        public void DisposeMessage(ReceiveContext receiveContext, DispositionContext dispositionContext) {
            if (dispositionContext.DeliveryState is Rejected) {
                lock (this.deadletterMessage) {
                    this.deadletterMessage.Add(receiveContext.Message);
                }
            } else if (dispositionContext.DeliveryState is Released) {
                lock (this.messages) {
                    this.messages.Enqueue(receiveContext.Message);
                }
            }

            dispositionContext.Complete();
        }
    }
}
