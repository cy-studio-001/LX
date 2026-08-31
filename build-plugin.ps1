<#>
.SYNOPSIS
    LX Music Plugin for ClassIsland - 构建脚本
.DESCRIPTION
    在 ClassIsland 源码环境中构建 LX Music 插件
    必须使用 PowerShell Core (pwsh.exe) 运行
.NOTES
    作者: LX Music Plugin Author
    版本: 1.0.0
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$ClassIslandRepoPath = "..\ClassIsland",
    
    [Parameter(Mandatory=$false)]
    [string]$PluginSourcePath = ".\LxMusicPlugin",
    
    [Parameter(Mandatory=$false)]
    [Switch]$Clean,
    
    [Parameter(Mandatory=$false)]
    [Switch]$SetupOnly
)

# 检查是否为 PowerShell Core
if ($PSVersionTable.PSEdition -ne 'Core') {
    Write-Error "必须使用 PowerShell Core (pwsh.exe) 运行此脚本！"
    Write-Host "请安装 PowerShell Core: https://github.com/PowerShell/PowerShell"
    exit 1
}

$ErrorActionPreference = "Stop"

function Write-Log($message, $level = "INFO") {
    $timestamp = Get-Date -Format "HH:mm:ss"
    $color = switch ($level) {
        "ERROR" { "Red" }
        "WARN"  { "Yellow" }
        "SUCCESS" { "Green" }
        default { "Cyan" }
    }
    Write-Host "[$timestamp] [$level] $message" -ForegroundColor $color
}

# 路径处理
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$pluginSource = Resolve-Path (Join-Path $scriptDir $PluginSourcePath)
$classislandRoot = Resolve-Path (Join-Path $scriptDir $ClassIslandRepoPath)

Write-Log "=== LX Music Plugin 构建脚本 ==="
Write-Log "插件源码: $pluginSource"
Write-Log "ClassIsland 根目录: $classislandRoot"

# 检查 ClassIsland 源码
if (-not (Test-Path (Join-Path $classislandRoot "ClassIsland.sln"))) {
    Write-Error "未找到 ClassIsland.sln，请确保 ClassIsland 源码在正确位置"
    Write-Host "可通过 -ClassIslandRepoPath 参数指定路径"
    exit 1
}

# 检查插件源码
if (-not (Test-Path (Join-Path $pluginSource "LxMusicPlugin.csproj"))) {
    Write-Error "未找到插件项目文件"
    exit 1
}

# 目标插件目录
$pluginTargetDir = Join-Path $classislandRoot "plugins" "LxMusicPlugin"

if ($Clean) {
    Write-Log "清理旧构建..." "WARN"
    if (Test-Path $pluginTargetDir) {
        Remove-Item $pluginTargetDir -Recurse -Force
    }
    if (Test-Path (Join-Path $pluginSource "bin")) {
        Remove-Item (Join-Path $pluginSource "bin") -Recurse -Force
    }
    if (Test-Path (Join-Path $pluginSource "obj")) {
        Remove-Item (Join-Path $pluginSource "obj") -Recurse -Force
    }
}

# 复制插件源码到 ClassIsland plugins 目录
Write-Log "复制插件源码到 ClassIsland plugins 目录..."
if (Test-Path $pluginTargetDir) {
    Remove-Item $pluginTargetDir -Recurse -Force
}
Copy-Item $pluginSource $pluginTargetDir -Recurse -Force

# 如果只是设置环境，不构建
if ($SetupOnly) {
    Write-Log "环境设置完成，跳过构建" "SUCCESS"
    Write-Host "插件已部署到: $pluginTargetDir"
    Write-Host "请在 Visual Studio/Rider 中打开 $classislandRoot\ClassIsland.sln 并构建 LxMusicPlugin 项目"
    exit 0
}

# 进入 ClassIsland 目录构建
Set-Location $classislandRoot

# 确保子模块已初始化
Write-Log "检查 Git 子模块..."
git submodule update --init --recursive

# 还原 NuGet 包
Write-Log "还原 NuGet 包..."
dotnet restore ClassIsland.sln

# 构建插件项目
Write-Log "构建 LxMusicPlugin..."
$buildResult = dotnet build (Join-Path $pluginTargetDir "LxMusicPlugin.csproj") -c Release --no-restore

if ($LASTEXITCODE -ne 0) {
    Write-Log "构建失败！" "ERROR"
    exit 1
}

Write-Log "构建成功！" "SUCCESS"

# 显示输出路径
$outputDir = Join-Path $pluginTargetDir "bin\Release\net8.0-windows"
if (Test-Path $outputDir) {
    Write-Log "输出目录: $outputDir"
    Get-ChildItem $outputDir | ForEach-Object {
        Write-Host "  $_" -ForegroundColor Gray
    }
}

Write-Log "=== 后续步骤 ===" "SUCCESS"
Write-Host "1. 在 ClassIsland 设置 → 插件 中点击「安装插件」"
Write-Host "2. 选择输出目录下的 LxMusicPlugin.dll 或整个文件夹"
Write-Host "3. 重启 ClassIsland"
Write-Host "4. 在设置 → 组件 中添加「LX Music 歌词」到主界面底部"
Write-Host "5. 在设置 → LX Music 集成 中配置 API 地址并测试连接"