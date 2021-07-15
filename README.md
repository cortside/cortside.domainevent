[![Build status](https://ci.appveyor.com/api/projects/status/43l1ckgn806lqxjx?svg=true)](https://ci.appveyor.com/project/cortside/cortside-domainevent)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=cortside_cortside.common&metric=alert_status)](https://sonarcloud.io/dashboard?id=cortside_cortside.domainevent)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=cortside_cortside.domainevent&metric=coverage)](https://sonarcloud.io/dashboard?id=cortside_cortside.domainevent)

## Cortside.DomainEvent

Classes for sending and listening to a message bus. Uses AMQPNETLITE (AMQP 1. 0 protocol).

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

```
    "Publisher.Settings": {
        "Protocol": "amqps",
        "Namespace": "namespace.servicebus.windows.net",
        "Policy": "Send",
        "Key": "44CharBASE64EncodedNoSlashes",
        "AppName": "test.publisher",
        "Topic": "topic.",
        "Durable": "0"
    },
    "Receiver.Settings": {
        "Protocol": "amqps",
        "Namespace": "namespace.servicebus.windows.net",
        "Policy": "Listen",
        "Key": "44CharBASE64EncodedNoSlashes",
        "AppName": "test.receiver",
        "Queue": "queue.testReceive",
        "Durable": "0"
    }
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
        "PurgePublished": true
      }
```

### sql to create table if not using migrations
```` sql
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
        CONSTRAINT [PK_Outbox] PRIMARY KEY ([MessageId])
    );

   CREATE INDEX [IX_ScheduleDate_Status] ON [dbo].[Outbox] ([ScheduledDate], [Status]) INCLUDE ([EventType]);
````

## migration from cortside.common.domainevent to cortside.domainevent

- DomainEventPublisher change in publish method
  - SendAsync to PublishAsync
    - overloads that took messageId should now use the overload with EventProperties
  - ScheduleMessageAsync to ScheduleAsync
  - overrides for both PublishAsync and ScheduleAsync use EventProperties for overrides that allowed for EventType or Topic
- namespaces all dropped common
  - make sure to check logging overrides for namespaces that might have changed
- namespace for handler interface changed to be in Handlers
- namespace for ReceiverHostedService changed to be in Hosting
- ReceiverHostedServiceSettings.Disabled changed to Enabled
- ReceiverHostedServiceSettings.TimedInterval should be specified in seconds, not milliseconds
- IDomainEventHandler HandleAsync now has return value of HandlerResultEnum
  - To keep current functionality, return HandlerResultEnum.Success and let uncaught exceptions trigger HandlerResultEnum.Failure result
- Publisher uses `Logger<DomainEventPublisher>` instead of `Logger<DomainEventComms>`
- Reciever uses `Logger<DomainEventReceiver>` instead of `Logger<DomainEventComms>`
- ServiceBusPublisherSettings renamed to DomainEventPublisherSettings
  - changed Address to Topic
- ServiceBusReceiverSettings renamed to DomainEventReceiverSettings
  - changed Address to Queue
- receiverHostedServiceSettings now has property for message type lookup dictionary named MessageTypes
 

## Transactions
- See E2ETransactionTest for use of transactions for accept/reject/release and publish operations

## examples

- https://github.com/cortside/cortside.webapistarter

## todo:

- publisher should return published message information -- at least messageId -- would make debugging easier
- allow publisher to be used to publish multiple events withing a using statement without having to create new connection for each publish
