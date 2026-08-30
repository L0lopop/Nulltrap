using System.Globalization;
using System.Text.RegularExpressions;

namespace Nulltrap.Core.Roblox;

public static partial class RobloxIdentity
{
    public const int LogsToRead = 10;

    public static long FromLogs(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (!Directory.Exists(directory))
        {
            return 0;
        }

        IEnumerable<string> newest;

        try
        {
            newest = new DirectoryInfo(directory)
                .EnumerateFiles("*.log")
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Take(LogsToRead)
                .Select(file => file.FullName);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return 0;
        }

        foreach (string path in newest)
        {
            long found = FromLog(path);

            if (found > 0)
            {
                return found;
            }
        }

        return 0;
    }

    public static long FromLog(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            using var reader = new StreamReader(
                new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

            while (reader.ReadLine() is { } line)
            {
                if (Read(TrackerId(), line, out long tracked))
                {
                    return tracked;
                }

                if (Read(JoinedAs(), line, out long joined))
                {
                    return joined;
                }
            }
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
        }

        return 0;
    }

    private static bool Read(Regex pattern, string line, out long id)
    {
        Match match = pattern.Match(line);

        id = match.Success
            && long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed)
                ? parsed
                : 0;

        return id > 0;
    }

    [GeneratedRegex(@"rbxuid=(\d+)")]
    private static partial Regex TrackerId();

    [GeneratedRegex(@"userid:(\d+)")]
    private static partial Regex JoinedAs();
}
