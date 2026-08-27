namespace Nulltrap.Core.Deployment;

public readonly record struct DeploymentChannel
{
    public const string DefaultName = "production";

    public static readonly DeploymentChannel Default = new(DefaultName);

    public DeploymentChannel(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public string Name { get; }

    public bool IsDefault =>
        string.Equals(Name, DefaultName, StringComparison.OrdinalIgnoreCase);

    public override string ToString() => Name;

    public static implicit operator DeploymentChannel(string name) => new(name);
}
