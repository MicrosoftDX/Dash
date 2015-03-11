param(
    [Parameter(Mandatory=$true)][string] $NamePrefix,
    [Parameter(Mandatory=$true)][string] $Location,
    [Parameter(Mandatory=$false)][boolean] $NoDash = $false,
    [Parameter(Mandatory=$false)][int] $NumStorageAccounts = 8,
    [Parameter(Mandatory=$false)][string] $DashService = $NamePrefix,
    [Parameter(Mandatory=$false)][string] $StorageAccountNameSuffix = "dashstorage",
    [Parameter(Mandatory=$false)][boolean] $NoHDI = $false,
    [Parameter(Mandatory=$false)][string] $HdiClusterName = $NamePrefix,
    [Parameter(Mandatory=$false)][int] $NumHdiDataNodes = 16,
    [Parameter(Mandatory=$false)][string] $HdiUserName,
    [Parameter(Mandatory=$false)][string] $HdiPassword,
    [Parameter(Mandatory=$false)][string] $HdiPrimaryStorageAccount = $NamePrefix + "Hdi",
    [Parameter(Mandatory=$false)][string] $HdiStorageContainer = "hdi"
)

function New-DashStorageAccount {
    param (
        [string] $accountName,
        [string] $accountLocation
    )
    $warn = ""
    Write-Host "Creating storage account: $accountName"
    New-AzureStorageAccount -StorageAccountName $accountName.ToLower() -Location $accountLocation | Out-Null
    New-Object -TypeName PSObject -Property @{ Account=(Get-AzureStorageAccount -StorageAccountName $accountName.ToLower() -WarningVariable warn); Key=(Get-AzureStorageKey -StorageAccountName $accountName.ToLower() -WarningVariable warn) }
}

function Update-DashServiceSetting {
    param (
        $configSettings,
        [string] $name,
        [string] $value
    )
    $setting = $configSettings | where { $_.name -eq $name }
    if ($setting) {
        $setting.value = $value
    }
}

Write-Output "Starting Dash\HDInsight installation at: $(Get-Date)"

$HdiPrimaryStorageAccount = $HdiPrimaryStorageAccount.ToLower()
$DashService = $DashService.ToLower()
$StorageAccountNameSuffix = $StorageAccountNameSuffix.ToLower()
$HdiStorageContainer = $HdiStorageContainer.ToLower()

# Double-up our primary HDI storage account to include diagnostics for Dash
$hdiPrimaryAccount = New-DashStorageAccount $HdiPrimaryStorageAccount $Location

