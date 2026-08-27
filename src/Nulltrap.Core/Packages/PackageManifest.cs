using System.Collections;
using System.Globalization;

namespace Nulltrap.Core.Packages;

public sealed class PackageManifest : IReadOnlyList<PackageEntry>
{
    public const string SupportedVersion = "v0";

    private const int FieldsPerEntry = 4;

    private readonly IReadOnlyList<PackageEntry> _entries;

    private PackageManifest(string versionGuid, IReadOnlyList<PackageEntry> entries)
    {
        VersionGuid = versionGuid;
        _entries = entries;
    }

    public string VersionGuid { get; }

    public int Count => _entries.Count;

    public PackageEntry this[int index] => _entries[index];

    public long TotalPackedSize => _entries.Sum(entry => entry.PackedSize);

    public long TotalUnpackedSize => _entries.Sum(entry => entry.UnpackedSize);

    public static PackageManifest Parse(string versionGuid, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionGuid);
        ArgumentNullException.ThrowIfNull(content);

        string[] lines = content
            .ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (lines.Length == 0)
        {
            throw new PackageManifestException("The package manifest is empty.");
        }

        if (!string.Equals(lines[0], SupportedVersion, StringComparison.Ordinal))
        {
            throw new PackageManifestException(
                $"Unsupported package manifest format '{lines[0]}'. Nulltrap understands '{SupportedVersion}'.");
        }

        int fieldCount = lines.Length - 1;

        if (fieldCount % FieldsPerEntry != 0)
        {
            throw new PackageManifestException(
                $"The package manifest has {fieldCount} fields after the header, which is not a whole "
                + $"number of {FieldsPerEntry}-line entries.");
        }

        var entries = new List<PackageEntry>(fieldCount / FieldsPerEntry);

        for (int index = 1; index < lines.Length; index += FieldsPerEntry)
        {
            entries.Add(new PackageEntry
            {
                Name = lines[index],
                Checksum = lines[index + 1],
                PackedSize = ParseSize(lines[index + 2], lines[index], "packed size"),
                UnpackedSize = ParseSize(lines[index + 3], lines[index], "unpacked size"),
            });
        }

        return new PackageManifest(versionGuid, entries);
    }

    public IEnumerator<PackageEntry> GetEnumerator() => _entries.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static long ParseSize(string value, string packageName, string field)
    {
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long size))
        {
            throw new PackageManifestException(
                $"The {field} for package '{packageName}' is not a number: '{value}'.");
        }

        return size;
    }
}
