[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [string]$PackagePath,
    [string]$ProjectPath,
    [switch]$BuildLatest,
    [switch]$SkipClearAssemblyCache,
    [switch]$ForceCloseArcGISPro
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [Console]::OutputEncoding
$env:DOTNET_CLI_FORCE_UTF8_ENCODING = "1"
$env:MSBUILDUTF8OUTPUT = "1"

# 默认工程路径不做用户硬编码，优先由调用参数与自动探测决定
$DefaultProjectPath = $null
$CacheRetryCount = 3
$CacheRetrySeconds = 1

function New-UserErrorMessage {
    param(
        [string]$Stage,
        [string]$Reason,
        [string]$Suggestion
    )

    return ("[错误] 阶段={0}；原因={1}；建议={2}" -f $Stage, $Reason, $Suggestion)
}

function Throw-UserError {
    param(
        [string]$Stage,
        [string]$Reason,
        [string]$Suggestion
    )

    throw (New-UserErrorMessage -Stage $Stage -Reason $Reason -Suggestion $Suggestion)
}

function Resolve-ProjectPath {
    param(
        [string]$PreferredPath,
        [string]$RepoRoot,
        [string]$DefaultPath
    )

    if (-not [string]::IsNullOrWhiteSpace($PreferredPath)) {
        $resolved = Resolve-Path -Path $PreferredPath -ErrorAction Stop
        if ([System.IO.Path]::GetExtension($resolved.Path) -ne ".aprx") {
            Throw-UserError -Stage "ResolveProjectPath" -Reason ("ProjectPath 不是 .aprx 文件: {0}" -f $resolved.Path) -Suggestion "请传入有效的 .aprx 工程路径。"
        }

        return $resolved.Path
    }

    if (-not [string]::IsNullOrWhiteSpace($DefaultPath) -and (Test-Path $DefaultPath)) {
        return (Resolve-Path -Path $DefaultPath).Path
    }

    $repoProject = Get-ChildItem -Path $RepoRoot -Recurse -Filter *.aprx -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -ne $repoProject) {
        return $repoProject.FullName
    }

    $defaultProjectsRoot = Join-Path $env:USERPROFILE "Documents\ArcGIS\Projects"
    if (Test-Path $defaultProjectsRoot) {
        $userProject = Get-ChildItem -Path $defaultProjectsRoot -Recurse -Filter *.aprx -File -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($null -ne $userProject) {
            return $userProject.FullName
        }
    }

    Throw-UserError -Stage "ResolveProjectPath" -Reason "未找到 .aprx 工程文件。" -Suggestion "请显式传入 -ProjectPath。"
}

function Resolve-RegisterToolPath {
    $candidate = "C:\Program Files\ArcGIS\Pro\bin\RegisterAddIn.exe"
    if (-not (Test-Path $candidate)) {
        Throw-UserError -Stage "ResolveRegisterToolPath" -Reason ("未找到 RegisterAddIn.exe: {0}" -f $candidate) -Suggestion "请确认 ArcGIS Pro 已正确安装。"
    }

    return $candidate
}

function Resolve-ArcGISProExePath {
    $candidate = "C:\Program Files\ArcGIS\Pro\bin\ArcGISPro.exe"
    if (Test-Path $candidate) {
        return $candidate
    }

    return $null
}

function Ensure-ArcGISProStopped {
    param(
        [switch]$ForceStop
    )

    $running = Get-Process -Name ArcGISPro -ErrorAction SilentlyContinue
    if ($null -eq $running) {
        return
    }

    if (-not $ForceStop) {
        Throw-UserError -Stage "StopArcGISPro" -Reason "检测到 ArcGIS Pro 正在运行。" -Suggestion "请先手动关闭 ArcGIS Pro，或加上 -ForceCloseArcGISPro 参数。"
    }

    Write-Host "检测到 ArcGIS Pro 正在运行，正在尝试关闭..."
    try {
        $running | Stop-Process -Force -ErrorAction Stop
    }
    catch {
        Throw-UserError -Stage "StopArcGISPro" -Reason ("自动关闭 ArcGISPro.exe 失败：{0}" -f $_.Exception.Message) -Suggestion "请使用管理员权限重试，或先手动关闭 ArcGISPro.exe。"
    }

    Start-Sleep -Milliseconds 500
    if ($null -ne (Get-Process -Name ArcGISPro -ErrorAction SilentlyContinue)) {
        Throw-UserError -Stage "StopArcGISPro" -Reason "尝试关闭后 ArcGIS Pro 仍在运行。" -Suggestion "请手动结束 ArcGISPro.exe 后重试。"
    }

    Write-Host "ArcGIS Pro 已关闭。"
}