# Deploy Dash & Storage Accounts
if (!$NoDash) {
    # Storage Accounts
    $NumStorageAccounts = [System.Math]::Min($NumStorageAccounts, 16)
    Write-Output "Creating storage accounts in: $Location"
    $accounts = 1..($NumStorageAccounts + 1) | % {
        # Diversify account names across the namespace to minimize partition server contention
        $accountName = [char]([int](($_-1) * 25 / ($NumStorageAccounts + 1)) + 97) + [char]((Get-Random -Maximum 25) + 97) + [char]((Get-Random -Maximum 25) + 97) + [char]((Get-Random -Maximum 25) + 97) + 
            $StorageAccountNameSuffix
        if ($_ -eq 1) {$accountName += "namespace"}
        
        New-DashStorageAccount $accountName $Location
    }

    # Dash service
    $DashKey = [System.Convert]::ToBase64String([System.Guid]::NewGuid().ToByteArray() + 
        [System.Guid]::NewGuid().ToByteArray() + 
        [System.Guid]::NewGuid().ToByteArray() + 
        [System.Guid]::NewGuid().ToByteArray())
    Write-Output "Generating key and configuration for Dash service: $DashService - $DashKey"
    $serviceConfig = [xml](New-Object System.Net.WebClient).DownloadString("https://www.dash-update.net/DashServer/Latest/http/ServiceConfiguration.Publish.cscfg")
    $configSettings = $serviceConfig.ServiceConfiguration.Role.ConfigurationSettings.Setting
    Update-DashServiceSetting $configSettings "Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" "DefaultEndpointsProtocol=http;AccountName=$($hdiPrimaryAccount.Account.StorageAccountName);AccountKey=$($hdiPrimaryAccount.Key.Primary)"
    Update-DashServiceSetting $configSettings "AccountName" $DashService
    Update-DashServiceSetting $configSettings "AccountKey" $DashKey
    Update-DashServiceSetting $configSettings "StorageConnectionStringMaster" "DefaultEndpointsProtocol=http;AccountName=$($accounts[0].Account.StorageAccountName);AccountKey=$($accounts[0].Key.Primary)"
    for ($i = 1; $i -le $NumStorageAccounts; $i++) {
        Update-DashServiceSetting $configSettings "ScaleoutStorage$($i - 1)" "DefaultEndpointsProtocol=https;AccountName=$($accounts[$i].Account.StorageAccountName);AccountKey=$($accounts[$i].Key.Primary)"
    }
    $configFile = "$env:TEMP\ServiceConfiguration.Publish.cscfg"
    $serviceConfig.Save($configFile)
    $storageCtx = New-AzureStorageContext -StorageAccountName $hdiPrimaryAccount.Account.StorageAccountName -StorageAccountKey $hdiPrimaryAccount.Key.Primary
    $destContainer = "dash-deployment"
    $destBlob = [System.Guid]::NewGuid().ToString() + ".cspkg"

    Write-Output "Copying package and updated configuration to storage: $($hdiPrimaryAccount.Account.StorageAccountName) $destContainer $destBlob"
    New-AzureStorageContainer -Name $destContainer -Permission Off -Context $storageCtx
    Start-CopyAzureStorageBlob -AbsoluteUri "https://www.dash-update.net/DashServer/Latest/http/DashServer.Azure.cspkg" -DestContext $storageCtx -DestContainer $destContainer -DestBlob $destBlob -Force
    Get-AzureStorageBlobCopyState -Context $storageCtx -Container $destContainer -Blob $destBlob -WaitForComplete

    Write-Output "Creating and deploying Dash service"
    Set-AzureSubscription -SubscriptionName (Get-AzureSubscription -Current).SubscriptionName -CurrentStorageAccountName $hdiPrimaryAccount.Account.StorageAccountName
    New-AzureService -ServiceName $DashService -Location $Location
    New-AzureDeployment -ServiceName $DashService -Slot Production -Package "https://$($hdiPrimaryAccount.Account.StorageAccountName).blob.core.windows.net/$destContainer/$destBlob" -Configuration $configFile -Name "DashDeployment"
    # Wait for the deployment to fully start
    Write-Output "Waiting for Dash service: $DashService to start"
    do {
        Write-Host "."
        Start-Sleep -Seconds 15
        $instances = Get-AzureRole -ServiceName $DashService -Slot Production -InstanceDetails
        $instancesReady = ($instances | where { $_.InstanceStatus -eq "ReadyRole" }).Length
        $instancesTotal = $instances.Length
    }
    while ($instancesReady -ne $instancesTotal)

    $dashCtx = New-AzureStorageContext -ConnectionString "BlobEndpoint=http://$DashService.cloudapp.net;AccountName=$DashService;AccountKey=$DashKey"
    $container = Get-AzureStorageContainer -Context $dashCtx -Name $HdiStorageContainer -ErrorAction SilentlyContinue
    if (!$container) {
        Write-Output "Creating container in virtual account: $DashService $HdiStorageContainer"
        New-AzureStorageContainer -Context $dashCtx -Name $HdiStorageContainer -Permission Off
    }
}

# Deploy HDI Cluster
if (!$NoHDI) {
    $secpasswd = ConvertTo-SecureString $HdiPassword -AsPlainText -Force
    $hdiCredential = New-Object System.Management.Automation.PSCredential ($HdiUserName, $secpasswd)

    New-AzureHDInsightClusterConfig -ClusterSizeInNodes $NumHdiDataNodes | 
        Set-AzureHDInsightDefaultStorage -StorageAccountName $hdiPrimaryAccount.Account.StorageAccountName -StorageAccountKey $hdiPrimaryAccount.Key.Primary -StorageContainerName $HdiStorageContainer | 
        Add-AzureHDInsightScriptAction -Name "Dash" -ClusterRoleCollection HeadNode,DataNode -Uri https://dashbuild.blob.core.windows.net/packages/Client/v0.1/HDIScript/update_dash_hdi.ps1 -Parameters "-DashService $DashService -DashKey $DashKey" | 
        New-AzureHDInsightCluster -Name $HdiClusterName -Location $Location -Credential $hdiCredential
}

Write-Output "Completed Dash\HDInsight installation at: $(Get-Date)"
