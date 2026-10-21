param([string]$Action='dump',[string]$Name='', [string]$Value='', [int]$Index=0)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$testPid=[int](Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ui-pid.txt'))
$condition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$testPid)
$windows=[System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,$condition)
if ($windows.Count -eq 0) { throw 'No test application window' }
$elements=@()
foreach ($window in $windows) {
    $elements += $window
    $elements += @($window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition))
}
if ($Action -eq 'dump') {
    $elements | ForEach-Object { [pscustomobject]@{Name=$_.Current.Name;Type=$_.Current.ControlType.ProgrammaticName;Enabled=$_.Current.IsEnabled;Offscreen=$_.Current.IsOffscreen;Rect=$_.Current.BoundingRectangle.ToString()} } | ConvertTo-Json -Depth 3
} else {
    if($Action -eq 'set') { $matches=@($elements | Where-Object {$_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Edit}) }
    elseif($Action -eq 'expand') { $matches=@($elements | Where-Object {$_.Current.ControlType -eq [System.Windows.Automation.ControlType]::ComboBox}) }
    else { $matches=@($elements | Where-Object {$_.Current.Name -eq $Name}) }
    if($matches.Count -le $Index) { throw "Element missing: $Name index $Index" }
    $target=$matches[$Index]
    if($Action -eq 'invoke') { $pattern=[System.Windows.Automation.InvokePattern]$target.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern); $pattern.Invoke() }
    elseif($Action -eq 'set') { $pattern=[System.Windows.Automation.ValuePattern]$target.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern); $pattern.SetValue($Value) }
    elseif($Action -eq 'select') { $pattern=[System.Windows.Automation.SelectionItemPattern]$target.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern); $pattern.Select() }
    elseif($Action -eq 'expand') { $pattern=[System.Windows.Automation.ExpandCollapsePattern]$target.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern); $pattern.Expand() }
}
