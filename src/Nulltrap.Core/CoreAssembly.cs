using System.Reflection;

namespace Nulltrap.Core;

public static class CoreAssembly
{
    public static Assembly Reference { get; } = typeof(CoreAssembly).Assembly;
}
