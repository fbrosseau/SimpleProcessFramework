param(
[Parameter(Mandatory=$True)]
[String]$Version,
[Parameter(Mandatory=$True)]
[String]$CliPath,
[Parameter(Mandatory=$True)]
[String]$Arch,
[Parameter(Mandatory=$False)]
[switch]$NoDownloadScript = $false
)

$ErrorActionPreference="Stop"
$ProgressPreference="SilentlyContinue"

if(!(Test-Path -Path $CliPath)) {
    New-Item -Type "directory" -Path $CliPath 
}

if(!($NoDownloadScript)) {
    $Start = Get-Date;
    Write-Host "Downloading the CLI installer...";

    Invoke-WebRequest `
        -Uri "https://dot.net/v1/dotnet-install.ps1" `
        -OutFile "$CliPath/dotnet-install.ps1"

    $End = Get-Date;
    Write-Host "Downloaded in $($End - $Start)";
}

Write-Host "Installing the CLI requested version ($Version) ..."

& $CliPath/dotnet-install.ps1 `
    -Channel $Version `
    -InstallDir $CliPath `
    -Architecture $Arch `
    -Runtime dotnet

Write-Host "Downloading and installation of the SDK is complete."