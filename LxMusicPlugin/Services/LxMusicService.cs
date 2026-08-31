using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ClassIsland.Core.Abstractions.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using LxMusicPlugin.Models;
using Microsoft.Extensions.Logging;

namespace LxMusicPlugin.Services;

public interface ILxMusicService
{
    event EventHandler<LxMusicStatus>? StatusChanged;
    event EventHandler<bool>? ConnectionChanged;
    event EventHandler<string>? ErrorOccurred;
    
    LxMusicStatus CurrentStatus { get; }
    bool IsConnected { get; }
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task<LxMusicStatus?> GetStatusAsync();
    Task<string?> GetLyricAsync();
    Task<LxMusicLyricData?> GetAllLyricsAsync();
    Task<bool> TestConnectionAsync();
    Task PlayAsync();
    Task PauseAsync();
    Task SkipNextAsync();
    Task SkipPrevAsync();
}

public partial class LxMusicService : ObservableObject, ILxMusicService, IHostedService, IDisposable
{
    private readonly ILogger<LxMusicService> _logger;
    private readonly LxMusicPluginSettings _settings;
    private HttpClient? _httpClient;
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private Task? _sseTask;
    private string _lastPicUrl = "";
    
    [ObservableProperty]
    private LxMusicStatus _currentStatus = new();
    
    [ObservableProperty]
    private bool _isConnected;

    public event EventHandler<LxMusicStatus>? StatusChanged;
    public event EventHandler<bool>? ConnectionChanged;
    public event EventHandler<string>? ErrorOccurred;

