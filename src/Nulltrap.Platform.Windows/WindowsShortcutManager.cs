using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

using Nulltrap.Platform.Abstractions;

namespace Nulltrap.Platform.Windows;

public sealed class WindowsShortcutManager : IShortcutManager
{
    public bool Exists(ShortcutLocation location, string name) => File.Exists(PathFor(location, name));

    public void Create(
        ShortcutLocation location,
        string name,
        string targetPath,
        string arguments,
        string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        CreateAt(PathFor(location, name), targetPath, arguments, description);
    }

    public void CreateAt(string linkPath, string targetPath, string arguments, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(linkPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(linkPath))!);

        var link = (IShellLinkW)(object)new ShellLink();

        link.SetPath(targetPath);
        link.SetArguments(arguments ?? string.Empty);
        link.SetDescription(description ?? string.Empty);
        link.SetWorkingDirectory(Path.GetDirectoryName(targetPath) ?? string.Empty);
        link.SetIconLocation(targetPath, 0);

        ((IPersistFile)link).Save(Path.GetFullPath(linkPath), fRemember: true);

        Marshal.FinalReleaseComObject(link);
    }

    public void Remove(ShortcutLocation location, string name)
    {
        string path = PathFor(location, name);

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public string? ResolveTarget(ShortcutLocation location, string name) =>
        ResolveTargetAt(PathFor(location, name));

    public string? ResolveTargetAt(string linkPath)
    {
        if (!File.Exists(linkPath))
        {
            return null;
        }

        var link = (IShellLinkW)(object)new ShellLink();
        ((IPersistFile)link).Load(Path.GetFullPath(linkPath), 0);

        var buffer = new StringBuilder(260);
        link.GetPath(buffer, buffer.Capacity, out _, 0);

        Marshal.FinalReleaseComObject(link);

        string target = buffer.ToString();
        return string.IsNullOrWhiteSpace(target) ? null : target;
    }

    private static string PathFor(ShortcutLocation location, string name)
    {
        string folder = location switch
        {
            ShortcutLocation.Desktop =>
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            ShortcutLocation.StartMenu =>
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
            _ => throw new ArgumentOutOfRangeException(nameof(location), location, null),
        };

        return Path.Combine(folder, $"{name}.lnk");
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class ShellLink
    {
    }

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath(
            [MarshalAs(UnmanagedType.LPWStr)] StringBuilder file,
            int maxPath,
            out WIN32_FIND_DATAW findData,
            uint flags);

        void GetIDList(out nint idList);

        void SetIDList(nint idList);

        void GetDescription([MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maxName);

        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);

        void GetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int maxPath);

        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);

        void GetArguments([MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int maxArguments);

        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);

        void GetHotkey(out short hotkey);

        void SetHotkey(short hotkey);

        void GetShowCmd(out int showCmd);

        void SetShowCmd(int showCmd);

        void GetIconLocation([MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int iconPathLength, out int icon);

        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int icon);

        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relativePath, uint reserved);

        void Resolve(nint window, uint flags);

        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WIN32_FIND_DATAW
    {
        public uint FileAttributes;
        public long CreationTime;
        public long LastAccessTime;
        public long LastWriteTime;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint Reserved0;
        public uint Reserved1;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string FileName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string AlternateFileName;
    }
}
