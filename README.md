# Koala for Azure Storage #

Koala is a solution from Microsoft that allows you to bypass space and I/O limits on Azure Storage. In order to use Koala, you will need to download the source, build the package, and deploy it to Azure.

## Prerequisites ##

You will need an active subscription to Azure and access to Visual Studio or Windows Powershell.

## Set up Azure Resources ##

Koala works by sharding your storage across multiple storage accounts. It uses one storage account to keep track of where your data is stored (a namespace account) and shards your data across the remaining accounts. To get started, you will need to create multiple empty storage accounts on your subscription. We recommend that you create at least five storage accounts to start (one namespace account and four storage accounts). You can create as many child storage accounts as you like, but you will need to add them to your .cscfg file and re-deploy Koala for it to take effect.

## Set up your .cscfg file ##

Open up the .cscfg file in the project. You will see variables named "ScaleoutNumberOfAccounts" and "ScaleoutStorage0" through "ScaleoutStorage7", as well as a "StorageConnectionStringMaster". We've provided stubs for these, but you'll need to change them to reflect your configuration.

"ScaleoutNumberOfAccounts" will need to be set to an integer value equal to the number of storage accounts that you want to shard out to. So the number of storage accounts you created minus one to account for the namespace account.

You will need to put a connection string in place for each of the "StorageConnectionStringMaster" and "ScaleoutStorageN" variables. The connection string will need to take the form of "DefaultEndpointsProtocol=http;AccountName=[storageAccountName];AccountKey=[storageAccessKey]". You will need to fill in storageAccountName and storageAccessKey from Azure.

You will also see an "AccountName" and "AccountKey" in your .cscfg file. Please assign a secure name and key to your account - these will be used for your other applications to authenticate against Koala.

## Deploy with Visual Studio ##

To deploy with visual studio, right click the "Koala.Azure" project and select "Publish". You will need to sign into Azure. You can then create a new cloud service under the "Cloud Service" dropdown, and then press Publish. You're almost ready to start partying with bigger, better Storage!