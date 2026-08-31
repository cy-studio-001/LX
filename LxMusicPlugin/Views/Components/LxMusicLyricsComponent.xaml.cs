using System.Windows.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Controls.Components;
using LxMusicPlugin.Models;
using LxMusicPlugin.Services;
using Microsoft.Extensions.Logging;

namespace LxMusicPlugin.Views.Components;

public partial class LxMusicLyricsComponent : ComponentBase<LxMusicLyricsComponentSettings>
{
    public LxMusicLyricsComponent()
    {
        InitializeComponent();
        DataContext = new LxMusicLyricsComponentViewModel(
            AppBase.Current.Services.GetRequiredService<ILxMusicService>(),
            AppBase.Current.Services.GetRequiredService<IBackgroundCoverService>(),
            AppBase.Current.Services.GetRequiredService<ILogger<LxMusicLyricsComponentViewModel>>(),
            Settings
        );
    }
}

public partial class LxMusicLyricsComponentViewModel : ObservableObject
{
    private readonly ILxMusicService _lxMusicService;
    private readonly IBackgroundCoverService _backgroundService;
    private readonly ILogger<LxMusicLyricsComponentViewModel> _logger;
    private readonly LxMusicLyricsComponentSettings _settings;
    private List<LyricLine> _parsedLyrics = new();
    private IDisposable? _statusSub;
    private IDisposable? _bgSub;

    [ObservableProperty]
    private string _songName = "暂无播放";

    [ObservableProperty]
    private string _artistAlbum = "";

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _progressText = "0:00 / 0:00";

    [ObservableProperty]
    private string _currentLyricLine = "";

    [ObservableProperty]
    private Bitmap? _coverImage;

    [ObservableProperty]
    private bool _hasCover;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private string _playPauseTooltip = "播放";

    [ObservableProperty]
    private StreamGeometry _playPauseIcon = PlayIcon;

    [ObservableProperty]
    private bool _showLyrics = true;

    public static readonly StreamGeometry PlayIcon = StreamGeometry.Parse("M8,5v14l11,-7z");
    public static readonly StreamGeometry PauseIcon = StreamGeometry.Parse("M6,19h4V5H6v14zm8-14v14h4V5h-4z");

    public ICommand PrevCommand { get; }
    public ICommand PlayPauseCommand { get; }
    public ICommand NextCommand { get; }

    public LxMusicLyricsComponentViewModel(ILxMusicService lxMusicService, IBackgroundCoverService backgroundService, 
        ILogger<LxMusicLyricsComponentViewModel> logger, LxMusicLyricsComponentSettings settings)
    {
        _lxMusicService = lxMusicService;
        _backgroundService = backgroundService;
        _logger = logger;
        _settings = settings;

        PrevCommand = new AsyncRelayCommand(SkipPrevAsync);
        PlayPauseCommand = new AsyncRelayCommand(PlayPauseAsync);
        NextCommand = new AsyncRelayCommand(SkipNextAsync);

        _statusSub = _lxMusicService.StatusChanged += OnStatusChanged;
        _bgSub = _backgroundService.BackgroundChanged += OnBackgroundChanged;
        _lxMusicService.ConnectionChanged += OnConnectionChanged;

        IsConnected = _lxMusicService.IsConnected;
        ShowLyrics = _settings.ShowLyrics;
        
        if (_lxMusicService.CurrentStatus != null)
        {
            OnStatusChanged(this, _lxMusicService.CurrentStatus);
        }
    }

    private void OnConnectionChanged(object? sender, bool connected)
    {
        IsConnected = connected;
        if (!connected)
        {
            SongName = "LX Music 未连接";
            ArtistAlbum = "请在设置中检查 API 地址";
            CurrentLyricLine = "";
            HasCover = false;
            CoverImage = null;
        }
    }

    private void OnBackgroundChanged(object? sender, Avalonia.Media.IBrush? brush)
    {
        if (brush is ImageBrush imgBrush && imgBrush.Source is Bitmap bitmap)
        {
            CoverImage = bitmap;
            HasCover = true;
        }
        else
        {
            HasCover = false;
            CoverImage = null;
        }
    }

    private void OnStatusChanged(object? sender, LxMusicStatus status)
    {
        SongName = string.IsNullOrEmpty(status.Name) ? "未知歌曲" : status.Name;
        ArtistAlbum = BuildArtistAlbum(status);
        
        IsPlaying = status.IsPlaying;
        PlayPauseTooltip = IsPlaying ? "暂停" : "播放";
        PlayPauseIcon = IsPlaying ? PauseIcon : PlayIcon;

        if (status.Duration > 0)
        {
            ProgressPercent = (status.Progress / status.Duration) * 100;
            ProgressText = LyricParser.FormatProgress(status.Progress, status.Duration);
        }
        else
        {
            ProgressPercent = 0;
            ProgressText = "0:00 / 0:00";
        }

        UpdateLyrics(status);
    }

    private string BuildArtistAlbum(LxMusicStatus status)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(status.Singer)) parts.Add(status.Singer);
        if (!string.IsNullOrEmpty(status.AlbumName)) parts.Add(status.AlbumName);
        return string.Join(" · ", parts);
    }

    private void UpdateLyrics(LxMusicStatus status)
    {
        if (!ShowLyrics) return;

        var currentTime = TimeSpan.FromSeconds(status.Progress);
        
        if (!string.IsNullOrEmpty(status.LyricLineText))
        {
            CurrentLyricLine = status.LyricLineText;
        }
        else if (!string.IsNullOrEmpty(status.Lyric))
        {
            if (_parsedLyrics.Count == 0 || _parsedLyrics[0].Text != status.Lyric.Split('\n')[0])
            {
                _parsedLyrics = LyricParser.ParseDualLrc(status.Lyric, status.Tlyric);
            }
            
            var line = LyricParser.GetCurrentLine(_parsedLyrics, currentTime);
            if (line != null)
            {
                CurrentLyricLine = string.IsNullOrEmpty(line.Translation) 
                    ? line.Text 
                    : $"{line.Text}  |  {line.Translation}";
            }
        }
        else
        {
            CurrentLyricLine = "";
        }
    }

    private async Task SkipPrevAsync()
    {
        try { await _lxMusicService.SkipPrevAsync(); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to skip previous"); }
    }

    private async Task PlayPauseAsync()
    {
        try 
        { 
            if (IsPlaying)
                await _lxMusicService.PauseAsync();
            else
                await _lxMusicService.PlayAsync();
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to play/pause"); }
    }

    private async Task SkipNextAsync()
    {
        try { await _lxMusicService.SkipNextAsync(); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to skip next"); }
    }
}

public class LxMusicLyricsComponentSettings : CommunityToolkit.Mvvm.ComponentModel.ObservableRecipient
{
    private string _apiUrl = "http://127.0.0.1:23330";
    private bool _showLyrics = true;
    private bool _showCover = true;
    private bool _showControls = true;

    public string ApiUrl
    {
        get => _apiUrl;
        set => SetProperty(ref _apiUrl, value);
    }

    public bool ShowLyrics
    {
        get => _showLyrics;
        set => SetProperty(ref _showLyrics, value);
    }

    public bool ShowCover
    {
        get => _showCover;
        set => SetProperty(ref _showCover, value);
    }

    public bool ShowControls
    {
        get => _showControls;
        set => SetProperty(ref _showControls, value);
    }
}