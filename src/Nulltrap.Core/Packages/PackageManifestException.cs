namespace Nulltrap.Core.Packages;

public sealed class PackageManifestException : Exception
{
    public PackageManifestException(string message)
        : base(message)
    {
    }
}
