using System.Windows;
using System.Windows.Media;

using Nulltrap.Core.Settings;

namespace Nulltrap.App;

public static class Themes
{
    private static readonly Dictionary<AppTheme, Palette> Skins = new()
    {
        [AppTheme.Nulltrap] = new Palette(
            Void: "#FF121019",
            Caption: "#FF1A1526",
            Surface: "#FF221C33",
            SurfaceHover: "#FF2E2545",
            Rule: "#FF3D3159",
            Purple: "#FF7A2FCC",
            PurpleBright: "#FFC77DFF",
            Glow: "#FFEFD0FF",
            Text: "#FFF3EFFA",
            TextSoft: "#FFC8C1D8",
            Danger: "#FFFF7E74",
            OnAccent: "#FFF7F3FF",
            Scrim: "#FF121019",
            NavHover: "#1AB96FF0",
            NavPicked: "#33B96FF0"),

        [AppTheme.Dark] = new Palette(
            Void: "#FF15161A",
            Caption: "#FF1C1E23",
            Surface: "#FF24272E",
            SurfaceHover: "#FF2F333C",
            Rule: "#FF3C414B",
            Purple: "#FF7A2FCC",
            PurpleBright: "#FFC77DFF",
            Glow: "#FFEFD0FF",
            Text: "#FFF2F3F5",
            TextSoft: "#FFBEC2CC",
            Danger: "#FFFF7E74",
            OnAccent: "#FFFFFFFF",
            Scrim: "#FF000000",
            NavHover: "#1AB96FF0",
            NavPicked: "#33B96FF0"),

        [AppTheme.Amoled] = new Palette(
            Void: "#FF000000",
            Caption: "#FF000000",
            Surface: "#FF0B0B0E",
            SurfaceHover: "#FF17171C",
            Rule: "#FF26262E",
            Purple: "#FF7A2FCC",
            PurpleBright: "#FFC77DFF",
            Glow: "#FFEFD0FF",
            Text: "#FFF5F5F7",
            TextSoft: "#FFADADB8",
            Danger: "#FFFF7E74",
            OnAccent: "#FFFFFFFF",
            Scrim: "#FF000000",
            NavHover: "#1AB96FF0",
            NavPicked: "#33B96FF0"),

        [AppTheme.Light] = new Palette(
            Void: "#FFF5F3F9",
            Caption: "#FFECE8F4",
            Surface: "#FFFFFFFF",
            SurfaceHover: "#FFF1EDFA",
            Rule: "#FFDCD6E8",
            Purple: "#FF7A2FCC",
            PurpleBright: "#FF5B21B6",
            Glow: "#FFD9C2F5",
            Text: "#FF1C1726",
            TextSoft: "#FF5F5872",
            Danger: "#FFC62828",
            OnAccent: "#FFFFFFFF",
            Scrim: "#FF1C1726",
            NavHover: "#147A2FCC",
            NavPicked: "#2E7A2FCC"),
    };

    public static AppTheme Current { get; private set; } = AppTheme.Nulltrap;

    public static bool IsLight => Current == AppTheme.Light;

    public static event EventHandler? Changed;

    public static void Apply(AppTheme theme)
    {
        if (!Skins.TryGetValue(theme, out Palette? skin) || Application.Current is null)
        {
            return;
        }

        ResourceDictionary shelf = Application.Current.Resources;

        Lay(shelf, "VoidBrush", skin.Void);
        Lay(shelf, "VeilBrush", skin.Void, 0.75);
        Lay(shelf, "CaptionBrush", skin.Caption);
        Lay(shelf, "CaptionVeilBrush", skin.Caption, 0.55);
        Lay(shelf, "SurfaceBrush", skin.Surface);
        Lay(shelf, "SurfaceHoverBrush", skin.SurfaceHover);
        Lay(shelf, "RuleBrush", skin.Rule);
        Lay(shelf, "PurpleBrush", skin.Purple);
        Lay(shelf, "PurpleBrightBrush", skin.PurpleBright);
        Lay(shelf, "GlowBrush", skin.Glow);
        Lay(shelf, "TextBrush", skin.Text);
        Lay(shelf, "TextSoftBrush", skin.TextSoft);
        Lay(shelf, "DangerBrush", skin.Danger);
        Lay(shelf, "OnAccentBrush", skin.OnAccent);
        Lay(shelf, "ScrimBrush", skin.Scrim, 0.62);
        Lay(shelf, "NavHoverBrush", skin.NavHover);
        Lay(shelf, "NavPickedBrush", skin.NavPicked);

        shelf["VoidColor"] = Of(skin.Void);
        shelf["CaptionColor"] = Of(skin.Caption);
        shelf["SurfaceColor"] = Of(skin.Surface);
        shelf["SurfaceHoverColor"] = Of(skin.SurfaceHover);
        shelf["RuleColor"] = Of(skin.Rule);
        shelf["PurpleColor"] = Of(skin.Purple);
        shelf["PurpleBrightColor"] = Of(skin.PurpleBright);
        shelf["GlowColor"] = Of(skin.Glow);
        shelf["TextColor"] = Of(skin.Text);
        shelf["TextSoftColor"] = Of(skin.TextSoft);
        shelf["DangerColor"] = Of(skin.Danger);
        shelf["OnAccentColor"] = Of(skin.OnAccent);

        var accent = new LinearGradientBrush(
            Of(skin.Purple),
            Of(skin.PurpleBright),
            new Point(0, 0),
            new Point(1, 1));

        accent.Freeze();
        shelf["AccentBrush"] = accent;

        Current = theme;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static void Lay(ResourceDictionary shelf, string key, string colour, double opacity = 1)
    {
        var brush = new SolidColorBrush(Of(colour)) { Opacity = opacity };

        brush.Freeze();
        shelf[key] = brush;
    }

    private static Color Of(string colour) => (Color)ColorConverter.ConvertFromString(colour)!;

    private sealed record Palette(
        string Void,
        string Caption,
        string Surface,
        string SurfaceHover,
        string Rule,
        string Purple,
        string PurpleBright,
        string Glow,
        string Text,
        string TextSoft,
        string Danger,
        string OnAccent,
        string Scrim,
        string NavHover,
        string NavPicked);
}
