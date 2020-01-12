param(
[Parameter(Mandatory=$True)]
[String]$Assembly
)

$Assembly = [System.IO.Path]::GetFullPath($Assembly);

Write-Host "Describing $Assembly...";

[Reflection.Assembly]::LoadFile($Assembly);
$txt = [Spfx.Utilities.HostFeaturesHelper]::DescribeHost();
Write-Host $txt