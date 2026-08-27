namespace Nulltrap.Core.Packages;

public sealed class PackageDownloadException : Exception
{
    public PackageDownloadException(string message)
        : base(message)
    {
    }

    public PackageDownloadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
