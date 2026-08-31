using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using LxMusicPlugin.Models;

namespace LxMusicPlugin.Services;

public static class LyricParser
{
    private static readonly Regex TimeTagRegex = new(@"\[(\d{2}):(\d{2})[.:](\d{2,3})\]");
    private static readonly Regex OffsetRegex = new(@"\[offset:(-?\d+)\]");

    public static List<LyricLine> ParseLrc(string lrcText)
    {
        var lines = new List<LyricLine>();
        
        if (string.IsNullOrWhiteSpace(lrcText))
            return lines;

        var offset = 0;
        var offsetMatch = OffsetRegex.Match(lrcText);
        if (offsetMatch.Success && int.TryParse(offsetMatch.Groups[1].Value, out var offsetMs))
        {
            offset = offsetMs;
        }

        var lineRegex = new Regex(@"^\[(\d{2}):(\d{2})[.:](\d{2,3})\](.*)$", RegexOptions.Multiline);
        var matches = lineRegex.Matches(lrcText);

        foreach (Match match in matches)
        {
            if (match.Groups.Count >= 5)
            {
                var minutes = int.Parse(match.Groups[1].Value);
                var seconds = int.Parse(match.Groups[2].Value);
                var milliseconds = int.Parse(match.Groups[3].Value.PadRight(3, '0')[..3]);
                var text = match.Groups[4].Value.Trim();

                var time = TimeSpan.FromMinutes(minutes)
                    .Add(TimeSpan.FromSeconds(seconds))
                    .Add(TimeSpan.FromMilliseconds(milliseconds))
                    .Add(TimeSpan.FromMilliseconds(offset));

                if (!string.IsNullOrEmpty(text))
                {
                    lines.Add(new LyricLine { Time = time, Text = text });
                }
            }
        }

        lines.Sort((a, b) => a.Time.CompareTo(b.Time));
        return lines;
    }

    public static List<LyricLine> ParseDualLrc(string mainLrc, string? translationLrc = null)
    {
        var mainLines = ParseLrc(mainLrc);
        
        if (string.IsNullOrEmpty(translationLrc))
            return mainLines;

        var transLines = ParseLrc(translationLrc);
        var transDict = transLines.ToDictionary(l => l.Time, l => l.Text);

        foreach (var line in mainLines)
        {
            if (transDict.TryGetValue(line.Time, out var transText))
            {
                line.Translation = transText;
            }
        }

        return mainLines;
    }

    public static LyricLine? GetCurrentLine(List<LyricLine> lines, TimeSpan currentTime)
    {
        LyricLine? current = null;
        
        foreach (var line in lines)
        {
            if (line.Time <= currentTime)
                current = line;
            else
                break;
        }
        
        return current;
    }

    public static string FormatTime(TimeSpan time)
    {
        return $"{time.Minutes:D2}:{time.Seconds:D2}.{time.Milliseconds / 10:D2}";
    }

    public static string FormatProgress(double current, double total)
    {
        if (total <= 0) return "0:00 / 0:00";
        
        var currentTs = TimeSpan.FromSeconds(current);
        var totalTs = TimeSpan.FromSeconds(total);
        
        return $"{currentTs.Minutes:D2}:{currentTs.Seconds:D2} / {totalTs.Minutes:D2}:{totalTs.Seconds:D2}";
    }
}