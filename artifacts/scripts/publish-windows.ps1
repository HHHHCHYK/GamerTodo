param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "0.1.0",
    [string]$OutputRoot = "artifacts\releases"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$clientProject = Join-Path $root "src\HeyeTodo.Client\HeyeTodo.Client.csproj"
$publishDir = Join-Path $root "$OutputRoot\client-$Runtime"
$packageDir = Join-Path $root $OutputRoot
$zipPath = Join-Path $packageDir "HeyeTodo-client-$Runtime-$Version.zip"

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
New-Item -ItemType Directory -Force -Path $packageDir | Out-Null

Write-Host "Publishing client for $Runtime..."
dotnet publish $clientProject -c $Configuration -r $Runtime --self-contained false -p:PublishSingleFile=false -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for $Runtime"
}

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Write-Host "Creating portable zip: $zipPath"
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

Write-Host "Done."
Write-Host "Publish directory: $publishDir"
Write-Host "Zip package: $zipPath"
