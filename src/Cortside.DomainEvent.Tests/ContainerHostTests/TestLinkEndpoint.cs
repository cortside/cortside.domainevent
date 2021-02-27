using Amqp;
using Amqp.Listener;

namespace Cortside.DomainEvent.Tests {
    public partial class BaseHostTest {
        public class TestLinkEndpoint : LinkEndpoint {
            public override void OnMessage(MessageContext messageContext) {
                messageContext.Complete();
            }

            public override void OnFlow(FlowContext flowContext) {
                for (int i = 0; i < flowContext.Messages; i++) {
                    var message = new Message("test message");
                    flowContext.Link.SendMessage(message, message.Encode());
                }
            }

            public override void OnDisposition(DispositionContext dispositionContext) {
                dispositionContext.Complete();
            }
        }
    }
}
