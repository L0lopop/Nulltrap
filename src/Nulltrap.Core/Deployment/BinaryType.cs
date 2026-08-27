namespace Nulltrap.Core.Deployment;

public enum BinaryType
{
    WindowsPlayer,
    WindowsStudio64,
}

public static class BinaryTypeExtensions
{
    public static string ToApiName(this BinaryType binaryType) => binaryType switch
    {
        BinaryType.WindowsPlayer => "WindowsPlayer",
        BinaryType.WindowsStudio64 => "WindowsStudio64",
        _ => throw new ArgumentOutOfRangeException(nameof(binaryType), binaryType, null),
    };

    public static string ToExecutableName(this BinaryType binaryType) => binaryType switch
    {
        BinaryType.WindowsPlayer => "RobloxPlayerBeta.exe",
        BinaryType.WindowsStudio64 => "RobloxStudioBeta.exe",
        _ => throw new ArgumentOutOfRangeException(nameof(binaryType), binaryType, null),
    };
}
