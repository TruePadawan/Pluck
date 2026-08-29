$ErrorActionPreference = 'Stop'

$toolsDir = "$(Split-Path -Parent $MyInvocation.MyCommand.Definition)"
$exePath  = Join-Path $toolsDir 'pluck.exe'

if (Test-Path $exePath) {
    Remove-Item $exePath -Force
}
