using System;
using System.Text.Json.Serialization;

namespace LxMusicPlugin.Models;

public class LxMusicStatus
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "stoped";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("singer")]
    public string Singer { get; set; } = "";

    [JsonPropertyName("albumName")]
    public string AlbumName { get; set; } = "";

    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    [JsonPropertyName("progress")]
    public double Progress { get; set; }

    [JsonPropertyName("playbackRate")]
    public double PlaybackRate { get; set; }

    [JsonPropertyName("picUrl")]
    public string PicUrl { get; set; } = "";

    [JsonPropertyName("lyricLineText")]
    public string LyricLineText { get; set; } = "";

    [JsonPropertyName("lyricLineAllText")]
    public string LyricLineAllText { get; set; } = "";

    [JsonPropertyName("lyric")]
    public string Lyric { get; set; } = "";

    [JsonPropertyName("tlyric")]
    public string Tlyric { get; set; } = "";

    [JsonPropertyName("rlyric")]
    public string Rlyric { get; set; } = "";

    [JsonPropertyName("lxlyric")]
    public string Lxlyric { get; set; } = "";

    [JsonPropertyName("collect")]
    public bool Collect { get; set; }

    [JsonPropertyName("volume")]
    public int Volume { get; set; }

    [JsonPropertyName("mute")]
    public bool Mute { get; set; }

    public bool IsPlaying => Status == "playing";
    public bool HasValidCover => !string.IsNullOrEmpty(PicUrl) && (PicUrl.StartsWith("http") || PicUrl.StartsWith("data:"));
}

public class LxMusicLyricData
{
    [JsonPropertyName("lyric")]
    public string Lyric { get; set; } = "";

    [JsonPropertyName("tlyric")]
    public string Tlyric { get; set; } = "";

    [JsonPropertyName("rlyric")]
    public string Rlyric { get; set; } = "";

    [JsonPropertyName("lxlyric")]
    public string Lxlyric { get; set; } = "";
}

public class LxMusicPluginSettings : CommunityToolkit.Mvvm.ComponentModel.ObservableRecipient
{
    private string _lxMusicApiUrl = "http://127.0.0.1:23330";
    private bool _enableBackgroundCover = true;
    private bool _enableLyricsDisplay = true;
    private int _pollIntervalMs = 1000;
    private bool _useSse = true;
    private double _backgroundOpacity = 0.6;
    private bool _blurBackground = true;

    public string LxMusicApiUrl
    {
        get => _lxMusicApiUrl;
        set => SetProperty(ref _lxMusicApiUrl, value);
    }

    public bool EnableBackgroundCover
    {
        get => _enableBackgroundCover;
        set => SetProperty(ref _enableBackgroundCover, value);
    }

    public bool EnableLyricsDisplay
    {
        get => _enableLyricsDisplay;
        set => SetProperty(ref _enableLyricsDisplay, value);
    }

    public int PollIntervalMs
    {
        get => _pollIntervalMs;
        set => SetProperty(ref _pollIntervalMs, value);
    }

    public bool UseSse
    {
        get => _useSse;
        set => SetProperty(ref _useSse, value);
    }

    public double BackgroundOpacity
    {
        get => _backgroundOpacity;
        set => SetProperty(ref _backgroundOpacity, value);
    }

    public bool BlurBackground
    {
        get => _blurBackground;
        set => SetProperty(ref _blurBackground, value);
    }
}

public class LyricLine
{
    public TimeSpan Time { get; set; }
    public string Text { get; set; } = "";
    public string Translation { get; set; } = "";
}