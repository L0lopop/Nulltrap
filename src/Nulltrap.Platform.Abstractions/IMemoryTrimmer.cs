namespace Nulltrap.Platform.Abstractions;

public interface IMemoryTrimmer
{
    long Held { get; }

    bool Trim();
}
