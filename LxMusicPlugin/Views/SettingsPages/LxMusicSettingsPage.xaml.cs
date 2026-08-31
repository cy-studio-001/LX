using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LxMusicPlugin.Models;
using LxMusicPlugin.Services;
using Material.Icons;
using Microsoft.Extensions.Logging;

namespace LxMusicPlugin.Views.SettingsPages;

[SettingsPageInfo(
    "lxmusic.settings",
    "LX Music 集成",
    PackIconKind.Music,
    PackIconKind.Music,
    SettingsPageCategory.External)]
public partial class LxMusicSettingsPage : SettingsPageBase
{
    public LxMusicSettingsPage()
    {
        InitializeComponent();
        DataContext = new LxMusicSettingsPageViewModel(
            AppBase.Current.Services.GetRequiredService<ILxMusicService>(),
            AppBase.Current.Services.GetRequiredService<LxMusicPluginSettings>(),
            AppBase.Current.Services.GetRequiredService<ILogger<LxMusicSettingsPageViewModel>>()
        );
    }
}

public partial class LxMusicSettingsPageViewModel : ObservableObject
{
    private readonly ILxMusicService _lxMusicService;
    private readonly LxMusicPluginSettings _settings;
    private readonly ILogger<LxMusicSettingsPageViewModel> _logger;
    private IDisposable? _statusSub;
    private IDisposable? _connSub;

    public LxMusicPluginSettings Settings => _settings;

    [ObservableProperty]
    private string _connectionStatus = "未测试";

    [ObservableProperty]
    private string _connectionStatusColor = "Gray";

    [ObservableProperty]
    private string _statusText = "未连接";

    [ObservableProperty]
    private string _statusColor = "Gray";

    [ObservableProperty]
    private string _currentSong = "无";

    [ObservableProperty]
    private string _playbackStatus = "停止";

    [ObservableProperty]
    private string _coverStatus = "无";

    [ObservableProperty]
    private string _lastUpdateTime = "从未";

    public IAsyncRelayCommand TestConnectionCommand { get; }

    public LxMusicSettingsPageViewModel(ILxMusicService lxMusicService, LxMusicPluginSettings settings, ILogger<LxMusicSettingsPageViewModel> logger)
    {
        _lxMusicService = lxMusicService;
        _settings = settings;
        _logger = logger;

        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);

        _statusSub = _lxMusicService.StatusChanged += OnStatusChanged;
        _connSub = _lxMusicService.ConnectionChanged += OnConnectionChanged;

        UpdateConnectionStatus(_lxMusicService.IsConnected);
    }

    private void OnConnectionChanged(object? sender, bool connected)
    {
        UpdateConnectionStatus(connected);
    }

    private void UpdateConnectionStatus(bool connected)
    {
        if (connected)
        {
            ConnectionStatus = "已连接";
            ConnectionStatusColor = "Green";
            StatusText = "正常";
            StatusColor = "Green";
        }
        else
        {
            ConnectionStatus = "未连接";
            ConnectionStatusColor = "Red";
            StatusText = "断开";
            StatusColor = "Red";
            CurrentSong = "无";
            PlaybackStatus = "停止";
            CoverStatus = "无";
        }
    }

    private void OnStatusChanged(object? sender, LxMusicStatus status)
    {
        CurrentSong = string.IsNullOrEmpty(status.Name) ? "未知歌曲" : $"{status.Name} - {status.Singer}";
        PlaybackStatus = status.Status switch
        {
            "playing" => "播放中",
            "paused" => "已暂停",
            "error" => "错误",
            _ => "停止"
        };
        CoverStatus = status.HasValidCover ? "已加载" : "无";
        LastUpdateTime = DateTime.Now.ToString("HH:mm:ss");
    }

    private async Task TestConnectionAsync()
    {
        ConnectionStatus = "测试中...";
        ConnectionStatusColor = "Orange";

        try
        {
            var connected = await _lxMusicService.TestConnectionAsync();
            UpdateConnectionStatus(connected);
            
            if (connected)
            {
                var status = await _lxMusicService.GetStatusAsync();
                if (status != null)
                {
                    OnStatusChanged(this, status);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed");
            ConnectionStatus = $"失败: {ex.Message}";
            ConnectionStatusColor = "Red";
        }
    }
}

public class BoolToIndexConverter
{
    public static readonly BoolToIndexConverter Instance = new();
    public int Convert(bool value) => value ? 0 : 1;
    public bool ConvertBack(int value) => value == 0;
}