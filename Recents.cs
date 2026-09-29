using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace SnipCanvas;

internal sealed record RecentItem(string Path, DateTime Time);

/// <summary>Finds earlier screenshots in the save folder so they can be reopened from the home screen.</summary>
internal static class Recents
{
    internal static List<RecentItem> Find(string folder, int count)
    {
        try
        {
            var directory = new DirectoryInfo(folder);
            if (!directory.Exists) return new();
            return directory.EnumerateFiles("*.png")
                .Where(file => file.Name.StartsWith("SnipCanvas-", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Take(count)
                .Select(file => new RecentItem(file.FullName, file.LastWriteTime))
                .ToList();
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException) { return new(); }
    }

    /// <summary>Small decoded copy for the home screen; the full PNG is only read when the user opens it.</summary>
    internal static BitmapSource? Thumbnail(string path, int width)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path);
            image.DecodePixelWidth = width;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException || ex is InvalidOperationException || ex is FormatException) { return null; }
    }

    internal static BitmapSource Load(string path)
    {
        using var stream = File.OpenRead(path);
        var image = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        image.Freeze();
        return image;
    }

    internal static string Describe(DateTime time, DateTime? now = null)
    {
        var today = (now ?? DateTime.Now).Date;
        string clock = time.ToString("t", CultureInfo.CurrentCulture);
        if (time.Date == today) return "Today, " + clock;
        if (time.Date == today.AddDays(-1)) return "Yesterday, " + clock;
        return time.ToString("MMM d", CultureInfo.CurrentCulture) + ", " + clock;
    }
}
