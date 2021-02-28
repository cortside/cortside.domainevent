[![Build status](https://ci.appveyor.com/api/projects/status/43l1ckgn806lqxjx?svg=true)](https://ci.appveyor.com/project/cortside/cortside-domainevent)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=cortside_cortside.common&metric=alert_status)](https://sonarcloud.io/dashboard?id=cortside_cortside.domainevent)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=cortside_cortside.domainevent&metric=coverage)](https://sonarcloud.io/dashboard?id=cortside_cortside.domainevent)

## Cortside.DomainEvent
Classes for sending and listening to a message bus. Uses AMQPNETLITE (AMQP 1. 0 protocol).
### Azure ServiceBus
#### General
- Authorization keys cannot contain '/'. They must be regenerated if they do. AMQPNETLITE does not like that value.
- I found inconsistent behavior if the topic and queue were created using the AzureSB UI.  I had success creating the topics, subscriptions, queues using ServiceBusExplorer (https://github.com/paolosalvatori/ServiceBusExplorer/releases)
#### Queues
- Names of queues cannot be single worded. Should be multipart (eg. auth.queue).
#### Topic
- The forward to setting for the topic subscription is not visible in the azure UI.  You can use ServiceBusExplorer to set that field.
#### Example
- for the following configuration settings for the test project with a TestEvent object
```
    "Publisher.Settings": {
        "Protocol": "amqps",
        "Namespace": "namespace.servicebus.windows.net",
        "Policy": "Send",
        "Key": "44CharBASE64EncodedNoSlashes",
        "AppName": "test.publisher",
        "Address": "topic.",
        "Durable": "0"
    },
    "Receiver.Settings": {
        "Protocol": "amqps",
        "Namespace": "namespace.servicebus.windows.net",
        "Policy": "Listen",
        "Key": "44CharBASE64EncodedNoSlashes",
        "AppName": "test.receiver",
        "Address": "queue.testReceive",
        "Durable": "0"
    }
```
**__(for test default settings from Service Bus Explorer are fine unless specified below)__**
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

todo:
* will probably want to set deduplication at the message broker since same messageId will be used
* will need to generate ef migration after adding following to OnModelCreating method in dbcontext class:
  * modelBuilder.AddDomainEventOutbox();
  * https://github.com/cortside/cortside.webapistarter/blob/outbox/src/Cortside.WebApiStarter.Data/Migrations/20210228035338_DomainEventOutbox.cs

## migration from cortside.common.domainevent

## examples
* https://github.com/cortside/cortside.webapistarter

## todo:
* complete implementation in DomainEventOutboxPublisher
* publisher should return published message information -- at least messageId -- would make debugging easier
* 