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
$blockSize = 128 * 1024
$start = Get-Date
$numFiles = 0
foreach ($blob in $Urls) {
    Write-Output "Destination: $targetFileSpec"
    Write-Output "Source: $($blob[0])"
    $response = Invoke-WebRequest -Method Head -Uri ($blob[0]) -UseBasicParsing
    $length = $response.Headers["Content-Length"]
    $targetFileSpec = $Destination + "\" + $blob[1]
    $outStream = [System.IO.File]::Create($targetFileSpec)
    for ($offset = 0; $offset -lt $length; $offset += $blockSize) {
        $response = Invoke-WebRequest -Method Get -Uri ($blob[0]) -Headers @{"x-ms-range"="bytes=$offset-$($offset + $blockSize - 1)";} -UseBasicParsing
        $response.RawContentStream.WriteTo($outStream)
    }
    $outStream.Close()
	$numFiles++
}
$duration = ((Get-Date) - $start).TotalMilliseconds
Write-Output "Copied $numFiles in $duration ms"

$azTarget = "$Destination\azcopy"
New-Item -ItemType directory -Path $azTarget 
$start = Get-Date
$numFiles = 0
foreach ($blob in $Urls) {
    $sas = $blob[0]
    "$env:WATASK_TVM_SHARED_DIR\batch-startup\azcopy /source:$sas /Dest:$azTarget /SourceType:Blob /V:.\azcopy.log" 
	$numFiles++
}
$duration = ((Get-Date) - $start).TotalMilliseconds
Write-Output "Copied $numFiles in $duration ms"
