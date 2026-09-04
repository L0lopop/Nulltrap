using System.Diagnostics;
using System.Runtime.InteropServices;

using Nulltrap.Platform.Abstractions;

namespace Nulltrap.Platform.Windows;

public sealed class WindowsMemoryTrimmer : IMemoryTrimmer
{
    public long Held
    {
        get
        {
            using Process self = Process.GetCurrentProcess();
            self.Refresh();

            return self.WorkingSet64;
        }
    }

    public bool Trim()
    {
        GC.Collect(2, GCCollectionMode.Optimized, blocking: false);

        try
        {
            using Process self = Process.GetCurrentProcess();

            return EmptyWorkingSet(self.Handle);
        }
        catch (Exception failure) when (failure is InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyWorkingSet(nint process);
}
