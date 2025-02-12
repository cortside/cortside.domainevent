## Cortside.DomainEvent.Health

Health check that will expose publish and consumption statistics about a running instance.

Configuration

```json
  "HealthCheckHostedService": {
    "Name": "{{Service:Name}}",
    "Enabled": true,
    "Interval": 5,
    "CacheDuration": 30,
    "Checks": [
      {
        "Name": "domainevent",
        "Type": "domainevent",
        "Required": false,
        "Value": "",
        "Interval": 30,
        "Timeout": 5
      }
```

```csharp
// add health services
services.AddHealth(o => {
    o.UseConfiguration(Configuration);
    o.AddCustomCheck("example", typeof(ExampleCheck));
    o.AddCustomCheck("domainevent", typeof(DomainEventCheck));
});
```

Example check in healh response:

```json
"domainevent":{
   "healthy":true,
   "status":"ok",
   "statusDetail":"Successful",
   "timestamp":"2025-01-09T19:29:44.9457334Z",
   "required":false,
   "availability":{
      "count":1,
      "success":1,
      "failure":0,
      "uptime":100,
      "totalDuration":1,
      "averageDuration":1,
      "lastSuccess":"2025-01-09T19:29:44.9461913Z",
      "lastFailure":"0001-01-01T00:00:00Z"
   },
   "statistics":{
      "received":0,
      "accepted":0,
      "rejected":0,
      "released":0,
      "retries":0,
      "lastReceived":null,
      "lastAccepted":null,
      "lastRejected":null,
      "lastReleased":null,
      "lastRetried":null,
      "queued":0,
      "lastQueued":null,
      "published":0,
      "publishFailed":0,
      "lastPublished":null
   }
}
```
