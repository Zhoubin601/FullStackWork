$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet restore ReadJournal.sln --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed' }
    dotnet build ReadJournal.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    dotnet test ReadJournal.sln -c Release --no-build --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
} finally { Pop-Location }
