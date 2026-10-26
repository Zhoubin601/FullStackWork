$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing

Add-Type -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public class WindowCapture {
    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left, Top, Right, Bottom;
    }

    public static void Capture(IntPtr hwnd, string filename) {
        RECT rc;
        GetWindowRect(hwnd, out rc);
        int w = rc.Right - rc.Left;
        int h = rc.Bottom - rc.Top;
        if (w <= 0 || h <= 0) return;
        using (Bitmap bmp = new Bitmap(w, h)) {
            using (Graphics g = Graphics.FromImage(bmp)) {
                IntPtr hdc = g.GetHdc();
                PrintWindow(hwnd, hdc, 2); // PW_RENDERFULLCONTENT = 2
                g.ReleaseHdc(hdc);
            }
            bmp.Save(filename, ImageFormat.Png);
        }
    }

    public static void CloseWindow(IntPtr hwnd) {
        PostMessage(hwnd, 0x0010, IntPtr.Zero, IntPtr.Zero); // WM_CLOSE
    }
}
"@ -ReferencedAssemblies System.Drawing

$evidenceDir = "$PWD\docs\evidence\live-windows-test-20261022"
$dataDir = "$evidenceDir\data"
if (Test-Path $dataDir) { Remove-Item -Recurse -Force $dataDir }
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null

$env:READJOURNAL_DATA_DIR = $dataDir
$env:READJOURNAL_LOG_BINDINGS = "1"
$exePath = "$PWD\Code\ReadJournal\bin\Release\net8.0\ReadJournal.exe"

Write-Host "=== 启动阅记应用 (Release 版本) ==="
$proc = Start-Process -FilePath $exePath -PassThru
Start-Sleep -Seconds 3

function Get-AllElements {
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
    $windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children, $condition)
    $all = @()
    foreach ($w in $windows) {
        $all += $w
        $all += @($w.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition))
    }
    return $all
}

function Shot([string]$name) {
    $elems = Get-AllElements
    $win = @($elems | Where-Object { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Window -and $_.Current.Name -like "*阅记*" })[0]
    $hwnd = [IntPtr]$win.Current.NativeWindowHandle
    $filePath = Join-Path $evidenceDir "$name.png"
    [WindowCapture]::Capture($hwnd, $filePath)
    Write-Host "[主窗口截图] 保存至: $name.png"
}

function Shot-Dialog([string]$name) {
    $elems = Get-AllElements
    $dlgs = @($elems | Where-Object { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Window -and $_.Current.Name -ne "阅记 · 阅读与书摘" -and $_.Current.Name -ne "" })
    if ($dlgs.Count -gt 0) {
        $hwnd = [IntPtr]$dlgs[0].Current.NativeWindowHandle
        $filePath = Join-Path $evidenceDir "$name.png"
        [WindowCapture]::Capture($hwnd, $filePath)
        Write-Host "[弹窗截图] 保存至: $name.png"
    } else {
        Shot $name
    }
}

function Click-Btn([string]$name, [int]$idx = 0) {
    for ($attempt = 0; $attempt -lt 15; $attempt++) {
        $elems = Get-AllElements
        $matches = @($elems | Where-Object { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and $_.Current.Name -eq $name })
        if ($matches.Count -gt $idx) {
            $pattern = [System.Windows.Automation.InvokePattern]$matches[$idx].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
            $pattern.Invoke()
            Write-Host "[点击按钮] '$name'"
            Start-Sleep -Milliseconds 600
            return
        }
        Start-Sleep -Milliseconds 400
    }
    throw "未找到按钮: '$name' (重试超时)"
}

function Set-Edit([int]$idx, [string]$val) {
    for ($attempt = 0; $attempt -lt 10; $attempt++) {
        $elems = Get-AllElements
        $matches = @($elems | Where-Object { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Edit })
        if ($matches.Count -gt $idx) {
            $pattern = [System.Windows.Automation.ValuePattern]$matches[$idx].GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
            $pattern.SetValue($val)
            Write-Host "[输入框 $idx] -> '$val'"
            Start-Sleep -Milliseconds 150
            return
        }
        Start-Sleep -Milliseconds 300
    }
    throw "未找到输入框 $idx (重试超时)"
}

function Check-Box([string]$name) {
    for ($attempt = 0; $attempt -lt 10; $attempt++) {
        $elems = Get-AllElements
        $matches = @($elems | Where-Object { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::CheckBox -and $_.Current.Name -eq $name })
        if ($matches.Count -gt 0) {
            $pattern = [System.Windows.Automation.TogglePattern]$matches[0].GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
            $pattern.Toggle()
            Write-Host "[复选框] '$name' -> Checked"
            Start-Sleep -Milliseconds 200
            return
        }
        Start-Sleep -Milliseconds 300
    }
    throw "未找到复选框 '$name'"
}

# 步骤 1：首次启动 - 概览初始空状态
Write-Host "`n>>> 步骤 1: 验证初始概览页 <<<"
Shot "01-overview-empty"

