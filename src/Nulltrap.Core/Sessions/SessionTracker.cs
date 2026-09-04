using System.Globalization;
using System.Text.RegularExpressions;

namespace Nulltrap.Core.Sessions;

public sealed partial class SessionTracker
{
    private const string EnteredPlaySession = "[FLog::SessionTransitionFSM] Entered play session.";
    private const string EnteredAppSession = "[FLog::SessionTransitionFSM] Entered app session.";
    private const string LeavingExperience = "sendAnalyticsBeforeLeave";
    private const string ReplicatorCreated = "[FLog::Network] Replicator created";

    private readonly Lock _gate = new();

    private RobloxSession _session = new();

    public SessionState State { get; private set; } = SessionState.Idle;

    public RobloxSession? Current => State == SessionState.Idle ? null : _session;

    public event EventHandler<RobloxSession>? Joining;

    public event EventHandler<RobloxSession>? Joined;

    public event EventHandler<RobloxSession>? Left;

    public event EventHandler<RobloxSession>? Moved;

    public event EventHandler<string>? MessageFromGame;

    public void GiveUp()
    {
        lock (_gate)
        {
            End();
        }
    }

    public void Feed(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        int channel = line.IndexOf("[FLog::", StringComparison.Ordinal);

        if (channel < 0)
        {
            return;
        }

        lock (_gate)
        {
            Sort(line[channel..]);
        }
    }

    private void Sort(string entry)
    {
        if (entry.StartsWith(EnteredPlaySession, StringComparison.Ordinal))
        {
            Begin();
            return;
        }

        if (entry.StartsWith(EnteredAppSession, StringComparison.Ordinal)
            || entry.Contains(LeavingExperience, StringComparison.Ordinal))
        {
            End();
            return;
        }

        if (Match(JoiningGame(), entry, out Match joining))
        {
            Begin();

            _session = _session with
            {
                JobId = joining.Groups[1].Value,
                PlaceId = ParseId(joining.Groups[2].Value),
                MachineAddress = joining.Groups[3].Value,
            };

            Joining?.Invoke(this, _session);
            return;
        }

        if (Match(JoinLoadTime(), entry, out Match loadTime))
        {
            _session = _session with
            {
                PlaceId = ParseId(loadTime.Groups["place"].Value),
                UniverseId = ParseId(loadTime.Groups["universe"].Value),
                UserId = ParseId(loadTime.Groups["user"].Value),
                ReferralPage = loadTime.Groups["referral"].Success
                    ? loadTime.Groups["referral"].Value
                    : _session.ReferralPage,
            };

            return;
        }

        if (Match(Udmux(), entry, out Match udmux))
        {
            _session = _session with
            {
                ServerAddress = udmux.Groups[1].Value,
                ServerPort = int.TryParse(udmux.Groups[2].Value, out int port) ? port : 0,
            };

            return;
        }

        if (entry.StartsWith(ReplicatorCreated, StringComparison.Ordinal))
        {
            if (State != SessionState.Playing)
            {
                State = SessionState.Playing;
                Joined?.Invoke(this, _session);
            }

            return;
        }

        if (Match(GameMessage(), entry, out Match message))
        {
            MessageFromGame?.Invoke(this, message.Groups[1].Value.Trim());
        }
    }

    private void Begin()
    {
        if (State == SessionState.Playing)
        {
            End(moving: true);
        }

        if (State != SessionState.Idle)
        {
            return;
        }

        State = SessionState.Joining;
        _session = new RobloxSession();
    }

    private void End(bool moving = false)
    {
        if (State == SessionState.Idle)
        {
            return;
        }

        RobloxSession finished = _session with { EndedAt = DateTimeOffset.UtcNow };

        State = SessionState.Idle;
        _session = new RobloxSession();

        if (moving)
        {
            Moved?.Invoke(this, finished);
            return;
        }

        Left?.Invoke(this, finished);
    }

    private static bool Match(Regex pattern, string entry, out Match match)
    {
        match = pattern.Match(entry);
        return match.Success;
    }

    private static long ParseId(string value) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long id) ? id : 0;

    [GeneratedRegex(@"! Joining game '([0-9a-f\-]{36})' place (\d+) at ([0-9\.]+)")]
    private static partial Regex JoiningGame();

    [GeneratedRegex(@"placeid:(?<place>\d+).*?universeid:(?<universe>\d+).*?(referral_page:(?<referral>[^,]*).*?)?userid:(?<user>\d+)")]
    private static partial Regex JoinLoadTime();

    [GeneratedRegex(@"UDMUX Address = ([0-9\.]+), Port = (\d+)")]
    private static partial Regex Udmux();

    [GeneratedRegex(@"\[NulltrapRPC\]\s*(.*)$")]
    private static partial Regex GameMessage();
}
