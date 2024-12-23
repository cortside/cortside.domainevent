[![Build status](https://ci.appveyor.com/api/projects/status/43l1ckgn806lqxjx?svg=true)](https://ci.appveyor.com/project/cortside/cortside-domainevent)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=cortside_cortside.common&metric=alert_status)](https://sonarcloud.io/dashboard?id=cortside_cortside.domainevent)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=cortside_cortside.domainevent&metric=coverage)](https://sonarcloud.io/dashboard?id=cortside_cortside.domainevent)

## Cortside.DomainEvent

Classes for sending and listening to a message bus. Uses AMQPNETLITE (AMQP 1. 0 protocol).  Has been used and tested with Azure Service Bus and RabbitMQ 3.x with the AMQP 1.0 plugin.

The DomainEvent library relies on some conventions to make a bus work without conflicts, such as:

* each service has a single queue to listen to
* only 1 service listens to a queue
* each service will publish events to topics
* topics may or may not have subscribers
* each subscriber will "forward" an event to a queue
* events can be published to the broker immediately or can be published to an outbox for another process to publish to the broker
    * outbox can participate in db transaction
    * outbox publish can be retried in the case there is an issue with broker availability
* handlers can accept (event will be consumed and removed from queue) or Failed (event will be rejected and sent to dead letter queue)

### Terminology and naming convention:

| Term | RabbitMQ | Axure Service Bus | Format | Example |
|---|---|---|---|---|
| Queue | Queue | Queue | {service}.queue | shoppingcart.queue |
| Topic | Exchange | Topic | {service}.{event} | shoppingcart.orderstatechangedevent |
| Subscription | Bindings | Subscription | {forwardservice}.subscription | communication.subscription |


- rabbit mq example
    - 3.x and 4.x

### Azure ServiceBus

#### General

- Authorization keys cannot contain '/'. They must be regenerated if they do. AMQPNETLITE does not like that value.
- I found inconsistent behavior if the topic and queue were created using the AzureSB UI. I had success creating the topics, subscriptions, queues using ServiceBusExplorer (https://github.com/paolosalvatori/ServiceBusExplorer/releases)

#### Queues

- Names of queues cannot be single worded. Should be multipart (eg. auth.queue).

#### Topic

- The forward to setting for the topic subscription is not visible in the azure UI. You can use ServiceBusExplorer to set that field.

#### Example

- for the following configuration settings for the test project with a TestEvent object

RabbitMQ

```json
  "ServiceBus": {
    "Service": "shoppingcart",
    "Protocol": "amqp",
    "Namespace": "localhost",
    "Policy": "admin",
    "Key": "password",
    "Queue": "shoppingcart.queue",
    "Topic": "/exchange/shoppingcart/",
    "Durable": "1",
    "Credits": 5
  },
```

Azure Service Bus

```json
"ServiceBus": {
    "Service": "shoppingcart",
    "Protocol": "amqps",
    "Namespace": "acme.servicebus.windows.net",
    "Policy": "SendListen",
    "Key": "secret",
    "Queue": "shoppingcart.queue",
    "Topic": "shoppingcart.",
    "Durable": "1",
    "Credits": 5
}
```

```json
  "OutboxHostedService": {
    "BatchSize": 5,
    "Enabled": true,
    "Interval": 5,
    "PurgePublished": true,
    "MaximumPublishCount": 10,
    "PublishRetryInterval": 60
  }
```

```json
  "ReceiverHostedService": {
    "Enabled": true,
    "TimedInterval": 60
  },
```

**(for test default settings from Service Bus Explorer are fine unless specified below)**

- Azure Service Bus Components:
  - a queue named queue.TestReceive
    - new authorization rule for queue
      - claimType = SharedAccessKey
      - claimValue = none
      - KeyName = "Listen"
      - Primary/Secondary Key = 44 Char BASE64 encoded string (33 char unencoded and remember no '/')
      - Manage - off
      - Send - off
      - Listen - on
  - a topic named topic.TestEvent
    - new authorization rule for topic
      - claimType = SharedAccessKey
      - claimValue = none
      - KeyName = "Send"
      - Primary/Secondary Key = 44 Char BASE64 encoded string (33 char unencoded and remember no '/')
      - Manage - off
      - Send - on
      - Listen - off
  - a subscription to topic.TestEvent named subscription.TestEvent
    - The "Forward To" setting for this subscription needs to be set to queue.TestReceive

## Outbox Pattern using Cortside.DomainEvent.EntityFramework

What To Do:

- will probably want to set deduplication at the message broker since same messageId will be used
- will need to generate ef migration after adding following to OnModelCreating method in dbcontext class:
  - modelBuilder.AddDomainEventOutbox();
  - https://github.com/cortside/cortside.webapistarter/blob/outbox/src/Cortside.WebApiStarter.Data/Migrations/20210228035338_DomainEventOutbox.cs
- register IDomainEventOutboxPublisher AND IDomainEventPublisher
  - use IDomainEventOutboxPublisher in classes that will publish to the outbox
  - SaveChanges after calling PublishAsync or ScheduleAsync -- and make part of transaction or workset in db so that publish becomes atomic with db changes
  - OutboxHostedService needs IDomainEventPublisher to actually publish to message broker
- register OutboxHostedService to publish messages from db to broker
- if publishing an entity id in message, might need to add a using around the work with a transaction and call savechanges twice if the entity id is assigned by the db
- add section for configuration:

```
OutboxHostedService": {
    "BatchSize":  10,
    "Enabled": true,
    "Interval": 5,
    "PurgePublished": true,
    "MaximumPublishCount": 10,
    "PublishRetryInterval": 60
}
```

### sql to create table if not using migrations

```sql
    CREATE TABLE [dbo].[Outbox] (
        [MessageId] nvarchar(36) NOT NULL,
        [CorrelationId] nvarchar(36) NULL,
        [EventType] nvarchar(250) NOT NULL,
        [Topic] nvarchar(100) NOT NULL,
        [RoutingKey] nvarchar(100) NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [Status] nvarchar(10) NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [ScheduledDate] datetime2 NOT NULL,
        [PublishedDate] datetime2 NULL,
        [LockId] nvarchar(36) NULL,
        [PublishCount] int NOT NULL DEFAULT 0,
        CONSTRAINT [PK_Outbox] PRIMARY KEY ([MessageId])
    );

   CREATE INDEX [IX_ScheduleDate_Status] ON [dbo].[Outbox] ([ScheduledDate], [Status]) INCLUDE ([EventType]);
```

## Transactions

- See E2ETransactionTest for use of transactions for accept/reject/release and publish operations

## Cortside.DomainEvent.Stubs

- Stubs allow integration tests code to publish/subscribe to events without using a real message broker
- To setup stubs in the integration tests setup do the following:

```
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

```
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

```
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

## Cortside.DomainEvent.Stubs

- Stubs allow integration tests code to publish/subscribe to events without using a real message broker
- To setup stubs in the integration tests setup do the following:

```
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

```
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

```
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

## examples

- https://github.com/cortside/coeus/tree/develop/shoppingcart-api

## todo:

- publisher should return published message information -- at least messageId -- would make debugging easier
- allow publisher to be used to publish multiple events withing a using statement without having to create new connection for each publish
