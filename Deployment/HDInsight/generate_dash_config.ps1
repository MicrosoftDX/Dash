$connectionStrings = Get-AzureStorageAccount | where { $_.StorageAccountName.EndsWith("wmdashwalmwest".ToLower()) } | 
    foreach { 
        $acct = $_; 
        $acctname = $acct.StorageAccountName; 
        $key = Get-AzureStorageKey -StorageAccountName $acctname; 
        $keyvalue = $key.Primary; 
        "DefaultEndpointsProtocol=http;AccountName=$acctname;AccountKey=$keyvalue" 
    }
[xml]$cfg = Get-Content 'C:\depot\DataAtScaleHub\DashServer.Azure\ServiceConfiguration.VNet - Internal Load Balancer.cscfg'
$storagesettings = $cfg.ServiceConfiguration.Role.ConfigurationSettings.Setting | where { $_.name.StartsWith("ScaleoutStorage") }
for ($i = 0; $i -lt $storagesettings.Length; $i++) { 
    $conn = $connectionStrings[$i]; 
    if ($conn -ne $null) { 
        $storagesettings[$i].value = $conn 
    } else {
        $storagesettings[$i] = ""
    }
}
$cfg.Save("C:\depot\DataAtScaleHub\DashServer.Azure\ServiceConfiguration.VNet - Internal Load Balancer.cscfg")
