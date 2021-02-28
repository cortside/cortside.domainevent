using System.Collections.Generic;
using Amqp;
using Amqp.Listener;

namespace Cortside.DomainEvent.Tests.ContainerHostTests {

    public class TestMessageProcessor : IMessageProcessor {
        public TestMessageProcessor()
            : this(20, new List<Message>()) {
        }

        public TestMessageProcessor(int credit, List<Message> messages) {
            this.Credit = credit;
            this.Messages = messages;
        }

        public List<Message> Messages {
            get;
            private set;
        }

        public int Credit {
            get;
            private set;
        }

        public void Process(MessageContext messageContext) {
            if (this.Messages != null) {
                this.Messages.Add(messageContext.Message);
            }

            messageContext.Complete();
        }
    }
}