    public LxMusicService(ILogger<LxMusicService> logger, LxMusicPluginSettings settings)
    {
        _logger = logger;
        _settings = settings;
        _settings.PropertyChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LxMusicPluginSettings.LxMusicApiUrl) or 
            nameof(LxMusicPluginSettings.UseSse) or
            nameof(LxMusicPluginSettings.PollIntervalMs))
        {
            _ = RestartMonitoringAsync();
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_settings.LxMusicApiUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(5)
        };
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        var connected = await TestConnectionAsync();
        IsConnected = connected;
        ConnectionChanged?.Invoke(this, connected);
        
        if (connected)
        {
            var status = await GetStatusAsync();
            if (status != null)
            {
                UpdateStatus(status);
            }
            
            if (_settings.UseSse)
            {
                _sseTask = RunSseMonitorAsync(_cts.Token);
            }
            else
            {
                _monitorTask = RunPollingMonitorAsync(_cts.Token);
            }
        }
        
        _logger.LogInformation("LX Music service started. Connected: {Connected}", connected);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _cts?.Cancel();
        
        if (_monitorTask != null)
            await _monitorTask.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        
        if (_sseTask != null)
            await _sseTask.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        
        _httpClient?.Dispose();
        _httpClient = null;
        
        IsConnected = false;
        ConnectionChanged?.Invoke(this, false);
        
        _logger.LogInformation("LX Music service stopped");
    }

    private async Task RestartMonitoringAsync()
    {
        try
        {
            await StopAsync();
            await StartAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart LX Music monitoring");
        }
    }

    private async Task RunSseMonitorAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "subscribe-player-status");
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
                
                using var response = await _httpClient!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                response.EnsureSuccessStatusCode();
                
                using var stream = await response.Content.ReadAsStreamAsync(token);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                
                IsConnected = true;
                ConnectionChanged?.Invoke(this, true);
                
                string? line;
                var eventName = "";
                var dataBuilder = new StringBuilder();
                
                while ((line = await reader.ReadLineAsync()) != null && !token.IsCancellationRequested)
                {
                    if (string.IsNullOrEmpty(line))
                    {
                        if (!string.IsNullOrEmpty(eventName) && dataBuilder.Length > 0)
                        {
                            ProcessSseEvent(eventName, dataBuilder.ToString());
                            eventName = "";
                            dataBuilder.Clear();
                        }
                        continue;
                    }
                    
                    if (line.StartsWith("event:"))
                    {
                        eventName = line["event:".Length..].Trim();
                    }
                    else if (line.StartsWith("data:"))
                    {
                        dataBuilder.AppendLine(line["data:".Length..].Trim());
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SSE connection lost, retrying in 5s...");
                ErrorOccurred?.Invoke(this, $"SSE连接断开: {ex.Message}");
                IsConnected = false;
                ConnectionChanged?.Invoke(this, false);
                
                try
                {
                    await Task.Delay(5000, token);
                }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private void ProcessSseEvent(string eventName, string data)
    {
        try
        {
            var status = CurrentStatus with { };
            
            switch (eventName)
            {
                case "status":
                    status.Status = data.Trim('"');
                    break;
                case "name":
                    status.Name = data.Trim('"');
                    break;
                case "singer":
                    status.Singer = data.Trim('"');
                    break;
                case "albumName":
                    status.AlbumName = data.Trim('"');
                    break;
                case "duration":
                    if (double.TryParse(data, out var d)) status.Duration = d;
                    break;
                case "progress":
                    if (double.TryParse(data, out var p)) status.Progress = p;
                    break;
                case "playbackRate":
                    if (double.TryParse(data, out var r)) status.PlaybackRate = r;
                    break;
                case "picUrl":
                    status.PicUrl = data.Trim('"');
                    break;
                case "lyricLineText":
                    status.LyricLineText = data.Trim('"');
                    break;
                case "lyricLineAllText":
                    status.LyricLineAllText = data.Trim('"');
                    break;
                case "lyric":
                    status.Lyric = data.Trim('"');
                    break;
                case "tlyric":
                    status.Tlyric = data.Trim('"');
                    break;
                case "rlyric":
                    status.Rlyric = data.Trim('"');
                    break;
                case "lxlyric":
                    status.Lxlyric = data.Trim('"');
                    break;
                case "collect":
                    status.Collect = bool.Parse(data.ToLower());
                    break;
                case "volume":
                    if (int.TryParse(data, out var v)) status.Volume = v;
                    break;
                case "mute":
                    status.Mute = bool.Parse(data.ToLower());
                    break;
            }
            
            UpdateStatus(status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process SSE event: {Event}", eventName);
        }
    }

    private async Task RunPollingMonitorAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var status = await GetStatusAsync();
                if (status != null)
                {
                    if (!IsConnected)
                    {
                        IsConnected = true;
                        ConnectionChanged?.Invoke(this, true);
                    }
                    UpdateStatus(status);
                }
                else if (IsConnected)
                {
                    IsConnected = false;
                    ConnectionChanged?.Invoke(this, false);
                }
            }
            catch (Exception ex)
            {
                if (IsConnected)
                {
                    IsConnected = false;
                    ConnectionChanged?.Invoke(this, false);
                    ErrorOccurred?.Invoke(this, $"轮询失败: {ex.Message}");
                }
            }
            
            try
            {
                await Task.Delay(_settings.PollIntervalMs, token);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private void UpdateStatus(LxMusicStatus newStatus)
    {
        var picChanged = newStatus.PicUrl != _lastPicUrl;
        _lastPicUrl = newStatus.PicUrl;
        
        CurrentStatus = newStatus;
        StatusChanged?.Invoke(this, newStatus);
        
        if (picChanged)
        {
            _logger.LogDebug("Cover art changed: {PicUrl}", newStatus.PicUrl);
        }
    }

    public async Task<LxMusicStatus?> GetStatusAsync()
    {
        if (_httpClient == null) return null;
        
        try
        {
            var response = await _httpClient.GetAsync("status?filter=status,name,singer,albumName,duration,progress,playbackRate,picUrl,lyricLineText,lyricLineAllText,lyric,tlyric,rlyric,lxlyric,collect,volume,mute");
            if (!response.IsSuccessStatusCode) return null;
            
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<LxMusicStatus>(json, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get status");
            return null;
        }
    }

    public async Task<string?> GetLyricAsync()
    {
        if (_httpClient == null) return null;
        
        try
        {
            var response = await _httpClient.GetAsync("lyric");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get lyric");
            return null;
        }
    }

    public async Task<LxMusicLyricData?> GetAllLyricsAsync()
    {
        if (_httpClient == null) return null;
        
        try
        {
            var response = await _httpClient.GetAsync("lyric-all");
            if (!response.IsSuccessStatusCode) return null;
            
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<LxMusicLyricData>(json, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get all lyrics");
            return null;
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        if (_httpClient == null) return false;
        
        try
        {
            var response = await _httpClient.GetAsync("status");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task PlayAsync()
    {
        if (_httpClient == null) return;
        await _httpClient.PostAsync("play", null);
    }

    public async Task PauseAsync()
    {
        if (_httpClient == null) return;
        await _httpClient.PostAsync("pause", null);
    }

    public async Task SkipNextAsync()
    {
        if (_httpClient == null) return;
        await _httpClient.PostAsync("skip-next", null);
    }

    public async Task SkipPrevAsync()
    {
        if (_httpClient == null) return;
        await _httpClient.PostAsync("skip-prev", null);
    }

    public void Dispose()
    {
        _settings.PropertyChanged -= OnSettingsChanged;
        _cts?.Cancel();
        _cts?.Dispose();
        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}

file static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}