$ErrorActionPreference = 'Stop'
$evidenceDir = "$PWD\docs\evidence\live-windows-test-20261022"
$dataDir = "$evidenceDir\data"
if (Test-Path $dataDir) { Remove-Item -Recurse -Force $dataDir }
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null

$env:READJOURNAL_DATA_DIR = $dataDir
$env:READJOURNAL_LOG_BINDINGS = "1"

$exePath = "$PWD\Code\ReadJournal\bin\Release\net8.0\ReadJournal.exe"
Write-Host "Starting ReadJournal from $exePath..."
$proc = Start-Process -FilePath $exePath -PassThru
Start-Sleep -Seconds 3

Write-Host "Process started with PID: $($proc.Id)"

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing

$condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
$windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children, $condition)
Write-Host "Found windows count: $($windows.Count)"

foreach ($w in $windows) {
    Write-Host "Window: '$($w.Current.Name)' NativeHandle: $($w.Current.NativeWindowHandle)"
    $buttons = $w.FindAll([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))
    Write-Host "Buttons found: $($buttons.Count)"
    foreach ($b in $buttons) {
        Write-Host "  - Button: '$($b.Current.Name)'"
    }
}

Write-Host "Stopping process..."
Stop-Process -Id $proc.Id -Force
Write-Host "Done!"
