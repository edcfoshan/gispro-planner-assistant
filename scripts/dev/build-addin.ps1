[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$Register
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [Console]::OutputEncoding
$env:DOTNET_CLI_FORCE_UTF8_ENCODING = "1"
$env:MSBUILDUTF8OUTPUT = "1"

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$projectProbe = Join-Path $repoRoot "GisProAddinWorkbench.AddIn\GisProAddinWorkbench.AddIn.csproj"
if (!(Test-Path $projectProbe)) {
    # 兼容脚本被挪到 scripts 根目录的场景
    $repoRoot = Split-Path -Parent $PSScriptRoot
}
$projectPath = Join-Path $repoRoot "GisProAddinWorkbench.AddIn\GisProAddinWorkbench.AddIn.csproj"
$configPath = Join-Path $repoRoot "GisProAddinWorkbench.AddIn\Config.daml"
$outputPath = Join-Path $repoRoot "GisProAddinWorkbench.AddIn\bin\$Configuration\net8.0-windows7.0"
$iconSourcePath = Join-Path $repoRoot "GisProAddinWorkbench.AddIn\Images\Icon"
$artifactRoot = Join-Path $repoRoot "artifacts\$Configuration"
$stagingRoot = Join-Path $artifactRoot "staging"

if (!(Test-Path $projectPath)) {
    throw ("未找到项目文件: {0}" -f $projectPath)
}

if (!(Test-Path $configPath)) {
    throw ("未找到 Config.daml: {0}" -f $configPath)
}

[xml]$projectXml = Get-Content $projectPath -Raw -Encoding UTF8
$publicVersion = @(
    $projectXml.Project.PropertyGroup.PublicVersion |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
) | Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($publicVersion)) {
    $publicVersion = @(
        $projectXml.Project.PropertyGroup.Version |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    ) | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($publicVersion)) {
    throw "在项目文件中未找到 PublicVersion 或 Version。"
}

[xml]$configXml = Get-Content $configPath -Raw -Encoding UTF8
$addInInfo = $configXml.ArcGIS.AddInInfo
if ($null -eq $addInInfo) {
    throw "在 Config.daml 中未找到 AddInInfo。"
}

$configUpdated = $false
if ($addInInfo.version -ne $publicVersion) {
    $addInInfo.SetAttribute("version", $publicVersion)
    $configUpdated = $true
    Write-Host ("已同步 Config.daml 版本号为: {0}" -f $publicVersion)
}

$buildDate = Get-Date -Format "M/d/yyyy h:mm:ss tt"
$dateNodes = $configXml.GetElementsByTagName("Date")
if ($dateNodes.Count -eq 0) {
    throw "在 Config.daml 中未找到 Date 节点。"
}

$dateNode = $dateNodes.Item(0)
if ($dateNode.InnerText -ne $buildDate) {
    $dateNode.InnerText = $buildDate
    $configUpdated = $true
    Write-Host ("已更新 Config.daml 发布时间为: {0}" -f $buildDate)
}

if ($configUpdated) {
    $configXml.Save($configPath)
}

Write-Host "正在构建项目..."
& dotnet restore $projectPath
if ($LASTEXITCODE -ne 0) {
    throw ("dotnet restore 执行失败, 退出码: {0}" -f $LASTEXITCODE)
}

& dotnet clean $projectPath -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw ("dotnet clean 执行失败, 退出码: {0}" -f $LASTEXITCODE)
}

& dotnet build $projectPath -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    throw ("dotnet build 执行失败, 退出码: {0}" -f $LASTEXITCODE)
}

[xml]$configXml = Get-Content $configPath -Raw -Encoding UTF8
$defaultAssembly = $configXml.ArcGIS.defaultAssembly
if ([string]::IsNullOrWhiteSpace($defaultAssembly)) {
    throw "在 Config.daml 中未找到 defaultAssembly."
}

$packageName = "JisigG-Planner-Workbench_{0}_x64.esriAddinX" -f $publicVersion
$packagePath = Join-Path $artifactRoot $packageName
$zipPath = Join-Path $artifactRoot ([System.IO.Path]::GetFileNameWithoutExtension($packageName) + ".zip")
$installRoot = Join-Path $stagingRoot "Install"

if (Test-Path $artifactRoot) {
    Remove-Item $artifactRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
Copy-Item $configPath -Destination $stagingRoot
Copy-Item (Join-Path $outputPath "*") -Destination $installRoot -Recurse -Force
if (Test-Path $iconSourcePath) {
    $iconTargetPath = Join-Path $stagingRoot "Images\Icon"
    New-Item -ItemType Directory -Path $iconTargetPath -Force | Out-Null
    Copy-Item (Join-Path $iconSourcePath "*") -Destination $iconTargetPath -Recurse -Force
}

$vendoredToolboxFile = Join-Path $installRoot "Vendored\codex_arcgis-geocode-toolbox\百度高德Geocode.pyt"
if (!(Test-Path $vendoredToolboxFile)) {
    throw ("打包后仍未找到内置 PYT 工具箱: {0}" -f $vendoredToolboxFile)
}

# 保留内置 seed keys，首次安装/启动时会自动同步到用户目录。
$toolboxGitDir = Join-Path $installRoot "Vendored\codex_arcgis-geocode-toolbox\.git"
if (Test-Path $toolboxGitDir) {
    Remove-Item $toolboxGitDir -Recurse -Force
}

Write-Host "正在打包 add-in..."
Compress-Archive -Path (Join-Path $stagingRoot "*") -DestinationPath $zipPath -Force
Move-Item $zipPath $packagePath -Force
Remove-Item $stagingRoot -Recurse -Force

if ($Register) {
    $registerTool = "C:\Program Files\ArcGIS\Pro\bin\RegisterAddIn.exe"
    if (!(Test-Path $registerTool)) {
        throw ("未找到 RegisterAddIn.exe: {0}" -f $registerTool)
    }

    Write-Host "正在注册 add-in..."
    & $registerTool $packagePath /s
}

Write-Host "add-in 包已生成:"
Write-Host $packagePath
