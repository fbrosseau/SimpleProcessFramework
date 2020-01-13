param(
[Parameter(Mandatory=$True)]
[String]$Assembly
)

$Assembly = [System.IO.Path]::GetFullPath($Assembly);

Write-Host "Describing $Assembly...";

#Add-Type -Path $Assembly
[Reflection.Assembly]::LoadFile($Assembly);

$txt = [Spfx.Utilities.Runtime.HostFeaturesHelper]::DescribeHost();
Write-Host $txt