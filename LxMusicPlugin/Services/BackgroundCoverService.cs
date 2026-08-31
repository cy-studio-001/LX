using System.Windows.Media.Imaging;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ClassIsland.Core.Abstractions.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using LxMusicPlugin.Models;
using Microsoft.Extensions.Logging;

namespace LxMusicPlugin.Services;

public interface IBackgroundCoverService
{
    event EventHandler<IBrush?>? BackgroundChanged;
    IBrush? CurrentBackground { get; }
    Task SetCoverAsync(string picUrl, double opacity, bool blur);
    Task ClearBackgroundAsync();
}

public partial class BackgroundCoverService : ObservableObject, IBackgroundCoverService, IDisposable
{
    private readonly ILogger<BackgroundCoverService> _logger;
    private readonly LxMusicPluginSettings _settings;
    private Bitmap? _currentBitmap;
    private readonly HttpClient _httpClient = new();
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);

    [ObservableProperty]
    private IBrush? _currentBackground;

    public event EventHandler<IBrush?>? BackgroundChanged;

    public BackgroundCoverService(ILogger<BackgroundCoverService> logger, LxMusicPluginSettings settings)
    {
        _logger = logger;
        _settings = settings;
        _settings.PropertyChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LxMusicPluginSettings.BackgroundOpacity) or nameof(LxMusicPluginSettings.BlurBackground))
        {
            if (!string.IsNullOrEmpty(_settings.LxMusicApiUrl) && CurrentBackground != null)
            {
                _ = RefreshBackgroundAsync();
            }
        }
    }

    public async Task SetCoverAsync(string picUrl, double opacity, bool blur)
    {
        if (string.IsNullOrEmpty(picUrl))
        {
            await ClearBackgroundAsync();
            return;
        }

        await _loadSemaphore.WaitAsync();
        try
        {
            Bitmap? bitmap = null;

            try
            {
                if (picUrl.StartsWith("data:"))
                {
                    bitmap = LoadFromDataUrl(picUrl);
                }
                else if (picUrl.StartsWith("http"))
                {
                    bitmap = await LoadFromUrlAsync(picUrl);
                }

                if (bitmap != null)
                {
                    _currentBitmap?.Dispose();
                    _currentBitmap = bitmap;
                    
                    var brush = CreateBackgroundBrush(bitmap, opacity, blur);
                    CurrentBackground = brush;
                    BackgroundChanged?.Invoke(this, brush);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load cover image: {Url}", picUrl);
                await ClearBackgroundAsync();
            }
        }
        finally
        {
            _loadSemaphore.Release();
        }
    }

    private Bitmap? LoadFromDataUrl(string dataUrl)
    {
        try
        {
            var commaIndex = dataUrl.IndexOf(',');
            if (commaIndex < 0) return null;
            
            var base64 = dataUrl[(commaIndex + 1)..];
            var bytes = Convert.FromBase64String(base64);
            
            using var stream = new MemoryStream(bytes);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    private async Task<Bitmap?> LoadFromUrlAsync(string url)
    {
        try
        {
            var bytes = await _httpClient.GetByteArrayAsync(url);
            using var stream = new MemoryStream(bytes);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    private IBrush CreateBackgroundBrush(Bitmap bitmap, double opacity, bool blur)
    {
        var imageBrush = new ImageBrush(bitmap)
        {
            Stretch = Stretch.UniformToFill,
            AlignmentX = AlignmentX.Center,
            AlignmentY = AlignmentY.Center,
            Opacity = opacity
        };

        if (blur)
        {
            return new VisualBrush(new Border
            {
                Background = imageBrush,
                Child = new Rectangle
                {
                    Fill = imageBrush,
                    Width = bitmap.PixelSize.Width,
                    Height = bitmap.PixelSize.Height
                }
            })
            {
                Stretch = Stretch.UniformToFill
            };
        }

        return imageBrush;
    }

    private async Task RefreshBackgroundAsync()
    {
        if (_currentBitmap != null)
        {
            var brush = CreateBackgroundBrush(_currentBitmap, _settings.BackgroundOpacity, _settings.BlurBackground);
            CurrentBackground = brush;
            BackgroundChanged?.Invoke(this, brush);
        }
    }

    public async Task ClearBackgroundAsync()
    {
        await _loadSemaphore.WaitAsync();
        try
        {
            _currentBitmap?.Dispose();
            _currentBitmap = null;
            CurrentBackground = null;
            BackgroundChanged?.Invoke(this, null);
        }
        finally
        {
            _loadSemaphore.Release();
        }
    }

    public void Dispose()
    {
        _settings.PropertyChanged -= OnSettingsChanged;
        _currentBitmap?.Dispose();
        _httpClient.Dispose();
        _loadSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}