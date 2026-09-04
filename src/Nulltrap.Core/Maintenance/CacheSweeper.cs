namespace Nulltrap.Core.Maintenance;

public sealed record SweepReport(int Files, long Bytes, int Kept);

public sealed class CacheSweeper
{
    private readonly IReadOnlyList<string> _places;

    public CacheSweeper(IEnumerable<string>? places = null)
    {
        _places = places?.ToArray() ?? Usual();
    }

    public IReadOnlyList<string> Places => _places;

    public static IReadOnlyList<string> Usual() =>
    [
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Roblox",
            "rbx-storage"),
        Path.Combine(Path.GetTempPath(), "Roblox", "http"),
    ];

    public long Weigh()
    {
        long total = 0;

        foreach (string place in _places)
        {
            foreach (FileInfo file in Files(place))
            {
                total += file.Length;
            }
        }

        return total;
    }

    public SweepReport Sweep()
    {
        int swept = 0;
        int kept = 0;
        long freed = 0;

        foreach (string place in _places)
        {
            foreach (FileInfo file in Files(place))
            {
                long size = file.Length;

                try
                {
                    file.Delete();
                    swept++;
                    freed += size;
                }
                catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
                {
                    kept++;
                }
            }

            Tidy(place);
        }

        return new SweepReport(swept, freed, kept);
    }

    private static IEnumerable<FileInfo> Files(string place)
    {
        if (!Directory.Exists(place))
        {
            yield break;
        }

        IEnumerator<FileInfo> walk;

        try
        {
            walk = new DirectoryInfo(place)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .GetEnumerator();
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        while (true)
        {
            try
            {
                if (!walk.MoveNext())
                {
                    break;
                }
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                break;
            }

            yield return walk.Current;
        }

        walk.Dispose();
    }

    private static void Tidy(string place)
    {
        if (!Directory.Exists(place))
        {
            return;
        }

        try
        {
            foreach (string folder in Directory.EnumerateDirectories(place, "*", SearchOption.AllDirectories)
                         .OrderByDescending(path => path.Length))
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(folder).Any())
                    {
                        Directory.Delete(folder);
                    }
                }
                catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
        }
    }
}
