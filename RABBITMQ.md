## Cortside.DomainEvent with RabbitMQ

- rabbit mq example
    - 3.x and 4.x

#### General

#### Queues

- Names of queues cannot be single worded. Should be multipart (eg. auth.queue).

#### Topic

- The forward to setting for the topic subscription is not visible in the azure UI. You can use ServiceBusExplorer to set that field.

#### Example

- for the following configuration settings for the test project with a TestEvent object

```json
  "DomainEvent": {
    "Connections": [
      {
        "Key": null,
        "Protocol": "amqp",
        "Server": "localhost",
        "Username": "admin",
        "Password": "password",
        "Queue": "shoppingcart.queue",
        "Topic": "/exchange/shoppingcart/",
        "Credits": 5,
        "Durable": "1",
        "ReceiverHostedService": {
          "Enabled": true,
          "TimedInterval": 60
        },
        "OutboxHostedService": {
          "BatchSize": 5,
          "Enabled": true,
          "Interval": 5,
          "PurgePublished": true,
          "MaximumPublishCount": 10,
          "PublishRetryInterval": 60
        }
      }
    ]
  }
```
