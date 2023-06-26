using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Cortside.DomainEvent.Tests {
    public class DomainEventPublisherTest {
        [Theory]
        [InlineData("ServiceBus:Topic")]
        [InlineData("ServiceBus:Exchange")]
        public void ShouldParseTopic(string key) {
            // arrange
            var value = Guid.NewGuid().ToString();
            var dictionary = new Dictionary<string, string> {
                {key, value},
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(dictionary)
                .Build();

            // act
            var settings = configuration.GetSection("ServiceBus").Get<DomainEventPublisherSettings>();

            // assert
            Assert.Equal(value, settings.Topic);
        }

        [Fact]
        public void ShouldHandleExchange() {
            // arrange
            var value = Guid.NewGuid().ToString();

            // act
            var settings = new DomainEventPublisherSettings() { Exchange = value };

            // assert
            Assert.Equal(value, settings.Topic);
        }
    }
}
