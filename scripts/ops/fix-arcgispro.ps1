param(
    [switch]$ResetArcGISProfile,
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Continue'

function Write-Step($msg) {
    Write-Host "`n==> $msg" -ForegroundColor Cyan
}

function Write-Ok($msg) {
    Write-Host "[OK] $msg" -ForegroundColor Green
}

function Write-WarnMsg($msg) {
    Write-Host "[WARN] $msg" -ForegroundColor Yellow
}

function Is-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($id)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

$arcProExe = 'C:\Program Files\ArcGIS\Pro\bin\ArcGISPro.exe'
$arcBinDir = 'C:\Program Files\ArcGIS\Pro\bin'

Write-Step 'Pre-checks'
if (-not (Test-Path $arcProExe)) {
    Write-Host "[ERROR] ArcGIS Pro executable not found: $arcProExe" -ForegroundColor Red
    exit 1
}
Write-Ok "Found ArcGIS Pro: $arcProExe"

Write-Step 'Stop AnyShare / sharesvc hooks if present'
$services = Get-CimInstance Win32_Service -ErrorAction SilentlyContinue |
    Where-Object {
        $_.DisplayName -eq 'AnyShare Service' -or
        $_.Name -eq 'AnyShare Service' -or
        $_.Name -eq 'AnyShareService' -or
        ($_.PathName -match '(?i)\\sharesvc\.exe')
    }

if ($services) {
    foreach ($svc in $services) {
        Write-Host "Service found: $($svc.Name) [$($svc.State)] Path=$($svc.PathName)"
        try {
            if ($svc.State -eq 'Running') {
                Stop-Service -Name $svc.Name -Force -ErrorAction Stop
                Write-Ok "Stopped service: $($svc.Name)"
            }
            Set-Service -Name $svc.Name -StartupType Manual -ErrorAction Stop
            Write-Ok "Set startup type Manual: $($svc.Name)"
        }
        catch {
            Write-WarnMsg "Failed to manage service $($svc.Name): $($_.Exception.Message)"
        }
    }
}
else {
    Write-WarnMsg 'No AnyShare-related service detected.'
}

$hookProcs = @('sharesvc')
foreach ($pn in $hookProcs) {
    $procs = Get-Process -Name $pn -ErrorAction SilentlyContinue
    if ($procs) {
        foreach ($p in $procs) {
            try {
                Stop-Process -Id $p.Id -Force -ErrorAction Stop
                Write-Ok "Stopped process: $($p.ProcessName) (PID $($p.Id))"
            }
            catch {
                Write-WarnMsg "Could not stop process $($p.ProcessName) PID $($p.Id): $($_.Exception.Message)"
            }
        }
    }
}

Write-Step 'Ensure Windows Defender exclusion for ArcGIS Pro bin'
if (Is-Admin) {
    try {
        $prefs = Get-MpPreference
        if ($prefs.ExclusionPath -contains $arcBinDir) {
            Write-Ok "Defender exclusion already exists: $arcBinDir"
        }
        else {
            Add-MpPreference -ExclusionPath $arcBinDir -ErrorAction Stop
            Write-Ok "Added Defender exclusion: $arcBinDir"
        }
    }
    catch {
        Write-WarnMsg "Unable to set Defender exclusion automatically: $($_.Exception.Message)"
    }
}
else {
    Write-WarnMsg 'Not running as Administrator. Defender exclusion check/add may fail.'
    Write-Host "Run as admin if needed: Add-MpPreference -ExclusionPath '$arcBinDir'"
}

if ($ResetArcGISProfile) {
    Write-Step 'Reset ArcGIS Pro user profile/cache (backup then recreate)'
    $ts = Get-Date -Format 'yyyyMMdd_HHmmss'
    $targets = @(
        (Join-Path $env:APPDATA 'Esri\ArcGISPro'),
        (Join-Path $env:LOCALAPPDATA 'ESRI\ArcGISPro')
    )

    foreach ($t in $targets) {
        if (Test-Path $t) {
            try {
                $bak = "$t.bak_$ts"
                Rename-Item -Path $t -NewName (Split-Path $bak -Leaf) -ErrorAction Stop
                Write-Ok "Backed up: $bak"
            }
            catch {
                Write-WarnMsg "Failed to backup ${t}: $($_.Exception.Message)"
            }
        }
        else {
            Write-WarnMsg "Not found (skip): $t"
        }
    }
}

if (-not $NoLaunch) {
    Write-Step 'Launch ArcGIS Pro'
    try {
        $p = Start-Process -FilePath $arcProExe -PassThru -ErrorAction Stop
        Start-Sleep -Seconds 8
        if (Get-Process -Id $p.Id -ErrorAction SilentlyContinue) {
            Write-Ok "ArcGIS Pro started (PID $($p.Id))."
        }
        else {
            Write-WarnMsg 'ArcGIS Pro process exited quickly. Check Event Viewer (.NET Runtime/Application Error).'
        }
    }
    catch {
        Write-Host "[ERROR] Failed to start ArcGIS Pro: $($_.Exception.Message)" -ForegroundColor Red
        exit 2
    }
}
else {
    Write-Ok 'NoLaunch specified, script finished without starting ArcGIS Pro.'
}

Write-Host "`nCompleted." -ForegroundColor Cyan
