using System.Reflection;

namespace Nulltrap.Platform.Windows;

public static class WindowsPlatformAssembly
{
    public static Assembly Reference { get; } = typeof(WindowsPlatformAssembly).Assembly;
}
