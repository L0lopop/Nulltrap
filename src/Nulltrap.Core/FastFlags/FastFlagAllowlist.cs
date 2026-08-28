namespace Nulltrap.Core.FastFlags;

public enum FastFlagCategory
{
    Geometry,
    Rendering,
    UserInterface,
}

public sealed record FastFlagDefinition(string Name, FastFlagCategory Category);

public static class FastFlagAllowlist
{
    public static IReadOnlyList<FastFlagDefinition> Definitions { get; } =
    [
        new("DFIntCSGLevelOfDetailSwitchingDistance", FastFlagCategory.Geometry),
        new("DFIntCSGLevelOfDetailSwitchingDistanceL12", FastFlagCategory.Geometry),
        new("DFIntCSGLevelOfDetailSwitchingDistanceL23", FastFlagCategory.Geometry),
        new("DFIntCSGLevelOfDetailSwitchingDistanceL34", FastFlagCategory.Geometry),

        new("FFlagHandleAltEnterFullscreenManually", FastFlagCategory.Rendering),
        new("DFFlagTextureQualityOverrideEnabled", FastFlagCategory.Rendering),
        new("DFIntTextureQualityOverride", FastFlagCategory.Rendering),
        new("FIntDebugForceMSAASamples", FastFlagCategory.Rendering),
        new("DFFlagDisableDPIScale", FastFlagCategory.Rendering),
        new("FFlagDebugGraphicsPreferD3D11", FastFlagCategory.Rendering),
        new("FFlagDebugGraphicsPreferVulkan", FastFlagCategory.Rendering),
        new("FFlagDebugGraphicsPreferOpenGL", FastFlagCategory.Rendering),
        new("FFlagDebugSkyGray", FastFlagCategory.Rendering),
        new("DFFlagDebugPauseVoxelizer", FastFlagCategory.Rendering),
        new("DFIntDebugFRMQualityLevelOverride", FastFlagCategory.Rendering),
        new("FIntFRMMaxGrassDistance", FastFlagCategory.Rendering),
        new("FIntFRMMinGrassDistance", FastFlagCategory.Rendering),

        new("FIntGrassMovementReducedMotionFactor", FastFlagCategory.UserInterface),
    ];

    private static readonly HashSet<string> Allowed =
        Definitions.Select(definition => definition.Name).ToHashSet(StringComparer.Ordinal);

    public static bool IsAllowed(string name) => Allowed.Contains(name);

    public static IReadOnlyList<string> RejectedIn(IEnumerable<string> names) =>
        names.Where(name => !IsAllowed(name)).ToArray();
}
