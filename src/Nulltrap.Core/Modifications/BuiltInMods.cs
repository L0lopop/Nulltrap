using System.Reflection;

namespace Nulltrap.Core.Modifications;

public sealed record BuiltInPiece(string RelativePath, string Resource);

public sealed record BuiltInMod(string Id, string NameKey, string HintKey, IReadOnlyList<BuiltInPiece> Pieces);

public static class BuiltInMods
{
    private const string Silence = "Nulltrap.Core.Modifications.Assets.silence";

    private static readonly Assembly Owner = typeof(BuiltInMods).Assembly;

    public static IReadOnlyList<BuiltInMod> All { get; } =
    [
        new(
            "quiet-death",
            "builtin.quietDeath",
            "builtin.quietDeathHint",
            [
                new("content/sounds/ouch.ogg", Silence + ".ogg"),
                new("content/sounds/oof.ogg", Silence + ".ogg"),
            ]),
        new(
            "quiet-steps",
            "builtin.quietSteps",
            "builtin.quietStepsHint",
            [
                new("content/sounds/action_footsteps_plastic.mp3", Silence + ".mp3"),
            ]),
        new(
            "quiet-jumps",
            "builtin.quietJumps",
            "builtin.quietJumpsHint",
            [
                new("content/sounds/action_jump.mp3", Silence + ".mp3"),
                new("content/sounds/action_jump_land.mp3", Silence + ".mp3"),
                new("content/sounds/action_get_up.mp3", Silence + ".mp3"),
            ]),
    ];

    public static BuiltInMod? Find(string id) =>
        All.FirstOrDefault(mod => string.Equals(mod.Id, id, StringComparison.OrdinalIgnoreCase));

    public static bool Owns(string relativePath) =>
        All.SelectMany(mod => mod.Pieces)
            .Any(piece => string.Equals(piece.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));

    public static bool IsOn(string id, string modsFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modsFolder);

        BuiltInMod? mod = Find(id);

        return mod is not null && mod.Pieces.All(piece => File.Exists(Land(modsFolder, piece)));
    }

    public static bool Apply(string id, string modsFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modsFolder);

        BuiltInMod? mod = Find(id);

        if (mod is null)
        {
            return false;
        }

        foreach (BuiltInPiece piece in mod.Pieces)
        {
            using Stream? held = Owner.GetManifestResourceStream(piece.Resource);

            if (held is null)
            {
                return false;
            }

            string target = Land(modsFolder, piece);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);

                using FileStream landing = File.Create(target);

                held.CopyTo(landing);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        return true;
    }

    public static bool Remove(string id, string modsFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modsFolder);

        BuiltInMod? mod = Find(id);

        if (mod is null)
        {
            return false;
        }

        foreach (BuiltInPiece piece in mod.Pieces)
        {
            try
            {
                File.Delete(Land(modsFolder, piece));
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        return true;
    }

    private static string Land(string modsFolder, BuiltInPiece piece) =>
        Path.Combine(modsFolder, piece.RelativePath.Replace('/', Path.DirectorySeparatorChar));
}
