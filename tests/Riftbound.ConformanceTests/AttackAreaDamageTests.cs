using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class AttackAreaDamageTests
{
    [Theory]
    [InlineData("VEN·019", 37704)]
    [InlineData("VEN·019a", 37705)]
    public async Task RenektonPrintsAreAdmittedWithTheOfficialHasteAndAttackText(string cardNo, long id)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == cardNo);
        Assert.Equal(id, card.Id);
        Assert.Equal("杀意骤起", card.SubTitle);
        Assert.Equal(6, card.Energy);
        Assert.Equal(6, card.Power);
        Assert.Contains("如果你控制的符文数量不超过四枚", card.CardEffect);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(cardNo, out var behavior));
        Assert.Equal(1, behavior.HasteReadyManaCost);
        Assert.Equal(1, behavior.HasteReadyPowerCost);
        Assert.Equal(RuneTrait.Red, behavior.HasteReadyPowerTrait);
    }

    [Theory]
    [InlineData("VEN·019", false)]
    [InlineData("VEN·019", true)]
    [InlineData("VEN·019a", false)]
    [InlineData("VEN·019a", true)]
    public async Task RenektonPaysThePrintedCostAndOptionalHasteSeparately(string cardNo, bool haste)
    {
        var initial = VenUnitEntryBasicTests.Position(cardNo);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new RunePool(7, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }) } };
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", cardNo, [],
            OptionalCosts: haste ? [HasteOptionalCostNames.HasteReady] : [], Destination: "BASE"));
        Assert.Equal(!haste, game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(haste ? 0 : 1, game.State.RunePools["P1"].Mana);
        Assert.Equal(haste ? 0 : 1, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·019")]
    [InlineData("VEN·019a")]
    public async Task EachHeroPrintingCanPayAndEnterFromTheChampionZone(string cardNo)
    {
        var initial = VenUnitEntryBasicTests.Position(cardNo);
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Hand = [], ChampionZone = ["UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["UNIT"] = new("P1", "CHAMPION") },
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new RunePool(6, 0) } };
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", cardNo, [], Destination: "BASE"));
        Assert.Empty(game.State.PlayerZones["P1"].ChampionZone);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·019")]
    [InlineData("VEN·019a")]
    public async Task HasteRejectsTheWrongPowerWithoutPayingOrMovingTheCard(string cardNo)
    {
        var initial = VenUnitEntryBasicTests.Position(cardNo);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new RunePool(7, 0, new Dictionary<string, int> { [RuneTrait.Green] = 1 }) } };
        var command = new PlayCardCommand("UNIT", cardNo, [], OptionalCosts: [HasteOptionalCostNames.HasteReady], Destination: "BASE");
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("invalid-haste", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData("VEN·019", 4, 2)]
    [InlineData("VEN·019a", 4, 2)]
    [InlineData("OGN·148/298", 5, 3)]
    public async Task AttackQueuesAnUntargetedEffectBeforeDamagingEveryEnemyHere(string cardNo, int runes, int amount)
    {
        var attack = await AttackDamageDistributionTests.Attack(Position(cardNo, runes));
        var item = Assert.Single(attack.State.StackItems);
        Assert.Equal("ATTACK_AREA_DAMAGE", item.FieldContext!.Kind);
        Assert.Empty(item.TargetObjectIds);
        Assert.Null(attack.State.PendingCardChoice);
        Assert.All(new[] { "ENEMY1", "ENEMY2", "ENEMY3" }, id => Assert.Equal(0, attack.State.CardObjects[id].Damage));
        ArmamentMightTests.PublicRecovery(attack.State);

        var resolved = await AttackDamageDistributionTests.Top(attack.State);

        Assert.All(new[] { "ENEMY1", "ENEMY2", "ENEMY3" }, id => Assert.Equal(amount, resolved.State.CardObjects[id].Damage));
        Assert.Equal(3, resolved.Events.Count(e => e.Kind == "DAMAGE_APPLIED"));
        ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Theory]
    [InlineData("VEN·019")]
    [InlineData("VEN·019a")]
    public async Task MoreThanFourRunesAtTheFirstAttackDoesNotQueueTheSkill(string cardNo)
    {
        var attack = await AttackDamageDistributionTests.Attack(Position(cardNo, 5));
        Assert.DoesNotContain(attack.State.StackItems, item => item.FieldContext?.Kind == "ATTACK_AREA_DAMAGE");
        Assert.DoesNotContain(attack.State.TriggerQueue, item => item.FieldContext?.Kind == "ATTACK_AREA_DAMAGE");
        Assert.DoesNotContain(attack.Events, e => e.Kind == "TRIGGER_QUEUED");
    }

    [Fact]
    public async Task GainingAFifthRuneInResponseDoesNotCancelTheEarnedAttackTrigger()
    {
        var initial = AttackDamageDistributionTests.Spell(Position("VEN·019", 4, runeInDeck: true), "GAIN-RUNE", "OGN·104/298", "P1");
        initial = LocalDestructionRecallTests.At(OfficialHoldSequenceTests.AddUnit(initial, "ALLY", "OGN·001/298", "F", "P1"), "ALLY", "P1", "BASE");
        var attack = await AttackDamageDistributionTests.Attack(initial);
        var response = await Act(attack.State, "P1", new PlayCardCommand("GAIN-RUNE", "OGN·104/298", ["ALLY"]));
        var afterResponse = await AttackDamageDistributionTests.Top(response.State);
        Assert.Contains("RUNE-DECK", afterResponse.State.PlayerZones["P1"].Base);
        Assert.Contains(afterResponse.State.StackItems, item => item.FieldContext?.Kind == "ATTACK_AREA_DAMAGE");

        var resolved = await AttackDamageDistributionTests.Top(afterResponse.State);

        Assert.All(new[] { "ENEMY1", "ENEMY2", "ENEMY3" }, id => Assert.Equal(2, resolved.State.CardObjects[id].Damage));
        ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Fact]
    public async Task MovingTheSourceToBaseChangesWhereTheAbilityDealsDamage()
    {
        var initial = AttackDamageDistributionTests.Spell(Position("VEN·019", 4), "FLASH", "OGS·011/024", "P1");
        var attack = await AttackDamageDistributionTests.Attack(initial);
        var moved = await AttackDamageDistributionTests.Top((await Act(attack.State, "P1",
            new PlayCardCommand("FLASH", "OGS·011/024", ["HOST"]))).State);
        Assert.Equal("BASE", moved.State.ObjectLocations["HOST"].Zone);
        var resolved = await AttackDamageDistributionTests.Top(moved.State);
        Assert.All(new[] { "ENEMY1", "ENEMY2", "ENEMY3" }, id => Assert.Equal(0, resolved.State.CardObjects[id].Damage));
        Assert.DoesNotContain(resolved.Events, e => e.Kind == "DAMAGE_APPLIED");
        ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Fact]
    public async Task DestroyingTheSourceBeforeResolutionLeavesNoCurrentHere()
    {
        var initial = AttackDamageDistributionTests.Spell(Position("VEN·019", 4), "SOUL", "SFD·163/221", "P1");
        initial = LocalDestructionRecallTests.At(OfficialHoldSequenceTests.AddUnit(initial, "ALLY", "OGN·001/298", "F", "P1"), "ALLY", "P1", "BASE");
        var attack = await AttackDamageDistributionTests.Attack(initial);
        var destroyed = await AttackDamageDistributionTests.Top((await Act(attack.State, "P1",
            new PlayCardCommand("SOUL", "SFD·163/221", ["HOST", "ALLY"]))).State);
        Assert.Contains("HOST", destroyed.State.PlayerZones["P1"].Graveyard);
        var resolved = await AttackDamageDistributionTests.Top(destroyed.State);
        Assert.All(new[] { "ENEMY1", "ENEMY2", "ENEMY3" }, id => Assert.Equal(0, resolved.State.CardObjects[id].Damage));
        Assert.DoesNotContain(resolved.Events, e => e.Kind == "DAMAGE_APPLIED");
        ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Fact]
    public async Task APrintedCopyCreatesTwoIndependentAttackTriggers()
    {
        var attack = await AttackDamageDistributionTests.Attack(Position("VEN·019", 4, copied: true));
        Assert.Equal(2, attack.State.TriggerQueue.Count);
        var ids = attack.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray();
        Assert.Equal(2, ids.Distinct().Count());
        var ordered = await Act(attack.State, "P1", new OrderTriggersCommand(OrderedTriggerIds: ids.Reverse().ToArray()));
        Assert.Equal(2, ordered.State.StackItems.Count);
        Assert.Single(ordered.State.StackItems, item => item.FieldContext!.Origin is not null);
        ArmamentMightTests.PublicRecovery(ordered.State);
        var first = await AttackDamageDistributionTests.Top(ordered.State);
        var second = await AttackDamageDistributionTests.Top(first.State);
        Assert.All(new[] { "ENEMY1", "ENEMY2", "ENEMY3" }, id => Assert.Equal(4, second.State.CardObjects[id].Damage));
        ArmamentMightTests.PublicRecovery(second.State);
    }

    [Fact]
    public async Task AttackDamageSurvivesSessionRecoveryAndFullActionReplay()
    {
        var game = new VenBasicFlowSpellTests.Driver(Position("VEN·019", 4));
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        for (var step = 0; step < 4 && game.State.FocusPlayerId is not null; step++)
            await game.Act(game.State.FocusPlayerId, new PassFocusCommand());
        if (game.PromptFor("P1").EnabledActions().Contains(CommandTypes.DeclareBattle))
            await game.Act("P1", new DeclareBattleCommand("F", ["HOST"], ["ENEMY1", "ENEMY2", "ENEMY3"], ["COMBAT_ASSIGNMENT"]));
        Assert.Equal("ATTACK_AREA_DAMAGE", Assert.Single(game.State.StackItems).FieldContext!.Kind);
        await game.Top();
        Assert.All(new[] { "ENEMY1", "ENEMY2", "ENEMY3" }, id => Assert.Equal(2, game.State.CardObjects[id].Damage));
        await game.Replay();
    }

    [Fact]
    public async Task ReturningTheSourceToHandDoesNotExposeItsNewPrivateIdentity()
    {
        var initial = AttackDamageDistributionTests.Spell(Position("VEN·019", 4, runeInDeck: true), "RETURN", "OGN·104/298", "P1");
        var attack = await AttackDamageDistributionTests.Attack(initial);
        var returned = await AttackDamageDistributionTests.Top((await Act(attack.State, "P1",
            new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]))).State);
        Assert.Contains("HOST", returned.State.PlayerZones["P1"].Hand);
        Assert.Contains(returned.State.StackItems, item => item.FieldContext?.Kind == "ATTACK_AREA_DAMAGE");
        var opponent = ResolutionResult.BuildSnapshots(returned.State)["P2"];
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(returned.State.RoomId, returned.State.Tick, 0, [], returned.State);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(opponent.Stack));
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(spectator.SpectatorSnapshot.Stack));
        ArmamentMightTests.PublicRecovery(returned.State);
        var resolved = await AttackDamageDistributionTests.Top(returned.State);
        Assert.DoesNotContain(resolved.Events, e => e.Kind == "DAMAGE_APPLIED");
        Assert.DoesNotContain(resolved.Events, e => Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "HOST"));
        ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Theory]
    [InlineData("missing-frame")]
    [InlineData("wrong-source")]
    [InlineData("wrong-field")]
    public async Task RecoveryRejectsAChangedAttackConditionContext(string change)
    {
        var state = (await AttackDamageDistributionTests.Attack(Position("VEN·019", 4))).State;
        var item = Assert.Single(state.StackItems);
        var context = item.FieldContext!;
        var frame = context.AttackConditionFrame!;
        var forged = change switch {
            "missing-frame" => context with { AttackConditionFrame = null },
            "wrong-source" => context with { AttackConditionFrame = frame with {
                CardObjects = frame.CardObjects.Where(pair => pair.Key != "HOST").ToDictionary() } },
            _ => context with { BattlefieldId = "UNKNOWN" } };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(state with { StackItems = [item with { FieldContext = forged }] }));
    }

    private static MatchState Position(string cardNo, int runeCount, bool runeInDeck = false, bool copied = false)
    {
        var state = AttackDamageDistributionTests.Position(cardNo, copied);
        var cards = state.CardObjects.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary();
        var zones = state.PlayerZones.ToDictionary();
        foreach (var id in new[] { "ENEMY1", "ENEMY2", "ENEMY3" })
            cards[id] = cards[id] with { Power = 10, Tags = [..cards[id].Tags, "法盾2"] };
        var runes = new List<string>();
        for (var i = 0; i < runeCount; i++)
        {
            var id = "RUNE-" + i;
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R01", "P1");
            locations[id] = new("P1", "BASE");
            runes.Add(id);
        }
        if (runeInDeck)
        {
            cards["RUNE-DECK"] = OfficialGarenConquestBattlefieldCountTests.Printed("RUNE-DECK", "UNL-R01", "P1");
            locations["RUNE-DECK"] = new("P1", "RUNE_DECK");
        }
        zones["P1"] = zones["P1"] with {
            Base = [..zones["P1"].Base, ..runes],
            RuneDeck = runeInDeck ? ["RUNE-DECK", ..zones["P1"].RuneDeck] : zones["P1"].RuneDeck };
        return state with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
    }
}
