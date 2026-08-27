namespace Nulltrap.Core.Bootstrapping;

public enum BootstrapStage
{
    Connecting,
    CheckingVersion,
    Downloading,
    Installing,
    Cleaning,
    Ready,
}

public sealed record BootstrapProgress(BootstrapStage Stage, string Message, double Fraction)
{
    public static BootstrapProgress For(BootstrapStage stage, string message) =>
        new(stage, message, StageFloor(stage));

    public static BootstrapProgress Within(BootstrapStage stage, string message, double fraction)
    {
        double floor = StageFloor(stage);
        double ceiling = StageCeiling(stage);
        return new BootstrapProgress(stage, message, floor + (ceiling - floor) * Math.Clamp(fraction, 0, 1));
    }

    private static double StageFloor(BootstrapStage stage) => stage switch
    {
        BootstrapStage.Connecting => 0.00,
        BootstrapStage.CheckingVersion => 0.05,
        BootstrapStage.Downloading => 0.10,
        BootstrapStage.Installing => 0.75,
        BootstrapStage.Cleaning => 0.97,
        BootstrapStage.Ready => 1.00,
        _ => 0,
    };

    private static double StageCeiling(BootstrapStage stage) => stage switch
    {
        BootstrapStage.Connecting => 0.05,
        BootstrapStage.CheckingVersion => 0.10,
        BootstrapStage.Downloading => 0.75,
        BootstrapStage.Installing => 0.97,
        BootstrapStage.Cleaning => 1.00,
        BootstrapStage.Ready => 1.00,
        _ => 1,
    };
}
