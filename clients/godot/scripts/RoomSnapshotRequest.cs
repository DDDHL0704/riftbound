using System;
using System.Threading.Tasks;

namespace Riftbound.GodotClient;

internal static class RoomSnapshotRequest
{
    public static async Task<bool> RunAsync(
        Func<Task> join,
        Func<Task> requestSnapshot,
        Func<bool> canContinue)
    {
        if (!canContinue()) return false;
        try
        {
            await join();
            if (!canContinue()) return false;
            await requestSnapshot();
            return canContinue();
        }
        catch (InvalidOperationException) when (!canContinue())
        {
            return false;
        }
    }
}
