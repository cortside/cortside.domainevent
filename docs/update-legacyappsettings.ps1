param(
    [string]$filepath = "appsettings.json"  # Path to the appsettings.json file
)

# read in the configuration file
$config = $config = Get-Content -Path $filepath -Raw | ConvertFrom-Json

# make sure projectType is set
if ($config.DomainEvent -eq $null) {
	$connection = [ordered]@{
		Key=$null

		Protocol= $config.ServiceBus.Protocol
		Server= $config.ServiceBus.Namespace
		Username= $config.ServiceBus.Policy
		Password= $config.ServiceBus.Key
		Queue= $config.ServiceBus.Queue
		Topic= $config.ServiceBus.Topic
		Credits= $config.ServiceBus.Credits
		
		ReceiverHostedService = $config.ReceiverHostedService
		OutboxHostedService = $config.OutboxHostedService
    }
	
	$domainEvent = [ordered]@{
		Connections = @()
	}
	$domainEvent.Connections += $connection
	
    $config | add-member -Name "DomainEvent" -value $domainEvent -MemberType NoteProperty
	$config.PSObject.Properties.Remove("ServiceBus")
	$config.PSObject.Properties.Remove("ReceiverHostedService")
	$config.PSObject.Properties.Remove("OutboxHostedService")
}

# write out file with any updates
$config | ConvertTo-Json -Depth 5 | Out-File $filepath -Encoding utf8
