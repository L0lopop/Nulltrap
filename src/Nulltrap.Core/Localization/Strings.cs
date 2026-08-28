using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Nulltrap.Core.Localization;

public sealed record Language(string Code, string NativeName, string EnglishName);

public static class Strings
{
    public const string Fallback = "en";

    private static readonly Assembly Owner = typeof(Strings).Assembly;

    private static readonly Dictionary<string, string> Loaded = Read(Fallback);

    private static Dictionary<string, string> _active = Loaded;

    public static IReadOnlyList<Language> Available { get; } =
    [
        new("en", "English", "English"),
        new("ru", "Русский", "Russian"),
    ];

    public static string Current { get; private set; } = Fallback;

    public static void Use(string? code)
    {
        code = string.IsNullOrWhiteSpace(code) ? Fallback : code.Trim().ToLowerInvariant();

        if (Available.All(language => language.Code != code))
        {
            code = Fallback;
        }

        Current = code;
        _active = code == Fallback ? Loaded : Read(code);

        var culture = CultureInfo.GetCultureInfo(code);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public static string Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_active.TryGetValue(key, out string? translated))
        {
            return translated;
        }

        return Loaded.TryGetValue(key, out string? original) ? original : key;
    }

    public static string Get(string key, params object[] arguments) =>
        arguments.Length == 0 ? Get(key) : string.Format(Get(key), arguments);

    private static Dictionary<string, string> Read(string code)
    {
        string resource = $"Nulltrap.Core.Localization.{code}.json";

        using Stream? stream = Owner.GetManifestResourceStream(resource);

        if (stream is null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
                ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
