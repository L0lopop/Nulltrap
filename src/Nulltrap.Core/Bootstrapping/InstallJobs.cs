using Nulltrap.Core.Deployment;
using Nulltrap.Core.Localization;
using Nulltrap.Core.Packages;

namespace Nulltrap.Core.Bootstrapping;

public sealed record InstallJob(BinaryType BinaryType, BootstrapProgress Progress)
{
    public bool Running { get; init; }

    public bool Cancelled { get; init; }

    public string? Failure { get; init; }

    public BootstrapResult? Result { get; init; }

    public bool Settled => !Running && (Failure is not null || Cancelled || Result is not null);
}

public sealed class InstallJobs : IDisposable
{
    private readonly ClientBootstrapper _bootstrapper;
    private readonly Lock _gate = new();
    private readonly Dictionary<BinaryType, CancellationTokenSource> _running = [];
    private readonly Dictionary<BinaryType, InstallJob> _latest = [];

    public InstallJobs(ClientBootstrapper bootstrapper)
    {
        ArgumentNullException.ThrowIfNull(bootstrapper);
        _bootstrapper = bootstrapper;
    }

    public event EventHandler<InstallJob>? Changed;

    public InstallJob? Of(BinaryType binaryType)
    {
        lock (_gate)
        {
            return _latest.GetValueOrDefault(binaryType);
        }
    }

    public bool IsRunning(BinaryType binaryType)
    {
        lock (_gate)
        {
            return _running.ContainsKey(binaryType);
        }
    }

    public bool Start(BinaryType binaryType, DeploymentChannel channel)
    {
        CancellationTokenSource cancellation;

        lock (_gate)
        {
            if (_running.ContainsKey(binaryType))
            {
                return false;
            }

            cancellation = new CancellationTokenSource();
            _running[binaryType] = cancellation;
        }

        Publish(new InstallJob(
            binaryType,
            BootstrapProgress.For(BootstrapStage.Connecting, Strings.Get("bootstrap.connecting")))
        {
            Running = true,
        });

        _ = Task.Run(() => RunAsync(binaryType, channel, cancellation), CancellationToken.None);

        return true;
    }

    public void Cancel(BinaryType binaryType)
    {
        lock (_gate)
        {
            _running.GetValueOrDefault(binaryType)?.Cancel();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (CancellationTokenSource cancellation in _running.Values)
            {
                cancellation.Cancel();
                cancellation.Dispose();
            }

            _running.Clear();
        }
    }

    private async Task RunAsync(
        BinaryType binaryType,
        DeploymentChannel channel,
        CancellationTokenSource cancellation)
    {
        var progress = new Progress<BootstrapProgress>(report =>
            Publish(new InstallJob(binaryType, report) { Running = true }));

        try
        {
            BootstrapResult result = await _bootstrapper
                .EnsureUpToDateAsync(binaryType, channel, progress, cancellation.Token)
                .ConfigureAwait(false);

            Settle(binaryType, cancellation, new InstallJob(
                binaryType,
                BootstrapProgress.For(BootstrapStage.Ready, Strings.Get("bootstrap.ready")))
            {
                Result = result,
            });
        }
        catch (OperationCanceledException)
        {
            Settle(binaryType, cancellation, new InstallJob(
                binaryType,
                BootstrapProgress.For(BootstrapStage.Connecting, Strings.Get("bootstrap.cancelled")))
            {
                Cancelled = true,
            });
        }
        catch (Exception failure)
        {
            Settle(binaryType, cancellation, new InstallJob(
                binaryType,
                BootstrapProgress.For(BootstrapStage.Connecting, failure.Message))
            {
                Failure = failure.Message,
            });
        }
    }

    private void Settle(BinaryType binaryType, CancellationTokenSource cancellation, InstallJob job)
    {
        lock (_gate)
        {
            if (_running.GetValueOrDefault(binaryType) == cancellation)
            {
                _running.Remove(binaryType);
            }
        }

        cancellation.Dispose();
        Publish(job);
    }

    private void Publish(InstallJob job)
    {
        lock (_gate)
        {
            _latest[job.BinaryType] = job;
        }

        Changed?.Invoke(this, job);
    }
}