# 步骤 2：切换至我的书库
Write-Host "`n>>> 步骤 2: 切换到我的书库 <<<"
Click-Btn "我的书库"
Shot "02-library-empty"

# 步骤 3：添加书籍与校验
Write-Host "`n>>> 步骤 3: 打开添加书籍并触发空校验 <<<"
Click-Btn "＋ 添加书籍"
Click-Btn "保存并返回"
Start-Sleep -Milliseconds 400
Shot "03-book-validation-error"

# 步骤 4：正确填写书籍信息并保存
Write-Host "`n>>> 步骤 4: 填写书籍信息并保存 <<<"
Set-Edit 0 "全栈开发实战与架构精要"
Set-Edit 1 "课程教学组"
Set-Edit 2 "计算机技术"
Set-Edit 3 "280"
Set-Edit 4 "2026秋季全栈大作业真机实测书籍样本。"
Shot "04-book-form-filled"
Click-Btn "保存并返回"
Start-Sleep -Seconds 1.5
Shot "05-book-in-library"

# 步骤 5：打开书籍详情
Write-Host "`n>>> 步骤 5: 打开书籍详情页 <<<"
Click-Btn "打开 →"
Start-Sleep -Seconds 1.5
Shot "06-book-detail"

# 步骤 6：添加阅读记录（模态窗口）
Write-Host "`n>>> 步骤 6: 添加阅读记录（模态窗口） <<<"
Click-Btn "＋ 阅读记录"
Start-Sleep -Seconds 1.5
Shot-Dialog "07-record-dialog"

Set-Edit 0 "1"
Set-Edit 1 "35"
Set-Edit 2 "40"
Shot-Dialog "08-record-dialog-filled"
Click-Btn "保存阅读记录"
Start-Sleep -Seconds 2
Shot "09-book-detail-with-record"

# 步骤 7：添加书摘并加入收藏
Write-Host "`n>>> 步骤 7: 添加书摘并加入收藏 <<<"
Click-Btn "＋ 书摘"
Start-Sleep -Seconds 1.5
Set-Edit 0 "软件设计的核心在于控制复杂度，抽象与分层是最好的武器。"
Set-Edit 1 "全栈开发中MVVM分层解耦的思想体现得淋漓尽致。"
Set-Edit 2 "15"
Check-Box "加入收藏"
Shot "10-quote-form-filled"
Click-Btn "保存并返回"
Start-Sleep -Seconds 1.5
Shot "11-book-detail-with-quote"

# 步骤 8：添加阅读计划
Write-Host "`n>>> 步骤 8: 添加阅读计划 <<<"
Click-Btn "＋ 阅读计划"
Start-Sleep -Seconds 1.5
Shot "12-plan-editor"
Click-Btn "保存并返回"
Start-Sleep -Seconds 1.5
Shot "13-book-detail-with-plan"

# 步骤 9：测试左上角返回按钮
Write-Host "`n>>> 步骤 9: 测试左上角返回按钮回到书库 <<<"
Click-Btn "← 返回"
Start-Sleep -Seconds 1.5
Shot "14-back-to-library"

# 步骤 10：切换至书摘收藏菜单
Write-Host "`n>>> 步骤 10: 切换至书摘收藏菜单 <<<"
Click-Btn "书摘收藏"
Start-Sleep -Seconds 1.5
Shot "15-quote-list"

# 步骤 11：切换至阅读计划菜单
Write-Host "`n>>> 步骤 11: 切换至阅读计划菜单 <<<"
Click-Btn "阅读计划"
Start-Sleep -Seconds 1.5
Shot "16-plan-list"

# 步骤 12：切换至阅读统计菜单
Write-Host "`n>>> 步骤 12: 切换至阅读统计菜单 <<<"
Click-Btn "阅读统计"
Start-Sleep -Seconds 1.5
Shot "17-statistics"

# 步骤 13：切换至阅读概览菜单
Write-Host "`n>>> 步骤 13: 切换至阅读概览菜单 <<<"
Click-Btn "阅读概览"
Start-Sleep -Seconds 1.5
Shot "18-overview-populated"

# 步骤 14：切换至设置与数据菜单
Write-Host "`n>>> 步骤 14: 切换至设置与数据菜单 <<<"
Click-Btn "设置与数据"
Start-Sleep -Seconds 1.5
Shot "19-settings"

# 步骤 15：安全退出应用
Write-Host "`n>>> 步骤 15: 正常关闭应用 <<<"
$elems = Get-AllElements
$win = @($elems | Where-Object { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Window -and $_.Current.Name -like "*阅记*" })[0]
[WindowCapture]::CloseWindow([IntPtr]$win.Current.NativeWindowHandle)
Start-Sleep -Seconds 2
if (!$proc.HasExited) {
    Write-Host "等待进程安全销毁..."
    $proc.WaitForExit(3000)
}
Write-Host "应用已退出，退出码: $($proc.ExitCode)"
Write-Host "`n=== 真机测试全流程执行完成 ==="
