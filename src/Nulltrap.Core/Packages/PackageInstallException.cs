namespace Nulltrap.Core.Packages;

public sealed class PackageInstallException : Exception
{
    public PackageInstallException(string message)
        : base(message)
    {
    }
}
