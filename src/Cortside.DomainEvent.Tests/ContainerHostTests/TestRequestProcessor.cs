using System.Threading;
using Amqp;
using Amqp.Listener;

namespace Cortside.DomainEvent.Tests.ContainerHostTests {
    public class TestRequestProcessor : IRequestProcessor {
        int totalCount;

        public TestRequestProcessor() {
        }

        public int Credit {
            get { return 100; }
        }

        public int TotalCount {
            get { return totalCount; }
        }

        public void Process(RequestContext requestContext) {
            int id = Interlocked.Increment(ref totalCount);
            requestContext.Complete(new Message("OK" + id));
        }
    }
}
