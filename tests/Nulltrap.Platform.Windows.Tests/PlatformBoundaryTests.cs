using System.Reflection;
using System.Runtime.Versioning;

using Nulltrap.Platform.Windows;

namespace Nulltrap.Platform.Windows.Tests;

public class PlatformBoundaryTests
{
    [Fact]
    public void Windows_platform_layer_is_pinned_to_windows()
    {
        string? platform = WindowsPlatformAssembly.Reference
            .GetCustomAttribute<TargetPlatformAttribute>()
            ?.PlatformName;

        Assert.NotNull(platform);
        Assert.Contains("Windows", platform, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Abstractions_layer_stays_portable()
    {
        string outputDirectory = Path.GetDirectoryName(WindowsPlatformAssembly.Reference.Location)!;
        string abstractionsPath = Path.Combine(outputDirectory, "Nulltrap.Platform.Abstractions.dll");

        Assert.True(
            File.Exists(abstractionsPath),
            $"Expected the abstractions assembly next to the Windows platform layer, at {abstractionsPath}.");

        string? platform = Assembly.LoadFrom(abstractionsPath)
            .GetCustomAttribute<TargetPlatformAttribute>()
            ?.PlatformName;

        Assert.True(
            platform is null,
            $"Nulltrap.Platform.Abstractions is pinned to '{platform}'. The contract layer "
            + "describes what an OS must provide; it must not be tied to one.");
    }
}
