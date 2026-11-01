using System.Text.Json.Serialization;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CeasedSelfAbilitySource(ObjectBinding Source, string CardNo, long FaceRevision,
    long AcquiredTick, string TokenFactoryCardNo, long DepartedTick);

internal static class SelfAbilitySourceRules
{
    internal static bool Valid(MatchState state, ObjectBinding source, string cardNo, long revision,
        long acquiredTick, string? tokenFactory, CeasedSelfAbilitySource? ceased, bool usesPrintedFace)
    {
        if (string.IsNullOrWhiteSpace(source.ObjectId) || source.Generation < 0 || revision < 0
            || acquiredTick <= 0 || acquiredTick > state.Tick) return false;
        if (state.CardObjects.TryGetValue(source.ObjectId, out var current))
            return ceased is null && current.ObjectGeneration >= source.Generation
                && current.TokenFactoryCardNo == tokenFactory
                && (!usesPrintedFace || current.ObjectGeneration > source.Generation
                    || CopyFaceTimeline.FaceAt(current, revision, acquiredTick) == cardNo);
        // CN 186.1 and 355.9.c: the token ceases, while its already captured
        // skill still resolves. Only a receipt captured before departure can
        // authorize the missing source during recovery.
        return P6TokenFactoryCatalog.IsUnitTokenFactory(tokenFactory) && ceased is not null
            && ceased.Source == source && ceased.CardNo == cardNo && ceased.FaceRevision == revision
            && ceased.AcquiredTick == acquiredTick && ceased.TokenFactoryCardNo == tokenFactory
            && ceased.DepartedTick > acquiredTick && ceased.DepartedTick <= state.Tick;
    }

    internal static CeasedSelfAbilitySource? Capture(MatchState before, long departedTick,
        ObjectBinding source, string cardNo, long revision, long acquiredTick, string? tokenFactory)
        => P6TokenFactoryCatalog.IsUnitTokenFactory(tokenFactory)
            && before.CardObjects.TryGetValue(source.ObjectId, out var previous)
            && previous.ObjectGeneration == source.Generation && previous.TokenFactoryCardNo == tokenFactory
            ? new(source, cardNo, revision, acquiredTick, tokenFactory!, departedTick) : null;
}

public sealed partial class CoreRuleEngine
{
    private static MatchState RecordCeasedSelfAbilitySources(MatchState before, MatchState state, IReadOnlySet<string> departed)
    {
        var stack = state.StackItems.Select(item => {
            if (item.SelfMightActivation is { CeasedSource: null } self
                && departed.Contains(self.Source.ObjectId) && SelfMightRules.Valid(before, item))
                return item with { SelfMightActivation = self with { CeasedSource = SelfAbilitySourceRules.Capture(
                    before, state.Tick, self.Source, self.CardNo, self.FaceRevision, self.AcquiredTick, self.SourceTokenFactoryCardNo) } };
            if (item.RecycleMightActivation is { CeasedSource: null } recycle
                && departed.Contains(recycle.Source.ObjectId) && RecycleMightRules.Valid(before, item))
                return item with { RecycleMightActivation = recycle with { CeasedSource = SelfAbilitySourceRules.Capture(
                    before, state.Tick, recycle.Source, recycle.CardNo, recycle.FaceRevision, recycle.AcquiredTick, recycle.SourceTokenFactoryCardNo) } };
            return item;
        }).ToArray();
        return state with { StackItems = stack };
    }
}
