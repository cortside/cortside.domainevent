## Cortside.DomainEvent with RabbitMQ

- rabbit mq example
    - 3.x and 4.x

#### General

- Authorization keys cannot contain '/'. They must be regenerated if they do. AMQPNETLITE does not like that value.
- I found inconsistent behavior if the topic and queue were created using the AzureSB UI. I had success creating the topics, subscriptions, queues using ServiceBusExplorer (https://github.com/paolosalvatori/ServiceBusExplorer/releases)

#### Queues

- Names of queues cannot be single worded. Should be multipart (eg. auth.queue).

#### Topic

- The forward to setting for the topic subscription is not visible in the azure UI. You can use ServiceBusExplorer to set that field.

#### Example

- for the following configuration settings for the test project with a TestEvent object

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