function Clear-AssemblyCache {
    param(
        [int]$MaxRetries = 3,
        [int]$RetryDelaySeconds = 1
    )

    $assemblyCachePath = Join-Path $env:LOCALAPPDATA "ESRI\ArcGISPro\AssemblyCache"
    if (-not (Test-Path $assemblyCachePath)) {
        Write-Host ("未找到 AssemblyCache 目录，跳过清理：{0}" -f $assemblyCachePath)
        return
    }

    for ($attempt = 1; $attempt -le $MaxRetries; $attempt++) {
        try {
            $items = @(Get-ChildItem -Path $assemblyCachePath -Force -ErrorAction Stop)
            if ($items.Count -eq 0) {
                Write-Host ("AssemblyCache 已为空：{0}" -f $assemblyCachePath)
                return
            }

            $items | Remove-Item -Recurse -Force -ErrorAction Stop
            Write-Host ("AssemblyCache 清理完成：{0}" -f $assemblyCachePath)
            return
        }
        catch [System.UnauthorizedAccessException], [System.IO.IOException] {
            $detail = $_.Exception.Message
            if ($attempt -lt $MaxRetries) {
                Write-Host ("AssemblyCache 清理失败，正在重试（{0}/{1}）：{2}" -f $attempt, $MaxRetries, $detail)
                Start-Sleep -Seconds $RetryDelaySeconds
                continue
            }

            Throw-UserError -Stage "ClearAssemblyCache" -Reason ("缓存文件被占用或无权限删除。路径：{0}。详情：{1}" -f $assemblyCachePath, $detail) -Suggestion "请确认 ArcGIS Pro 已完全退出，等待数秒后重试；如仍失败，请以管理员身份运行。"
        }
        catch {
            Throw-UserError -Stage "ClearAssemblyCache" -Reason ("清理 AssemblyCache 时发生异常：{0}" -f $_.Exception.Message) -Suggestion "请检查目录权限并重试。"
        }
    }
}

function Resolve-RepoRoot {
    $repoRootFromDevScript = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    if (Test-Path (Join-Path $repoRootFromDevScript "GisProAddinWorkbench.sln")) {
        return $repoRootFromDevScript
    }

    $repoRootFromScriptsRoot = Split-Path -Parent $PSScriptRoot
    if (Test-Path (Join-Path $repoRootFromScriptsRoot "GisProAddinWorkbench.sln")) {
        return $repoRootFromScriptsRoot
    }

    Throw-UserError -Stage "ResolveRepoRoot" -Reason ("无法定位仓库根目录。脚本目录：{0}" -f $PSScriptRoot) -Suggestion "请确认脚本位于 scripts\\dev 或 scripts 目录下。"
}

function Invoke-RefreshAddinAndOpen {
    $repoRoot = Resolve-RepoRoot
    $buildScriptPath = Join-Path $PSScriptRoot "build-addin.ps1"

    if ([string]::IsNullOrWhiteSpace($PackagePath)) {
        $PackagePath = Join-Path $repoRoot "artifacts\$Configuration\GisProAddinWorkbench.AddIn.esriAddinX"
    }

    if ($BuildLatest) {
        if (-not (Test-Path $buildScriptPath)) {
            Throw-UserError -Stage "BuildAddin" -Reason ("未找到构建脚本：{0}" -f $buildScriptPath) -Suggestion "请检查 scripts\\dev 目录是否完整。"
        }

        Write-Host "正在构建最新 add-in 包..."
        & $buildScriptPath -Configuration $Configuration
    }

    if (-not (Test-Path $PackagePath)) {
        Throw-UserError -Stage "CheckAddinPackage" -Reason ("未找到 add-in 包：{0}" -f $PackagePath) -Suggestion "请先构建 add-in，或通过 -PackagePath 指定正确路径。"
    }

    Ensure-ArcGISProStopped -ForceStop:$ForceCloseArcGISPro

    if (-not $SkipClearAssemblyCache) {
        Clear-AssemblyCache -MaxRetries $CacheRetryCount -RetryDelaySeconds $CacheRetrySeconds
    }
    else {
        Write-Host "已跳过 AssemblyCache 清理。"
    }

    $registerToolPath = Resolve-RegisterToolPath
    Write-Host ("正在注册 add-in 包：{0}" -f $PackagePath)
    $registerProcess = Start-Process -FilePath $registerToolPath -ArgumentList @($PackagePath, "/s") -Wait -PassThru
    if ($registerProcess.ExitCode -ne 0) {
        Throw-UserError -Stage "RegisterAddin" -Reason ("RegisterAddIn.exe 返回非零退出码：{0}" -f $registerProcess.ExitCode) -Suggestion "请检查 add-in 包完整性与 ArcGIS Pro 安装状态。"
    }

    $resolvedProjectPath = Resolve-ProjectPath -PreferredPath $ProjectPath -RepoRoot $repoRoot -DefaultPath $DefaultProjectPath
    $arcgisProExePath = Resolve-ArcGISProExePath

    if ($null -ne $arcgisProExePath) {
        Write-Host ("正在打开 ArcGIS Pro 工程：{0}" -f $resolvedProjectPath)
        Start-Process -FilePath $arcgisProExePath -ArgumentList "`"$resolvedProjectPath`""
    }
    else {
        Write-Host ("未找到 ArcGISPro.exe，将通过文件关联打开工程：{0}" -f $resolvedProjectPath)
        Start-Process -FilePath $resolvedProjectPath
    }

    Write-Host "执行完成。"
}

try {
    Invoke-RefreshAddinAndOpen
    exit 0
}
catch {
    $message = $_.Exception.Message
    if ($null -ne $message) {
        $message = $message.Trim()
    }
    if ([string]::IsNullOrWhiteSpace($message)) {
        $message = "[错误] 阶段=Unknown；原因=发生未知错误；建议=请重试并检查脚本输出。"
    }
    elseif ($message -notmatch '^\[错误\]') {
        $message = ("[错误] 阶段=Unhandled；原因={0}；建议=请重试，若仍失败请反馈完整日志。" -f $message)
    }

    Write-Host $message
    exit 1
}
