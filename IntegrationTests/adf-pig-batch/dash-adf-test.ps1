param(
	[Parameter(Mandatory=$true)][string] $subscriptionName,
	[Parameter(Mandatory=$false)][string] $location = "westus",
    [Parameter(Mandatory=$false)][string] $resourceGroupName = "dash-adf-test",
    [Parameter(Mandatory=$false)][string] $batch_name = "dashadfbatchtest",
    [Parameter(Mandatory=$false)][string] $pipeline_name = "dash-adf-test-xform-parts",
    [Parameter(Mandatory=$true)][string] $storage_account_name,
    [Parameter(Mandatory=$true)][string] $storage_account_key,
    [Parameter(Mandatory=$true)][string] $dash_uri,
    [Parameter(Mandatory=$true)][string] $dash_account_name,
    [Parameter(Mandatory=$true)][string] $dash_account_key
)
$data_factory_name = $resourceGroupName
$scriptDir = Split-Path -Path $MyInvocation.MyCommand.Definition -Parent

function Get-TaskOutput {
    param (
        $task,
        [string] $taskName
    )
    if ($task -eq $null) {
        $task = Get-AzureBatchTask -BatchContext $batchCtx -WorkItemName $batch_name -JobName $batchJob.Name -Name $taskName
    }
    Write-Output $task
    $localDir = "$($env:TEMP)\$($task.Name)"
    if (!(Test-Path -Path $localDir)) {
        New-Item $localDir -ItemType Directory > $tmp
    }
    Get-AzureBatchTaskFile -BatchContext $batchCtx -Task $task -Recursive | Where-Object { $_.IsDirectory -and !$_.Name.Contains("CopiedData") -and !(Test-Path -Path "$localDir\$($_.Name)").Exists } | ForEach-Object { New-Item -Path "$localDir\$($_.Name)" -ItemType Directory > $tmp }
    Get-AzureBatchTaskFile -BatchContext $batchCtx -Task $task -Recursive | Where-Object { !$_.IsDirectory -and !$_.Name.Contains("CopiedData") } | ForEach-Object { $localPath = $localDir + "\" + $_.Name; Get-AzureBatchTaskFileContent -BatchContext $batchCtx -InputObject $_ -DestinationPath $localPath; }
    foreach ($taskFile in Get-ChildItem -Recurse -Path "$localDir\*" -File) {
        Write-Output ""
        Write-Output $taskFile.Name
        Write-Output ""
        type $taskFile.FullName
    }
}

Select-AzureSubscription -SubscriptionName $subscriptionName
Switch-AzureMode -Name AzureResourceManager
New-AzureResourceGroup -Name $resourceGroupName -Location $location

# Create & run the data factory
$factory = New-AzureDataFactory -Name $data_factory_name -Location $location -ResourceGroupName $resourceGroupName
# Data & compute stores
New-AzureDataFactoryLinkedService -DataFactory $factory -Name DashTestEast -File $scriptDir\adf-snippets\DashTestEastLinkedService.json
New-AzureDataFactoryLinkedService -DataFactory $factory -Name ClusterDataStore -File $scriptDir\adf-snippets\ClusterDataStoreLinkedService.json
New-AzureDataFactoryLinkedService -DataFactory $factory -Name dash-adf-test-hdi-cluster -File $scriptDir\adf-snippets\on-demand-hdi-cluster.json
# Data tables
New-AzureDataFactoryTable -DataFactory $factory -Name Part -File $scriptDir\adf-snippets\PartTable.json
New-AzureDataFactoryTable -DataFactory $factory -Name ProjectedPart -File $scriptDir\adf-snippets\ProjectedPartTable.json
# Pipeline
New-AzureDataFactoryPipeline -DataFactory $factory -Name $pipeline_name -File $scriptDir\adf-snippets\dash-adf-test-pipeline.json
# Set the dates & start it running
$startDate = [System.DateTime]::UtcNow.AddDays(-1)
Set-AzureDataFactoryPipelineActivePeriod -DataFactory $factory -PipelineName $pipeline_name -StartDateTime $startDate -ForceRecalculate -Force
Resume-AzureDataFactoryPipeline -DataFactory $factory -Name $pipeline_name -Force
# Monitor
Write-Output "Waiting for slice to complete (will take about 30mins)"
$endStates = "Ready", "Failed", "Skip", "Failed Validation"
do {
    Start-Sleep -Seconds 60
    $slice = (Get-AzureDataFactorySlice -DataFactory $factory -TableName ProjectedPart -StartDateTime $startDate)[0]
    Write-Output -InputObject $slice
}
while ($endStates -notcontains $slice.Status)

# Create the Batch service
New-AzureBatchAccount –AccountName $batch_name –Location $location –ResourceGroupName $resourceGroupName
# Allow replication to take place
Write-Output "Emulating delay between ETL & Calc Engine"
Start-Sleep -Seconds (60 * 10)

$batchCtx = Get-AzureBatchAccountKeys -AccountName $batch_name
# List the install blobs
$storageCtx = New-AzureStorageContext -StorageAccountName $storage_account_name -StorageAccountKey $storage_account_key 
$container = Get-AzureStorageContainer -Context $storageCtx -Name "dash-adf-test"
$installBlobs = Get-AzureStorageBlob -Context $storageCtx -Container $container.Name -Prefix "batch-startup"
$startTask = new-object Microsoft.Azure.Commands.Batch.Models.PSStartTask
$startTask.ResourceFiles = New-Object System.Collections.Generic.List``1[Microsoft.Azure.Commands.Batch.Models.PSResourceFile]   
foreach ($installFile in $installBlobs) {
    $sas = New-AzureStorageBlobSASToken -ICloudBlob $installFile.ICloudBlob -Context $storageCtx -Permission r -FullUri
    $r = New-Object Microsoft.Azure.Commands.Batch.Models.PSResourceFile -ArgumentList @($sas, $installFile.Name.Replace("/", "\"))
    $startTask.ResourceFiles.Add($r)
}
$startTask.CommandLine = "cmd /c batch-startup\CopyFiles.cmd"
$startTask.WaitForSuccess = $true
New-AzureBatchPool -Name $batch_name -BatchContext $batchCtx -VMSize "small" -OSFamily "4" -TargetOSVersion "*" -TargetDedicated 20 -StartTask $startTask
$batchJobEnv = New-Object Microsoft.Azure.Commands.Batch.Models.PSJobExecutionEnvironment
$batchJobEnv.PoolName = $batch_name
New-AzureBatchWorkItem -Name $batch_name -BatchContext $batchCtx -JobExecutionEnvironment $batchJobEnv
Start-Sleep 10
$batchJob = Get-AzureBatchJob -BatchContext $batchCtx -WorkItemName $batch_name
# Wait for the VMs to spin up
Write-Output ""
Write-Output "Waiting for VMs to startup"
do {
    Start-Sleep 15
    $vms = Get-AzureBatchVM -BatchContext $batchCtx -PoolName $batch_name
}
while ($vms.Count -le 0)
do {
    Start-Sleep 15
    $vm = (Get-AzureBatchVM -BatchContext $batchCtx -PoolName $batch_name)[0]
    Write-Output $vm.State
}
while ($vm.State -ne "Idle")

$today = (Get-Date).ToUniversalTime().AddDays(-1)
$blobPath = [System.String]::Format("output-blobs/{0:yyyy}/{0:MM}/{0:dd}/", $today)
$storageCtx = New-AzureStorageContext -StorageAccountName $dash_account_name -StorageAccountKey $dash_account_key -Endpoint $dash_uri
$copyBlobs = Get-AzureStorageBlob -Context $storageCtx -Container "dash-adf-test" -Prefix $blobPath
$blobUrls = $copyBlobs | foreach { "('" + (New-AzureStorageBlobSASToken -ICloudBlob $_.ICloudBlob -Context $storageCtx -Permission r -FullUri) + "', '" + [System.IO.Path]::GetFileName($_.Name.Replace('/', '\')) + "')" }
$cmd = 'cmd /c powershell -command %WATASK_TVM_SHARED_DIR%\batch-startup\copyblobs.ps1 -Destination .\CopiedData -Urls "(' + [System.String]::Join(",", $blobUrls) + ')"' 
for ($i = 0; $i -lt 500; $i++) {
    $taskName = "Task$i"
    New-AzureBatchTask -Name $taskName -BatchContext $batchCtx -Job $batchJob -CommandLine $cmd
}
Write-Output ""
Write-Output "Waiting for tasks to complete"
do {
    Start-Sleep 15
    $task = (Get-AzureBatchTask -BatchContext $batchCtx -WorkItemName $batch_name -JobName $batchJob.Name)[0]
    Write-Output $task.State
}
while ($task.State -ne "Completed")
Get-TaskOutput -Task $task

