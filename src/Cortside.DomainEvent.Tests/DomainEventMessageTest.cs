using Xunit;

namespace Cortside.DomainEvent.Tests {
    public class DomainEventMessageTest {

        [Fact]
        public void ShouldBeAbleToAssignData() {
            // act
            var @event = new DomainEventMessage() { Data = new TestEvent() };

            // assert
            Assert.IsType<TestEvent>(@event.Data);
        }

        [Fact]
        public void ShouldBeAbleToAssignTData() {
            // act
            var @event = new DomainEventMessage<TestEvent>() { Data = new TestEvent() };

            // assert
            Assert.IsType<TestEvent>(@event.Data);
            Assert.IsType<TestEvent>(((DomainEventMessage)@event).Data);
        }
    }
}
