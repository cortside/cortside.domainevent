using System;
using Amqp.Listener;

namespace Cortside.DomainEvent.Tests.ContainerHostTests {

    public class TestLinkProcessor : ILinkProcessor {
        Func<AttachContext, bool> attachHandler;
        readonly Func<ListenerLink, LinkEndpoint> factory;

        public TestLinkProcessor() {
        }

        public TestLinkProcessor(Func<ListenerLink, LinkEndpoint> factory) {
            this.factory = factory;
        }

        public void SetHandler(Func<AttachContext, bool> attachHandler) {
            this.attachHandler = attachHandler;
        }

        public void Process(AttachContext attachContext) {
            if (attachHandler != null && attachHandler(attachContext)) {
                return;
            }

            attachContext.Complete(
                factory != null ? factory(attachContext.Link) : new TestLinkEndpoint(),
                attachContext.Attach.Role ? 0 : 30);
        }
    }

}
