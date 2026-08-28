using Nulltrap.Core.Deployment;

namespace Nulltrap.Core.Packages;

public static class PackageDirectoryMap
{
    private static readonly IReadOnlyDictionary<string, string> Common =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Libraries.zip"] = "",
            ["redist.zip"] = "",
            ["shaders.zip"] = "shaders",
            ["ssl.zip"] = "ssl",
            ["WebView2.zip"] = "",
            ["WebView2RuntimeInstaller.zip"] = "WebView2RuntimeInstaller",
            ["content-avatar.zip"] = "content/avatar",
            ["content-configs.zip"] = "content/configs",
            ["content-fonts.zip"] = "content/fonts",
            ["content-sky.zip"] = "content/sky",
            ["content-sounds.zip"] = "content/sounds",
            ["content-textures2.zip"] = "content/textures",
            ["content-models.zip"] = "content/models",
            ["content-textures3.zip"] = "PlatformContent/pc/textures",
            ["content-terrain.zip"] = "PlatformContent/pc/terrain",
            ["content-platform-fonts.zip"] = "PlatformContent/pc/fonts",
            ["content-platform-dictionaries.zip"] = "PlatformContent/pc/shared_compression_dictionaries",
            ["extracontent-luapackages.zip"] = "ExtraContent/LuaPackages",
            ["extracontent-translations.zip"] = "ExtraContent/translations",
            ["extracontent-models.zip"] = "ExtraContent/models",
            ["extracontent-textures.zip"] = "ExtraContent/textures",
            ["extracontent-places.zip"] = "ExtraContent/places",
        };

    private static readonly IReadOnlyDictionary<string, string> Player =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RobloxApp.zip"] = "",
        };

    private static readonly IReadOnlyDictionary<string, string> Studio =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RobloxStudio.zip"] = "",
            ["LibrariesQt5.zip"] = "",
            ["ApplicationConfig.zip"] = "ApplicationConfig",
            ["BuiltInPlugins.zip"] = "BuiltInPlugins",
            ["BuiltInStandalonePlugins.zip"] = "BuiltInStandalonePlugins",
            ["Plugins.zip"] = "Plugins",
            ["Qml.zip"] = "Qml",
            ["StudioFonts.zip"] = "StudioFonts",
            ["RibbonConfig.zip"] = "RibbonConfig",
            ["content-studio_svg_textures.zip"] = "content/studio_svg_textures",
            ["content-qt_translations.zip"] = "content/qt_translations",
            ["content-api-docs.zip"] = "content/api_docs",
            ["extracontent-scripts.zip"] = "ExtraContent/scripts",
            ["studiocontent-models.zip"] = "StudioContent/models",
            ["studiocontent-textures.zip"] = "StudioContent/textures",
        };

    public static IReadOnlyDictionary<string, string> For(BinaryType binaryType)
    {
        IReadOnlyDictionary<string, string> specific = binaryType switch
        {
            BinaryType.WindowsPlayer => Player,
            BinaryType.WindowsStudio64 => Studio,
            _ => throw new ArgumentOutOfRangeException(nameof(binaryType), binaryType, null),
        };

        var merged = new Dictionary<string, string>(Common, StringComparer.OrdinalIgnoreCase);

        foreach ((string package, string directory) in specific)
        {
            merged[package] = directory;
        }

        return merged;
    }

    public static bool IsInstallable(PackageEntry entry) =>
        entry.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
}
