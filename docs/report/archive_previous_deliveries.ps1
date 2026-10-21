$ErrorActionPreference = 'Stop'
$workspaceRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$outputRoot = (Resolve-Path -LiteralPath (Join-Path $workspaceRoot 'output')).Path.TrimEnd('\')
$workRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot 'release-v1')).Path.TrimEnd('\')
$validation = Get-Content -LiteralPath (Join-Path $workRoot 'validation.json') -Raw | ConvertFrom-Json
$baseline = Get-Content -LiteralPath (Join-Path $workRoot 'baseline.json') -Raw | ConvertFrom-Json
if (-not $validation.validated) { throw 'New release has not passed validation.' }
foreach ($file in $validation.files) {
    if ((Get-FileHash -LiteralPath $file.path).Hash.ToLowerInvariant() -ne $file.sha256) {
        throw 'A validated deliverable changed.'
    }
}
$archiveRoot = [System.IO.Path]::GetFullPath((Join-Path $workRoot 'previous-deliveries'))
if (-not [System.String]::Equals([System.IO.Path]::GetDirectoryName($archiveRoot), $workRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Archive path must remain inside the intended process directory.'
}
$names = @('20261021-阅记-项目报告-v2.docx', '20261021-阅记-项目报告-v3.docx', '20261021-阅记-源码-v5.zip')
$moves = @()
foreach ($name in $names) {
    $source = [System.IO.Path]::GetFullPath((Join-Path $outputRoot $name))
    $target = [System.IO.Path]::GetFullPath((Join-Path $archiveRoot $name))
    if (-not [System.String]::Equals([System.IO.Path]::GetDirectoryName($source), $outputRoot, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Source path escaped output.' }
    if (-not [System.String]::Equals([System.IO.Path]::GetDirectoryName($target), $archiveRoot, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Target path escaped archive.' }
    if ((Get-Item -LiteralPath $source).PSIsContainer) { throw 'Only individual files may be archived.' }
    if (Test-Path -LiteralPath $target) { throw 'Archive target already exists.' }
    $hash = (Get-FileHash -LiteralPath $source).Hash.ToLowerInvariant()
    if ($hash -ne $baseline.old_output_files.PSObject.Properties[$name].Value) { throw 'Old delivery changed during processing.' }
    $moves += [pscustomobject]@{Source=$source;Target=$target;Sha256=$hash}
}
New-Item -ItemType Directory -Path $archiveRoot | Out-Null
foreach ($move in $moves) {
    Move-Item -LiteralPath $move.Source -Destination $move.Target -ErrorAction Stop
    if ((Get-FileHash -LiteralPath $move.Target).Hash.ToLowerInvariant() -ne $move.Sha256) { throw 'Archived file hash mismatch.' }
}
$result = [pscustomobject]@{
    Method='Reversible archive after permanent deletion was blocked by automatic approval policy'
    Archived=$moves
    RemainingRootFiles=@(Get-ChildItem -LiteralPath $outputRoot -File | Select-Object -ExpandProperty Name)
    NewVersionFiles=@(Get-ChildItem -LiteralPath (Join-Path $outputRoot 'v1') -File | Select-Object -ExpandProperty Name)
}
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $workRoot 'cleanup.json') -Encoding utf8
$result | ConvertTo-Json -Depth 6
