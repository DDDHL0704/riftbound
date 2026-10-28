using System.Diagnostics;
using System.Text.Json;

namespace Riftbound.Engine;

// Diagnostic branch only. Never included in the ordinary L2 or production candidate.
internal sealed class L2RecoveryDiagnosticTrace(string scope)
{
    private readonly string? path = Environment.GetEnvironmentVariable("RIFTBOUND_L2_PROFILE_LOG");
    private long started = Stopwatch.GetTimestamp();

    internal void Mark(string phase, int commands = 0)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        File.AppendAllText(path, JsonSerializer.Serialize(new { scope, phase, commands, elapsedMs = elapsed }) + "\n");
        started = Stopwatch.GetTimestamp();
    }
}
