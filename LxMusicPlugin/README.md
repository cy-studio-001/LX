# LX Music Plugin for ClassIsland

将 LX Music 当前播放歌曲的封面设为 ClassIsland 主界面背景，并在底部显示实时歌词、歌曲信息、播放进度。

## 功能特性

- 🎵 **实时背景封面** - 播放时自动将主界面背景设为当前歌曲封面，支持透明度和模糊调节
- 📝 **实时歌词显示** - 底部组件显示当前播放歌词，支持原文/翻译双行显示
- 🎮 **播放控制** - 组件内置上一曲/播放暂停/下一曲按钮
- 📊 **进度显示** - 实时显示播放进度条和时间
- ⚙️ **完整设置** - API 地址、更新方式(SSE/轮询)、背景效果等全可配置
- 🔌 **自动连接** - 支持 SSE 实时推送和轮询两种模式，断线自动重连

## 效果预览

```
┌─────────────────────────────────────────────────────────────┐
│                    ClassIsland 主界面                        │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  课表组件、天气组件等...                              │   │
│  │                                                     │   │
│  │         (背景自动变为当前歌曲封面，带模糊效果)        │   │
│  │                                                     │   │
│  └─────────────────────────────────────────────────────┘   │
├─────────────────────────────────────────────────────────────┤
│  🎵 天使的翅膀 - 徐誉滕 · 李雷和韩梅梅      [⏮] [⏸] [⏭]    │
│  ████████████░░░░░░░░░░ 45%    1:37 / 3:34                 │
│  🎤 落叶随风将要去何方  |  只留给天空美丽一场               │
└─────────────────────────────────────────────────────────────┘
```

## 前置要求

1. **ClassIsland** v1.7.0+
2. **LX Music 桌面版** v2.7.0+ (已开启开放 API 服务)
3. **.NET 8.0 SDK**
4. **Visual Studio 2022** 或 **JetBrains Rider** (含 .NET 桌面开发工作负载)
5. **PowerShell Core** (`pwsh.exe`)

## LX Music 设置

1. 打开 LX Music 桌面版设置
2. 进入 **实验室** 页面
3. 开启 **开放 API 服务**
4. 默认端口 `23330`，API 地址：`http://127.0.0.1:23330`

## 构建插件

### 方式一：在 ClassIsland 源码树中构建（推荐）

```powershell
# 1. 克隆 ClassIsland 源码
git clone https://github.com/ClassIsland/ClassIsland
cd ClassIsland
git submodule update --init --recursive

# 2. 运行插件构建环境初始化脚本 (必须用 PowerShell Core)
pwsh ./tools/plugin/build.ps1

# 3. 将本插件源码放入 plugins 目录
# 假设插件源码在 D:\local programming\LX in CI\LxMusicPlugin
cp -r "D:\local programming\LX in CI\LxMusicPlugin" ./plugins/LxMusicPlugin

# 4. 在 ClassIsland.sln 中添加插件项目，或直接构建
dotnet build ./plugins/LxMusicPlugin/LxMusicPlugin.csproj -c Release

# 5. 构建输出在 plugins/LxMusicPlugin/bin/Release/net8.0-windows/
```

### 方式二：独立构建（需 ClassIsland.Core.dll）

如果已有 ClassIsland.Core.dll，可直接引用构建：

```powershell
cd "D:\local programming\LX in CI\LxMusicPlugin"
dotnet build -c Release
```

输出目录：`bin/Release/net8.0-windows/`

## 安装插件

1. 打开 ClassIsland 设置 → 插件
2. 点击「安装插件」，选择构建输出的 `LxMusicPlugin.dll` (或整个文件夹)
3. 重启 ClassIsland
4. 在设置 → 组件 中找到「LX Music 歌词」并拖拽到主界面底部区域
5. 在设置 → LX Music 集成 中配置 API 地址并测试连接

## 配置说明

| 设置项 | 说明 | 默认值 |
|--------|------|--------|
| API 服务地址 | LX Music 开放 API 地址 | `http://127.0.0.1:23330` |
| 启用背景封面 | 播放时自动更换主界面背景 | 开启 |
| 背景透明度 | 封面背景不透明度 | 60% |
| 启用模糊效果 | 对背景应用模糊，提升前景可读性 | 开启 |
| 启用歌词显示 | 底部组件显示歌词 | 开启 |
| 更新方式 | SSE 实时推送 / 轮询模式 | SSE |
| 轮询间隔 | 轮询模式下的更新间隔(ms) | 1000 |

## 架构设计

```
LxMusicPlugin/
├── LxMusicPlugin.cs              # 插件入口，实现 IPlugin
├── Services/
│   ├── LxMusicService.cs         # LX Music API 客户端 (SSE + 轮询)
│   ├── BackgroundCoverService.cs # 背景封面加载与渲染
│   └── LyricParser.cs            # LRC 歌词解析器
├── Models/
│   └── LxMusicModels.cs          # 数据模型 (状态、歌词、设置)
├── Views/
│   ├── Components/
│   │   ├── LxMusicLyricsComponent.xaml/.cs        # 主歌词组件
│   │   └── LxMusicLyricsComponentSettingsControl.xaml/.cs  # 组件设置
│   └── SettingsPages/
│       └── LxMusicSettingsPage.xaml/.cs           # 插件设置页面
├── Converters/
│   └── Converters.cs             # XAML 转换器
└── plugin.json                   # 插件清单
```

## 核心技术点

### SSE 实时订阅
```csharp
// 使用 Server-Sent Events 实时接收播放器状态变更
GET /subscribe-player-status
event: status
data: "playing"
event: name
data: "天使的翅膀"
```

### 背景渲染管线
1. 支持 HTTP URL 和 Data URL (base64) 两种图片源
2. 异步下载 + 缓存，避免 UI 阻塞
3. `ImageBrush` + `Stretch.UniformToFill` 全屏铺满
4. `Opacity` + 可选模糊效果 保证文字可读性

### 歌词解析
- 标准 LRC 格式解析 (`[mm:ss.xx]歌词`)
- 支持偏移量 (`[offset:xxx]`)
- 双语歌词合并 (原文 + 翻译按时间轴对齐)
- 实时高亮当前行

### 生命周期管理
- `IHostedService` 实现后台监控
- 组件 `Loaded/Unloaded` 自动订阅/取消订阅
- `IDisposable` 正确释放 HttpClient、Bitmap 等非托管资源
- 插件卸载时清理背景、停止监控任务

## 常见问题

### Q: 背景没有变化？
A: 检查设置中「启用背景封面」是否开启，且 LX Music 正在播放 (非暂停)。查看设置页面「当前状态」确认连接正常。

### Q: 歌词不同步？
A: 确保使用 SSE 模式。如果用轮询，建议间隔设为 500-1000ms。LX Music 本身歌词时间轴可能有偏移。

### Q: 组件不显示？
A: 在设置 → 组件 中将「LX Music 歌词」拖拽到主界面底部容器。组件高度自适应 (60-120px)。

### Q: 连接失败？
A: 
1. 确认 LX Music 已开启「开放 API 服务」
2. 确认端口正确 (默认 23330)
3. 尝试在浏览器打开 `http://127.0.0.1:23330/status` 验证

## 许可证

MIT License - 可自由使用、修改、分发

## 致谢

- [ClassIsland](https://github.com/ClassIsland/ClassIsland) - 优秀的课表软件平台
- [LX Music](https://github.com/lyswhut/lx-music-desktop) - 开源音乐播放器
- [Avalonia UI](https://avaloniaui.net/) - 跨平台 UI 框架