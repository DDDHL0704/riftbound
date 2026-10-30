using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Riftbound.ConformanceTests;

// Opt-in diagnostics from the executing testhost, rather than the inventory
// helper. No sampling thread, forced collection, or change to test results.
internal static class CiTestHostObservation
{
    private static int started;

    internal static void Start()
    {
        var directory = Environment.GetEnvironmentVariable("RIFTBOUND_TEST_RUNTIME_DIR");
        if (string.IsNullOrEmpty(directory) || Interlocked.Exchange(ref started, 1) != 0) return;
        var began = Stopwatch.GetTimestamp();
        var startedUtc = DateTimeOffset.UtcNow;

        void Save(string status)
        {
            try
            {
                using var process = Process.GetCurrentProcess();
                var memory = GC.GetGCMemoryInfo();
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, $"testhost-{Environment.ProcessId}.json"),
                    JsonSerializer.Serialize(new
                    {
                        status, startedUtc, processId = Environment.ProcessId,
                        processorCount = Environment.ProcessorCount,
                        framework = RuntimeInformation.FrameworkDescription,
                        elapsedSeconds = Stopwatch.GetElapsedTime(began).TotalSeconds,
                        processCpuSeconds = process.TotalProcessorTime.TotalSeconds,
                        peakWorkingSetBytes = process.PeakWorkingSet64,
                        serverGCRequested = Environment.GetEnvironmentVariable("DOTNET_gcServer"),
                        serverGC = GCSettings.IsServerGC,
                        latencyMode = GCSettings.LatencyMode.ToString(),
                        regexCacheSize = Regex.CacheSize,
                        totalAllocatedBytes = GC.GetTotalAllocatedBytes(precise: true),
                        totalGCPauseSeconds = GC.GetTotalPauseDuration().TotalSeconds,
                        collections = Enumerable.Range(0, GC.MaxGeneration + 1).Select(GC.CollectionCount).ToArray(),
                        heapSizeBytes = memory.HeapSizeBytes,
                        totalCommittedBytes = memory.TotalCommittedBytes,
                        availableMemoryBytes = memory.TotalAvailableMemoryBytes,
                        pauseTimePercentage = memory.PauseTimePercentage,
                        scope = "Process lifetime counters include test discovery and runner overhead."
                    }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // Diagnostics must not turn a successful rule execution into a failure.
                Console.Error.WriteLine("Cannot write optional testhost runtime observation: " + error.Message);
            }
        }

        Save("started");
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Save("finished");
    }
}
