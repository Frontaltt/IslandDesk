using System.Management;
using System.Runtime.InteropServices;

namespace IslandDesk.Services;

public sealed record SystemMetrics(double CpuPercent, double RamPercent, double UsedRamGb,
    double TotalRamGb, double? GpuPercent, double? Fps);

public sealed class SystemMetricsService
{
    private ulong _previousIdle;
    private ulong _previousKernel;
    private ulong _previousUser;
    private bool _hasCpuSample;
    private readonly RtssFpsService _fpsService = new();

    public Task<SystemMetrics> ReadAsync() => Task.Run(() =>
    {
        double cpu = ReadCpu();
        var (ramPercent, usedGb, totalGb) = ReadRam();
        double? gpu = ReadGpu();
        double? fps = _fpsService.ReadFps();
        return new SystemMetrics(cpu, ramPercent, usedGb, totalGb, gpu, fps);
    });

    private double ReadCpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return 0;

        ulong idleNow = ToUInt64(idle);
        ulong kernelNow = ToUInt64(kernel);
        ulong userNow = ToUInt64(user);

        if (!_hasCpuSample)
        {
            _previousIdle = idleNow;
            _previousKernel = kernelNow;
            _previousUser = userNow;
            _hasCpuSample = true;
            return 0;
        }

        ulong idleDelta = idleNow - _previousIdle;
        ulong totalDelta = (kernelNow - _previousKernel) + (userNow - _previousUser);
        _previousIdle = idleNow;
        _previousKernel = kernelNow;
        _previousUser = userNow;

        if (totalDelta == 0) return 0;
        return Math.Clamp(100.0 * (1.0 - (double)idleDelta / totalDelta), 0, 100);
    }

    private static (double Percent, double UsedGb, double TotalGb) ReadRam()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status)) return (0, 0, 0);

        double total = status.TotalPhysical / 1024d / 1024d / 1024d;
        double available = status.AvailablePhysical / 1024d / 1024d / 1024d;
        double used = Math.Max(0, total - available);
        double percent = total <= 0 ? 0 : used / total * 100;
        return (percent, used, total);
    }

    private static double? ReadGpu()
    {
        try
        {
            // Windows exposes per-engine counters. We use the busiest 3D engine,
            // which avoids adding multiple engines and accidentally going above 100%.
            using var searcher = new ManagementObjectSearcher(
                "SELECT UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine " +
                "WHERE Name LIKE '%engtype_3D%' ");
            using var results = searcher.Get();
            double? maximum = null;
            foreach (ManagementObject item in results)
            {
                try
                {
                    var raw = item["UtilizationPercentage"];
                    if (raw != null && double.TryParse(raw.ToString(), out double value))
                        maximum = Math.Max(maximum ?? 0, Math.Clamp(value, 0, 100));
                }
                finally { item.Dispose(); }
            }
            return maximum;
        }
        catch
        {
            // Some GPU drivers do not publish the Windows GPU Engine WMI counter.
            return null;
        }
    }

    private static ulong ToUInt64(FileTime time) => ((ulong)time.High << 32) | time.Low;

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime { public uint Low; public uint High; }

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
    private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
