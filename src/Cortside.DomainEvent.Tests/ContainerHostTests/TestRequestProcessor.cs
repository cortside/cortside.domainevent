using System.Threading;
using Amqp;
using Amqp.Listener;

namespace Cortside.DomainEvent.Tests.ContainerHostTests {
    public partial class BaseHostTest {
        public class TestRequestProcessor : IRequestProcessor {
            int totalCount;

            public TestRequestProcessor() {
            }

            public int Credit {
                get { return 100; }
            }

            public int TotalCount {
                get { return this.totalCount; }
            }

            public void Process(RequestContext requestContext) {
                int id = Interlocked.Increment(ref this.totalCount);
                requestContext.Complete(new Message("OK" + id));
            }
        }
    }
}
