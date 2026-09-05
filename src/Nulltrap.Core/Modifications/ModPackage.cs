using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nulltrap.Core.Modifications;

public sealed record ModCard
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("madeBy")]
    public string MadeBy { get; init; } = string.Empty;

    [JsonPropertyName("madeWith")]
    public string MadeWith { get; init; } = string.Empty;

    [JsonPropertyName("madeAt")]
    public DateTimeOffset MadeAt { get; init; }
}

public sealed record ModContents(ModCard Card, IReadOnlyList<ModFile> Files, long Bytes);

public enum ModTrouble
{
    None,
    NotAPackage,
    Empty,
    Escapes,
    Runnable,
    OutsideAssets,
    TooBig,
}

public sealed record ModReading(ModContents? Contents, ModTrouble Trouble, string? Offender = null)
{
    public bool Sound => Trouble == ModTrouble.None && Contents is not null;
}

public static class ModPackage
{
    public const string Extension = ".nulltrapmod";
    public const string CardName = "mod.json";

    public const long SizeLimit = 512L * 1024 * 1024;
    public const int CountLimit = 20000;

    private static readonly JsonSerializerOptions Shape = new() { WriteIndented = true };

    private static readonly string[] Assets = ["content", "extracontent"];

    private static readonly HashSet<string> Runnable = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".com", ".scr", ".sys", ".drv", ".ocx", ".cpl", ".msi", ".msp",
        ".bat", ".cmd", ".ps1", ".psm1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh",
        ".hta", ".lnk", ".pif", ".reg", ".jar", ".node", ".so", ".dylib", ".appx", ".msix",
    };

    public static bool Escapes(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
        {
            return true;
        }

        string plain = entry.Replace('\\', '/');

        if (plain.StartsWith('/') || plain.Contains(':', StringComparison.Ordinal))
        {
            return true;
        }

        return plain
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(part => part is "." or "..");
    }

    public static bool IsRunnable(string entry) => Runnable.Contains(Path.GetExtension(entry));

    public static bool InAssets(string entry)
    {
        string[] parts = entry.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length > 1 && Assets.Contains(parts[0], StringComparer.OrdinalIgnoreCase);
    }

    public static ModTrouble Judge(string entry, out string? offender)
    {
        offender = entry;

        if (Escapes(entry))
        {
            return ModTrouble.Escapes;
        }

        if (IsRunnable(entry))
        {
            return ModTrouble.Runnable;
        }

        if (!InAssets(entry))
        {
            return ModTrouble.OutsideAssets;
        }

        offender = null;
        return ModTrouble.None;
    }

    public static ModReading Read(Stream package)
    {
        ArgumentNullException.ThrowIfNull(package);

        ZipArchive folded;

        try
        {
            folded = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            return new ModReading(null, ModTrouble.NotAPackage);
        }

        using (folded)
        {
            ModCard card = new();
            var files = new List<ModFile>();
            long bytes = 0;
            int counted = 0;

            foreach (ZipArchiveEntry entry in folded.Entries)
            {
                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                {
                    continue;
                }

                if (string.Equals(entry.FullName, CardName, StringComparison.OrdinalIgnoreCase))
                {
                    card = ReadCard(entry) ?? card;
                    continue;
                }

                if (++counted > CountLimit)
                {
                    return new ModReading(null, ModTrouble.TooBig, entry.FullName);
                }

                bytes += entry.Length;

                if (bytes > SizeLimit)
                {
                    return new ModReading(null, ModTrouble.TooBig, entry.FullName);
                }

                ModTrouble trouble = Judge(entry.FullName, out string? offender);

                if (trouble != ModTrouble.None)
                {
                    return new ModReading(null, trouble, offender);
                }

                files.Add(new ModFile(
                    entry.FullName.Replace('\\', '/'),
                    entry.Length,
                    entry.LastWriteTime));
            }

            if (files.Count == 0)
            {
                return new ModReading(null, ModTrouble.Empty);
            }

            files.Sort((left, right) =>
                string.Compare(left.RelativePath, right.RelativePath, StringComparison.OrdinalIgnoreCase));

            return new ModReading(new ModContents(card, files, bytes), ModTrouble.None);
        }
    }

    public static ModReading Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using FileStream file = File.OpenRead(path);

        return Read(file);
    }

    public static void Pack(string modsFolder, ModCard card, Stream into)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modsFolder);
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(into);

        using var folded = new ZipArchive(into, ZipArchiveMode.Create, leaveOpen: true);

        ZipArchiveEntry front = folded.CreateEntry(CardName, CompressionLevel.Optimal);

        using (Stream writing = front.Open())
        {
            JsonSerializer.Serialize(writing, card, Shape);
        }

        foreach (ModFile mod in ModManager.List(modsFolder))
        {
            if (Judge(mod.RelativePath, out _) != ModTrouble.None)
            {
                continue;
            }

            string source = Path.Combine(
                modsFolder,
                mod.RelativePath.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(source))
            {
                continue;
            }

            folded.CreateEntryFromFile(source, mod.RelativePath, CompressionLevel.Optimal);
        }
    }

    public static IReadOnlyList<string> Packable(string modsFolder) =>
        ModManager.List(modsFolder)
            .Where(mod => Judge(mod.RelativePath, out _) == ModTrouble.None)
            .Select(mod => mod.RelativePath)
            .ToList();

    public static ModReading Install(string path, string modsFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(modsFolder);

        ModReading reading = Read(path);

        if (!reading.Sound)
        {
            return reading;
        }

        using FileStream file = File.OpenRead(path);
        using var folded = new ZipArchive(file, ZipArchiveMode.Read);

        Directory.CreateDirectory(modsFolder);

        foreach (ModFile mod in reading.Contents!.Files)
        {
            ZipArchiveEntry? entry = folded.GetEntry(mod.RelativePath);

            if (entry is null)
            {
                continue;
            }

            string target = Path.Combine(
                modsFolder,
                mod.RelativePath.Replace('/', Path.DirectorySeparatorChar));

            string full = Path.GetFullPath(target);

            if (!full.StartsWith(Path.GetFullPath(modsFolder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return new ModReading(null, ModTrouble.Escapes, mod.RelativePath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            entry.ExtractToFile(full, overwrite: true);
        }

        return reading;
    }

    private static ModCard? ReadCard(ZipArchiveEntry entry)
    {
        try
        {
            using Stream reading = entry.Open();

            return JsonSerializer.Deserialize<ModCard>(reading);
        }
        catch (Exception failure) when (failure is JsonException or InvalidDataException or IOException)
        {
            return null;
        }
    }
}
