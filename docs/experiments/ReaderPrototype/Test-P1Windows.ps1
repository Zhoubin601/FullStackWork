param(
    [Parameter(Mandatory=$true)][ValidateSet(100,150,200)][int]$DisplayScale,
    [Parameter(Mandatory=$true)][string]$SamplesDirectory,
    [Parameter(Mandatory=$true)][string]$NewEvidenceDirectory
)
$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'This script requires an actual Windows desktop.' }
$project = $PSScriptRoot
$testProject = Join-Path $project '../ReaderPrototype.Tests/ReaderPrototype.Tests.csproj'
$evidence = [IO.Path]::GetFullPath($NewEvidenceDirectory)
if (Test-Path $evidence) { throw 'Choose a new evidence directory; existing evidence is never overwritten.' }
$samples = [IO.Path]::GetFullPath($SamplesDirectory)
if (!(Test-Path (Join-Path $samples 'million.txt'))) { throw 'Generate the P0 synthetic samples first.' }
New-Item -ItemType Directory -Path $evidence | Out-Null
$sdk = (& dotnet --version)
if ($LASTEXITCODE -ne 0 -or $sdk -notmatch '^8\.0\.4\d\d$' -or ([version]$sdk -lt [version]'8.0.424')) {
    throw 'Select .NET SDK 8.0.424 or a later 8.0.4xx patch before running this acceptance script.'
}

$machine = [ordered]@{
    DeclaredDisplayScale = $DisplayScale
    OS = (Get-CimInstance Win32_OperatingSystem | Select-Object Caption,Version,OSArchitecture,TotalVisibleMemorySize)
    CPU = (Get-CimInstance Win32_Processor | Select-Object Name,NumberOfLogicalProcessors)
    SDK = $sdk
    Fonts = @(Get-ChildItem "$env:WINDIR/Fonts" -File | Select-Object -ExpandProperty Name)
    ManualChecks = 'Pending: actual 100/150/200% DPI appearance, mouse selection, clipboard paste, focus and window resize.'
}
$machine | ConvertTo-Json -Depth 6 | Set-Content -Encoding utf8 (Join-Path $evidence 'windows-environment.json')

& dotnet restore $testProject --locked-mode 2>&1 | Tee-Object (Join-Path $evidence 'restore.log')
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
& dotnet test $testProject -c Release --no-restore --logger 'trx;LogFileName=prototype.trx' --results-directory $evidence 2>&1 | Tee-Object (Join-Path $evidence 'tests.log')
if ($LASTEXITCODE -ne 0) { throw 'Native experiment tests failed.' }
& dotnet run --project (Join-Path $project 'ReaderPrototype.csproj') -c Release --no-build --no-restore -- --file (Join-Path $samples 'million.txt') --samples $samples --probe-output (Join-Path $evidence 'native') 2>&1 | Tee-Object (Join-Path $evidence 'native.log')
if ($LASTEXITCODE -ne 0) { throw 'Real desktop probe failed; inspect native/probe.json.' }
$probe = Get-Content -Raw (Join-Path $evidence 'native/probe.json') | ConvertFrom-Json
$expectedScale = $DisplayScale / 100.0
if ([Math]::Abs($probe.renderScaling - $expectedScale) -gt 0.01) {
    throw "Actual Avalonia scale $($probe.renderScaling) differs from declared $expectedScale; set the Windows display scale and rerun in a new directory."
}
Write-Host 'Automated Windows probe passed at the recorded actual scaling.'
Write-Host 'Windows compatibility remains unverified until real font appearance, drag/copy, input focus and resize checks are recorded for all three scaling levels. Current P1 uses the user-approved Linux acceptance platform.'
Write-Host 'Open the reader normally with --file <unicode.txt> or --file <million.txt> for the manual checks in README.md.'
