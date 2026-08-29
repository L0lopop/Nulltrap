using Nulltrap.Core.Deployment;
using Nulltrap.Core.State;

namespace Nulltrap.Core.Bootstrapping;

public sealed class ClientUpdateWatcher : IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(4);

    private readonly DeploymentClient _deployment;
    private readonly InstallStateStore _state;
    private readonly InstallJobs _jobs;
    private readonly CancellationTokenSource _cancellation = new();

    private Task? _loop;

    public ClientUpdateWatcher(DeploymentClient deployment, InstallStateStore state, InstallJobs jobs)
    {
        ArgumentNullException.ThrowIfNull(deployment);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(jobs);

        _deployment = deployment;
        _state = state;
        _jobs = jobs;
    }

    public DeploymentChannel Channel { get; set; } = DeploymentChannel.Default;

    public event EventHandler<BinaryType>? Started;

    public static bool NeedsUpdate(InstalledClient? installed, ClientVersion latest)
    {
        ArgumentNullException.ThrowIfNull(latest);

        return installed is not null
            && !string.Equals(installed.VersionGuid, latest.VersionGuid, StringComparison.OrdinalIgnoreCase);
    }

    public void Start() => _loop ??= Task.Run(() => RunAsync(_cancellation.Token));

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

    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        InstallState state = _state.Load();
        int started = 0;

        foreach (BinaryType binaryType in Enum.GetValues<BinaryType>().Where(type => state.Get(type) is not null))
        {
            if (_jobs.IsRunning(binaryType))
            {
                continue;
            }

            try
            {
                ClientVersion latest = await _deployment
                    .GetClientVersionAsync(binaryType, Channel, cancellationToken)
                    .ConfigureAwait(false);

                if (!NeedsUpdate(state.Get(binaryType), latest) || !_jobs.Start(binaryType, Channel))
                {
                    continue;
                }

                started++;
                Started?.Invoke(this, binaryType);
            }
            catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
            {
            }
        }

        return started;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);

            do
            {
                await SweepAsync(cancellationToken).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
        }
    }
}
