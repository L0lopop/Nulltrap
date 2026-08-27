namespace Nulltrap.Platform.Abstractions;

public sealed record UninstallEntryInfo(
    string DisplayName,
    string DisplayVersion,
    string Publisher,
    string InstallLocation,
    string ExecutablePath,
    long EstimatedSizeKilobytes);

public interface IUninstallEntry
{
    bool Exists { get; }

    void Write(UninstallEntryInfo info);

    void Remove();
}
