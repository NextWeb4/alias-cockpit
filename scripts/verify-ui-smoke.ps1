param(
    [string]$ExePath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = [System.IO.Path]::GetFullPath((Join-Path $scriptDir ".."))
if ([string]::IsNullOrWhiteSpace($ExePath)) {
    $ExePath = Join-Path $workspace "artifacts\AliasCockpit-win-x64-portable\AliasCockpit.App.exe"
}

$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class Win32UiSmoke
{
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    public const int SW_RESTORE = 9;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_SHOWWINDOW = 0x0040;
}
"@

function Wait-ForMainWindow {
    param(
        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process,
        [int]$TimeoutSeconds = 15
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $Process.Refresh()
        if ($Process.MainWindowHandle -ne [IntPtr]::Zero) {
            return $Process.MainWindowHandle
        }

        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for main window."
}

function Get-WindowRect {
    param(
        [Parameter(Mandatory = $true)]
        [IntPtr]$Handle
    )

    $rect = New-Object Win32UiSmoke+RECT
    if (-not [Win32UiSmoke]::GetWindowRect($Handle, [ref]$rect)) {
        throw "GetWindowRect failed."
    }

    return $rect
}

function Get-AutomationRoot {
    param(
        [Parameter(Mandatory = $true)]
        [IntPtr]$Handle
    )

    $root = [System.Windows.Automation.AutomationElement]::FromHandle($Handle)
    if ($null -eq $root) {
        throw "Could not create UI Automation root."
    }

    return $root
}

function Find-AutomationElement {
    param(
        [Parameter(Mandatory = $true)]
        [System.Windows.Automation.AutomationElement]$Root,
        [Parameter(Mandatory = $true)]
        [string]$AutomationId,
        [int]$TimeoutSeconds = 10
    )

    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $AutomationId)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $element = $Root.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $condition)
        if ($null -ne $element) {
            return $element
        }

        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for automation element '$AutomationId'."
}

function Set-AutomationText {
    param(
        [Parameter(Mandatory = $true)]
        [System.Windows.Automation.AutomationElement]$Root,
        [Parameter(Mandatory = $true)]
        [string]$AutomationId,
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    $element = Find-AutomationElement -Root $Root -AutomationId $AutomationId
    $pattern = $null
    if ($element.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pattern)) {
        $pattern.SetValue($Value)
        Start-Sleep -Milliseconds 250
        return
    }

    $element.SetFocus()
    Start-Sleep -Milliseconds 150
    Set-FocusedText -Value $Value
}

function Invoke-AutomationButton {
    param(
        [Parameter(Mandatory = $true)]
        [System.Windows.Automation.AutomationElement]$Root,
        [Parameter(Mandatory = $true)]
        [string]$AutomationId
    )

    $element = Find-AutomationElement -Root $Root -AutomationId $AutomationId
    $pattern = $null
    if ($element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
        $pattern.Invoke()
        Start-Sleep -Milliseconds 500
        return
    }

    $element.SetFocus()
    Start-Sleep -Milliseconds 150
    Invoke-Key -VirtualKey 0x20 -Down $true
    Invoke-Key -VirtualKey 0x20 -Down $false
}

