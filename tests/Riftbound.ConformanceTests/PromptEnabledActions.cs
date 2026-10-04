using Riftbound.Contracts;

namespace Riftbound.ConformanceTests;

internal static class PromptEnabledActions
{
    // Actions names may include disabled operations so the client can explain them.
    // Drivers and legality assertions must respect the candidate's Enabled flag.
    internal static IReadOnlyList<string> EnabledActions(this ActionPromptDto prompt)
        => prompt.Actionable && prompt.Candidates is { Count: > 0 }
            ? prompt.Candidates.Where(candidate => candidate.Enabled).Select(candidate => candidate.Action).ToArray()
            : prompt.Actions;
}
