using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Nexus.Models;

namespace Nexus.Services;

internal sealed class SystemMetricsService
{
    private ulong _lastIdle;
    private ulong _lastKernel;
    private ulong _lastUser;
    private bool _hasCpuSample;
    private long _lastReceived;
    private long _lastSent;
    private long _lastNetworkTimestamp;

    public SystemMetricsSnapshot Read()
    {
        var cpu = ReadCpuPercent();
        var (ramUsed, ramTotal) = ReadMemory();
        var (diskUsed, diskTotal) = ReadSystemDrive();
        var (download, upload) = ReadNetworkRates();

        return new SystemMetricsSnapshot(
            cpu,
            ramUsed,
            ramTotal,
            diskUsed,
            diskTotal,
            download,
            upload,
            TimeSpan.FromMilliseconds(Environment.TickCount64),
            Environment.OSVersion.VersionString);
    }

    private double ReadCpuPercent()
    {
        try
        {
            if (!GetSystemTimes(out var idle, out var kernel, out var user))
                return 0;

            var idleNow = idle.ToUInt64();
            var kernelNow = kernel.ToUInt64();
            var userNow = user.ToUInt64();

            if (!_hasCpuSample)
            {
                _lastIdle = idleNow;
                _lastKernel = kernelNow;
                _lastUser = userNow;
                _hasCpuSample = true;
                return 0;
            }

            var idleDelta = idleNow - _lastIdle;
            var kernelDelta = kernelNow - _lastKernel;
            var userDelta = userNow - _lastUser;
            var totalDelta = kernelDelta + userDelta;

            _lastIdle = idleNow;
            _lastKernel = kernelNow;
            _lastUser = userNow;

            if (totalDelta == 0)
                return 0;

            var busy = totalDelta > idleDelta ? totalDelta - idleDelta : 0;
            return Math.Clamp((double)busy / totalDelta * 100.0, 0, 100);
        }
        catch
        {
            return 0;
        }
    }

    private static (ulong Used, ulong Total) ReadMemory()
    {
        try
        {
            var status = new MemoryStatusEx
            {
                Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
            };

            if (GlobalMemoryStatusEx(ref status))
                return (status.TotalPhysical - status.AvailablePhysical, status.TotalPhysical);
        }
        catch
        {
            // Leave the dashboard alive if Windows does not expose this counter.
        }

        return (0, 0);
    }

    private static (ulong Used, ulong Total) ReadSystemDrive()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory);
            if (!string.IsNullOrWhiteSpace(root))
            {
                var drive = new DriveInfo(root);
                if (drive.IsReady)
                    return ((ulong)(drive.TotalSize - drive.AvailableFreeSpace), (ulong)drive.TotalSize);
            }
        }
        catch
        {
            // Removable or unavailable drives are not fatal to the dashboard.
        }

        return (0, 0);
    }

    private (double Download, double Upload) ReadNetworkRates()
    {
        try
        {
            long received = 0;
            long sent = 0;

            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up ||
                    adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                try
                {
                    var stats = adapter.GetIPv4Statistics();
                    received += stats.BytesReceived;
                    sent += stats.BytesSent;
                }
                catch
                {
                    // Ignore adapters that disappear while metrics are being sampled.
                }
            }

            var now = Stopwatch.GetTimestamp();
            if (_lastNetworkTimestamp == 0)
            {
                _lastReceived = received;
                _lastSent = sent;
                _lastNetworkTimestamp = now;
                return (0, 0);
            }

            var seconds = (double)(now - _lastNetworkTimestamp) / Stopwatch.Frequency;
            var down = Math.Max(0, received - _lastReceived);
            var up = Math.Max(0, sent - _lastSent);

            _lastReceived = received;
            _lastSent = sent;
            _lastNetworkTimestamp = now;

            return seconds <= 0 ? (0, 0) : (down / seconds, up / seconds);
        }
        catch
        {
            return (0, 0);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileTime
    {
        public uint Low;
        public uint High;

        public readonly ulong ToUInt64() => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out NativeFileTime idleTime,
        out NativeFileTime kernelTime,
        out NativeFileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}