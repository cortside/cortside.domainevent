# NOTES

this is the commented out code from DomainEventReceiver for retries that had to be backed out
```csharp
switch (result) {
    case HandlerResult.Success:
        receiver.Accept(message);
        Logger.LogInformation($"Message {message.Properties.MessageId} accepted");
        break;

    case HandlerResult.Retry:
        // until i can figure out how to have a similar safe way to handle accept and requeue
        // without transactions, disabling this functionality.
        // https://github.com/cortside/cortside.domainevent/issues/21
        Logger.LogInformation($"Message {message.Properties.MessageId} being failed instead of expected retry.  See issue https://github.com/cortside/cortside.domainevent/issues/21");
        receiver.Reject(message);
        break;
    //var deliveryCount = message.Header.DeliveryCount;
    //var delay = 10 * deliveryCount;
    //var scheduleTime = DateTime.UtcNow.AddSeconds(delay);

    //using (var ts = new TransactionScope()) {
    //    var sender = new SenderLink(Link.Session, Settings.AppName + "-retry", Settings.Queue);
    //    // create a new message to be queued with scheduled delivery time
    //    var retry = new Message(body) {
    //        Header = message.Header,
    //        Footer = message.Footer,
    //        Properties = message.Properties,
    //        ApplicationProperties = message.ApplicationProperties
    //    };
    //    retry.ApplicationProperties[Constants.SCHEDULED_ENQUEUE_TIME_UTC] = scheduleTime;
    //    sender.Send(retry);
    //    receiver.Accept(message);
    //}
    //Logger.LogInformation($"Message {message.Properties.MessageId} requeued with delay of {delay} seconds for {scheduleTime}");
    //break;
    case HandlerResult.Failed:
        receiver.Reject(message);
        break;
    case HandlerResult.Release:
        receiver.Release(message);
        break;
    default:
        throw new ArgumentOutOfRangeException($"Unknown HandlerResult value of {result}");
}
```


https://carlos.mendible.com/2016/07/17/step-by-step-net-core-azure-service-bus-and-amqp/

https://github.com/Azure/amqpnetlite/blob/master/Examples/Reconnect/ReconnectSender/ReconnectSender.cs