## Cortside.DomainEvent.Stubs

Stubs allow integration tests code to publish/subscribe to events without using a real message broker.  The stubs implement the same interfaces as the actual implementation making testing seamless without having to make any changes to actual code.

To setup stubs in the integration tests setup do the following:

```csharp
        private void RegisterDomainEventPublisher(IServiceCollection services)
        {
            // Remove the real IDomainEventPublisher
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDomainEventPublisher));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Remove the real IDomainEventReceiver
            descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDomainEventReceiver));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IStubBroker, ConcurrentQueueBroker>();
            services.AddTransient<IDomainEventPublisher, DomainEventPublisherStub>();
            services.AddTransient<IDomainEventReceiver, DomainEventReceiverStub>();
        }
```

And then register it during the WebHostBuilder step:

```csharp
        protected override IHostBuilder CreateHostBuilder()
        {
            // other setup here
            // then create the host
            return Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration(builder =>
                {
                    builder.AddConfiguration(Configuration);
                })
                .ConfigureWebHostDefaults(webbuilder =>
                {
                    webbuilder
                    .UseConfiguration(Configuration)
                    .UseStartup<Startup>()
                    .UseSerilog(Log.Logger)
                    .ConfigureTestServices(sc =>
                    {
                        RegisterDomainEventPublisher(sc);
                        // other custom stuff goes here
                    });
                });
        }
```

Once it's been registered, you can inject the IStubBroker into any test where you want to verify messages have been published or consumed:

```csharp
    public class TestClass : IClassFixture<IntegrationTestFactory<Startup>>
    {
        private readonly IntegrationTestFactory<Startup> fixture;
        private readonly HttpClient testServerClient;
        private readonly IStubBroker broker;

        public TestClass(IntegrationTestFactory<Startup> fixture)
        {
            this.fixture = fixture;
            testServerClient = fixture.CreateClient();
            broker = fixture.Services.GetService<IStubBroker>();
        }

        [Fact]
        public void SomeTest()
        {
            // do something to generate messages
            // check that the message was consumed
            // it's a good practice to wait for a bit for the async publishing/receiving to happen
            await AsyncUtil.WaitUntilAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token, () => !broker.HasItems);
            // or
            await Task.Delay(10000);
            Assert.False(broker.HasItems);

            // grab the messages by type
            var myMessages = broker.GetAcceptedMessagesByType<MyDomainEventType>(x => x.Id == 1); // grab our specific message from the completed list
            Assert.NotNull(myMessages);
        }
    }
```
