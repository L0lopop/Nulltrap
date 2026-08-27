namespace Nulltrap.Platform.Abstractions;

public enum ShortcutLocation
{
    Desktop,
    StartMenu,
}

public interface IShortcutManager
{
    bool Exists(ShortcutLocation location, string name);

    void Create(ShortcutLocation location, string name, string targetPath, string arguments, string description);

    void Remove(ShortcutLocation location, string name);
}
