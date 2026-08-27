namespace Nulltrap.Core.Deployment;

public class DeploymentException : Exception
{
    public DeploymentException(string message)
        : base(message)
    {
    }

    public DeploymentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class NoReachableMirrorException : DeploymentException
{
    public NoReachableMirrorException(string message, IReadOnlyList<Exception> attempts)
        : base(message)
    {
        Attempts = attempts;
    }

    public IReadOnlyList<Exception> Attempts { get; }
}

public sealed class UnknownChannelException : DeploymentException
{
    public UnknownChannelException(DeploymentChannel channel)
        : base($"Roblox does not recognise the '{channel}' channel.")
    {
        Channel = channel;
    }

    public DeploymentChannel Channel { get; }
}
