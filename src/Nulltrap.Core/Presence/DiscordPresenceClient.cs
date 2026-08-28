using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

using Nulltrap.Platform.Abstractions;

namespace Nulltrap.Core.Presence;

public sealed class DiscordPresenceClient : IDisposable
{
    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IPresenceTransportFactory _transports;
    private readonly string _applicationId;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IPresenceTransport? _transport;

    public DiscordPresenceClient(IPresenceTransportFactory transports, string applicationId)
    {
        ArgumentNullException.ThrowIfNull(transports);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);

        _transports = transports;
        _applicationId = applicationId.Trim();
    }

    public bool IsConnected => _transport?.IsConnected == true;

    public async Task<bool> SetAsync(PresenceActivity? activity, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            string json = JsonSerializer.Serialize(
                new
                {
                    cmd = "SET_ACTIVITY",
                    nonce = Guid.NewGuid().ToString(),
                    args = new
                    {
                        pid = Environment.ProcessId,
                        activity = activity is null ? null : Describe(activity),
                    },
                },
                Json);

            await _transport!.WriteAsync(DiscordFrame.Encode(DiscordOpcode.Frame, json), cancellationToken)
                .ConfigureAwait(false);

            return true;
        }
        catch (Exception failure) when (failure is IOException or InvalidOperationException or ObjectDisposedException)
        {
            Drop();
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<bool> ClearAsync(CancellationToken cancellationToken = default) =>
        SetAsync(null, cancellationToken);

    public void Dispose()
    {
        Drop();
        _gate.Dispose();
    }

    internal static ActivityPayload Describe(PresenceActivity activity) => new()
    {
        Details = Trim(activity.Details),
        State = Trim(activity.State),
        Timestamps = activity.StartedAt is null
            ? null
            : new TimestampsPayload { Start = activity.StartedAt.Value.ToUnixTimeSeconds() },
        Assets = activity.LargeImage is null && activity.SmallImage is null
            ? null
            : new AssetsPayload
            {
                LargeImage = activity.LargeImage,
                LargeText = Trim(activity.LargeText),
                SmallImage = activity.SmallImage,
                SmallText = Trim(activity.SmallText),
            },
        Buttons = activity.Buttons.Count == 0
            ? null
            : activity.Buttons
                .Take(2)
                .Select(button => new ButtonPayload { Label = button.Label, Url = button.Url })
                .ToList(),
    };

    private static string? Trim(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        return trimmed.Length <= 128 ? trimmed : trimmed[..127] + "…";
    }

    private async Task<bool> EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (IsConnected)
        {
            return true;
        }

        Drop();

        if (!IsDiscordRunning())
        {
            return false;
        }

        IPresenceTransport transport = _transports.Create();

        if (!await transport.ConnectAsync(cancellationToken).ConfigureAwait(false))
        {
            transport.Dispose();
            return false;
        }

        string handshake = JsonSerializer.Serialize(new { v = 1, client_id = _applicationId }, Json);

        await transport.WriteAsync(DiscordFrame.Encode(DiscordOpcode.Handshake, handshake), cancellationToken)
            .ConfigureAwait(false);

        _transport = transport;
        return true;
    }

    private static bool IsDiscordRunning()
    {
        foreach (string name in new[] { "Discord", "DiscordPTB", "DiscordCanary", "DiscordDevelopment" })
        {
            Process[] found = Process.GetProcessesByName(name);

            foreach (Process process in found)
            {
                process.Dispose();
            }

            if (found.Length > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void Drop()
    {
        _transport?.Dispose();
        _transport = null;
    }
}
