[CmdletBinding()]
param([string]$Destination)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$repository = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Destination)) {
    $Destination = Join-Path $repository 'LocalAssets\Animation'
}
$resolvedDestination = [IO.Path]::GetFullPath($Destination)
New-Item -ItemType Directory -Force -Path $resolvedDestination | Out-Null

$fixtures = @(
    @{ Name = 'RiggedSimple.glb'; Sha256 = '3A79DABB67BB0CD598A18D08B954D9D357C27C30672F82EF5D3F4E7FE6CA3401'; Url = 'https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Assets/main/Models/RiggedSimple/glTF-Binary/RiggedSimple.glb' },
    @{ Name = 'CesiumMan.glb'; Sha256 = 'B7001EAEEA8254BD44773BCD247E78696D94169388FBB2A1800FC69434E777D9'; Url = 'https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Assets/main/Models/CesiumMan/glTF-Binary/CesiumMan.glb' },
    @{ Name = 'Fox.glb'; Sha256 = 'D97044E701822BAC5A62696459B27D7B375AADA5DE8574ED4362EDBBA94771F7'; Url = 'https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Assets/main/Models/Fox/glTF-Binary/Fox.glb' }
)

foreach ($fixture in $fixtures) {
    $target = Join-Path $resolvedDestination $fixture.Name
    if (-not (Test-Path -LiteralPath $target)) {
        Invoke-WebRequest -UseBasicParsing -Uri $fixture.Url -OutFile $target
    }
    $actualHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    if ($actualHash -ne $fixture.Sha256) {
        throw "Fixture hash mismatch: $($fixture.Name)"
    }
    Write-Host $target
}
