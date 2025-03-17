## Cortside.DomainEvent.Health

Health check that will expose publish and consumption statistics about a running instance.

NOTE: this does not supported keyed configuration of multiple broker connections.

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
   "domainevent": {
      "availability": {
         "averageDuration": 0,
         "count": 5,
         "failure": 0,
         "lastFailure": "0001-01-01T00:00:00Z",
         "lastSuccess": "2025-02-14T23:15:45.4391754Z",
         "success": 5,
         "totalDuration": 0,
         "uptime": 100
      },
      "healthy": true,
      "required": false,
      "statistics": {
         "accepted": 1,
         "lastAccepted": "2025-02-14T23:15:55.0500122Z",
         "lastPublished": "2025-02-14T23:15:53.2803267Z",
         "lastQueued": "2025-02-14T23:15:47.7147326Z",
         "lastReceived": "2025-02-14T23:15:53.411306Z",
         "lastRejected": "2025-02-14T23:15:52.9582428Z",
         "lastReleased": null,
         "lastRetried": null,
         "published": 2,
         "publishFailed": 0,
         "queued": 2,
         "received": 2,
         "rejected": 1,
         "released": 0,
         "retries": 0
      },
      "status": "ok",
      "statusDetail": "Successful",
      "timestamp": "2025-02-14T23:15:45.4391713Z"
   }
```
