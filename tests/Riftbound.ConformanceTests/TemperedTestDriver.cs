using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

internal static class TemperedTestDriver
{
    internal static void AssertPromptGearChoices(MatchState state, string source, string card, params string[] expected)
    {
        var played = new CoreRuleEngine().ResolveAsync(state, new("tempered-prompt-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(source, card, []), default).GetAwaiter().GetResult();
        Assert.True(played.Accepted, played.ErrorMessage);
        var choice = Assert.IsType<PendingCardChoiceState>(played.State.PendingCardChoice);
        Assert.Equal("TEMPERED_CONFIRMATION", choice.ChoiceWindow);
        Assert.Equal(expected.Order(StringComparer.Ordinal), choice.LegalObjectIds);
        Assert.DoesNotContain(source, choice.LegalObjectIds);
        Assert.True(played.Prompts["P1"].Actionable); Assert.False(played.Prompts["P2"].Actionable);
        Assert.DoesNotContain(CommandTypes.ChooseCards, played.Prompts["P2"].Actions);
    }
    // Migrate the old fixture's selection notation into two real commands. The
    // server receives PLAY_CARD without the obsolete TEMPERED_ATTACH cost, then
    // CHOOSE_CARDS for the independent trigger. Invalid old PLAY tokens remain
    // direct rejection probes, preserving their original no-mutation assertions.
    internal static async Task<ResolutionResult> PlayFixtureAsync(CoreRuleEngine engine, MatchState state,
        PlayerIntent intent, PlayCardCommand command)
    {
        var oldChoices = (command.OptionalCosts ?? []).Where(c => c.StartsWith("TEMPERED_ATTACH:", StringComparison.Ordinal)).ToArray();
        var gear = oldChoices.Length == 1 ? oldChoices[0]["TEMPERED_ATTACH:".Length..] : null;
        if (oldChoices.Length > 0 && (oldChoices.Length != 1 || string.IsNullOrWhiteSpace(gear)
            || !state.CardObjects.TryGetValue(gear, out var equipment) || equipment.IsFaceDown
            || equipment.ControllerId != intent.PlayerId || !equipment.Tags.Contains(CardObjectTags.EquipmentCard)
            || !equipment.Tags.Contains(CardEquipmentKeywordNames.Weapon)
            || !(state.PlayerZones[intent.PlayerId].Base.Contains(gear) || state.PlayerZones[intent.PlayerId].Battlefields.Contains(gear))))
            return await engine.ResolveAsync(state, intent, command, default);
        var play = command with { OptionalCosts = (command.OptionalCosts ?? []).Where(c => !oldChoices.Contains(c)).ToArray() };
        var result = await engine.ResolveAsync(state, intent, play, default);
        if (!result.Accepted || result.State.PendingCardChoice?.ChoiceWindow != "TEMPERED_CONFIRMATION") return result;
        return await ConfirmAsync(engine, result, gear);
    }

    internal static async Task<ResolutionResult> ConfirmAsync(CoreRuleEngine engine, ResolutionResult played, string? gear = null)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(played.State.PendingCardChoice);
        Assert.Equal("TEMPERED_CONFIRMATION", choice.ChoiceWindow);
        var result = await engine.ResolveAsync(played.State,
            new("tempered-confirm-" + played.State.Tick, choice.PlayerId, CommandTypes.ChooseCards),
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, gear is null ? [] : [gear]), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        return result with { Events = played.Events.Concat(result.Events).ToArray() };
    }

    internal static string Entry(ResolutionResult result, string source, string? expectedHash = null)
    {
        Assert.Equal(1, result.State.Tick);
        Assert.Contains(source, result.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(source, result.State.PlayerZones["P1"].Hand);
        Assert.Equal("BASE", result.State.ObjectLocations[source].Zone);
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Null(result.State.PriorityPlayerId);
        var choice = Assert.IsType<PendingCardChoiceState>(result.State.PendingCardChoice);
        Assert.Equal("TEMPERED_CONFIRMATION", choice.ChoiceWindow); Assert.Equal(source, choice.SourceObjectId);
        var item = Assert.Single(result.State.StackItems, i => i.Tempered is not null);
        Assert.Equal(source, item.SourceObjectId); Assert.False(item.SourceConfirmed);
        Assert.Empty(item.TargetObjectIds); Assert.Empty(item.OptionalCosts);
        var hash = MatchStateHasher.Hash(result.State);
        if (expectedHash is not null) Assert.Equal(expectedHash, hash);
        return hash;
    }
}
