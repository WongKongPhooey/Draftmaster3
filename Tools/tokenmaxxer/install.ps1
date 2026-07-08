# Registers the Tokenmaxxer scheduled task: fires controller.py every 30 minutes.
# Run once (re-run safe: replaces existing task). No admin needed.
$ErrorActionPreference = "Stop"

$taskName = "Tokenmaxxer"
$scriptDir = $PSScriptRoot
$pythonw = "C:\Python310\pythonw.exe"   # pythonw: no console window flash
$controller = Join-Path $scriptDir "controller.py"

if (-not (Test-Path $pythonw)) { throw "pythonw.exe not found at $pythonw" }
if (-not (Test-Path $controller)) { throw "controller.py not found at $controller" }

New-Item -ItemType Directory -Force "$env:LOCALAPPDATA\Tokenmaxxer" | Out-Null

$action = New-ScheduledTaskAction -Execute $pythonw -Argument "`"$controller`"" -WorkingDirectory $scriptDir
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) `
    -RepetitionInterval (New-TimeSpan -Minutes 30) -RepetitionDuration (New-TimeSpan -Days 3650)
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 3) -MultipleInstances IgnoreNew
# Interactive logon type = runs only when Josh is logged on, sharing the session
# with the open Unity editor (needed for Unity MCP EditMode tests).
$principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive

try { Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction Stop } catch {}
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger `
    -Settings $settings -Principal $principal | Out-Null

Write-Output "Registered scheduled task '$taskName' (every 30 min)."
Write-Output "Logs: $env:LOCALAPPDATA\Tokenmaxxer\log.txt"
