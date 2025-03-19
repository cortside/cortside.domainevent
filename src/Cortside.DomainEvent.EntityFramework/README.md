## Cortside.DomainEvent.EntityFramework

Cortside.DomainEvent.EntityFramework is an implementation of a transactional outbox pattern using EntityFramework.  See description of  [Transactional Outbox Pattern](https://github.com/cortside/guidelines/blob/master/docs/architecture/Messaging.md#transactional-outbox-pattern) from the Cortside Guidelines repository.

The outbox can be easily added in Startup.cs:

```csharp
// add domain event publish with outbox
services.AddDomainEventOutboxPublisher<DatabaseContext>(Configuration);
```

In classes that need to be able to publish an outbox message inject `IDomainEventOutboxPublisher` instead of `IDomainEventPublisher`.  Use Publish methods as you would for direct publishing.  Make sure to call SaveChanges on your db context so that the message ends up in the outbox table.  If you have your entity changes that relate to the event in the change tracker, SaveChanges will save them together, getting atomic transactions with event publication.

```
OutboxHostedService": {
    "BatchSize":  10,
    "Enabled": true,
    "Interval": 5,
    "PurgePublished": true,
    "MaximumPublishCount": 10,
    "PublishRetryInterval": 60,
    "Overrides": [
        {
            "EventType": "MyEvent",
            "MaximumPublishCount": 5
        },
        {
            "EventType": "OtherEvent",
            "MaximumPublishCount": 10
        }
    ]
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
