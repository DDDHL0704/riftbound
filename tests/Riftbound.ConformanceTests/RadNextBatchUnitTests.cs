using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadNextBatchUnitTests
{
    private const string FlameRaider = "RAD·019/167";
    private const string BombMonkey = "RAD·094/167";
    private const string Battlefield = "BF";

    [Theory]
    [InlineData("RAD·003/167", "比港追捕者", 39493, 3, 3)]
    [InlineData("RAD·008/167", "战争斥候", 39498, 3, 2)]
    [InlineData("RAD·058/167", "怒海法师", 39554, 4, 3)]
    [InlineData("RAD·118/167", "帝皇令使", 39623, 5, 3)]
    [InlineData("RAD·040/167", "鼓舞之魂", 39533, 7, 7)]
    [InlineData(FlameRaider, "焰烙劫掠者", 39509, 6, 6)]
    [InlineData(BombMonkey, "爆破猿", 39596, 6, 6)]
    public async Task OfficialPrintingsAndSharedDefinitionsRemainBound(
        string cardNo, string name, long id, int mana, int power)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards,
            candidate => candidate.CardNo == cardNo);
        Assert.Equal(id, card.Id);
        Assert.Equal(name, card.CardName);
        Assert.Equal(mana, card.Energy);
        Assert.Equal(power, card.Power);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(cardNo, out var behavior));
        Assert.Equal(mana, behavior.ManaCost);
        Assert.Equal(power, behavior.SourceUnitPower);
        Assert.True(behavior.PlaysSourceToBaseAsUnit);
        if (cardNo == "RAD·008/167" || cardNo == "RAD·058/167")
        {
            Assert.Contains(CardPermissionKeywordNames.Haste, behavior.SourceUnitTags);
            Assert.Equal(1, behavior.HasteReadyManaCost);
            Assert.Equal(1, behavior.HasteReadyPowerCost);
            Assert.Equal(cardNo == "RAD·008/167" ? RuneTrait.Red : RuneTrait.Blue,
                behavior.HasteReadyPowerTrait);
        }
        else if (cardNo == FlameRaider)
        {
            Assert.False(behavior.SourceUnitIsExhausted);
            Assert.True(behavior.ReadiesAllEnemyUnitsOnPlay);
            Assert.True(behavior.OtherUnitsEnterReady);
            Assert.True(behavior.SuppressesAllUnitWake);
        }
        else if (cardNo == "RAD·003/167")
        {
            Assert.Equal(1, behavior.DamageAmount);
            Assert.Equal(2, behavior.RequiredTargetCount);
            Assert.Equal(0, behavior.MinTargetCount);
            Assert.Equal(CardTargetScopes.AnyUnit, behavior.TargetScope);
        }
        else if (cardNo == "RAD·118/167")
        {
            Assert.Equal(CardTargetScopes.FriendlyUnit, behavior.TargetScope);
            Assert.Equal(5, behavior.BasePowerUntilEndOfTurn);
        }
        else if (cardNo == "RAD·040/167")
        {
            Assert.Equal(CardTargetScopes.FriendlyLegend, behavior.TargetScope);
            Assert.True(behavior.ReadiesTarget);
            Assert.Contains("灵体", behavior.SourceUnitTags);
        }
        else
        {
            Assert.Equal(3, behavior.CreatedBattlefieldEquipmentTokenCount);
            Assert.Equal("炸弹", behavior.CreatedBattlefieldEquipmentTokenName);
            Assert.Equal(CardEquipmentKeywordNames.Deploy, behavior.CreatedBattlefieldEquipmentTokenTags);
            Assert.True(behavior.SourceUnitIsExhausted);
        }
    }

    [Fact]
    public async Task FlameRaiderEntersReadyReadiesVisibleEnemyUnitsAndLeavesOtherObjectsAlone()
    {
        var initial = FlamePosition();
        var cards = initial.CardObjects.ToDictionary();
        var zones = initial.PlayerZones.ToDictionary();
        var locations = initial.ObjectLocations.ToDictionary();
        cards["ENEMY"] = cards["ENEMY"] with { IsExhausted = true };
        cards["OTHER"] = cards["OTHER"] with { IsExhausted = true };
        cards["GEAR"] = cards["GEAR"] with { OwnerId = "P2", ControllerId = "P2", IsExhausted = true };
        zones["P1"] = zones["P1"] with { Base = zones["P1"].Base.Where(id => id != "GEAR").ToArray() };
        zones["P2"] = zones["P2"] with { Base = ["ENEMY", "GEAR"], Battlefields = ["OTHER"] };
        locations["GEAR"] = new("P2", "BASE");
        cards["SECRET"] = OfficialGarenConquestBattlefieldCountTests.Printed("SECRET", "OGN·219/298", "P2")
            with { IsExhausted = true, IsFaceDown = true, Tags = [CardObjectTags.UnitCard, CardObjectTags.Standby] };
        zones["P2"] = zones["P2"] with { Battlefields = [.. zones["P2"].Battlefields, "SECRET"] };
        locations["SECRET"] = new("P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(initial with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations });

        await game.Act("P1", new PlayCardCommand("UNIT", FlameRaider, [], Destination: "BASE"));
        await game.Top();

        Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.False(game.State.CardObjects["ENEMY"].IsExhausted);
        Assert.True(game.State.CardObjects["OTHER"].IsExhausted);
        Assert.True(game.State.CardObjects["GEAR"].IsExhausted);
        Assert.True(game.State.CardObjects["SECRET"].IsExhausted);
        Assert.Contains(game.Events(), entry => entry.Event.Kind == "UNIT_READIED"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), "ENEMY"));
        Assert.DoesNotContain(game.Events(), entry => Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), "SECRET"));
        await game.Replay();
    }

    [Fact]
    public async Task FlameRaiderMakesAUnitPlayedLaterEnterReady()
    {
        var initial = FlamePosition();
        var cards = initial.CardObjects.ToDictionary();
        var zones = initial.PlayerZones.ToDictionary();
        var locations = initial.ObjectLocations.ToDictionary();
        cards["NEW"] = OfficialGarenConquestBattlefieldCountTests.Printed("NEW", "VEN·002", "P1");
        zones["P1"] = zones["P1"] with { Hand = ["UNIT", "NEW"] };
        locations["NEW"] = new("P1", "HAND");
        var game = new Driver(initial with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations });

        await game.Act("P1", new PlayCardCommand("UNIT", FlameRaider, [], Destination: "BASE"));
        await game.Top();
        await game.Act("P1", new PlayCardCommand("NEW", "VEN·002", [], Destination: "BASE"));

        Assert.False(game.State.CardObjects["NEW"].IsExhausted);
        Assert.Contains(game.Events(), entry => entry.Event.Kind == "UNIT_PLAYED_TO_BASE"
            && Equals(entry.Event.Payload.GetValueOrDefault("unitObjectId"), "NEW"));
        await game.Replay();
    }

    [Fact]
    public async Task FlameRaiderSuppressesTurnStartUnitWakeAcrossControllers()
    {
        var initial = FlamePosition();
        var cards = initial.CardObjects.ToDictionary();
        var zones = initial.PlayerZones.ToDictionary();
        var locations = initial.ObjectLocations.ToDictionary();
        zones["P1"] = zones["P1"] with { Hand = [], Base = ["UNIT", "ALLY", "GEAR"] };
        locations["UNIT"] = new("P1", "BASE");
        cards["UNIT"] = cards["UNIT"] with { IsExhausted = false };
        cards["ALLY"] = cards["ALLY"] with { IsExhausted = true };
        var state = initial with
        {
            CardObjects = cards,
            PlayerZones = zones,
            ObjectLocations = locations,
            Phase = MatchPhases.TurnStart,
            TurnStartStep = "READY",
            TimingState = TimingStates.NeutralClosed,
            ActivePlayerId = "P1",
            TurnPlayerId = "P1"
        };
        var game = new Driver(state);

        await game.Act("P1", new PassPriorityCommand());

        Assert.Equal(MatchPhases.Main, game.State.Phase);
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "OBJECTS_READIED"
            && ((IEnumerable<string>)entry.Event.Payload["objectIds"]!).Contains("ALLY"));
        await game.Replay();
    }

    [Fact]
    public async Task FlameRaiderRejectsAPlayWithoutEnoughManaWithoutChangingState()
    {
        var initial = FlamePosition() with
        {
            RunePools = new Dictionary<string, RunePool>(FlamePosition().RunePools)
            {
                ["P1"] = new(5, 0)
            }
        };
        var before = MatchStateHasher.Hash(initial);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(initial));
        var result = await new CoreRuleEngine().ResolveAsync(initial,
            new("illegal-flame-cost", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("UNIT", FlameRaider, [], Destination: "BASE"), default);

        Assert.False(result.Accepted);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
    }

    [Fact]
    public async Task HarborBountyHunterDamagesUpToTwoUnitsAndAllowsTheZeroTargetBoundary()
    {
        var game = new Driver(VenUnitEntryBasicTests.Position("RAD·003/167"));
        await game.Act("P1", new PlayCardCommand("UNIT", "RAD·003/167", ["ALLY", "ENEMY"], Destination: "BASE"));
        await game.Top();
        Assert.Equal(1, game.State.CardObjects["ALLY"].Damage);
        Assert.Equal(1, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();

        var zero = new Driver(VenUnitEntryBasicTests.Position("RAD·003/167"));
        await zero.Act("P1", new PlayCardCommand("UNIT", "RAD·003/167", [], Destination: "BASE"));
        if (zero.State.PriorityPlayerId is not null) await zero.Top();
        Assert.Equal(0, zero.State.CardObjects["ALLY"].Damage);
        Assert.Equal(0, zero.State.CardObjects["ENEMY"].Damage);
        await zero.Replay();

        var tooMany = VenUnitEntryBasicTests.Position("RAD·003/167");
        var before = MatchStateHasher.Hash(tooMany);
        var result = await new CoreRuleEngine().ResolveAsync(tooMany,
            new("illegal-bounty-target-count", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("UNIT", "RAD·003/167", ["ALLY", "ENEMY", "BF"], Destination: "BASE"), default);
        Assert.False(result.Accepted);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
    }

    [Theory]
    [InlineData("RAD·008/167", "RED", 2)]
    [InlineData("RAD·058/167", "BLUE", -3)]
    public async Task HasteScoutsPayTheirTypedOptionalReadyCostAndApplyTheirPrintedPowerChange(
        string cardNo, string trait, int modifier)
    {
        var initial = VenUnitEntryBasicTests.Position(cardNo) with
        {
            RunePools = new Dictionary<string, RunePool>(VenUnitEntryBasicTests.Position(cardNo).RunePools)
            {
                ["P1"] = new(10, 1, new Dictionary<string, int> { [trait] = 1 })
            }
        };
        var target = cardNo == "RAD·008/167" ? "ALLY" : "ENEMY";
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", cardNo, [target],
            OptionalCosts: [HasteOptionalCostNames.HasteReady], Destination: "BASE"));
        await game.Top();

        Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(initial.CardObjects[target].Power + modifier, game.State.CardObjects[target].Power);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(initial.CardObjects[target].Power, game.State.CardObjects[target].Power);
        await game.Replay();

        var invalid = VenUnitEntryBasicTests.Position(cardNo);
        var before = MatchStateHasher.Hash(invalid);
        var invalidTarget = cardNo == "RAD·008/167" ? "GEAR" : "ALLY";
        var rejected = await new CoreRuleEngine().ResolveAsync(invalid,
            new("illegal-haste-target", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("UNIT", cardNo, [invalidTarget], Destination: "BASE"), default);
        Assert.False(rejected.Accepted);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        Assert.Empty(rejected.Events);
    }

    [Fact]
    public async Task ImperialEmissarySetsOnlyAFriendlyUnitBasePowerUntilEndOfTurn()
    {
        var initial = VenUnitEntryBasicTests.Position("RAD·118/167");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "RAD·118/167", ["ALLY"], Destination: "BASE"));
        await game.Top();
        var view = CurrentPowerTargetTests.Snapshot(game.State);
        Assert.Equal(5, view.GetProperty("players").GetProperty("P1").GetProperty("objects")
            .GetProperty("ALLY").GetProperty("effectivePower").GetInt32());
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(initial.CardObjects["ALLY"].Power, game.State.CardObjects["ALLY"].Power);
        await game.Replay();

        var invalid = VenUnitEntryBasicTests.Position("RAD·118/167");
        var before = MatchStateHasher.Hash(invalid);
        var rejected = await new CoreRuleEngine().ResolveAsync(invalid,
            new("illegal-emissary-target", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("UNIT", "RAD·118/167", ["ENEMY"], Destination: "BASE"), default);
        Assert.False(rejected.Accepted);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        Assert.Empty(rejected.Events);
    }

    [Fact]
    public async Task BombMonkeyEntersExhaustedAndRejectsAnUnknownBattlefieldDestination()
    {
        var initial = BombPosition();
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", BombMonkey, [], Destination: "BASE"));
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Empty(Bombs(game.State));
        await game.Replay();

        var invalid = BombPosition();
        var before = MatchStateHasher.Hash(invalid);
        var result = await new CoreRuleEngine().ResolveAsync(invalid,
            new("illegal-bomb-destination", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("UNIT", BombMonkey, [], Destination: "BATTLEFIELD:MISSING"), default);
        Assert.False(result.Accepted);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task BombMonkeyDefendingAFieldCreatesExactlyThreeActiveBombsThere()
    {
        var initial = BombPosition();
        var cards = initial.CardObjects.ToDictionary();
        var zones = initial.PlayerZones.ToDictionary();
        var locations = initial.ObjectLocations.ToDictionary();
        cards["UNIT"] = cards["UNIT"] with { IsExhausted = true };
        zones["P1"] = zones["P1"] with { Hand = [], Base = ["ALLY", "GEAR"], Battlefields = [Battlefield, "UNIT"] };
        locations["UNIT"] = new("P1", "BATTLEFIELD", Battlefield);
        var state = initial with
        {
            CardObjects = cards,
            PlayerZones = zones,
            ObjectLocations = locations,
            TurnPlayerId = "P2",
            ActivePlayerId = "P2"
        };
        var game = new Driver(state);

        await game.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:" + Battlefield, []));
        for (var i = 0; game.State.FocusPlayerId is not null; i++)
        {
            Assert.InRange(i, 0, 3);
            await game.Act(game.State.FocusPlayerId, new PassFocusCommand());
        }
        Assert.Equal("STANDBY_DEFEND_BOMBS", Assert.Single(game.State.StackItems).FieldContext!.Kind);
        await game.Top();

        var bombs = Bombs(game.State);
        Assert.Equal(3, bombs.Length);
        Assert.All(bombs, id =>
        {
            var bomb = game.State.CardObjects[id];
            Assert.Equal(P6TokenFactoryCatalog.BombTokenCardNo, bomb.TokenFactoryCardNo);
            Assert.False(bomb.IsExhausted);
            Assert.Contains(CardEquipmentKeywordNames.Deploy, bomb.Tags);
            Assert.Equal(Battlefield, game.State.ObjectLocations[id].BattlefieldObjectId);
        });
        Assert.Equal(3, game.Events().Count(entry => entry.Event.Kind == "EQUIPMENT_TOKEN_CREATED"
            && Equals(entry.Event.Payload.GetValueOrDefault("tokenCardNo"), P6TokenFactoryCatalog.BombTokenCardNo)));
        await game.Replay();
    }

    [Fact]
    public async Task SoulOfInspirationReadiesAFriendlyLegendAndGrantsItsSharedPowerSkill()
    {
        var initial = SoulOfInspirationPosition();
        var game = new Driver(initial);

        await game.Act("P1", new PlayCardCommand("UNIT", "RAD·040/167", ["LEGEND"], Destination: "BASE"));
        await game.Top();

        Assert.False(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        var ability = Assert.Single(P4ActivatedAbilityCatalog.GetAll(), candidate =>
            candidate.SourceCardNo == "RAD·040/167"
            && candidate.Kind == ActivatedAbilityKinds.UnitPowerModifierUntilEndOfTurn);
        var prompt = game.PromptFor("P1");
        Assert.Contains(prompt.Candidates ?? [], candidate => candidate.Action == CommandTypes.ActivateAbility
            && candidate.Sources?.Any(source => source.Id == "LEGEND") == true);

        await game.Act("P1", new ActivateAbilityCommand("LEGEND", ability.AbilityId, ["ALLY"]));
        await game.Top();

        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(initial.CardObjects["ALLY"].Power + 3, game.State.CardObjects["ALLY"].Power);
        await game.Replay();
    }

    [Fact]
    public async Task SoulOfInspirationRejectsAnEnemyLegendAndLosesTheGrantedSkillWhenItLeaves()
    {
        var initial = SoulOfInspirationPosition();
        var before = MatchStateHasher.Hash(initial);
        var rejected = await new CoreRuleEngine().ResolveAsync(initial,
            new("illegal-soul-target", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("UNIT", "RAD·040/167", ["ENEMY-LEGEND"], Destination: "BASE"), default);
        Assert.False(rejected.Accepted);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        Assert.Empty(rejected.Events);

        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "RAD·040/167", ["LEGEND"], Destination: "BASE"));
        await game.Top();
        var ability = Assert.Single(P4ActivatedAbilityCatalog.GetAll(), candidate =>
            candidate.SourceCardNo == "RAD·040/167"
            && candidate.Kind == ActivatedAbilityKinds.UnitPowerModifierUntilEndOfTurn);
        var cards = game.State.CardObjects.ToDictionary();
        var zones = game.State.PlayerZones.ToDictionary();
        var locations = game.State.ObjectLocations.ToDictionary();
        zones["P1"] = zones["P1"] with { Base = zones["P1"].Base.Where(id => id != "UNIT").ToArray(), Hand = ["UNIT"] };
        locations["UNIT"] = new("P1", "HAND");
        var removed = game.State with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
        var noSkill = await new CoreRuleEngine().ResolveAsync(removed,
            new("skill-without-source", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("LEGEND", ability.AbilityId, ["ALLY"]), default);
        Assert.False(noSkill.Accepted);
        Assert.Equal(MatchStateHasher.Hash(removed), MatchStateHasher.Hash(noSkill.State));
        Assert.Empty(noSkill.Events);
    }

    internal static MatchState SoulOfInspirationPosition()
    {
        var state = VenUnitEntryBasicTests.Position("RAD·040/167");
        var cards = state.CardObjects.ToDictionary();
        var zones = state.PlayerZones.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary();
        cards["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "OGN·255/298", "P1")
            with { Tags = [CardObjectTags.LegendCard], IsExhausted = true };
        cards["ENEMY-LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY-LEGEND", "OGN·255/298", "P2")
            with { Tags = [CardObjectTags.LegendCard] };
        zones["P1"] = zones["P1"] with { LegendZone = ["LEGEND"] };
        zones["P2"] = zones["P2"] with { LegendZone = ["ENEMY-LEGEND"] };
        locations["LEGEND"] = new("P1", "LEGEND");
        locations["ENEMY-LEGEND"] = new("P2", "LEGEND");
        return state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
    }

    private static MatchState FlamePosition()
        => VenUnitEntryBasicTests.Position(FlameRaider);

    private static MatchState BombPosition()
        => VenUnitEntryBasicTests.Position(BombMonkey);

    private static string[] Bombs(MatchState state)
        => state.CardObjects.Values
            .Where(card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo)
            .Select(card => card.ObjectId)
            .Order(StringComparer.Ordinal)
            .ToArray();
}
