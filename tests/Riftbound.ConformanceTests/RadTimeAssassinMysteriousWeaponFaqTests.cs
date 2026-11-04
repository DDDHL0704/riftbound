using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// DOC-17: when the Time Assassin destruction is replaced by Mysterious Weapon,
// the replacement is optional, requires red power, and expires with the turn.
public sealed class RadTimeAssassinMysteriousWeaponFaqTests
{
    private const string Weapon = "OGN·023/298";
    private const string Unit = "SFD·143/221";

    [Theory]
    [InlineData("accept", true)]
    [InlineData("decline", false)]
    [InlineData("wrong-color", false)]
    public async Task TheWeaponOnlyOffersItsRedReplacementAndTheGrantExpires(string scenario, bool expectRecall)
    {
        var initial = VenUnitEntryBasicTests.Position(Unit);
        initial = WithTimeAssassinLegend(initial);
        initial = HandDiscardObserverTests.Put(initial, "WEAPON", Weapon, "P1", "HAND");
        initial = HandDiscardObserverTests.Put(initial, "COST", "UNL-009/219", "P1", "HAND");
        initial = initial with
        {
            RunePools = new Dictionary<string, RunePool>(initial.RunePools)
            {
                ["P1"] = scenario == "wrong-color"
                    ? new(20, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 })
                    : new(20, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 })
            }
        };
        var game = new Driver(initial);

        await game.Act("P1", new PlayCardCommand("WEAPON", Weapon, []));
        await game.Act("P1", new LegendActCommand("LEGEND", LegendActionAbilityCatalog.TimeAssassinLegendAbilityId, [], []));
        await game.Top();
        await game.Act("P1", new PlayCardCommand("UNIT", Unit, []));
        Assert.Contains(game.State.CardObjects["UNIT"].UntilEndOfTurnEffects,
            effect => effect.StartsWith(CoreRuleEngine.TimeAssassinDestructionPrefix, StringComparison.Ordinal));

        await game.Act("P1", new ActivateAbilityCommand("WEAPON",
            P4ActivatedAbilityCatalog.MysteriousWeaponRecallAbilityId, ["UNIT"],
            OptionalCosts: ["DISCARD_HAND_CARD:COST"]));
        await game.Top();
        Assert.Contains(game.State.CardObjects["UNIT"].UntilEndOfTurnEffects,
            effect => effect.StartsWith(CoreRuleEngine.MysteriousWeaponRecallPrefix, StringComparison.Ordinal));

        await game.Act("P1", new EndTurnCommand());
        await game.Top();

        if (scenario == "wrong-color")
        {
            Assert.Null(game.State.PendingRuleChoice);
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        }
        else
        {
            var request = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
            var paid = Assert.Single(request.Options,
                option => option.Id.StartsWith("PAID_RECALL:UNIT:", StringComparison.Ordinal));
            var selected = expectRecall ? paid.Id : "DECLINE";
            await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [selected]));

            if (expectRecall)
            {
                Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
                Assert.DoesNotContain("UNIT", game.State.PlayerZones["P1"].Graveyard);
                Assert.Equal(0, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
                Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
            }
            else
                Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        }

        Assert.DoesNotContain(game.State.CardObjects["UNIT"].UntilEndOfTurnEffects,
            effect => effect.StartsWith(CoreRuleEngine.TimeAssassinDestructionPrefix, StringComparison.Ordinal));
        Assert.DoesNotContain(game.State.CardObjects["UNIT"].UntilEndOfTurnEffects,
            effect => effect.StartsWith(CoreRuleEngine.MysteriousWeaponRecallPrefix, StringComparison.Ordinal));

        if (expectRecall)
        {
            await game.Act("P2", new EndTurnCommand());
            await game.Act("P1", new EndTurnCommand());
            Assert.Empty(game.State.StackItems);
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
            Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "UNIT_DESTROYED"
                && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), "UNIT"));
        }

        await game.ColdCheckpoint();
    }

    private static MatchState WithTimeAssassinLegend(MatchState state)
        => state with
        {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "RAD·139/167", "P1")
                    with { IsEmpowered = true }
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations)
            {
                ["LEGEND"] = new("P1", "LEGEND")
            },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
            {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] }
            }
        };
}
