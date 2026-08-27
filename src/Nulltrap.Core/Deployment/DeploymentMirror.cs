namespace Nulltrap.Core.Deployment;

public sealed record DeploymentMirror(Uri BaseUri, int Priority)
{
    public static IReadOnlyList<DeploymentMirror> Known { get; } =
    [
        new(new Uri("https://setup.rbxcdn.com"), 0),
        new(new Uri("https://setup-aws.rbxcdn.com"), 2),
        new(new Uri("https://setup-ak.rbxcdn.com"), 2),
        new(new Uri("https://s3.amazonaws.com/setup.roblox.com"), 4),
    ];

    public Uri GetResourceUri(string resource) =>
        new($"{BaseUri.AbsoluteUri.TrimEnd('/')}/channel/common/{resource.TrimStart('/')}");

    public Uri GetPackageManifestUri(string versionGuid) =>
        GetResourceUri($"{versionGuid}-rbxPkgManifest.txt");

    public Uri GetPackageUri(string versionGuid, string packageName) =>
        GetResourceUri($"{versionGuid}-{packageName}");

    public override string ToString() => BaseUri.Host;
}
