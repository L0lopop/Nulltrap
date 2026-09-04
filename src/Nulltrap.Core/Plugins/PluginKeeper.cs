using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;

using Nulltrap.Platform.Abstractions;
using Nulltrap.Plugins;

namespace Nulltrap.Core.Plugins;

public sealed record PluginInfo(
    string Name,
    string Author,
    string Version,
    string File,
    bool Running,
    string? Trouble);

public sealed class PluginKeeper : IDisposable
{
    public const string FolderName = "Plugins";

    private readonly IApplicationPaths _paths;
    private readonly string _launcherVersion;
    private readonly List<Held> _held = [];
    private readonly ConcurrentDictionary<string, string> _asked = new(StringComparer.Ordinal);

    public PluginKeeper(IApplicationPaths paths, string launcherVersion)
    {
        ArgumentNullException.ThrowIfNull(paths);

        _paths = paths;
        _launcherVersion = launcherVersion ?? "0.0.0";
    }

    public string Folder => Path.Combine(_paths.Root, FolderName);

    public IReadOnlyList<PluginInfo> Found { get; private set; } = [];

    public IReadOnlyDictionary<string, string> Flags => _asked;

    public static IEnumerable<string> Assemblies(string folder)
    {
        if (!Directory.Exists(folder))
        {
            yield break;
        }

        foreach (string file in Directory.EnumerateFiles(folder, "*.dll", SearchOption.TopDirectoryOnly))
        {
            yield return file;
        }

        foreach (string nested in Directory.EnumerateDirectories(folder))
        {
            foreach (string file in Directory.EnumerateFiles(nested, "*.dll", SearchOption.TopDirectoryOnly))
            {
                yield return file;
            }
        }
    }

    public static string KeyFor(string file) => Path.GetFileNameWithoutExtension(file);

    public void Start(IReadOnlyCollection<string> allowed)
    {
        ArgumentNullException.ThrowIfNull(allowed);

        Stop();
        Directory.CreateDirectory(Folder);

        var seen = new List<PluginInfo>();

        foreach (string file in Assemblies(Folder))
        {
            string key = KeyFor(file);

            if (!allowed.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                seen.Add(new PluginInfo(key, string.Empty, string.Empty, file, Running: false, Trouble: null));
                continue;
            }

            seen.Add(Raise(file, key));
        }

        Found = seen;
    }

    public void Stop()
    {
        foreach (Held held in _held)
        {
            try
            {
                held.Plugin.Stop();
            }
            catch (Exception failure) when (failure is not OutOfMemoryException and not StackOverflowException)
            {
            }

            held.Room.Unload();
        }

        _held.Clear();
        _asked.Clear();
    }

    public void Tell(PluginSession session, bool joined)
    {
        ArgumentNullException.ThrowIfNull(session);

        foreach (Held held in _held)
        {
            try
            {
                held.Host.Raise(session, joined);
            }
            catch (Exception failure) when (failure is not OutOfMemoryException and not StackOverflowException)
            {
            }
        }
    }

    public void Dispose() => Stop();

    private PluginInfo Raise(string file, string key)
    {
        var room = new PluginRoom(file);

        try
        {
            Assembly built = room.LoadFromAssemblyPath(file);

            Type? found = built.GetTypes().FirstOrDefault(type =>
                typeof(INulltrapPlugin).IsAssignableFrom(type)
                && type is { IsAbstract: false, IsInterface: false });

            if (found is null)
            {
                room.Unload();
                return new PluginInfo(key, string.Empty, string.Empty, file, Running: false, "plugins.noEntry");
            }

            if (Activator.CreateInstance(found) is not INulltrapPlugin plugin)
            {
                room.Unload();
                return new PluginInfo(key, string.Empty, string.Empty, file, Running: false, "plugins.noEntry");
            }

            var host = new PluginRoomHost(_launcherVersion, Path.Combine(Folder, key + ".data"), _asked, key);

            plugin.Start(host);
            _held.Add(new Held(plugin, host, room));

            return new PluginInfo(plugin.Name, plugin.Author, plugin.Version, file, Running: true, Trouble: null);
        }
        catch (Exception failure) when (failure is not OutOfMemoryException and not StackOverflowException)
        {
            room.Unload();
            return new PluginInfo(key, string.Empty, string.Empty, file, Running: false, failure.Message);
        }
    }

    private sealed record Held(INulltrapPlugin Plugin, PluginRoomHost Host, PluginRoom Room);

    private sealed class PluginRoom(string file) : AssemblyLoadContext(isCollectible: true)
    {
        private readonly AssemblyDependencyResolver _resolver = new(file);

        private static readonly string? Contract = typeof(INulltrapPlugin).Assembly.GetName().Name;

        protected override Assembly? Load(AssemblyName name)
        {
            if (string.Equals(name.Name, Contract, StringComparison.Ordinal))
            {
                return null;
            }

            string? found = _resolver.ResolveAssemblyToPath(name);

            return found is null ? null : LoadFromAssemblyPath(found);
        }
    }

    private sealed class PluginRoomHost(
        string launcherVersion,
        string dataDirectory,
        ConcurrentDictionary<string, string> asked,
        string key) : IPluginHost
    {
        public string LauncherVersion => launcherVersion;

        public string DataDirectory
        {
            get
            {
                Directory.CreateDirectory(dataDirectory);
                return dataDirectory;
            }
        }

        public event EventHandler<PluginSession>? Joined;

        public event EventHandler<PluginSession>? Left;

        public void Log(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(dataDirectory);

                File.AppendAllText(
                    Path.Combine(dataDirectory, "log.txt"),
                    $"{DateTimeOffset.Now:u}  {message}{Environment.NewLine}");
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
            }
        }

        public void AskForFlag(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            asked[name.Trim()] = value?.Trim() ?? string.Empty;
        }

        public void Raise(PluginSession session, bool joined)
        {
            if (joined)
            {
                Joined?.Invoke(this, session);
                return;
            }

            Left?.Invoke(this, session);
        }

        public override string ToString() => key;
    }
}
