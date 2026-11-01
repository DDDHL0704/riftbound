using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenAmbessaDamageImmunityTests
{
    private static MatchState HeroPosition(string no, bool empowered = false)
    {
        var state = AttackDamageDistributionTests.Position(no);
        return state with { RoomId = "VEN-AMBESSA-IMMUNITY-" + no,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", no, "P1") with { IsEmpowered = empowered } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Orange] = 3 }) } };
    }

    private static MatchState DefenderPosition(string no, bool empowered)
    {
        var state = DeathAndDuelLifecycleTests.State();
        state = state with { RoomId = "VEN-AMBESSA-DEFENDER-" + no,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["D"] = OfficialGarenConquestBattlefieldCountTests.Printed("D", no, "P2") with { IsEmpowered = empowered } },
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(20, 20), ["P2"] = new(20, 20) } };
        state = AttackDamageDistributionTests.Spell(state, "BURN", "OGS·003/024", "P1");
        return AttackDamageDistributionTests.Spell(state, "FLASH", "OGS·011/024", "P2");
    }

    [Theory]
    [InlineData("VEN·084")] [InlineData("VEN·084a")] [InlineData("T1S·001/005")]
    public async Task ChinaStandardDeckCanSubmitAndOpenWithTheHeroInTheMainDeck(string no)
    {
        const OfficialDeckFormat format = OfficialDeckFormat.ChinaStandard20260724;
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(card => ChinaStandardBanList.IsAllowed(card, format)).ToArray() };
        string Rune(string color) => allowed.Cards.Where(card => card.CardCategory == "rune" && card.CardColorList.Contains(color))
            .OrderBy(card => card.CardNo, StringComparer.Ordinal).First().CardNo;
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(card => card.CardNo, StringComparer.Ordinal),
            "OGN·269/298", "OGN·164/298", new[] { no },
            new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" },
            Enumerable.Repeat(Rune("orange"), 6).Concat(Enumerable.Repeat(Rune("yellow"), 6)).ToArray()])!;
        var validation = OfficialDeckValidator.Validate(deck, catalog, format);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors)); Assert.Contains(no, deck.MainDeck);
        var initial = MatchReplayInitialStateBuilder.FromSeats("ambessa-main-deck-" + no,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var session = new MatchSession(initial, new CoreRuleEngine(), NoopMatchJournal.Instance, NoopMatchPlayerStore.Instance,
            new MatchSessionOptions(false, format));
        var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var raw = JsonSerializer.SerializeToElement(command, options);
        foreach (var player in new[] { "P1", "P2" })
        {
            var submitted = await session.SubmitDeckAsync(player, "deck-" + player, command, raw, default);
            Assert.True(submitted.Accepted, submitted.ErrorMessage);
        }
        var ready = JsonSerializer.SerializeToElement(new ReadyCommand(), options);
        Assert.True((await session.ReadyAsync("P1", "ready-P1", ready, default)).Accepted);
        var opened = await session.ReadyAsync("P2", "ready-P2", ready, default);
        Assert.True(opened.Accepted, opened.ErrorMessage); Assert.Equal(MatchPhases.Mulligan, opened.State.Phase);
    }

    [Theory]
    [InlineData("VEN·084", 37778)] [InlineData("VEN·084a", 37779)] [InlineData("T1S·001/005", 38003)]
    public async Task OfficialPrintsHaveTheSameEmpoweredImmunityAndMight(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("恶狼意志", card.SubTitle); Assert.Equal(4, card.Energy); Assert.Equal(4, card.Power);
        var immunity = Assert.Single(StaticAbilityParser.Parse(card.CardEffect, []), a => a.Kind == StaticAbilityKinds.SourceUnitDamageImmunity);
        Assert.True(immunity.RequiresSourceEmpowered); Assert.True(immunity.RequiresSourceOutsideBattle);
        Assert.Equal(3, EmpowerParser.Self(card.CardEffect).Might);
        var cost = Assert.Single(Assert.Single(EmpowerParser.Parse(card.CardEffect)).Alternatives);
        Assert.Equal(3, cost.Mana); Assert.Equal(1, cost.TypedPower[RuneTrait.Orange]);
    }

    [Theory]
    [InlineData("VEN·084")] [InlineData("VEN·084a")] [InlineData("T1S·001/005")]
    public async Task EmpowerCostsThreeManaAndOrangeAndTheCurrentViewShowsProtectionAndMight(string no)
    {
        var game = new Driver(HeroPosition(no));
        await game.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []));
        Assert.Equal(17, game.State.RunePools["P1"].Mana);
        Assert.Equal(2, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Orange));
        Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
        await game.Top(); Assert.True(game.State.CardObjects["HOST"].IsEmpowered);
        var host = CurrentPowerTargetTests.Snapshot(game.State).GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("HOST");
        Assert.Equal(7, host.GetProperty("effectivePower").GetInt32()); Assert.True(host.GetProperty("cannotTakeDamage").GetBoolean());
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·084")] [InlineData("VEN·084a")] [InlineData("T1S·001/005")]
    public async Task WrongEmpowerPowerIsRejectedWithoutPartialPayment(string no)
    {
        var state = HeroPosition(no) with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 3 }), ["P2"] = new(20, 20) } };
        var command = new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("wrong-power", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData("VEN·084")] [InlineData("VEN·084a")] [InlineData("T1S·001/005")]
    public async Task HeroCanBePlayedFromChampionZoneOnlyWithFourMana(string no)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = [], ChampionZone = ["UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "CHAMPION") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(4, 0) } };
        var command = new PlayCardCommand("UNIT", no, [], Destination: "BASE");
        var unpaid = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(3, 0) } };
        var rejected = await new CoreRuleEngine().ResolveAsync(unpaid, new("unpaid", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(MatchStateHasher.Hash(unpaid), MatchStateHasher.Hash(rejected.State));
        var game = new Driver(state); await game.Act("P1", command);
        Assert.Empty(game.State.PlayerZones["P1"].ChampionZone); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted); Assert.Equal(0, game.State.RunePools["P1"].Mana); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·084", false, 2)] [InlineData("VEN·084", true, 0)]
    [InlineData("VEN·084a", false, 2)] [InlineData("VEN·084a", true, 0)]
    [InlineData("T1S·001/005", false, 2)] [InlineData("T1S·001/005", true, 0)]
    public async Task TargetedDamageIsLegalButOnlyEmpoweredUnitOutsideBattleIsImmune(string no, bool empowered, int damage)
    {
        var game = new Driver(DefenderPosition(no, empowered));
        await game.Act("P1", new PlayCardCommand("BURN", "OGS·003/024", ["D"])); await game.Top();
        Assert.Equal(damage, game.State.CardObjects["D"].Damage);
        var applied = Assert.Single(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "D"));
        Assert.Equal(empowered, applied.Event.Payload.ContainsKey("preventedDamage")); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·084")] [InlineData("VEN·084a")] [InlineData("T1S·001/005")]
    public async Task UntargetedDamageProtectsTheEmpoweredUnitButNotAnotherEnemy(string no)
    {
        var state = OfficialHoldSequenceTests.AddUnit(DefenderPosition(no, true), "D2", "OGN·001/298", "BF", "P2");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["D2"] = state.CardObjects["D2"] with { Power = 10 } } };
        state = AttackDamageDistributionTests.Spell(state, "AREA", "OGS·002/024", "P1");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("AREA", "OGS·002/024", ["BF"])); await game.Top();
        Assert.Equal(0, game.State.CardObjects["D"].Damage); Assert.Equal(3, game.State.CardObjects["D2"].Damage);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·084")] [InlineData("VEN·084a")] [InlineData("T1S·001/005")]
    public async Task EmpoweredDefenderCanTakeSpellDamageDuringTheFirstBattleDuel(string no)
    {
        var game = new Driver(DefenderPosition(no, true));
        await game.Act("P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        Assert.True(game.State.CardObjects["D"].IsDefending); Assert.True(game.State.BattleState.IsActive);
        await game.Act("P1", new PlayCardCommand("BURN", "OGS·003/024", ["D"])); await game.Top();
        Assert.Equal(2, game.State.CardObjects["D"].Damage); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·084")] [InlineData("VEN·084a")] [InlineData("T1S·001/005")]
    public async Task EmpoweredDefenderTakesCombatDamageAndUsesItsEmpoweredMight(string no)
    {
        var game = new Driver(DefenderPosition(no, true));
        await game.Act("P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        var battle = game.State.BattleState;
        await game.Act("P1", new DeclareBattleCommand("BF", battle.AttackerObjectIds, battle.DefenderObjectIds, ["COMBAT_ASSIGNMENT"]));
        for (var step = 0; step < 20 && game.State.BattleState.IsActive && game.State.PendingBattleResult is null; step++)
        {
            var prompt = game.State.Seats.Keys.Select(game.PromptFor).FirstOrDefault(p => p.Actionable
                && (p.Actions.Contains(CommandTypes.AssignCombatDamage) || p.Actions.Contains(CommandTypes.PassPriority)));
            if (prompt is null) break;
            var current = game.State.BattleState;
            GameCommand action = prompt.Actions.Contains(CommandTypes.AssignCombatDamage)
                ? new AssignCombatDamageCommand(current.BattleId!, current.BattlefieldObjectId!, CombatTestDriver.Assignments(prompt))
                : new PassPriorityCommand();
            await game.Act(prompt.PlayerId, action);
        }
        Assert.Contains(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "A")
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "D")
            && Convert.ToInt32(e.Event.Payload.GetValueOrDefault("damage")) == 4);
        Assert.Contains(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "D")
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "A")
            && Convert.ToInt32(e.Event.Payload.GetValueOrDefault("damage")) == 7);
        Assert.Contains("A", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("D", game.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(0, game.State.CardObjects["D"].Damage); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·084", false, 0)] [InlineData("VEN·084", true, 2)]
    [InlineData("VEN·084a", false, 0)] [InlineData("VEN·084a", true, 2)]
    [InlineData("T1S·001/005", false, 0)] [InlineData("T1S·001/005", true, 2)]
    public async Task EmpowermentAtResolutionDeterminesWhetherDamageIsPrevented(string no, bool initiallyEmpowered, int damage)
    {
        var state = AttackDamageDistributionTests.Spell(DefenderPosition(no, initiallyEmpowered), "STATUS", "VEN·035", "P1");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("BURN", "OGS·003/024", ["D"]));
        await game.Act("P1", new PlayCardCommand("STATUS", "VEN·035", ["D"],
            Mode: initiallyEmpowered ? "REMOVE_THEN_EMPOWER" : "EMPOWER_THEN_REMOVE"));
        await game.Top(); Assert.Equal(!initiallyEmpowered, game.State.CardObjects["D"].IsEmpowered);
        await game.Top(); Assert.Equal(damage, game.State.CardObjects["D"].Damage); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·084")] [InlineData("VEN·084a")] [InlineData("T1S·001/005")]
    public async Task CopiedTokenCanEmpowerAndUseTheSamePrintedDamageImmunity(string no)
    {
        var state = AttackDamageDistributionTests.Spell(OfficialCopyIdentityTests.Position(no), "BEAM", "OGS·022/024", "P1");
        state = state with { Tick = 0, RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(20, 20, new Dictionary<string, int> { [RuneTrait.Orange] = 1 }) } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("C", "UNL-200/219", ["TARGET"])); await game.Top(); await game.Top();
        var token = game.State.CardObjects["C-TOKEN-001"];
        Assert.Equal(no, token.CardNo); Assert.False(token.IsEmpowered);
        await game.Act("P1", new ActivateAbilityCommand("C-TOKEN-001", "EMPOWER:0:0", [])); await game.Top();
        Assert.True(game.State.CardObjects["C-TOKEN-001"].IsEmpowered);
        await game.Act("P1", new PlayCardCommand("BEAM", "OGS·022/024", ["C-TOKEN-001"])); await game.Top();
        Assert.Equal(0, game.State.CardObjects["C-TOKEN-001"].Damage);
        Assert.Contains(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "C-TOKEN-001")
            && Equals(e.Event.Payload.GetValueOrDefault("preventedDamage"), 8));
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·084")] [InlineData("VEN·084a")] [InlineData("T1S·001/005")]
    public async Task MovingDefenderOutOfBattleInResponseRestoresItsImmunity(string no)
    {
        var game = new Driver(DefenderPosition(no, true));
        await game.Act("P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PlayCardCommand("BURN", "OGS·003/024", ["D"]));
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("FLASH", "OGS·011/024", ["D"])); await game.Top();
        Assert.Equal("BASE", game.State.ObjectLocations["D"].Zone); Assert.False(game.State.CardObjects["D"].IsDefending);
        await game.Top(); Assert.Equal(0, game.State.CardObjects["D"].Damage); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·084")] [InlineData("VEN·084a")] [InlineData("T1S·001/005")]
    public async Task DamageImmunityDoesNotPreventDirectDestruction(string no)
    {
        var state = AttackDamageDistributionTests.Spell(DefenderPosition(no, true), "DESTROY", "OGN·229/298", "P1");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("DESTROY", "OGN·229/298", ["D"])); await game.Top();
        Assert.Contains("D", game.State.PlayerZones["P2"].Graveyard); await game.Replay();
    }
}
