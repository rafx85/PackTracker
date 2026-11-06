[CmdletBinding()]
param(
    [string]$PackagePath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\PackTracker.zip')
)

$ErrorActionPreference = 'Stop'
$artifacts = Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts'
$testRoot = Join-Path $artifacts ('verify-updater-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null

function New-UpdateStage($Name, $InstalledPlugin, $HostId, $HostStartTicks) {
    $stage = Join-Path $testRoot $Name
    $payload = Join-Path $stage 'payload'
    Expand-Archive -LiteralPath $PackagePath -DestinationPath $payload
    $version = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $payload 'PackTracker.dll')).Version
    @{
        ProcessId = $HostId
        ProcessStartUtcTicks = $HostStartTicks
        HdtExecutable = $hostExecutable
        InstalledPlugin = $InstalledPlugin
        ExpectedVersion = $version.ToString(4)
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stage 'update-request.json') -Encoding utf8NoBOM
    $stage
}

function Start-StagedUpdater($Stage) {
    $helper = Join-Path $Stage 'payload\PackTracker.Updater.exe'
    $request = Join-Path $Stage 'update-request.json'
    Start-Process -FilePath $helper -ArgumentList ('"{0}" --quiet' -f $request) -PassThru -WindowStyle Hidden
}

function Assert-FileMatches($Expected, $Actual) {
    if ((Get-FileHash -LiteralPath $Expected).Hash -ne (Get-FileHash -LiteralPath $Actual).Hash) {
        throw "$Actual did not match $Expected."
    }
}

function Wait-ForMarker($Path) {
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    while (!(Test-Path -LiteralPath $Path)) {
        if ([DateTime]::UtcNow -ge $deadline) { throw "Timed out waiting for $Path." }
        Start-Sleep -Milliseconds 100
    }
}

$target = Join-Path $testRoot 'installed'
New-Item -ItemType Directory -Path $target | Out-Null
$installedPlugin = Join-Path $target 'PackTracker.dll'
$installedHelper = Join-Path $target 'PackTracker.Updater.exe'
$previousPlugin = Join-Path $testRoot 'PackTracker.dll'
$previousHelper = Join-Path $testRoot 'PackTracker.Updater.exe'
$compiler = Join-Path $env:SystemRoot 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$pluginSource = Join-Path $testRoot 'PreviousPackTracker.cs'
'[assembly: System.Reflection.AssemblyVersion("0.1.0.0")] public class PreviousPackTracker {}' | Set-Content -LiteralPath $pluginSource
& $compiler /nologo /target:library "/out:$previousPlugin" $pluginSource
if ($LASTEXITCODE -ne 0) { throw 'Failed to build the previous plugin fixture.' }
Set-Content -LiteralPath $previousHelper -Value 'Previous helper'
Copy-Item -LiteralPath $previousPlugin, $previousHelper -Destination $target
$unrelated = Join-Path $target 'OtherPlugin.dll'
Set-Content -LiteralPath $unrelated -Value 'Other plugin'

$hostSource = Join-Path $testRoot 'FakeHdt.cs'
$hostExecutable = Join-Path $testRoot 'HearthstoneDeckTracker.exe'
$restarted = Join-Path $testRoot 'restarted'
$ready = Join-Path $testRoot 'ready'
$exitSignal = Join-Path $testRoot 'exit'
@'
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

class FakeHdt
{
    static void Main(string[] args)
    {
        var directory = AppDomain.CurrentDomain.BaseDirectory;
        if (args.Length == 0)
        {
            File.WriteAllText(Path.Combine(directory, "restarted"), "started");
            return;
        }

        using (var process = Process.GetCurrentProcess())
            File.WriteAllText(Path.Combine(directory, "ready.tmp"), process.StartTime.ToUniversalTime().Ticks.ToString());
        File.Move(Path.Combine(directory, "ready.tmp"), Path.Combine(directory, "ready"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!File.Exists(Path.Combine(directory, "exit")) && DateTime.UtcNow < deadline)
            Thread.Sleep(50);
    }
}
'@ | Set-Content -LiteralPath $hostSource
& $compiler /nologo /target:winexe "/out:$hostExecutable" $hostSource
if ($LASTEXITCODE -ne 0) { throw 'Failed to build the HDT process fixture.' }

$hostProcess = Start-Process -FilePath $hostExecutable -ArgumentList 'hold' -PassThru -WindowStyle Hidden
try {
    Wait-ForMarker $ready
    $stage = New-UpdateStage 'install' $installedPlugin $hostProcess.Id ([long](Get-Content -LiteralPath $ready -Raw))
    $updater = Start-StagedUpdater $stage
    if ($updater.WaitForExit(750)) {
        throw "The installer exited before the host. See $stage\update.log."
    }
    Assert-FileMatches $previousPlugin $installedPlugin
    Set-Content -LiteralPath $exitSignal -Value 'exit'
    if (!$updater.WaitForExit(15000) -or $updater.ExitCode -ne 0) {
        throw "The staged installation failed. See $stage\update.log."
    }
}
finally {
    if (!$hostProcess.HasExited) { $hostProcess.Kill() }
    $hostProcess.Dispose()
}
Wait-ForMarker $restarted

foreach ($file in 'PackTracker.dll', 'PackTracker.Updater.exe') {
    Assert-FileMatches (Join-Path $stage "payload\$file") (Join-Path $target $file)
    Assert-FileMatches (Join-Path $testRoot $file) (Join-Path $stage "backup\$file")
}

Copy-Item -LiteralPath $previousPlugin, $previousHelper -Destination $target -Force
Remove-Item -LiteralPath $restarted
$rollback = New-UpdateStage 'rollback' $installedPlugin ([int]::MaxValue) 1
$lockedPlugin = [IO.File]::Open($installedPlugin, 'Open', 'Read', 'Read')
try {
    $updater = Start-StagedUpdater $rollback
    if (!$updater.WaitForExit(15000) -or $updater.ExitCode -eq 0) {
        throw "The updater unexpectedly replaced a locked plugin. See $rollback\update.log."
    }
}
finally {
    $lockedPlugin.Dispose()
}
Wait-ForMarker $restarted
Assert-FileMatches $previousPlugin $installedPlugin
Assert-FileMatches $previousHelper $installedHelper
if ((Get-Content -LiteralPath $unrelated -Raw).Trim() -ne 'Other plugin') {
    throw 'The update affected an unrelated plugin.'
}
if (!(Test-Path -LiteralPath (Join-Path $rollback 'backup\PackTracker.Updater.exe'))) {
    throw 'The updater failed before it could prepare the rollback backup.'
}

Write-Output "Verified staged install, host exit and restart, backups, and rollback. Logs are under $testRoot."
