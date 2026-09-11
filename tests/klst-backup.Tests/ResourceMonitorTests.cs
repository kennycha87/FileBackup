using KlstBackup.Services;
using Xunit;

namespace KlstBackup.Tests;

public class ResourceMonitorTests
{
    [Fact]
    public void GetCpuUsage_ReturnsNonNegative()
    {
        var monitor = new ResourceMonitor();
        // First call to PerformanceCounter.NextValue() may return 0
        var cpu = monitor.GetCpuUsage();
        Assert.True(cpu >= 0);
    }

    [Fact]
    public void GetMemoryUsage_ReturnsPositive()
    {
        var monitor = new ResourceMonitor();
        var memory = monitor.GetMemoryUsage();
        Assert.True(memory > 0);
    }
}
