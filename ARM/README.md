# DASH Deployment

This Azure Resource Manager template will create:

* Namespace Storage Account
* Scaleout Storage Account x 3
* Redis Cache


## Prerequisities

* Install Azure PowerShell

## How to Use


```
$ Add-AzureAccount
$ Get-AzureSubscription
$ Select-AzureSubscription -SubscriptionID <Subscription-ID>
$ New-AzureResourceGroup -Name <Resource-Group-Name> -Location <Resource-Group-Location> -TemplateFile .\deploymentTemplate.json -TemplateParameterFile .\deploymentTemplate.param.dev.json -Verbose
```