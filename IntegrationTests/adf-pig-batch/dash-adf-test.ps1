$resourceGroupName = "dash-adf-test"
$data_factory_name = $resourceGroupName
$batch_name = "dashadfbatchtest"
$pipeline_name = "dash-adf-test-xform-parts"
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

Select-AzureSubscription -SubscriptionName "Data At Scale Hub -- jamesbak"
Switch-AzureMode -Name AzureResourceManager
New-AzureResourceGroup -Name $resourceGroupName -Location "westus"

# Create & run the data factory
$factory = New-AzureDataFactory -Name $data_factory_name -Location "westus" -ResourceGroupName $resourceGroupName
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
New-AzureBatchAccount –AccountName $batch_name –Location "westus" –ResourceGroupName $resourceGroupName
# Allow replication to take place
Write-Output "Emulating delay between ETL & Calc Engine"
Start-Sleep -Seconds (60 * 10)

$batchCtx = Get-AzureBatchAccountKeys -AccountName $batch_name
# List the install blobs
$storageCtx = New-AzureStorageContext -ConnectionString "DefaultEndpointsProtocol=https;AccountName=dashadftest;AccountKey=w5mNcPZQ2sooPH2cMFMErFkfWGDAwjuqKjzWolKIc0WRw1Erk3biDSlEoR6HlHi8VunpqkUcheSR1ff5x1hAtg=="
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
$storageCtx = New-AzureStorageContext -ConnectionString "AccountName=dashtesteast;AccountKey=wCNvIdXcltACBiDUMyO0BflZpKmjseplqOlzE62tx87qnkwpUMBV/GQhrscW9lmdZVT0x8DilYqUoHMNBlVIGg==;BlobEndpoint=http://dashtesteast.cloudapp.net;TableEndpoint=http://dashtesteast.cloudapp.net;QueueEndpoint=http://dashtesteast.cloudapp.net;"
$copyBlobs = Get-AzureStorageBlob -Context $storageCtx -Container "dash-adf-test" -Prefix $blobPath
$blobUrls = $copyBlobs | foreach { "('" + (New-AzureStorageBlobSASToken -ICloudBlob $_.ICloudBlob -Context $storageCtx -Permission r -FullUri) + "', '" + [System.IO.Path]::GetFileName($_.Name.Replace('/', '\')) + "')" }
$cmd = 'cmd /c powershell -command %WATASK_TVM_SHARED_DIR%\batch-startup\copyblobs.ps1 -Destination .\CopiedData -Urls "(' + [System.String]::Join(",", $blobUrls) + ')"' 
#$cmd = "cmd /c %WATASK_TVM_SHARED_DIR%\batch-startup\azcopy /source:$blobPath /Dest:.\CopiedData /SourceKey:wCNvIdXcltACBiDUMyO0BflZpKmjseplqOlzE62tx87qnkwpUMBV/GQhrscW9lmdZVT0x8DilYqUoHMNBlVIGg== /SourceType:Blob /S /V:.\azcopy.log"
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

