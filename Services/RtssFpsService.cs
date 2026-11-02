using System.IO.MemoryMappedFiles;

namespace IslandDesk.Services;

/// <summary>
/// Reads the FPS value RTSS already measures for the last foreground 3D application.
/// No game hooking or frame injection is performed by IslandDesk.
/// </summary>
public sealed class RtssFpsService
{
    private const string MappingName = "RTSSSharedMemoryV2";
    private const uint RtssSignature = 0x52545353; // RTSS SDK signature DWORD
    private const int MinimumAppEntryBytes = 284;
    private const uint MaximumEntries = 256;

    public double? ReadFps()
    {
        try
        {
            using var mapping = MemoryMappedFile.OpenExisting(MappingName, MemoryMappedFileRights.Read);
            using var view = mapping.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            uint signature = view.ReadUInt32(0);
            uint version = view.ReadUInt32(4);
            if (signature != RtssSignature || (version >> 16) != 2) return null;

            uint entrySize = view.ReadUInt32(8);
            uint appArrayOffset = view.ReadUInt32(12);
            uint appArraySize = view.ReadUInt32(16);
            if (entrySize < MinimumAppEntryBytes || appArrayOffset == 0) return null;

            // RTSS 2.16+ exposes the last foreground 3D application PID at byte 68.
            uint foregroundPid = version >= 0x00020010 ? view.ReadUInt32(68) : 0;
            uint count = Math.Min(appArraySize, MaximumEntries);
            double? fallback = null;

            for (uint i = 0; i < count; i++)
            {
                long offset = checked((long)appArrayOffset + (long)i * entrySize);
                uint pid = view.ReadUInt32(offset);
                if (pid == 0) continue;

                uint flags = view.ReadUInt32(offset + 264);
                if ((flags & 0x0000FFFF) == 0) continue;
                uint time0 = view.ReadUInt32(offset + 268);
                uint time1 = view.ReadUInt32(offset + 272);
                uint frames = view.ReadUInt32(offset + 276);
                uint frameTimeMicroseconds = view.ReadUInt32(offset + 280);

                double? fps = null;
                if (frameTimeMicroseconds >= 1000 && frameTimeMicroseconds <= 1_000_000)
                    fps = 1_000_000d / frameTimeMicroseconds;
                else if (time1 > time0 && frames > 0)
                    fps = 1000d * frames / (time1 - time0);

                if (fps is null || double.IsNaN(fps.Value) || double.IsInfinity(fps.Value) || fps < 1 || fps > 1000)
                    continue;

                if (foregroundPid != 0 && pid == foregroundPid)
                    return Math.Round(fps.Value);

                fallback ??= Math.Round(fps.Value);
            }

            return fallback;
        }
        catch
        {
            // RTSS is optional. A missing shared memory block is not an app error.
            return null;
        }
    }
}
