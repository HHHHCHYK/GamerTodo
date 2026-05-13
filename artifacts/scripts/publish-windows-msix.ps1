param(
    [string]$Configuration = "Release",
    [string]$Version = "0.1.0",
    [string]$OutputRoot = "artifacts\releases"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$clientProject = Join-Path $root "src\GamerTodo.Client\GamerTodo.Client.csproj"
$packageDir = Join-Path $root $OutputRoot
$msixRoot = Join-Path $packageDir "msix"
$manifestPath = Join-Path $msixRoot "AppxManifest.xml"
$mappingPath = Join-Path $msixRoot "mapping.txt"
$msixPath = Join-Path $packageDir "GamerTodo-client-win-x64-$Version.msix"
$publishDir = Join-Path $packageDir "client-win-x64-msix"
$makeAppx = Get-Command makeappx.exe -ErrorAction SilentlyContinue

if (-not $makeAppx) {
    throw "makeappx.exe was not found. Install Windows SDK App Certification Kit / MakeAppx tooling first."
}

New-Item -ItemType Directory -Force -Path $publishDir, $msixRoot, $packageDir | Out-Null

Write-Host "Publishing client for MSIX layout..."
dotnet publish $clientProject -c $Configuration -r win-x64 --self-contained false -p:PublishSingleFile=false -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for win-x64"
}

$displayVersion = $Version
if ($displayVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    $displayVersion = "$Version.0"
}

@"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
         xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
         IgnorableNamespaces="uap">
  <Identity Name="GamerTodo.Client"
            Publisher="CN=GamerTodo"
            Version="$displayVersion" />
  <Properties>
    <DisplayName>GamerTodo</DisplayName>
    <PublisherDisplayName>GamerTodo</PublisherDisplayName>
    <Description>GamerTodo desktop client</Description>
    <Logo>Assets\\avalonia-logo.ico</Logo>
  </Properties>
  <Resources>
    <Resource Language="en-us" />
    <Resource Language="zh-cn" />
  </Resources>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.26100.0" />
  </Dependencies>
  <Applications>
    <Application Id="App"
                 Executable="GamerTodo.Client.exe"
                 EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements DisplayName="GamerTodo"
                          Description="GamerTodo desktop client"
                          BackgroundColor="transparent"
                          Square150x150Logo="Assets\\avalonia-logo.ico"
                          Square44x44Logo="Assets\\avalonia-logo.ico" />
    </Application>
  </Applications>
  <Capabilities>
    <Capability Name="internetClient" />
  </Capabilities>
</Package>
"@ | Set-Content -Path $manifestPath -Encoding UTF8

$logoSource = Join-Path $root "src\GamerTodo.Client\Assets\avalonia-logo.ico"
$logoTargetDir = Join-Path $msixRoot "Assets"
New-Item -ItemType Directory -Force -Path $logoTargetDir | Out-Null
Copy-Item $logoSource (Join-Path $logoTargetDir "avalonia-logo.ico") -Force
Copy-Item (Join-Path $publishDir "*") $msixRoot -Recurse -Force

$mappingLines = @("[Files]", '"' + $manifestPath + '" "AppxManifest.xml"')
Get-ChildItem -Path $publishDir -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($publishDir.Length + 1).Replace('\\', '/')
    $mappingLines += ('"' + $_.FullName + '" "' + $relative.Replace('/', '\\') + '"')
}
$mappingLines += ('"' + (Join-Path $logoTargetDir "avalonia-logo.ico") + '" "Assets\\avalonia-logo.ico"')
$mappingLines | Set-Content -Path $mappingPath -Encoding ASCII

if (Test-Path $msixPath) {
    Remove-Item $msixPath -Force
}

Write-Host "Packing MSIX: $msixPath"
& $makeAppx.Source pack /o /f $mappingPath /p $msixPath
if ($LASTEXITCODE -ne 0) {
    throw "makeappx pack failed"
}

Write-Host "Unsigned MSIX created at: $msixPath"
Write-Host "Sign the package separately before distribution if needed."
