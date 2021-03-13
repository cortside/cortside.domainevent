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
            deadletterMessage = new List<Message>();
        }

        public int Count {
            get {
                lock (messages) {
                    return messages.Count;
                }
            }
        }

        public int DeadLetterCount {
            get {
                lock (deadletterMessage) {
                    return deadletterMessage.Count;
                }
            }
        }

        // Not thread safe
        public IList<Message> DeadletterMessage {
            get { return deadletterMessage; }
        }

        public Task<ReceiveContext> GetMessageAsync(ListenerLink link) {
            lock (messages) {
                ReceiveContext context = null;
                if (messages.Count > 0) {
                    context = new ReceiveContext(link, messages.Dequeue());
                }

                var tcs = new TaskCompletionSource<ReceiveContext>();
                tcs.SetResult(context);
                return tcs.Task;
            }
        }

        public void DisposeMessage(ReceiveContext receiveContext, DispositionContext dispositionContext) {
            if (dispositionContext.DeliveryState is Rejected) {
                lock (deadletterMessage) {
                    deadletterMessage.Add(receiveContext.Message);
                }
            } else if (dispositionContext.DeliveryState is Released) {
                lock (messages) {
                    messages.Enqueue(receiveContext.Message);
                }
            }

            dispositionContext.Complete();
        }
    }
}
