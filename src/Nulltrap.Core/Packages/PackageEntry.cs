namespace Nulltrap.Core.Packages;

public sealed record PackageEntry
{
    public required string Name { get; init; }

    public required string Checksum { get; init; }

    public required long PackedSize { get; init; }

    public required long UnpackedSize { get; init; }

    public override string ToString() => $"{Name} ({PackedSize:N0} bytes packed)";
}
