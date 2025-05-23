[![Build status](https://ci.appveyor.com/api/projects/status/43l1ckgn806lqxjx?svg=true)](https://ci.appveyor.com/project/cortside/cortside-domainevent)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=cortside_cortside.domainevent&metric=alert_status)](https://sonarcloud.io/dashboard?id=cortside_cortside.domainevent)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=cortside_cortside.domainevent&metric=coverage)](https://sonarcloud.io/dashboard?id=cortside_cortside.domainevent)

## Cortside.DomainEvent

Classes for publishing and consuming events on a message bus. Relies on the AMQP 1.0 protocol and can communicate with any broker that supports AMQP 1.0. Has been used and tested with Azure Service Bus and RabbitMQ 3.x with the AMQP 1.0 plugin.

The DomainEvent library relies on some conventions to make a bus work without conflicts, such as:

- Each service has a single queue to listen to (v6 - v8 supports multiple connections)
- Only 1 service listens to a given queue (v6)
- Each service will publish events to topics
- Topics may or may not have subscribers
- Each subscriber will "forward" an event to a queue
- Events can be published to the broker immediately or can be published to an outbox for another process to publish to the broker
  - Publication to outbox can participate in database transactions
  - Outbox publish can be retried in the case there is an issue with broker availability
- Handlers will determine the disposition of an event
  - Accept (event will be consumed and removed from queue)
  - Failed (event will be rejected and sent to dead letter queue)

### Terminology and naming conventions

| Term         | RabbitMQ | Axure Service Bus | Format                        | Example                             |
| ------------ | -------- | ----------------- | ----------------------------- | ----------------------------------- |
| Queue        | Queue    | Queue             | {service}.queue               | shoppingcart.queue                  |
| Topic        | Exchange | Topic             | {service}.{event}             | shoppingcart.orderstatechangedevent |
| Subscription | Bindings | Subscription      | {forwardservice}.subscription | communication.subscription          |

See the following pages for documentations and example configuration:

- [RabbitMQ](RABBITMQ.md)
- [Azure Service Bus](AZURESERVICEBUS.md)

### Configuration

Register the receiver with handlers:

```csharp
    // add domain event receiver with handlers
    services.AddDomainEventReceiver(o => {
        o.UseConfiguration(Configuration);
        o.AddHandler<OrderStateChangedEvent, OrderStateChangedHandler>();
    });

    // add domain event publisher (if not using Outbox)
    services.AddDomainEventPublisher(configuration);

    // add domain event publish with outbox
    services.AddDomainEventOutboxPublisher<DatabaseContext>(Configuration);
```

Add Outbox entity to DbContext for EntityFramework

```csharp
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.AddDomainEventOutbox();
    }
```

## Cortside.DomainEvent.EntityFramework

Cortside.DomainEvent.EntityFramework is an implementation of a transactional outbox pattern using EntityFramework. Documentation can be found here:

- [Transactional Outbox](src/Cortside.DomainEvent.EntityFramework/README.md)

## Cortside.DomainEvent.Stubs

Stubs allow integration tests code to publish/subscribe to events without using a real message broker.

- [Stubs](src/Cortside.DomainEvent.Stub/README.md)

## Example implementation

Example implementation of publisher, handlers, outbox and testing with stubs can be found here:

- [Coeus shoppingcart-api](https://github.com/cortside/coeus/tree/develop/shoppingcart-api)

## Cortside.DomainEvent.Health

- [Health](src/Cortside.DomainEvent.Stub/README.md)


## release note stuff to do
.\add-migration.ps1 -migration OutboxPublisherKey

for release notes
- add efcore migration for new outbox column `Key`
- part of the migration to multiple connections (even if only one?) will be updating existing outbox records with the desired Key value
- update every injection of IDomainEventOutboxPublisher or IDomainEventPublisher to use attribute (or IServiceProvider.GetKeyedService extension) per net8 style `[FromKeyedServices(nameof(DomainEventConnectionKey.MyConnectionKey))] IDomainEventOutboxPublisher publisher`
- use new extension method(s) to register multiple connections
- add new "DomainEvent" property to appsettings/configuration (see example)

```json
"DomainEvent": {
    "Connections": [
        {
            "Key": "Internal",
            "Protocol": "amqp",
            "Server": "localhost",
            "Username": "admin",
            "Password": "password",
            "Queue": "gateway.queue",
            "Topic": "/exchange/gateway/",
            "Credits": 5,
            "ReceiverHostedService": {
                "Enabled": true,
                "TimedInterval": 60
            },
            "OutboxHostedService": {
                "BatchSize": 25,
                "Enabled": true,
                "Interval": 15,
                "PurgePublished": true
            }
        },
        {
            "Key": "External",
            "Protocol": "amqp",
            "Server": "localhost",
            "Username": "admin",
            "Password": "password",
            "Credits": 5,
            "Queue": "productbridge.queue",
            "Topic": "/exchange/productbridge/",
            "UndefinedTypeName": "Acme.DomainEvent.Events.ErpMessageEvent",
            "ReceiverHostedService": {
                "Enabled": true,
                "TimedInterval": 60
            },
            "OutboxHostedService": {
                "BatchSize": 25,
                "Enabled": false,
                "Interval": 15,
                "PurgePublished": false
            }
        }
    ]
},
```

- use docs/update-legacyappsettings.ps1 to convert from lagacy to new style
