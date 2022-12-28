
$CurrentDirectory = Get-Location
$Source = $CurrentDirectory 
$Destination = Join-Path -Path $CurrentDirectory -ChildPath "artifacts/nuget-packages"
$Filter = $Filter = [regex]".*Diligent-.*(.nupkg|.snupkg)"

$Tree = Get-ChildItem -Recurse -Path $Source | Where-Object {$_.Name -match $Filter}

if (!(Test-Path -path $Destination)) {New-Item $Destination -Type Directory}

foreach ($Item in $Tree) {
	Copy-Item -Path $Item.FullName -Destination $Destination
}
