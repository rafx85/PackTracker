[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
[xml]$props = Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw
$version = [version]$props.Project.PropertyGroup.PackTrackerVersion
$expectedVersion = [version]::new($version.Major, $version.Minor, $version.Build, [Math]::Max(0, $version.Revision))
$files = @(
    (Join-Path $root "PackHistorian\bin\$Configuration\net472\PackTracker.dll")
    (Join-Path $root "PackTracker.Updater\bin\$Configuration\net472\PackTracker.Updater.exe")
)

foreach ($file in $files) {
    $assembly = [Reflection.AssemblyName]::GetAssemblyName($file)
    if ($assembly.Name -ne [IO.Path]::GetFileNameWithoutExtension($file) -or $assembly.Version -ne $expectedVersion) {
        throw "$file does not match the expected release version $expectedVersion. Rebuild the solution."
    }
}

$artifacts = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$zip = Join-Path $artifacts 'PackTracker.zip'
Compress-Archive -LiteralPath $files -DestinationPath $zip -Force
Write-Output $zip
