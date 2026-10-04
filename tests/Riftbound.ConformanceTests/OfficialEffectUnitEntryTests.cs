using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// CN 124.1 / 143.4 / 143.4.a. These tests cover entry characteristics;
// nested play choices, rune costs and the entire play-trigger sequence remain separate gates.
public sealed class OfficialEffectUnitEntryTests
{
    [Theory]
    [InlineData("SFD·125/221", 4, true, false)]
    [InlineData("SFD·125/221", 4, true, true)]
    [InlineData("SFD·006/221", 3, false, false)]
    [InlineData("SFD·006/221", 3, false, true)]
    public async Task RescueStartsANewPrintedObjectUnderItsOwner(string cardNo, int power, bool exhausted, bool stolen)
    {
        var owner = stolen ? "P2" : "P1";
        var state = Position("OGN·102/298", cardNo, "FIELD");
        var cards = state.CardObjects.ToDictionary(x => x.Key, x => x.Value);
        cards["UNIT"] = cards["UNIT"] with { OwnerId = owner, Power = power + 5, Damage = 2,
            IsExhausted = true, UntilEndOfTurnPowerModifier = 5, UntilEndOfTurnEffects = ["STUNNED"],
            Tags = [CardObjectTags.UnitCard, "TEMPORARY_TEST_TAG"], ObjectGeneration = 7 };
        cards["EQUIPMENT"] = new("EQUIPMENT", tags: [CardObjectTags.EquipmentCard],
            cardNo: "OGN·028/298", ownerId: "P1", controllerId: "P1", attachedToObjectId: "UNIT");
        var zones = state.PlayerZones.ToDictionary(x => x.Key, x => x.Value);
        zones["P1"] = zones["P1"] with { Base = ["UNIT", "EQUIPMENT"] };
        state = state with { CardObjects = cards, PlayerZones = zones };
        var result = await PlayAndResolve(state);
        var unit = result.State.CardObjects["UNIT"];
        Assert.Contains("UNIT", result.State.PlayerZones[owner].Base);
        if (stolen) Assert.DoesNotContain("UNIT", result.State.PlayerZones["P1"].Base);
        Assert.Equal(owner, unit.ControllerId);
        Assert.Equal(owner, unit.OwnerId);
        Assert.Equal(power, unit.Power);
        Assert.Equal(0, unit.Damage);
        Assert.Equal(exhausted, unit.IsExhausted);
        Assert.Empty(unit.UntilEndOfTurnEffects);
        Assert.Equal(0, unit.UntilEndOfTurnPowerModifier);
        Assert.DoesNotContain("TEMPORARY_TEST_TAG", unit.Tags);
        Assert.True(unit.ObjectGeneration >= 9);
        Assert.True(string.IsNullOrWhiteSpace(result.State.CardObjects["EQUIPMENT"].AttachedToObjectId));
        var restored = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(result.State))!;
        Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(restored));
    }

    [Theory]
    [InlineData("SFD·125/221", true)]
    [InlineData("SFD·006/221", false)]
    public async Task GraveyardPlayUsesOrdinaryEntryAndExplicitReadyException(string cardNo, bool exhausted)
    {
        var result = await PlayAndResolve(Position("OGN·198/298", cardNo, "GRAVEYARD"));
        var unit = result.State.CardObjects["UNIT"];
        Assert.Contains("UNIT", result.State.PlayerZones["P1"].Base);
        Assert.Equal(exhausted, unit.IsExhausted);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(cardNo, out var behavior));
        foreach (var tag in behavior.SourceUnitTags.Split('|', StringSplitOptions.RemoveEmptyEntries))
            Assert.Contains(tag, unit.Tags);
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public async Task EffectEntryChecksCurrentFriendlyReadyAura(int experience, bool exhausted)
    {
        var state = Position("OGN·198/298", "OGN·010/298", "GRAVEYARD");
        var zones = state.PlayerZones.ToDictionary(x => x.Key, x => x.Value);
        zones["P1"] = zones["P1"] with { LegendZone = ["LEGEND"] };
        var cards = state.CardObjects.ToDictionary(x => x.Key, x => x.Value);
        cards["LEGEND"] = new("LEGEND", cardNo: "UNL-191/219", ownerId: "P1", controllerId: "P1");
        state = state with { PlayerZones = zones, CardObjects = cards,
            PlayerExperience = new Dictionary<string, int> { ["P1"] = experience, ["P2"] = 0 } };
        var result = await PlayAndResolve(state);
        Assert.Equal(exhausted, result.State.CardObjects["UNIT"].IsExhausted);
    }

    private static MatchState Position(string spell, string unit, string zone)
        => new("EFFECT-ENTRY", 1, 3, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(20, 20), ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones>
            {
                ["P1"] = PlayerZones.Empty with { Hand = ["SPELL"],
                    Graveyard = zone == "GRAVEYARD" ? ["UNIT"] : [], Base = zone == "FIELD" ? ["UNIT"] : [] },
                ["P2"] = PlayerZones.Empty
            }, cardObjects: new Dictionary<string, CardObjectState>
            {
                ["SPELL"] = new("SPELL", cardNo: spell, tags: [CardObjectTags.SpellCard], ownerId: "P1", controllerId: "P1"),
                ["UNIT"] = new("UNIT", cardNo: unit, power: 4, tags: [CardObjectTags.UnitCard], ownerId: "P1", controllerId: "P1")
            });

    private static async Task<ResolutionResult> PlayAndResolve(MatchState state)
    {
        var engine = new CoreRuleEngine();
        var result = await engine.ResolveAsync(state, new("play-effect", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("SPELL", state.CardObjects["SPELL"].CardNo!, ["UNIT"]), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        for (var i = 0; result.State.StackItems.Count > 0 && i < 8; i++)
        {
            result = await engine.ResolveAsync(result.State,
                new($"pass-{i}", result.State.PriorityPlayerId!, CommandTypes.PassPriority), new PassPriorityCommand(), default);
            Assert.True(result.Accepted, result.ErrorMessage);
        }
        Assert.Empty(result.State.StackItems);
        return result;
    }
}
