param(
    [Parameter(Mandatory=$true)][string] $DashService,
    [Parameter(Mandatory=$true)][string] $DashKey,
    [string] $DashContainer = ""
)

function Edit-CoreSiteFile {
    param (
        [parameter(Mandatory)] $ConfigFile,
        [parameter(Mandatory)][string] $Name,
        [parameter(Mandatory)][string] $Value
    )
    $existingproperty = $configFile.configuration.property | where {$_.Name -eq $Name}
    
    if ($existingproperty) {
        $existingproperty.Value = $Value
    } else {
        $newproperty = @($configFile.configuration.property)[0].Clone()
        $newproperty.Name = $Name
        $newproperty.Value = $Value
        $configFile.configuration.AppendChild($newproperty)
    }
}

# Download config action module from a well-known directory.
$CONFIGACTIONURI = "https://hdiconfigactions.blob.core.windows.net/configactionmodulev02/HDInsightUtilities-v02.psm1";
$CONFIGACTIONMODULE = "$env:TEMP\HDInsightUtilities.psm1";
Invoke-WebRequest -Uri $CONFIGACTIONURI -OutFile $CONFIGACTIONMODULE
# (TIP) Import config action helper method module to make writing config action easy.
if (Test-Path ($CONFIGACTIONMODULE))
{ 
    Import-Module $CONFIGACTIONMODULE;
} 
else
{
    Write-Output "Failed to load HDInsightUtilities module, exiting ...";
    exit;
}
Write-HDILog "Starting Dash installation at: $(Get-Date)";

$hadoop_directory = $env:HADOOP_HOME
$hbase_directory = $env:HBASE_HOME
$core_site_path = "$hadoop_directory\etc\hadoop\core-site.xml"
#$core_site_path = "C:\work\core-site.xml"
$isActiveHeadNode = Test-IsActiveHDIHeadNode

Write-HDILog "Stopping HDInsight services";
$hdiservices = Get-HDIServicesRunning
$output = $hdiservices | Stop-Service -verbose *>&1 | Out-String
Write-HDILog $output

Write-HDILog "Modifying core-site.xml: $core_site_path";
[xml]$core_site = Get-Content $core_site_path
# Update core-site.xml file adding Dash server key
$output = Edit-CoreSiteFile -ConfigFile $core_site -Name "fs.azure.account.key.$DashService.cloudapp.net" -Value $DashKey | Out-String
Write-HDILog $output

# Update core-site.xml file updating read self throttling
$output = Edit-CoreSiteFile -ConfigFile $core_site -Name "fs.azure.selfthrottling.read.factor" -Value "1.0" | Out-String
Write-HDILog $output

# Update core-site.xml file updating disabling write self throttling
$output = Edit-CoreSiteFile -ConfigFile $core_site -Name "fs.azure.selfthrottling.write.factor" -Value "1.0" | Out-String
Write-HDILog $output

# Update core-site.xml file updating default file system
# This is currently not enabled as we need to determine which files need to be copied to default file system if switching after setup
# $element = $core_site.configuration.property | where { $_.name -eq "fs.defaultFS" } 
# $element.value = “wasb://$DashContainer@$DashService@cloudapp.net”
$output = Edit-CoreSiteFile -ConfigFile $core_site -Name "fs.defaultFS" -Value "wasb://$DashContainer@$DashService.cloudapp.net" | Out-String
Write-HDILog $output

# Update core-site.xml file deleting property configuring custom topology discovery to work around yarn scheduler bug 
$element = $core_site.configuration.property | where { $_.name -eq "topology.script.file.name" } 
if ($element) {
    $core_site.configuration.RemoveChild($element)
}
$core_site.Save($core_site_path)

# Replace storage client library with Dash version
Write-HDILog "Updating Azure Storage Client SDK"
$new_jar_uri = "https://www.dash-update.net/client/v0.3/StorageSDK2.0/dash-azure-storage-2.0.0.jar"
$directories = "$hadoop_directory\share\hadoop\common\lib", "$hadoop_directory\share\hadoop\tools\lib", "$hadoop_directory\share\hadoop\yarn\lib", "$hbase_directory\lib"
foreach ($directory in $directories) 
{
    $output = remove-item "$directory\azure-storage-2.0.0.jar" -ErrorAction SilentlyContinue  -verbose *>&1 | Out-String
    Write-HDILog $output
    $output = Invoke-WebRequest -Uri $new_jar_uri -Method Get -OutFile "$directory\dash-azure-storage-2.0.0.jar"  -verbose *>&1 | Out-String
    Write-HDILog $output
}

Write-HDILog "Restarting HDInsight services";
$output = $hdiservices | Start-Service | Out-String
    Write-HDILog $output

# Create a container in the Dash account to work from. Given that this script is running on every VM in the cluster
# this will be a race condition between all script invocations - first one wins, everyone else fails benignly
if ($isActiveHeadNode -and [bool]$DashContainer) {
    if ((Get-Module -Name "Azure") -eq $null) {
        $azureInstalled = Get-Module -ListAvailable | where {$_.Name -eq "Azure"}
        if ($azureInstalled) {
            Write-HDILog "Activating Azure Powershell Cmdlets"
            $output = Import-Module "Azure" | Out-String
            Write-HDILog $output
        }
        else {
            Write-HDILog "Installing Azure Powershell Cmdlets"
            # IE isn't available to parse the DOM, so we can't use Invoke-WebRequest here - do it with regex
            $webclient = New-Object System.Net.WebClient
            $doc = $webclient.DownloadString("https://github.com/Azure/azure-sdk-tools/releases")
            $links = $doc | Select-String '<a href="(.*?)"' -AllMatches | select -ExpandProperty Matches | where { $_.Value -like "*.msi*" }
            $msiuri = $links[0].Groups[1].Value
            $msifile = Split-Path $msiuri -Leaf
            $msifile = "d:\$msifile"
            $output = Invoke-WebRequest -uri $msiuri -OutFile $msifile | Out-String
            Write-HDILog $output
            Write-HDILog "Installing Azure Powershell Cmdlets from package: $msifile"
            $output = Invoke-HDICmdScript -CmdToExecute "msiexec /qb /l*v d:\azure-powershell.log /i $msifile"
            Write-HDILog $output
            $output = Import-Module "${env:ProgramFiles(x86)}\Microsoft SDKs\Azure\PowerShell\ServiceManagement\Azure\Azure.psd1" | Out-String
            Write-HDILog $output
            Write-HDILog "Done installing Azure Powershell Cmdlets"
        }
    }
    Write-HDILog "Verifying container in virtual account: $DashService $DashContainer"
    $storagectx = New-AzureStorageContext -ConnectionString "BlobEndpoint=http://$DashService.cloudapp.net;AccountName=$DashService;AccountKey=$DashKey"
    $container = Get-AzureStorageContainer -Context $storagectx -Name $DashContainer.ToLower() -ErrorAction SilentlyContinue
    if (!$container) {
        Write-HDILog "Creating container in virtual account: $DashService $DashContainer"
        $output = New-AzureStorageContainer -Context $storagectx -Name $DashContainer.ToLower() -Permission Off | Out-String
        Write-HDILog $output
    }
    # Copy required files to the default container

}

Write-HDILog "Done with Dash installation at: $(Get-Date)";

