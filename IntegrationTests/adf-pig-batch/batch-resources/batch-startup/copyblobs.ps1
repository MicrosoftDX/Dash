param(
    [String] $Destination,
    [String[][]] $Urls
)
Write-Output "Copying source blobs to $Destination"
if (!(Test-Path -Path $Destination)) {
    Write-Output "Creating target directory: $Destination"
    New-Item -ItemType directory -Path $Destination
}
$path = Resolve-Path $Destination
$Destination = $path.Path
foreach ($blob in $Urls) {
    $targetFileSpec = $Destination + "\" + $blob[1]
    Write-Output "Destination: $targetFileSpec"
    Write-Output "Source: $blob[0]"
    Invoke-WebRequest -Uri $blob[0] -Method Get -OutFile $targetFileSpec -verbose
}

$azTarget = "$Destination\azcopy"
New-Item -ItemType directory -Path $azTarget 
foreach ($blob in $Urls) {
    "$env:WATASK_TVM_SHARED_DIR\batch-startup\azcopy /source:$blob[0] /Dest:$azTarget /SourceType:Blob /V:.\azcopy.log" 
}
