using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadAkaliMovementTests
{
    private static readonly string[] Printings = ["RAD·015/167", "RAD·SP2/005"];

    [Theory]
    [MemberData(nameof(PrintingsData))]
    public async Task BothOfficialPrintingsUseTheExactCatalogTextAndBehavior(string no)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, card => card.CardNo == no);
        Assert.Equal("阿卡丽", card.CardName);
        Assert.Equal("狂傲不羁", card.SubTitle);
        Assert.Equal(5, card.Energy);
        Assert.Equal(5, card.Power);
        Assert.Contains("每回合首次移动", card.CardEffect);
        Assert.Contains("每回合第二次移动", card.CardEffect);
        var triggers = TriggerParser.Parse(card.CardEffect);
        Assert.Single(triggers, trigger => trigger.Kind == TriggerKinds.UnitOrdinalMovementGainResources
            && trigger.MovementOrdinal == 1);
        Assert.Single(triggers, trigger => trigger.Kind == TriggerKinds.UnitOrdinalMovementBanishTopPlay
            && trigger.MovementOrdinal == 2);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior));
        Assert.Equal("UNIT_ORDINAL_MOVEMENT_PLAY:" + (no == Printings[0] ? "RAD015" : "RADSP2"), behavior.EffectKind);
        Assert.Equal(5, behavior.ManaCost);
        Assert.Equal(5, behavior.SourceUnitPower);
    }

    [Theory]
    [MemberData(nameof(PrintingsData))]
    public async Task ARealPlayPaysThePrintedCostAndEntersDormantWithoutMovementTriggers(string no)
    {
        var game = new Driver(VenUnitEntryBasicTests.Position(no));
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Equal(15, game.State.RunePools["P1"].Mana);
        Assert.Equal(19, game.State.RunePools["P1"].TotalPower);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        Assert.Equal(0, game.State.CardObjects["UNIT"].MovementCountThisTurn);
        Assert.Empty(game.State.TriggerQueue); Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [MemberData(nameof(PrintingsData))]
    public async Task AnUnpaidPlayPreservesTheHandAndDoesNotGrantMovementResources(string no)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty } };
        var before = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("akali-unpaid", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("UNIT", no, [], Destination: "BASE"), default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Contains("UNIT", result.State.PlayerZones["P1"].Hand);
    }

    [Theory]
    [MemberData(nameof(PrintingsData))]
    public async Task FirstMoveAddsManaAndPowerWithoutOpeningAResponseWindow(string no)
    {
        var game = new Driver(Position(no));
        var mana = game.State.RunePools["P1"].Mana;
        var power = game.State.RunePools["P1"].Power;
        await Move(game, no);
        Assert.Empty(game.State.StackItems);
        Assert.Empty(game.State.TriggerQueue);
        Assert.Equal(mana + 1, game.State.RunePools["P1"].Mana);
        Assert.Equal(power + 1, game.State.RunePools["P1"].Power);
        Assert.Contains(game.Events(), e => e.Event.Kind == "MANA_GAINED" && Equals(e.Event.Payload["generatedMana"], 1));
        Assert.Contains(game.Events(), e => e.Event.Kind == "POWER_GAINED" && Equals(e.Event.Payload["generatedPower"], 1));
        await game.Replay();
    }

    [Theory]
    [MemberData(nameof(PrintingsData))]
    public async Task SecondMoveBanishesTheTopCardAndOffersOnlyThatCardUntilMyNextEndingPhase(string no)
    {
        var game = new Driver(Position(no));
        await Move(game, no);
        var beforeSecond = game.State;
        await Move(game, no);
        Assert.Contains(game.State.StackItems, item => item.MovementObserver?.Kind == TriggerKinds.UnitOrdinalMovementBanishTopPlay);
        Assert.Empty(game.State.TimedBanishPlayGrants ?? []);
        await game.Top();
        var grant = Assert.Single(game.State.TimedBanishPlayGrants!);
        Assert.Equal("P1", grant.ControllerId);
        Assert.Equal("TOP", grant.Card.ObjectId);
        Assert.Contains("TOP", game.State.PlayerZones["P1"].Banished);
        Assert.DoesNotContain("TOP", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("TOP", EffectSources(game.State, "P2"));
        Assert.Empty(game.State.StackItems);
        Assert.Contains("TOP", EffectSources(game.State, "P1"));
        Assert.Equal(beforeSecond.CardObjects["UNIT"].MovementCountThisTurn + 1,
            game.State.CardObjects["UNIT"].MovementCountThisTurn);
        await game.Replay();
    }

    [Theory]
    [MemberData(nameof(PrintingsData))]
    public async Task ThePermissionPaysThePrintedCostAndIsConsumedByTheSuccessfulPlay(string no)
    {
        var game = new Driver(Position(no));
        await Move(game, no); await Move(game, no); await game.Top();
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", new PlayCardCommand("TOP", "OGN·048/298", []));
        Assert.Empty(game.State.TimedBanishPlayGrants ?? []);
        Assert.Equal(mana - 2, game.State.RunePools["P1"].Mana);
        await game.Top();
        Assert.Contains("TOP", game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(game.State.StackItems);
        Assert.Empty(game.State.TimedBanishPlayGrants ?? []);
        Assert.Contains("P1-DRAW-1", game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Theory]
    [MemberData(nameof(PrintingsData))]
    public async Task ThirdMoveDoesNotBanishAnotherCardAndEndOfTurnExpiresThePermission(string no)
    {
        var game = new Driver(Position(no));
        await Move(game, no); await Move(game, no); await game.Top();
        var banished = game.State.PlayerZones["P1"].Banished.ToArray();
        await Move(game, no);
        Assert.Equal(banished, game.State.PlayerZones["P1"].Banished);
        await game.Act("P1", new EndTurnCommand());
        Assert.Empty(game.State.TimedBanishPlayGrants ?? []);
        Assert.DoesNotContain("TOP", EffectSources(game.State, "P1"));
        await game.Replay();
    }

    [Fact]
    public async Task ARejectedSecondMoveLeavesTheCountAndDeckUnchanged()
    {
        var game = new Driver(Position(Printings[0]));
        await Move(game, Printings[0]);
        var before = game.State;
        var rejected = await new CoreRuleEngine().ResolveAsync(before,
            new("akali-invalid-move", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand("UNIT", "BASE", "BASE"), default);
        Assert.False(rejected.Accepted);
        Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(before), MatchStateHasher.Hash(rejected.State));
        Assert.Equal(before.CardObjects["UNIT"].MovementCountThisTurn,
            rejected.State.CardObjects["UNIT"].MovementCountThisTurn);
        Assert.Equal(before.PlayerZones["P1"].MainDeck, rejected.State.PlayerZones["P1"].MainDeck);
    }

    [Fact]
    public async Task PermissionAndItsPublicBanishAreRestorableWithoutRevealingOpponentsHands()
    {
        var game = new Driver(Position(Printings[0]));
        await Move(game, Printings[0]); await Move(game, Printings[0]); await game.Top();
        Assert.NotNull(game.State.TimedBanishPlayGrants);
        var restored = OfficialInsightAndSpellLockTests.Restore(game.State);
        Assert.Equal(MatchStateHasher.Hash(game.State), MatchStateHasher.Hash(restored));
        Assert.Single(restored.TimedBanishPlayGrants!);
        Assert.Contains("TOP", EffectSources(restored, "P1"));
        Assert.DoesNotContain("TOP", EffectSources(restored, "P2"));
        Assert.DoesNotContain("P2-HIDDEN", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(restored)["P2"]));
    }

    public static IEnumerable<object[]> PrintingsData()
        => Printings.Select(no => new object[] { no });

    private static IReadOnlyList<string> EffectSources(MatchState state, string player)
        => typeof(CoreRuleEngine).GetMethod("EffectPlaySources", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static) is { } method
            ? (IReadOnlyList<string>)method.Invoke(null, [state, player])!
            : throw new InvalidOperationException("EffectPlaySources reflection unavailable");

    private static async Task Move(Driver game, string no)
    {
        var location = game.State.ObjectLocations["UNIT"];
        var toBattlefield = location.Zone == "BASE";
        if (!toBattlefield) {
            // A second ordinary move cannot pay the exhausted unit's tap cost.
            // Flash moves that same object without readying it or resetting its count.
            await game.Act("P1", new PlayCardCommand("FLASH", "OGS·011/024", ["UNIT"]));
            await game.Top();
        }
        else {
            if (game.State.CardObjects["UNIT"].IsExhausted) {
                await game.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["UNIT"]));
                await game.Top();
            }
            for (var step = 0; step < 8 && game.State.SpellDuelState.IsActive; step++)
                await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
            Assert.False(game.State.SpellDuelState.IsActive);
            await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF"));
        }
        Assert.Equal(no, game.State.CardObjects["UNIT"].CardNo);
    }

    private static MatchState Position(string no)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        // At relocates field objects only. Remove the original hand entry before
        // using it to prepare this already-public movement fixture.
        state = state with { PlayerZones = state.PlayerZones.ToDictionary(pair => pair.Key,
            pair => pair.Key == "P1" ? pair.Value with { Hand = [] } : pair.Value) };
        state = LocalDestructionRecallTests.At(state, "UNIT", "P1", "BASE");
        state = LocalDestructionRecallTests.At(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        state = AttackDamageDistributionTests.Spell(state, "FLASH", "OGS·011/024", "P1");
        state = AttackDamageDistributionTests.Spell(state, "READY", "UNL-009/219", "P1");
        var cards = state.CardObjects.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary();
        var p1 = state.PlayerZones["P1"];
        var top = OfficialGarenConquestBattlefieldCountTests.Printed("TOP", "OGN·048/298", "P1");
        var hidden = OfficialGarenConquestBattlefieldCountTests.Printed("P2-HIDDEN", "OGN·048/298", "P2");
        cards["TOP"] = top; cards["P2-HIDDEN"] = hidden;
        locations["TOP"] = new("P1", "MAIN_DECK"); locations["P2-HIDDEN"] = new("P2", "MAIN_DECK");
        return state with {
            PlayerZones = state.PlayerZones.ToDictionary(pair => pair.Key, pair => pair.Key == "P1"
                ? pair.Value with { MainDeck = ["TOP", ..pair.Value.MainDeck] }
                : pair.Value with { MainDeck = ["P2-HIDDEN", ..pair.Value.MainDeck] }),
            CardObjects = cards, ObjectLocations = locations
        };
    }
}
