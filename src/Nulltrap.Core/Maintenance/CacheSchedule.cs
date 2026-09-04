namespace Nulltrap.Core.Maintenance;

public enum CacheSweep
{
    Never,
    EachStart,
    Daily,
    Weekly,
}

public static class CacheSchedule
{
    public static bool Due(CacheSweep plan, DateTimeOffset? last, DateTimeOffset now) => plan switch
    {
        CacheSweep.EachStart => true,
        CacheSweep.Daily => Older(last, now, TimeSpan.FromDays(1)),
        CacheSweep.Weekly => Older(last, now, TimeSpan.FromDays(7)),
        _ => false,
    };

    private static bool Older(DateTimeOffset? last, DateTimeOffset now, TimeSpan span) =>
        last is null || now - last.Value >= span;
}