function Test-AutomationContentAvailable {
    param(
        [Parameter(Mandatory = $true)]
        [System.Windows.Automation.AutomationElement]$Root
    )

    try {
        Find-AutomationElement -Root $Root -AutomationId "EmailAddressBox" -TimeoutSeconds 2 | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

function Invoke-Click {
    param(
        [Parameter(Mandatory = $true)]
        [int]$X,
        [Parameter(Mandatory = $true)]
        [int]$Y
    )

    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point($X, $Y)
    Start-Sleep -Milliseconds 80
    [Win32UiSmoke]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [Win32UiSmoke]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 120
}

function Invoke-Key {
    param(
        [Parameter(Mandatory = $true)]
        [byte]$VirtualKey,
        [Parameter(Mandatory = $true)]
        [bool]$Down
    )

    $flags = 0
    if (-not $Down) {
        $flags = 0x0002
    }
    [Win32UiSmoke]::keybd_event($VirtualKey, 0, $flags, [UIntPtr]::Zero)
}

function Invoke-CtrlKey {
    param(
        [Parameter(Mandatory = $true)]
        [byte]$VirtualKey
    )

    Invoke-Key -VirtualKey 0x11 -Down $true
    Start-Sleep -Milliseconds 50
    Invoke-Key -VirtualKey $VirtualKey -Down $true
    Start-Sleep -Milliseconds 50
    Invoke-Key -VirtualKey $VirtualKey -Down $false
    Start-Sleep -Milliseconds 50
    Invoke-Key -VirtualKey 0x11 -Down $false
    Start-Sleep -Milliseconds 180
}

function Set-FocusedText {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    Set-Clipboard -Value $Value
    Start-Sleep -Milliseconds 120
    Invoke-CtrlKey -VirtualKey 0x41
    Invoke-CtrlKey -VirtualKey 0x56
    Start-Sleep -Milliseconds 250
}

function Wait-ForClipboardAlias {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Expected,
        [int]$TimeoutSeconds = 8
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $text = Get-Clipboard -Raw
        if ($text -like "*$Expected*") {
            return $text
        }

        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)

    throw "Clipboard did not contain expected alias '$Expected'. Last clipboard text: '$text'"
}

function Invoke-CopyAllFallback {
    param(
        [Parameter(Mandatory = $true)]
        [Win32UiSmoke+RECT]$Rect
    )

    $points = @(
        @{ X = $Rect.Right - 55; Y = $Rect.Top + 73 },
        @{ X = $Rect.Right - 80; Y = $Rect.Top + 73 },
        @{ X = $Rect.Right - 45; Y = $Rect.Top + 82 }
    )

    foreach ($point in $points) {
        Invoke-Click -X $point.X -Y $point.Y
        Start-Sleep -Milliseconds 300
    }
}

$handle = [IntPtr]::Zero
$process = Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe) -PassThru
try {
    $handle = Wait-ForMainWindow -Process $process
    [Win32UiSmoke]::ShowWindow($handle, [Win32UiSmoke]::SW_RESTORE) | Out-Null
    [Win32UiSmoke]::MoveWindow($handle, 80, 80, 1100, 740, $true) | Out-Null
    [Win32UiSmoke]::SetWindowPos(
        $handle,
        [Win32UiSmoke]::HWND_TOPMOST,
        0,
        0,
        0,
        0,
        [Win32UiSmoke]::SWP_NOMOVE -bor [Win32UiSmoke]::SWP_NOSIZE -bor [Win32UiSmoke]::SWP_SHOWWINDOW) | Out-Null
    [Win32UiSmoke]::SetForegroundWindow($handle) | Out-Null
    Start-Sleep -Seconds 1

    $rect = Get-WindowRect -Handle $handle
    $width = $rect.Right - $rect.Left
    if ($width -lt 900) {
        throw "Unexpectedly narrow main window: $width"
    }

    $root = Get-AutomationRoot -Handle $handle
    $useAutomation = Test-AutomationContentAvailable -Root $root
    if ($useAutomation) {
        Set-AutomationText -Root $root -AutomationId "EmailAddressBox" -Value "test.alias+old@gmail.com"
        Set-AutomationText -Root $root -AutomationId "TagsBox" -Value "login,work"
        Set-AutomationText -Root $root -AutomationId "CountBox" -Value "8"
    }
    else {
        Write-Host "UI Automation content unavailable; using coordinate fallback."
        Invoke-Click -X ($rect.Left + 200) -Y ($rect.Top + 240)
        Set-FocusedText -Value "test.alias+old@gmail.com"

        Invoke-Click -X ($rect.Left + 200) -Y ($rect.Top + 345)
        Set-FocusedText -Value "login,work"

        Invoke-Click -X ($rect.Left + 180) -Y ($rect.Top + 445)
        Set-FocusedText -Value "8"
    }

    Start-Sleep -Seconds 2

    $clipboardText = $null
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        if ($useAutomation) {
            Invoke-AutomationButton -Root $root -AutomationId "CopyAllButton"
        }
        else {
            Invoke-CopyAllFallback -Rect $rect
        }

        try {
            $clipboardText = Wait-ForClipboardAlias -Expected "testalias+login@gmail.com" -TimeoutSeconds 3
            break
        }
        catch {
            if ($attempt -eq 3) {
                throw
            }
        }
    }

    if ($clipboardText -notlike "*testalias@gmail.com*") {
        throw "Clipboard did not contain canonical Gmail alias. Clipboard text: '$clipboardText'"
    }

    $lineCount = ($clipboardText -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }).Count
    if ($lineCount -lt 2) {
        throw "Expected at least two generated aliases in clipboard, got $lineCount."
    }

    Write-Host "UI smoke passed."
}
finally {
    if (-not $process.HasExited) {
        if ($handle -ne [IntPtr]::Zero) {
            [Win32UiSmoke]::SetWindowPos(
                $handle,
                [Win32UiSmoke]::HWND_NOTOPMOST,
                0,
                0,
                0,
                0,
                [Win32UiSmoke]::SWP_NOMOVE -bor [Win32UiSmoke]::SWP_NOSIZE) | Out-Null
        }

        Stop-Process -Id $process.Id -Force
    }
}
