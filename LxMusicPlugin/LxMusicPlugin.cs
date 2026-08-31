using System.Reflection;
using Avalonia.Media;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Controls;
using ClassIsland.PluginSdk;
using LxMusicPlugin.Models;
using LxMusicPlugin.Services;
using LxMusicPlugin.Views.Components;
using LxMusicPlugin.Views.SettingsPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LxMusicPlugin;

[PluginInfo(
    "LX Music Integration",
    "将 LX Music 当前播放歌曲的封面设为 ClassIsland 背景，并在底部显示实时歌词",
    "1.0.0",
    "LX Music Plugin Author",
    "https://github.com/lyswhut/lx-music-desktop"
)]
public class LxMusicPlugin : IPlugin, IDisposable
{
    private IHost? _host;
    private ILxMusicService? _lxMusicService;
    private IBackgroundCoverService? _backgroundService;
    private IComponentService? _componentService;
    private IMainWindowService? _mainWindowService;
    private IDisposable? _statusSubscription;
    private IDisposable? _backgroundSubscription;
    private IDisposable? _connectionSubscription;
    private bool _disposed;

    public void OnServiceConfiguring(HostBuilderContext context, IServiceCollection services)
    {
        services.Configure<LxMusicPluginSettings>(context.Configuration.GetSection("LxMusicPlugin"));
        
        services.AddSingleton<LxMusicPluginSettings>(sp => 
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<LxMusicPluginSettings>>().Value);
        
        services.AddSingleton<ILxMusicService, LxMusicService>();
        services.AddSingleton<IBackgroundCoverService, BackgroundCoverService>();
        
        services.AddComponent<LxMusicLyricsComponent, LxMusicLyricsComponentSettingsControl>();
        services.AddSettingsPage<LxMusicSettingsPage>();
    }

    public async void OnServiceConfigured(IHost host)
    {
        _host = host;
        
        _lxMusicService = host.Services.GetRequiredService<ILxMusicService>();
        _backgroundService = host.Services.GetRequiredService<IBackgroundCoverService>();
        _componentService = host.Services.GetRequiredService<IComponentService>();
        _mainWindowService = host.Services.GetRequiredService<IMainWindowService>();

        _statusSubscription = _lxMusicService.StatusChanged += OnStatusChanged;
        _backgroundSubscription = _backgroundService.BackgroundChanged += OnBackgroundChanged;
        _connectionSubscription = _lxMusicService.ConnectionChanged += OnConnectionChanged;
        
        _lxMusicService.ErrorOccurred += OnErrorOccurred;

        await _lxMusicService.StartAsync();
        
        _componentService.RegisterComponent<LxMusicLyricsComponent>();
    }

    private void OnConnectionChanged(object? sender, bool connected)
    {
        if (!connected && _backgroundService != null)
        {
            _ = _backgroundService.ClearBackgroundAsync();
        }
    }

    private void OnStatusChanged(object? sender, LxMusicStatus status)
    {
        if (_backgroundService != null && _lxMusicService != null)
        {
            var settings = _host!.Services.GetRequiredService<LxMusicPluginSettings>();
            
            if (settings.EnableBackgroundCover && status.IsPlaying && status.HasValidCover)
            {
                _ = _backgroundService.SetCoverAsync(status.PicUrl, settings.BackgroundOpacity, settings.BlurBackground);
            }
            else if (!status.IsPlaying || !status.HasValidCover)
            {
                _ = _backgroundService.ClearBackgroundAsync();
            }
        }
    }

    private void OnBackgroundChanged(object? sender, IBrush? brush)
    {
        if (_mainWindowService != null && _mainWindowService.MainWindow is { } window)
        {
            window.Background = brush ?? Brushes.Transparent;
        }
    }

    private void OnErrorOccurred(object? sender, string error)
    {
        System.Diagnostics.Debug.WriteLine($"[LxMusicPlugin] Error: {error}");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _statusSubscription?.Dispose();
        _backgroundSubscription?.Dispose();
        _connectionSubscription?.Dispose();
        
        if (_lxMusicService != null)
        {
            _lxMusicService.ErrorOccurred -= OnErrorOccurred;
            _ = _lxMusicService.StopAsync();
        }

        _backgroundService?.Dispose();
        _host?.Dispose();
        
        GC.SuppressFinalize(this);
    }
}