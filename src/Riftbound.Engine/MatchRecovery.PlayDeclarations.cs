namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidatePlayAttributeDeclarationHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        if (!events.Any(entry => entry.Event.Kind == "ATTRIBUTE_DECLARED")
            && !state.CardObjects.Values.Any(card => card.LinkedAttributes is not null)
            && !state.StackItems.Any(item => item.SimpleActivation?.LinkedAttribute is not null)) return;
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        foreach (var frame in new[] { state, state.PendingRuleChoice?.Origin }.OfType<MatchState>())
        {
            var births = new Dictionary<ObjectBinding, (long Created, long Revision, string Hash)>();
            foreach (var entry in events.Where(entry => entry.Sequence > boundary && entry.Tick <= frame.Tick
                && entry.Event.Kind == "ATTRIBUTE_DECLARED"))
            {
                if (!TryReadObjectString(entry.Event.Payload, "sourceObjectId", out var source) || source is null
                    || !TryReadObjectLong(entry.Event.Payload, "sourceGeneration", out var generation)
                    || !TryReadObjectLong(entry.Event.Payload, "faceRevision", out var revision)
                    || !TryReadObjectLong(entry.Event.Payload, "createdTick", out var created)
                    || !TryReadObjectString(entry.Event.Payload, "declarationHash", out var hash) || hash is null
                    || commands.Count(command => command.Accepted && command.CompletedTick == entry.Tick
                        && command.StartedEventSequence < entry.Sequence && command.CompletedEventSequence >= entry.Sequence
                        && created > command.StartedTick && created <= command.CompletedTick) != 1
                    || !births.TryAdd(new(source, generation), (created, revision, hash)))
                    errors.Add("play attribute declaration lacks its unique original accepted play choice");
            }
            bool Matches(string source, LinkedAttributeDeclaration declaration)
                => births.TryGetValue(new(source, declaration.Ability.SourceGeneration), out var birth)
                    && birth.Created == declaration.CreatedTick && birth.Revision == declaration.Ability.FaceRevision
                    && birth.Hash == CoreRuleEngine.LinkedAttributeDeclarationHash(source, declaration);
            foreach (var card in frame.CardObjects.Values)
            {
                foreach (var declaration in card.LinkedAttributes ?? [])
                    if (!Matches(card.ObjectId, declaration))
                        errors.Add("play attribute declaration differs from its original accepted play choice");
                // A different incarnation or a different copied text has no
                // obligation to retain this printed text's old declaration.
                if (births.TryGetValue(new(card.ObjectId, card.ObjectGeneration), out var original)
                    && (card.CopyFaceTransitions?.Count ?? 0) == original.Revision
                    && frame.PlayerZones.Values.Any(zones => zones.Base.Contains(card.ObjectId)
                        || zones.Battlefields.Contains(card.ObjectId))
                    && card.LinkedAttributes?.Any(declaration => Matches(card.ObjectId, declaration)) != true)
                    errors.Add("current printed play declaration was erased from original accepted history");
            }
            foreach (var item in frame.StackItems)
                if (item.SimpleActivation?.LinkedAttribute is { } declaration && !Matches(item.SourceObjectId, declaration))
                    errors.Add("captured activation changed its original accepted play declaration");
        }
    }

    private static void ValidateOptionalExperiencePlayHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        foreach (var frame in new[] { state, state.PendingRuleChoice?.Origin }.OfType<MatchState>())
            foreach (var item in frame.StackItems.Where(item => StackObjectRules.IsSpell(item)
                && CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var behavior)
                && CoreRuleEngine.HasOptionalExperiencePayment(behavior)))
            {
                var payments = events.Where(entry => entry.Tick <= frame.Tick && entry.Event.Kind == "COST_PAID"
                    && TryReadObjectString(entry.Event.Payload, "playId", out var id) && id == item.StackItemId
                    && TryReadObjectString(entry.Event.Payload, "sourceObjectId", out var source) && source == item.SourceObjectId).ToArray();
                if (payments.Length != 1
                    || !TryReadObjectStringList(payments[0].Event.Payload, "optionalCosts", out var costs)
                    || !costs.SequenceEqual(item.OptionalCosts)
                    || commands.Count(command => command.Accepted && command.CompletedTick == payments[0].Tick
                        && command.StartedEventSequence < payments[0].Sequence
                        && command.CompletedEventSequence >= payments[0].Sequence) != 1)
                    errors.Add("optional experience play choices differ from the original accepted payment");
            }
    }
}
