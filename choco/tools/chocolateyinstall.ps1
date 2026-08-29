$ErrorActionPreference = 'Stop'

$toolsDir = "$(Split-Path -Parent $MyInvocation.MyCommand.Definition)"
$version  = $env:chocolateyPackageVersion
$url      = "https://github.com/TruePadawan/Pluck/releases/download/cli-v${version}/pluck-v${version}-win-x64.exe"

$exePath = Join-Path $toolsDir 'pluck.exe'

Get-ChocolateyWebFile -PackageName $env:ChocolateyPackageName `
    -FileFullPath $exePath `
    -Url64bit $url `
    -Checksum64 '__CHECKSUM__' `
    -ChecksumType64 'sha256'

# Chocolatey will automatically shim pluck.exe onto PATH
