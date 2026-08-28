using System.Text;

namespace Nulltrap.Core.Sessions;

public sealed class RobloxLogWatcher : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan NewLogGrace = TimeSpan.FromMinutes(2);

    private readonly string _directory;
    private readonly SessionTracker _tracker;
    private readonly CancellationTokenSource _cancellation = new();

    private Task? _loop;

    public RobloxLogWatcher(string directory, SessionTracker tracker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(tracker);

        _directory = directory;
        _tracker = tracker;
    }

    public static string DefaultDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Roblox",
            "logs");

    public string? WatchedFile { get; private set; }

    public event EventHandler<string>? LineRead;

    public void Start()
    {
        _loop ??= Task.Run(() => RunAsync(_cancellation.Token));
    }

    public void Dispose()
    {
        _cancellation.Cancel();

        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _cancellation.Dispose();
    }

    public static string? NewestLog(string directory, DateTimeOffset notOlderThan)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        return new DirectoryInfo(directory)
            .EnumerateFiles("*_Player_*.log")
            .Where(file => file.LastWriteTimeUtc >= notOlderThan.UtcDateTime)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault()
            ?.FullName;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset startedWatching = DateTimeOffset.UtcNow - NewLogGrace;

        while (!cancellationToken.IsCancellationRequested)
        {
            string? path = NewestLog(_directory, startedWatching);

            if (path is null)
            {
                await Delay(cancellationToken).ConfigureAwait(false);
                continue;
            }

            WatchedFile = path;

            try
            {
                await ReadAsync(path, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                await Delay(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ReadAsync(string path, CancellationToken cancellationToken)
    {
        await using var file = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        using var reader = new StreamReader(file, Encoding.UTF8);

        while (!cancellationToken.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

            if (line is null)
            {
                // A newer log means Roblox restarted; go back and pick it up.
                if (!string.Equals(NewestLog(_directory, DateTimeOffset.MinValue), path, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                await Delay(cancellationToken).ConfigureAwait(false);
                continue;
            }

            LineRead?.Invoke(this, line);
            _tracker.Feed(line);
        }
    }

    private static async Task Delay(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
