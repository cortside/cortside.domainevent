## Cortside.DomainEvent.Mvc

This library provides a controller to be able to interact with outbox messages that have failed.  Specifically to enumerate them, retry them, and delete them.


Adding the controller to a service can be accomplished by the following:
```csharp
// add controllers and set api defaults
var mvcBuilder = services.AddApiDefaults(InternalDateTimeHandling.Utc, options => {
    options.Filters.Add<MessageExceptionResponseFilter>();
});
mvcBuilder.PartManager.ApplicationParts.Add(new AssemblyPart(typeof(OutboxController).Assembly));
```


### Examples

Get a list of outbox messages that have failed:
```bash
curl --location 'localhost:5000/api/outbox/messages?pageNumber=1&pageSize=10' \
--header 'Authorization: Bearer <token>'
```

Reset an outbox message that has failed to queued with new attempts:
```bash
curl --location --request POST 'localhost:5000/api/outbox/messages/0254efbf-2828-4a26-9085-df4e62ed03e3/reset' \
--header 'Authorization: Bearer <token>'
```

Get a list of outbox messages that have failed:
```bash
curl --location --request DELETE 'localhost:5000/api/outbox/messages/0316fc22-9e12-44d0-b44e-eed684a57768' \
--header 'Authorization: Bearer <token>'
```
