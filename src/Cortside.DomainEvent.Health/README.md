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
