using System.Reflection;
using System.Runtime.Versioning;

using Nulltrap.Core;

namespace Nulltrap.Core.Tests;

public class ArchitectureTests
{
    private static readonly string[] WindowsOnlyAssemblies =
    [
        "Microsoft.Win32.Registry",
        "Microsoft.Win32.SystemEvents",
        "Microsoft.Windows.SDK.NET",
        "PresentationCore",
        "PresentationFramework",
        "System.Drawing.Common",
        "System.Management",
        "System.ServiceProcess.ServiceController",
        "System.Windows.Forms",
        "WindowsBase",
        "WinRT.Runtime",
    ];

    [Fact]
    public void Core_declares_no_target_platform()
    {
        string? platform = CoreAssembly.Reference
            .GetCustomAttribute<TargetPlatformAttribute>()
            ?.PlatformName;

        Assert.True(
            platform is null,
            $"Nulltrap.Core is pinned to the '{platform}' platform. Its target framework "
            + "must stay net10.0 with no platform suffix - that is what makes a second "
            + "UI on a second platform possible later.");
    }

    [Fact]
    public void Core_does_not_reference_windows_only_assemblies()
    {
        string[] offenders = CoreAssembly.Reference
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .Intersect(WindowsOnlyAssemblies, StringComparer.OrdinalIgnoreCase)
            .ToArray()!;

        Assert.True(
            offenders.Length == 0,
            $"Nulltrap.Core references Windows-only assemblies: {string.Join(", ", offenders)}. "
            + "Move the code behind an interface in Nulltrap.Platform.Abstractions "
            + "and implement it in Nulltrap.Platform.Windows.");
    }

    [Fact]
    public void Every_nulltrap_assembly_on_the_portable_path_is_portable()
    {
        string outputDirectory = Path.GetDirectoryName(CoreAssembly.Reference.Location)!;

        string[] assemblyPaths = Directory
            .EnumerateFiles(outputDirectory, "Nulltrap.*.dll")
            .Where(path => !Path.GetFileNameWithoutExtension(path).EndsWith(".Tests", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(assemblyPaths);

        List<string> offenders = [];

        foreach (string path in assemblyPaths)
        {
            string? platform = Assembly.LoadFrom(path)
                .GetCustomAttribute<TargetPlatformAttribute>()
                ?.PlatformName;

            if (platform is not null)
            {
                offenders.Add($"{Path.GetFileName(path)} ({platform})");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "These assemblies are on Nulltrap.Core's dependency path but are pinned to a "
            + $"platform: {string.Join(", ", offenders)}.");
    }
}
