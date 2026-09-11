using System;
using System.Diagnostics;

namespace KlstBackup.Services;

public class ResourceMonitor
{
    private readonly PerformanceCounter _cpuCounter;
    private readonly PerformanceCounter _memoryCounter;

    public ResourceMonitor()
    {
        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
    }

    public double GetCpuUsage()
    {
        return _cpuCounter.NextValue();
    }

    public long GetMemoryUsage()
    {
        return GC.GetTotalMemory(false) / (1024 * 1024);
    }

    public long GetAvailableMemory()
    {
        return (long)_memoryCounter.NextValue() * 1024 * 1024;
    }
}
