using Nulltrap.Core.Localization;

namespace Nulltrap.Core.Bootstrapping;

public sealed class TransferRate
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(3);

    private readonly Queue<(DateTimeOffset At, long Bytes)> _samples = new();

    public void Add(long bytes, DateTimeOffset at)
    {
        if (_samples.Count > 0 && bytes < _samples.Last().Bytes)
        {
            _samples.Clear();
        }

        _samples.Enqueue((at, bytes));

        while (_samples.Count > 2 && at - _samples.Peek().At > Window)
        {
            _samples.Dequeue();
        }
    }

    public void Reset() => _samples.Clear();

    public double BytesPerSecond()
    {
        if (_samples.Count < 2)
        {
            return 0;
        }

        (DateTimeOffset At, long Bytes) first = _samples.Peek();
        (DateTimeOffset At, long Bytes) last = _samples.Last();

        double seconds = (last.At - first.At).TotalSeconds;

        return seconds <= 0 ? 0 : (last.Bytes - first.Bytes) / seconds;
    }
}

public static class Sizes
{
    private static readonly string[] Units = ["size.bytes", "size.kilobytes", "size.megabytes", "size.gigabytes"];

    public static string Describe(long bytes)
    {
        double value = Math.Max(0, bytes);
        int unit = 0;

        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        string number = unit == 0 || value >= 100 ? value.ToString("N0") : value.ToString("N1");

        return $"{number} {Strings.Get(Units[unit])}";
    }

    public static string Rate(double bytesPerSecond) =>
        bytesPerSecond <= 0 ? "-" : Describe((long)bytesPerSecond) + "/s";
}
