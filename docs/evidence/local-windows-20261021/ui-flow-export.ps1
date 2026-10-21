$ErrorActionPreference='Stop'
$helper=Join-Path $PSScriptRoot 'uia.ps1'
$driver=Join-Path $PSScriptRoot 'windows-ui.py'
function Invoke-Button([string]$label) { & $helper -Action invoke -Name $label; Start-Sleep -Milliseconds 500 }
function Set-Field([int]$index,[string]$text) { & $helper -Action set -Index $index -Value $text; Start-Sleep -Milliseconds 120 }
function Dump-State([string]$file) { & $helper -Action dump | Out-File -LiteralPath (Join-Path $PSScriptRoot $file) -Encoding utf8 }
Invoke-Button '阅读计划'
Dump-State 'ui-12-plans.json'
& python $driver shot '12-plans'
Invoke-Button '阅读统计'
Dump-State 'ui-13-statistics.json'
& python $driver shot '13-statistics'
Invoke-Button '阅读概览'
Dump-State 'ui-14-overview.json'
& python $driver shot '14-overview'
Invoke-Button '设置与数据'
Invoke-Button '导出 JSON 备份'
Dump-State 'ui-15-export-picker.json'
