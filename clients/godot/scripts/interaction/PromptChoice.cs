using System;
using System.Collections.Generic;
using System.Linq;

namespace Riftbound.GodotClient.Interaction;

public sealed record PromptChoice(
    string Role,
    string Id,
    string Label,
    IReadOnlyList<string> ObjectIds,
    int StepIndex)
{
    public bool MatchesObject(string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && (string.Equals(Id, objectId, StringComparison.Ordinal)
                || ObjectIds.Contains(objectId, StringComparer.Ordinal));
    }

    public IEnumerable<string> SelectableObjectIds()
    {
        if (!string.IsNullOrWhiteSpace(Id))
        {
            yield return Id;
        }

        foreach (var objectId in ObjectIds)
        {
            if (!string.IsNullOrWhiteSpace(objectId)
                && !string.Equals(objectId, Id, StringComparison.Ordinal))
            {
                yield return objectId;
            }
        }
    }
}

public sealed record PromptActionOption(
    string Name,
    string Label,
    string Reason,
    bool Enabled,
    bool HasTemplate,
    bool IsSpecial,
    string SubmitKind);
