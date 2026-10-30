using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class RadCombatHeroTests
{
    private const string Graves = "RAD·085/167";
    private static readonly string[] Prints = ["RAD·042/167", "RAD·042a/167", Graves];
    public static IEnumerable<object[]> Numbers() => Prints.Select(no => new object[] { no });

    [Theory]
    [InlineData("RAD·042/167", 39535, 3, 0)]
    [InlineData("RAD·042a/167", 39536, 3, 0)]
    [InlineData(Graves, 39584, 5, 1)]
    public async Task EachOfficialPrintingKeepsItsIdentityCostMightAndExactParsedRule(string no, long id, int mana, int power)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(mana, card.Energy); Assert.Equal(power, card.ReturnEnergy ?? 0); Assert.Equal(4, card.Power);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior));
        Assert.Equal(mana, behavior.ManaCost); Assert.Equal(4, behavior.SourceUnitPower);
        if (no == Graves) {
            var trigger = Assert.Single(TriggerParser.Parse(card.CardEffect), t => t.Kind == TriggerKinds.UnitAttackOrDefendSourceMightDamageAllEnemyUnitsHere);
            Assert.Equal(TriggerTimings.UnitAttackOrDefend, trigger.Timing); Assert.Equal(TriggerTargetScopes.AllEnemyUnitsHere, trigger.TargetScope);
            Assert.Null(trigger.DamageAmount);
            Assert.Contains(StaticAbilityParser.Parse(card.CardEffect, []), a => a.Kind == StaticAbilityKinds.UnitCannotDealCombatDamage);
        } else {
            Assert.Contains("{{坚守}}", card.CardEffect);
            Assert.Single(StaticAbilityParser.Parse(card.CardEffect, []), a => a.Kind == StaticAbilityKinds.UnitFriendlyEquipmentHereCombatDamage);
        }
    }

    [Theory, MemberData(nameof(Numbers))]
    public async Task ActualPlayPaysThePrintedCostAndChampionZoneEntryKeepsTheSameBehavior(string no)
    {
        var s = VenUnitEntryBasicTests.Position(no);
        s = s with {
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with { Hand = [], ChampionZone = ["UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["UNIT"] = new("P1", "CHAMPION") },
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(no == Graves ? 5 : 3, 0,
                no == Graves ? new Dictionary<string, int> { [RuneTrait.Orange] = 1 } : null) } };
        var game = new Driver(s);
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Empty(game.State.PlayerZones["P1"].ChampionZone); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power); Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory, MemberData(nameof(Numbers))]
    public async Task AnUnpayableCardCannotSpendResourcesOrLeaveTheHand(string no)
    {
        var s = VenUnitEntryBasicTests.Position(no);
        s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = RunePool.Empty } };
        await Reject(s, "P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
    }

    private static MatchState Board(string no, bool defend = false, bool copied = false)
    {
        var s = AhriCombatMightTests.Board(no, defend, copied);
        var source = defend ? "ENEMY1" : "HOST";
        return s with { CardObjects = s.CardObjects.ToDictionary(p => p.Key, p => p.Key == source
            ? p.Value with { Power = 4, Tags = no == Graves ? [CardObjectTags.UnitCard] : [CardObjectTags.UnitCard, "坚守"] }
            : p.Key is "HOST" or "ENEMY1" or "ENEMY2" or "ENEMY3" ? p.Value with { Power = 20 } : p.Value) };
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task GravesQueuesExactlyOneUntargetedSkillOnEitherRoleAndDamagesEveryCurrentLocalEnemy(bool defend)
    {
        var initial = Board(Graves, defend);
        initial = initial with { CardObjects = initial.CardObjects.ToDictionary(p => p.Key, p => p.Key is "HOST" or "ENEMY1" or "ENEMY2" or "ENEMY3"
            ? p.Value with { Tags = [CardObjectTags.UnitCard, "法盾2"] } : p.Value) };
        var game = new Driver(initial); await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var item = Assert.Single(game.State.StackItems); Assert.Equal("ATTACK_AREA_DAMAGE", item.FieldContext!.Kind);
        Assert.Empty(item.TargetObjectIds); Assert.Null(game.State.PendingCardChoice); Assert.Null(game.State.PendingPayment);
        Assert.Empty(game.State.TriggerQueue); Assert.All(new[] { "HOST", "ENEMY1", "ENEMY2", "ENEMY3" }, id => Assert.Equal(0, game.State.CardObjects[id].Damage));
        await Reject(game.State, defend ? "P2" : "P1", new ChooseCardsCommand("forged", "TRIGGER_CONFIRMATION", [defend ? "HOST" : "ENEMY1"]));
        await game.Top();
        var enemies = defend ? new[] { "HOST" } : new[] { "ENEMY1", "ENEMY2", "ENEMY3" };
        Assert.All(enemies, id => Assert.Equal(4, game.State.CardObjects[id].Damage));
        Assert.Equal(0, game.State.CardObjects[defend ? "ENEMY2" : "HOST"].Damage);
        Assert.Equal(initial.RunePools[defend ? "P2" : "P1"].TotalPower, game.State.RunePools[defend ? "P2" : "P1"].TotalPower);
        await FinishCombat(game, defend ? "ENEMY1" : "HOST", 0); await game.Replay();
    }

    [Fact]
    public async Task ARealBuffResponseChangesTheAreaDamageAtResolution()
    {
        var initial = AttackDamageDistributionTests.Spell(Board(Graves), "BUFF", "SFD·034/221", "P1");
        var game = new Driver(initial); await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        await game.Act("P1", new PlayCardCommand("BUFF", "SFD·034/221", ["HOST"])); await game.Top();
        Assert.Equal(6, game.State.CardObjects["HOST"].Power); await game.Top();
        Assert.All(new[] { "ENEMY1", "ENEMY2", "ENEMY3" }, id => Assert.Equal(6, game.State.CardObjects[id].Damage));
        await game.Replay();
    }

    [Theory, InlineData("recall"), InlineData("prevent")]
    public async Task AResponseCanRecallTheSourceOrPreventAllOfTheAreaDamage(string branch)
    {
        var s = Board(Graves);
        // Gust's limit is three Might; it must actually be legal to recall the source.
        if (branch == "recall") s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["HOST"] = s.CardObjects["HOST"] with { Power = 3 } } };
        var response = branch == "recall" ? "OGN·169/298" : "OGN·145/298";
        s = EquipChainTests.Response(s, response, "P2");
        var game = new Driver(s); await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Assert.Single(game.State.StackItems); await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RESPONSE", response, branch == "recall" ? ["HOST"] : []));
        await game.Top(); if (branch == "recall") Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Empty(game.State.StackItems); Assert.All(new[] { "ENEMY1", "ENEMY2", "ENEMY3" }, id => Assert.Equal(0, game.State.CardObjects[id].Damage));
        await game.Replay();
    }

    [Fact]
    public async Task ACounterThatRequiresFriendlyTargetsCannotSelectTheUntargetedAreaSkill()
    {
        var game = new Driver(EquipChainTests.Response(Board(Graves), "SFD·045/221", "P2"));
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var skill = Assert.Single(game.State.StackItems); await game.Act("P1", new PassPriorityCommand());
        await Reject(game.State, "P2", new PlayCardCommand("RESPONSE", "SFD·045/221", [skill.StackItemId]));
        await game.Act("P2", new PassPriorityCommand());
        Assert.All(new[] { "ENEMY1", "ENEMY2", "ENEMY3" }, id => Assert.Equal(4, game.State.CardObjects[id].Damage));
        Assert.Contains("RESPONSE", game.State.PlayerZones["P2"].Hand); await game.Replay();
    }

    [Fact]
    public async Task CopiedGravesTextCreatesTwoIndependentUntargetedSkills()
    {
        var game = new Driver(Board(Graves, copied: true));
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        if (game.State.TriggerQueue.Count > 0) await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        Assert.Equal(2, game.State.StackItems.Count); Assert.Single(game.State.StackItems, i => i.FieldContext!.Origin is not null);
        await game.Top(); await game.Top();
        Assert.All(new[] { "ENEMY1", "ENEMY2", "ENEMY3" }, id => Assert.Equal(8, game.State.CardObjects[id].Damage)); await game.Replay();
    }

    [Theory, InlineData(0), InlineData(-2)]
    public async Task NonpositiveSourceMightCannotProduceDamage(int might)
    {
        var s = Board(Graves); s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["HOST"] = s.CardObjects["HOST"] with { Power = might } } };
        var game = new Driver(s); await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", [])); await game.Top();
        Assert.All(new[] { "ENEMY1", "ENEMY2", "ENEMY3" }, id => Assert.Equal(0, game.State.CardObjects[id].Damage));
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED"); await game.Replay();
    }

    [Fact]
    public async Task BothPlayersCanEarnIndependentAreaSkillsInTheSameBattle()
    {
        var s = Board(Graves); s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["ENEMY1"] = s.CardObjects["ENEMY1"] with { CardNo = Graves } } };
        var game = new Driver(s); await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Assert.Equal(2, game.State.StackItems.Count(i => i.FieldContext?.Kind == "ATTACK_AREA_DAMAGE")
            + game.State.TriggerQueue.Count(i => i.FieldContext?.Kind == "ATTACK_AREA_DAMAGE"));
        var ids = game.State.StackItems.Select(i => i.StackItemId).Concat(game.State.TriggerQueue.Select(i => i.TriggerId)).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count()); await game.Replay();
    }

    // Prepare Night's Edge through real commands. With no friendly unit here,
    // its mandatory attachment skill has no legal target and is discarded.
    // The equipment stays here when the opponent subsequently conquers it.
    private static async Task<Driver> WithUnattachedEquipment(MatchState s, string controller,
        bool inBase = false, bool faceDown = false, string? response = null)
    {
        const string no = "SFD·139/221";
        if (response is not null) s = EquipChainTests.Response(s, response, "P2");
        s = LocalDestructionRecallTests.At(s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", no, controller) } }, "GEAR", controller, "BASE");
        if (inBase) return new Driver(s);
        foreach (var id in new[] { "ENEMY1", "ENEMY2", "ENEMY3" })
            s = LocalDestructionRecallTests.At(s, id, "P2", "BASE");
        var cards = s.CardObjects.ToDictionary(); var zones = s.PlayerZones.ToDictionary(); var locations = s.ObjectLocations.ToDictionary();
        cards["F"] = cards["F"] with { ControllerId = controller };
        zones[controller] = zones[controller] with {
            Base = zones[controller].Base.Where(id => id != "GEAR").ToArray(),
            Hand = zones[controller].Hand.Append("GEAR").ToArray() };
        locations["GEAR"] = new(controller, "HAND");
        foreach (var player in new[] { "P1", "P2" }) {
            var deck = Enumerable.Range(0, 8).Select(n => $"COMBAT-PREP-{player}-{n}").ToArray();
            foreach (var id in deck) {
                cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "OGN·001/298", player);
                locations[id] = new(player, "MAIN_DECK");
            }
            zones[player] = zones[player] with { MainDeck = zones[player].MainDeck.Concat(deck).ToArray() };
            foreach (var n in Enumerable.Range(0, 6)) {
                var id = $"COMBAT-RUNE-{player}-{n}";
                cards[id] = new(id, cardNo: "UNL-R06", ownerId: player, controllerId: player,
                    tags: [CardObjectTags.RuneCard, "COLOR:purple"]);
                locations[id] = new(player, "BASE");
                zones[player] = zones[player] with { Base = zones[player].Base.Append(id).ToArray() };
            }
        }
        var game = new Driver(s with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations,
            ActivePlayerId = controller, TurnPlayerId = controller });
        await game.Act(controller, new HideCardCommand("GEAR", no, "BATTLEFIELD:F", ["STANDBY_A"]));
        if (!faceDown) {
            await FaceDownPlayTestDriver.NextTurn(game); await FaceDownPlayTestDriver.NextTurn(game);
            await game.Act(controller, new PlayCardCommand("GEAR", no, []));
            Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
            Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        }
        if (game.State.TurnPlayerId != "P2") await FaceDownPlayTestDriver.NextTurn(game);
        await game.Act("P2", new MoveUnitCommand("ENEMY1", "BASE", "BATTLEFIELD:F", [],
            SourceObjectIds: ["ENEMY1", "ENEMY2", "ENEMY3"]));
        for (var step = 0; game.State.SpellDuelState.IsActive && step < 8; step++) {
            if (game.State.StackItems.Count > 0) await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            else await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        }
        Assert.False(game.State.SpellDuelState.IsActive); Assert.False(game.State.BattleState.IsActive);
        await FaceDownPlayTestDriver.NextTurn(game);
        Assert.Equal("P1", game.State.TurnPlayerId);
        return game;
    }

    public static IEnumerable<object[]> EquipmentCases() => Prints.Where(no => no != Graves).SelectMany(no =>
        new[] { "here", "attached", "base", "enemy", "face-down" }.Select(branch => new object[] { no, branch }));

    [Theory, MemberData(nameof(EquipmentCases))]
    public async Task OriannaNeedsACurrentFriendlyPublicEquipmentHereAndDoesNotRequireAnAttachment(string no, string branch)
    {
        var defend = branch == "face-down";
        var controller = defend ? "P2" : "P1";
        var game = await WithUnattachedEquipment(Board(no, defend), branch == "enemy" ? "P2" : controller,
            inBase: branch == "base", faceDown: branch == "face-down");
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        if (branch == "attached") {
            await game.Act("P1", new RecycleRuneCommand("COMBAT-RUNE-P1-0"));
            await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_PURPLE"])); await game.Top();
            Assert.Equal("HOST", game.State.CardObjects["GEAR"].AttachedToObjectId);
        }
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        await FinishCombat(game, defend ? "ENEMY1" : "HOST", branch is "here" or "attached" ? 4 : 0); await game.Replay();
    }

    [Theory, InlineData("RAD·042/167"), InlineData("RAD·042a/167")]
    public async Task OriannaCanDealHerHoldfastMightWhenDefendingBesideFriendlyEquipment(string no)
    {
        var game = await WithUnattachedEquipment(Board(no, defend: true), "P2");
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        await FinishCombat(game, "ENEMY1", 5); await game.Replay();
    }

    [Theory, InlineData("OGN·022/298"), InlineData("SFD·135/221")]
    public async Task DestroyingOrRecallingTheEquipmentDuringResponsesDisablesCombatDamageImmediately(string response)
    {
        var game = await WithUnattachedEquipment(Board("RAD·042/167"), "P1", response: response);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        // The open battle duel lets either player answer before combat damage.
        if (game.State.FocusPlayerId == "P1") await game.Act("P1", new PassFocusCommand());
        foreach (var n in Enumerable.Range(0, 6)) await game.Act("P2", new TapRuneCommand($"COMBAT-RUNE-P2-{n}"));
        await game.Act("P2", new PlayCardCommand("RESPONSE", response, response == "SFD·135/221" ? ["GEAR"] : []));
        await game.Top(); await FinishCombat(game, "HOST", 0); await game.Replay();
    }

    private static async Task FinishCombat(Driver game, string source, int expectedPool)
    {
        var sawPool = false;
        for (var step = 0; step < 35 && !game.Events().Any(e => e.Event.Kind == "BATTLE_CLOSED"); step++) {
            var s = game.State; var prompts = ResolutionResult.BuildPrompts(s);
            var assignment = prompts.Values.FirstOrDefault(p => p.Candidates?.Any(c => c.Action == CommandTypes.AssignCombatDamage && c.Enabled) == true);
            if (assignment is not null) {
                var metadata = JsonSerializer.SerializeToElement(assignment.View!.Metadata);
                if (metadata.GetProperty("assignableDamagePool").TryGetProperty(source, out var pool)) {
                    Assert.Equal(expectedPool, pool.GetInt32()); sawPool = true;
                }
                await game.Act(assignment.PlayerId, new AssignCombatDamageCommand(s.BattleState.BattleId!, "F", CombatTestDriver.Assignments(assignment)));
            } else if (s.PriorityPlayerId is { } priority) await game.Act(priority, new PassPriorityCommand());
            else if (s.FocusPlayerId is { } focus) await game.Act(focus, new PassFocusCommand());
            else {
                var actor = prompts.Values.First(p => p.Candidates?.Any(c => c.Action == CommandTypes.DeclareBattle && c.Enabled) == true).PlayerId;
                await game.Act(actor, new DeclareBattleCommand("F", s.BattleState.AttackerObjectIds, s.BattleState.DefenderObjectIds, ["COMBAT_ASSIGNMENT"]));
            }
        }
        Assert.Contains(game.Events(), e => e.Event.Kind == "BATTLE_CLOSED");
        var dealt = game.Events().Where(e => e.Event.Kind == "DAMAGE_APPLIED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), source) && e.Event.Payload.ContainsKey("combatRole")).ToArray();
        Assert.Equal(expectedPool, dealt.Sum(e => Convert.ToInt32(e.Event.Payload["damage"])));
        if (expectedPool > 0) Assert.True(sawPool);
    }

    private static async Task Reject(MatchState s, string player, GameCommand command)
    {
        var before = MatchStateHasher.Hash(s);
        var result = await new CoreRuleEngine().ResolveAsync(s, new("rad-combat-illegal", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }
}
