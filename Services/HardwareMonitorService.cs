using LibreHardwareMonitor.Hardware;

namespace IslandDesk.Services;

public sealed record HardwareTemperatures(
    double? CpuCelsius,
    double? GpuCelsius,
    string CpuSensor = "",
    string GpuSensor = "",
    string Diagnostic = "");

/// <summary>
/// Reads real temperature sensors exposed by LibreHardwareMonitor.
/// Sensor names are kept for diagnostics so an unavailable probe is not silently
/// replaced with a plausible-looking value from an unrelated device.
/// </summary>
public sealed class HardwareMonitorService : IDisposable
{
    private readonly Computer _computer = new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMotherboardEnabled = true,
        IsControllerEnabled = true
    };
    private readonly object _sync = new();
    private bool _opened;
    private string _lastError = "";

    public Task<HardwareTemperatures> ReadAsync() => Task.Run(() =>
    {
        lock (_sync)
        {
            try
            {
                if (!_opened)
                {
                    _computer.Open();
                    _opened = true;
                }

                double? cpu = null;
                double? gpu = null;
                int cpuScore = -1;
                int gpuScore = -1;
                string cpuSensor = "";
                string gpuSensor = "";

                foreach (var hardware in _computer.Hardware)
                    ReadHardware(hardware, ref cpu, ref cpuScore, ref cpuSensor,
                        ref gpu, ref gpuScore, ref gpuSensor);

                string diagnostic = cpu.HasValue || gpu.HasValue
                    ? "Temperature sensors are available."
                    : string.IsNullOrWhiteSpace(_lastError)
                        ? "No supported temperature sensors were exposed by the hardware or driver."
                        : _lastError;

                return new HardwareTemperatures(cpu, gpu, cpuSensor, gpuSensor, diagnostic);
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                System.Diagnostics.Debug.WriteLine($"Hardware monitor open/read failed: {ex}");
                return new HardwareTemperatures(null, null, "", "", ex.Message);
            }
        }
    });

    private void ReadHardware(IHardware hardware,
        ref double? cpu, ref int cpuScore, ref string cpuSensor,
        ref double? gpu, ref int gpuScore, ref string gpuSensor)
    {
        try
        {
            hardware.Update();
            bool isCpu = hardware.HardwareType == HardwareType.Cpu;
            bool isGpu = hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;

            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.SensorType != SensorType.Temperature || !sensor.Value.HasValue) continue;
                double value = sensor.Value.Value;
                // Ignore zero placeholders and physically implausible readings.
                if (!double.IsFinite(value) || value < 1 || value > 140) continue;

                string name = sensor.Name ?? "Temperature";
                if (isCpu)
                {
                    int score = ScoreCpuSensor(name);
                    if (score > cpuScore)
                    {
                        cpu = value;
                        cpuScore = score;
                        cpuSensor = $"{hardware.Name} · {name}";
                    }
                }
                else if (isGpu)
                {
                    int score = ScoreGpuSensor(name);
                    if (score > gpuScore)
                    {
                        gpu = value;
                        gpuScore = score;
                        gpuSensor = $"{hardware.Name} · {name}";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            System.Diagnostics.Debug.WriteLine($"Sensor read failed ({hardware.Name}): {ex.Message}");
        }

        foreach (var sub in hardware.SubHardware)
            ReadHardware(sub, ref cpu, ref cpuScore, ref cpuSensor,
                ref gpu, ref gpuScore, ref gpuSensor);
    }

    private static int ScoreCpuSensor(string name)
    {
        if (name.Contains("Package", StringComparison.OrdinalIgnoreCase)) return 120;
        if (name.Contains("Tctl", StringComparison.OrdinalIgnoreCase) || name.Contains("Tdie", StringComparison.OrdinalIgnoreCase)) return 115;
        if (name.Contains("Die", StringComparison.OrdinalIgnoreCase)) return 105;
        if (name.Contains("Core Average", StringComparison.OrdinalIgnoreCase)) return 95;
        if (name.Contains("Core", StringComparison.OrdinalIgnoreCase)) return 75;
        if (name.Contains("Temperature", StringComparison.OrdinalIgnoreCase)) return 40;
        return 10;
    }

    private static int ScoreGpuSensor(string name)
    {
        if (name.Contains("GPU Core", StringComparison.OrdinalIgnoreCase)) return 120;
        if (name.Contains("GPU Temperature", StringComparison.OrdinalIgnoreCase)) return 115;
        if (name.Equals("Core", StringComparison.OrdinalIgnoreCase)) return 105;
        if (name.Contains("Hot Spot", StringComparison.OrdinalIgnoreCase) || name.Contains("Hotspot", StringComparison.OrdinalIgnoreCase)) return 90;
        if (name.Contains("Junction", StringComparison.OrdinalIgnoreCase)) return 85;
        if (name.Contains("Memory", StringComparison.OrdinalIgnoreCase)) return 65;
        if (name.Contains("Temperature", StringComparison.OrdinalIgnoreCase)) return 45;
        return 10;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (!_opened) return;
            try { _computer.Close(); } catch { }
            _opened = false;
        }
    }
}
