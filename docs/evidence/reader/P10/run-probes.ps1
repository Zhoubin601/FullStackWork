$ErrorActionPreference = 'Stop'
$evidence = Join-Path $PWD 'docs/evidence/reader/P10'
foreach ($phase in @('P2','P3','P4','P7')) {
    $project = "docs/tools/Reader${phase}Probe/Reader${phase}Probe.csproj"
    & dotnet restore $project --locked-mode *> "$evidence/$phase-restore.log"
    if ($LASTEXITCODE -ne 0) { throw "$phase restore failed" }
    & dotnet build $project -c Release --no-restore *> "$evidence/$phase-build.log"
    if ($LASTEXITCODE -ne 0) { throw "$phase build failed" }
    $probe = "docs/tools/Reader${phase}Probe/bin/Release/net8.0/Reader${phase}Probe.exe"
    [string[]]$probeArgs = @(switch ($phase) {
        'P2' { @('Code/ReadJournal.UnitTest/Fixtures/Reader/schema1-sql.json','Code/ReadJournal.UnitTest/Fixtures/Reader/schema1.json',"$evidence/p2-process") }
        'P3' { @("$evidence/p3-process") }
        'P4' { @("$evidence/p4-process") }
        'P7' { @('--compatibility',"$evidence/p7-compatibility") }
    })
    & $probe @probeArgs *> "$evidence/$phase-probe.log"
    if ($LASTEXITCODE -ne 0) { throw "$phase process probe failed; see $phase-probe.log" }
    Write-Output "$phase process probe passed"
}
& dotnet restore docs/experiments/ReaderPrototype.Tests/ReaderPrototype.Tests.csproj --locked-mode *> "$evidence/prototype-restore.log"
if ($LASTEXITCODE -ne 0) { throw 'Prototype restore failed' }
& dotnet test docs/experiments/ReaderPrototype.Tests/ReaderPrototype.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=windows-prototype.trx' --results-directory $evidence *> "$evidence/prototype-test.log"
if ($LASTEXITCODE -ne 0) { throw 'Prototype tests failed' }
Write-Output 'Prototype tests passed'
