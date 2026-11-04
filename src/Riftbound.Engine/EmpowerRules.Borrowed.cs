using System.Security.Cryptography;
using System.Text;

namespace Riftbound.Engine;

internal static partial class EmpowerRules
{
    private static IEnumerable<EmpowerAbility> BorrowedAbilities(MatchState state, CardObjectState source)
    {
        // CN 827.1.c: the keyword is a complete activated skill. Only its
        // exhaust-cost alternatives qualify for Heimerdinger's permission.
        if (source.IsEmpowered && MaximumCount(state, source) <= 1 || source.ControllerId is not { } player) yield break;
        foreach (var (borrower, donor) in BorrowedExhaustRules.Providers(state, player, source))
        foreach (var text in AttachedRuleTextRules.Instances(state, donor))
        foreach (var spec in Specs(text.CardNo))
        for (var index = 0; index < spec.Alternatives.Count; index++)
        {
            var cost = spec.Alternatives[index];
            if (!cost.Exhaust || !cost.SupportedResources) continue;
            var identity = $"{borrower.Id}|{borrower.CardNo}|{text.Id}|{text.CardNo}|{spec.Index}|{index}";
            var id = $"EMPOWER:{spec.Index}:{index}:BORROWED:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
            yield return new(id, text, spec, cost, index, Borrowed: new(
                BorrowedExhaustRules.Capture(source, borrower), BorrowedExhaustRules.Capture(donor, text)));
        }
    }

    private static bool ValidSource(MatchState state, StackItemState item, EmpowerActivationContext context)
    {
        if (context.Borrowed is not { } borrowed)
            return SelfAbilitySourceRules.Valid(state, context.Source, context.CardNo, context.FaceRevision, context.AcquiredTick,
                context.SourceTokenFactoryCardNo, context.CeasedSource, context.Origin is null,
                context.DestructionCost is { } destruction && destruction.Unit == context.Source
                    && destruction.TokenFactoryCardNo == context.SourceTokenFactoryCardNo
                    && destruction.TokenCeasedAtTick == context.AcquiredTick)
                && AttachedRuleTextRules.ValidCaptured(state, context.Origin, item.SourceObjectId, context.Source.Generation, context.CardNo);
        return borrowed.Borrower is { Object: not null } borrower && borrowed.Donor is { Object: not null } donor
            && borrower.Object == context.Source && borrower.Object.ObjectId != donor.Object.ObjectId
            && BorrowedExhaustRules.Inherits(borrower.CardNo)
            && donor.CardNo == context.CardNo && donor.Origin == context.Origin
            && borrower.FaceRevision == context.FaceRevision && borrower.TokenFactoryCardNo == context.SourceTokenFactoryCardNo
            && context.CeasedSource is null && Specs(context.CardNo)[context.Keyword].Alternatives[context.Alternative] is { Exhaust: true, SupportedResources: true }
            && BorrowedExhaustRules.ValidSource(state, borrower, context.AcquiredTick)
            && BorrowedExhaustRules.ValidSource(state, donor, context.AcquiredTick);
    }
}
