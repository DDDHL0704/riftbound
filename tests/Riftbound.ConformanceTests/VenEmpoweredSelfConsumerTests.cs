using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenEmpoweredSelfConsumerTests
{
    private static readonly string[] Cards = ["VEN·043", "VEN·050", "VEN·070"];
    internal static string Text(string no) => no switch {
        "VEN·043" => "{{法盾}}（对手必须支付{{A}}才能将我选作法术或技能的目标。）\n{{强化7}}（支付{{7}}：强化我。仅在未强化时可用。）\n{{已强化>}} 我获得{{S}}+7。",
        "VEN·050" => "{{强化12}}。你每控制一枚符文，此技能的费用便减少{{1}}。（支付此费用：强化我。仅在未强化时可用。）\n{{已强化>}} 我获得{{法盾}}和{{坚守3}}。（对手必须支付{{A}}才能将我选作法术或技能的目标。如果我是防守方，则{{S}}+3。）",
        _ => "{{强化3}}（支付{{3}}：强化我。仅在未强化时可用。）\n{{已强化>}} 我获得{{S}}+2和{{游走}}。（我可以向其他战场进行移动。）"
    };
    private static int PrintedMight(string no) => no == "VEN·043" ? 0 : 4;
    private static int Mana(string no) => no == "VEN·043" ? 1 : no == "VEN·050" ? 4 : 3;
    private static int EmpowerMana(string no) => no == "VEN·043" ? 7 : no == "VEN·050" ? 12 : 3;
    private static int EmpoweredMight(string no) => no == "VEN·043" ? 7 : no == "VEN·050" ? 4 : 6;

    [Theory]
    [InlineData("VEN·043", 37733, "钢爪", "green")]
    [InlineData("VEN·050", 37741, "凶暴的岩熊", "blue")]
    [InlineData("VEN·070", 37764, "残暴猎手", "orange")]
    public async Task ExactCnSourceAndItsCompleteExistingEmpowerProfileAreRegistered(string no, long id, string name, string trait)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(name, card.CardName); Assert.Equal(Text(no), card.CardEffect);
        Assert.Equal([trait], card.CardColorList); Assert.Equal(PrintedMight(no), card.Power); Assert.Equal(Mana(no), card.Energy);
        Assert.Equal(no == "VEN·070" ? 1 : (int?)null, card.ReturnEnergy);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var b)); Assert.True(b.PlaysSourceToBaseAsUnit);
        Assert.Equal(PrintedMight(no), b.SourceUnitPower); Assert.Equal(Mana(no), b.ManaCost);
        var spec = Assert.Single(EmpowerParser.Parse(card.CardEffect)); var cost = Assert.Single(spec.Alternatives);
        Assert.True(cost.SupportedResources); Assert.False(cost.Exhaust); Assert.Equal(EmpowerMana(no), cost.Mana);
        Assert.Equal(0, cost.Power); Assert.Empty(cost.TypedPower); Assert.Equal(no == "VEN·050", spec.ReduceManaPerRune);
        var self = EmpowerParser.Self(card.CardEffect);
        Assert.Equal(no == "VEN·043" ? 7 : no == "VEN·070" ? 2 : 0, self.Might);
        Assert.Equal(no == "VEN·050" ? ["法盾", "坚守3"] : no == "VEN·070" ? ["游走"] : Array.Empty<string>(), self.Keywords);
    }

    public static IEnumerable<object[]> Destinations() => Cards.SelectMany(no => new[] { "BASE", "BATTLEFIELD:BF" }.Select(d => new object[] { no, d }));
    [Theory]
    [MemberData(nameof(Destinations))]
    public async Task RealPlayAndActivationPaySeparatelyAndPermanentSelfEffectsSurviveTurnEnd(string no, string destination)
    {
        var initial = Position(no); var driver = await Play(initial, no, destination);
        Assert.Equal(40 - Mana(no), driver.State.RunePools["P1"].Mana);
        Assert.Equal(no == "VEN·070" ? 3 : 4, driver.State.RunePools["P1"].PowerByTrait[RuneTrait.Orange]);
        Assert.Equal(PrintedMight(no), Might(driver.State)); Assert.False(driver.State.CardObjects["UNIT"].IsEmpowered);
        Assert.Equal(no == "VEN·043", driver.State.CardObjects["UNIT"].Tags.Contains(CardObjectTags.Spellshield));
        var generation = driver.State.CardObjects["UNIT"].ObjectGeneration;
        await driver.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
        Assert.False(driver.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(PrintedMight(no), Might(driver.State));
        Assert.Equal(40 - Mana(no) - EmpowerMana(no), driver.State.RunePools["P1"].Mana);
        var activation = Assert.Single(driver.State.StackItems).EmpowerActivation!;
        Assert.Equal(EmpowerMana(no), activation.PaidMana); Assert.Empty(driver.State.StackItems.Single().TargetObjectIds);
        await driver.Top(); Assert.Equal(EmpoweredMight(no), Might(driver.State));
        Assert.True(driver.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(PrintedMight(no), driver.State.CardObjects["UNIT"].Power);
        Assert.True(driver.State.CardObjects["UNIT"].IsExhausted); Assert.Equal(generation, driver.State.CardObjects["UNIT"].ObjectGeneration);
        Assert.Equal(no != "VEN·070", driver.State.CardObjects["UNIT"].Tags.Contains(CardObjectTags.Spellshield));
        Assert.Empty(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers);
        await driver.Act("P1", new EndTurnCommand()); Assert.True(driver.State.CardObjects["UNIT"].IsEmpowered);
        Assert.Equal(EmpoweredMight(no), Might(driver.State)); await driver.Replay(); Export($"permanent-{no}-{destination}", initial, driver);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(5)] [InlineData(11)] [InlineData(12)] [InlineData(13)]
    public async Task ActualRockbearActivationCountsAllControlledFieldRunesAndFloorsItsManaAtZero(int count)
    {
        var initial = Position("VEN·050"); var cards = initial.CardObjects.ToDictionary(); var locations = initial.ObjectLocations.ToDictionary(); var zones = initial.PlayerZones.ToDictionary();
        for (var i = 0; i < count; i++) {
            var id = "CONTROLLED-RUNE-" + i; cards[id] = Printed(id, "UNL-R03", "P1") with { IsExhausted = i % 2 == 0 };
            locations[id] = new("P1", "BASE"); zones["P1"] = zones["P1"] with { Base = [..zones["P1"].Base, id] };
        }
        cards["UNRELEASED"] = Printed("UNRELEASED", "UNL-R03", "P1"); locations["UNRELEASED"] = new("P1", "RUNE_DECK");
        zones["P1"] = zones["P1"] with { RuneDeck = ["UNRELEASED"] };
        cards["OPPONENT-RUNE"] = Printed("OPPONENT-RUNE", "UNL-R03", "P2"); locations["OPPONENT-RUNE"] = new("P2", "BASE");
        zones["P2"] = zones["P2"] with { Base = [..zones["P2"].Base, "OPPONENT-RUNE"] };
        initial = initial with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
        var driver = await Play(initial, "VEN·050"); var before = driver.State.RunePools["P1"].Mana;
        await driver.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
        var context = Assert.Single(driver.State.StackItems).EmpowerActivation!;
        Assert.Equal(count, context.ControlledRunes); Assert.Equal(Math.Max(0, 12 - count), context.PaidMana);
        Assert.Equal(before - Math.Max(0, 12 - count), driver.State.RunePools["P1"].Mana);
        for (var i = 0; i < count; i++) Assert.Equal(i % 2 == 0, driver.State.CardObjects["CONTROLLED-RUNE-" + i].IsExhausted);
        JsonRestore(initial, driver);
        await driver.Top(); Assert.True(driver.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(4, Might(driver.State));
        await driver.Replay(); Export("rockbear-controlled-runes-" + count, initial, driver);
    }

    public static IEnumerable<object[]> ShieldCases() => Cards.SelectMany(no => new[] { false, true }.Select(e => new object[] { no, e }));
    [Theory]
    [MemberData(nameof(ShieldCases))]
    public async Task ARealEnemyReactionPaysPrintedOrEmpowerGrantedWardAtTheActualTargetWindow(string no, bool empowered)
    {
        var initial = Add(Add(Position(no), "OPEN", "VEN·081", "P1"), "ENEMY-SPELL", "UNL-066/219", "P2");
        var driver = await Play(initial, no); if (empowered) await Empower(driver);
        await driver.Act("P1", new PlayCardCommand("OPEN", "VEN·081", ["ALLY"])); await driver.Act("P1", new PassPriorityCommand());
        var expectedTax = no == "VEN·043" || no == "VEN·050" && empowered ? 1 : 0;
        var command = new PlayCardCommand("ENEMY-SPELL", "UNL-066/219", ["UNIT"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P2", PlayCostPreviewTests.Request(driver.State, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(7, quote.Cost!.Mana); Assert.Equal(expectedTax, quote.Cost.GenericPower);
        var before = driver.State.RunePools["P2"]; await driver.Act("P2", command);
        Assert.Equal(before.Mana - 7, driver.State.RunePools["P2"].Mana); Assert.Equal(before.TotalPower - expectedTax, driver.State.RunePools["P2"].TotalPower);
        await driver.Top(); Assert.Equal(PrintedMight(no) - 10, driver.State.CardObjects["UNIT"].Power);
        Assert.Equal(0, Might(driver.State)); // CN143.2.c: negative Might is referenced as zero.
        Assert.Equal(-10, Assert.Single(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers).PowerDelta);
        await driver.Top(); await driver.Replay(); Export($"actual-ward-{no}-{empowered}", initial, driver);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task RealRockbearBattleRolesUseSteadfastOnlyWhenEmpoweredAndDefending(bool empowered, bool defending)
    {
        var initial = Position("VEN·050");
        if (!defending) initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        var driver = await Play(initial, "VEN·050", defending ? "BATTLEFIELD:BF" : "BASE"); if (empowered) await Empower(driver);
        await driver.Act("P1", new EndTurnCommand());
        if (!defending) await driver.Act("P2", new EndTurnCommand());
        var actor = defending ? "P2" : "P1"; var attacker = defending ? "ENEMY" : "UNIT"; var defender = defending ? "UNIT" : "ENEMY"; var field = defending ? "BF" : "OTHER";
        await driver.Act(actor, new MoveUnitCommand(attacker, "BASE", "BATTLEFIELD:" + field, []));
        await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand()); await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        await driver.Act(actor, new DeclareBattleCommand(field, [attacker], [defender], ["COMBAT_ASSIGNMENT"]));
        SaveJournal($"rockbear-battle-role-frontier-{empowered}-{defending}", initial, driver);
        var expected = empowered && defending ? 7 : 4;
        var damage = Assert.Single(driver.Events(), e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "UNIT"));
        Assert.Equal(expected, damage.Event.Payload["combatPower"]); Assert.Equal(expected, damage.Event.Payload["damage"]);
        Assert.Equal(defending ? "DEFENDER" : "ATTACKER", damage.Event.Payload["combatRole"]);
        Assert.Equal(empowered && defending ? 3 : 0, damage.Event.Payload["keywordBonus"]);
        Assert.False(driver.State.BattleState.IsActive); Assert.Equal(4, Might(driver.State));
        Assert.Equal(empowered && defending, driver.State.PlayerZones["P1"].Battlefields.Contains("UNIT"));
        Assert.False(driver.State.CardObjects["UNIT"].IsDefending); Assert.False(driver.State.CardObjects["UNIT"].IsAttacking);
        await driver.Replay(); Export($"rockbear-battle-role-{empowered}-{defending}", initial, driver);
    }

    [Fact]
    public async Task ActualHunterEmpowerEnablesRoamAndThatPermissionPersistsAcrossTurnEnd()
    {
        var initial = Add(Position("VEN·070"), "READY", "UNL-009/219", "P1");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["OTHER"] = initial.CardObjects["OTHER"] with { ControllerId = null } } };
        var driver = await Play(initial, "VEN·070", "BATTLEFIELD:BF");
        await driver.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["UNIT"])); await driver.Top();
        var move = new MoveUnitCommand("UNIT", "BATTLEFIELD:BF", "BATTLEFIELD:OTHER", ["ROAM"]);
        await OptionalEntryTargetBoundaryTests.Reject(driver.State, "P1", move);
        await Empower(driver); Assert.Equal(6, Might(driver.State)); var generation = driver.State.CardObjects["UNIT"].ObjectGeneration;
        await driver.Act("P1", move); await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand()); await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        await driver.Act("P1", new EndTurnCommand()); await driver.Act("P2", new EndTurnCommand());
        Assert.True(driver.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(6, Might(driver.State));
        Assert.Empty(driver.State.CardObjects["UNIT"].UntilEndOfTurnEffects);
        await driver.Act("P1", new MoveUnitCommand("UNIT", "BATTLEFIELD:OTHER", "BATTLEFIELD:BF", ["ROAM"]));
        Assert.Equal("BF", driver.State.ObjectLocations["UNIT"].BattlefieldObjectId); Assert.Equal(generation, driver.State.CardObjects["UNIT"].ObjectGeneration);
        await driver.Replay(); Export("hunter-permanent-roam", initial, driver);
    }

    public static IEnumerable<object[]> ReturnCases() => Cards.SelectMany(no => new[] { false, true }.Select(b => new object[] { no, b }));
    [Theory]
    [MemberData(nameof(ReturnCases))]
    public async Task ARealReturnBeforeOrAfterEmpowerResolutionClearsStatusAndCannotEmpowerTheNewHandIncarnation(string no, bool before)
    {
        var initial = Add(Position(no), "RETURN", "OGN·104/298", "P1"); var driver = await Play(initial, no);
        var generation = driver.State.CardObjects["UNIT"].ObjectGeneration;
        await driver.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
        PendingSourceViews(driver.State, no, "UNIT"); JsonRestore(initial, driver); if (!before) await driver.Top();
        try {
            await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await driver.Top();
            if (before) {
                PendingSourceViews(driver.State, no, "HIDDEN"); JsonRestore(initial, driver); RejectForgedSourceViews(driver);
                Assert.Equal(generation, driver.State.StackItems.Single().EmpowerActivation!.Source.Generation); await driver.Top();
            }
        } finally { SaveJournal($"empower-return-frontier-{no}-{before}", initial, driver); }
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Hand); Assert.False(driver.State.CardObjects["UNIT"].IsEmpowered);
        Assert.Equal(PrintedMight(no), driver.State.CardObjects["UNIT"].Power); Assert.True(driver.State.CardObjects["UNIT"].ObjectGeneration > generation);
        Assert.Null(driver.State.CardObjects["UNIT"].RuleDerivedTags); await driver.Replay(); Export($"empower-return-{no}-{before}", initial, driver);
    }

    public static IEnumerable<object[]> IllegalActivations() => Cards.SelectMany(no => new[] { "mana", "controller", "repeated", "target" }.Select(b => new object[] { no, b }));
    [Theory]
    [MemberData(nameof(IllegalActivations))]
    public async Task EachNewConsumersIllegalActivationCannotPayExhaustOrPartlyEnableSelfEffects(string no, string branch)
    {
        var initial = Position(no);
        if (branch == "mana") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = initial.RunePools["P1"] with { Mana = Mana(no) + EmpowerMana(no) - 1 } } };
        if (branch == "controller") initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["ENEMY"] = Printed("ENEMY", no, "P2") } };
        var driver = await Play(initial, no); if (branch == "repeated") await Empower(driver);
        await OptionalEntryTargetBoundaryTests.Reject(driver.State, "P1", new ActivateAbilityCommand(branch == "controller" ? "ENEMY" : "UNIT", "EMPOWER:0:0", branch == "target" ? ["UNIT"] : []));
        await driver.Replay(); Export($"illegal-activation-{no}-{branch}", initial, driver);
    }

    [Theory]
    [InlineData("VEN·043")] [InlineData("VEN·050")] [InlineData("VEN·070")]
    public async Task TheRealTargetFilteredCounterCannotTreatTheEmpowerSourceAsATarget(string no)
    {
        var initial = Add(Position(no), "COUNTER", "SFD·045/221", "P2"); var driver = await Play(initial, no);
        await driver.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); var id = Assert.Single(driver.State.StackItems).StackItemId;
        await driver.Act("P1", new PassPriorityCommand());
        await OptionalEntryTargetBoundaryTests.Reject(driver.State, "P2", new PlayCardCommand("COUNTER", "SFD·045/221", [id]));
        await driver.Act("P2", new PassPriorityCommand()); Assert.True(driver.State.CardObjects["UNIT"].IsEmpowered);
        await driver.Replay(); Export("target-filtered-counter-" + no, initial, driver);
    }

    [Theory]
    [InlineData("paid-mana")] [InlineData("controlled-runes")] [InlineData("coordinated-cost-and-pool")]
    public async Task TheOriginalAcceptedLogRejectsInventedRockbearDiscountSnapshots(string mutation)
    {
        var initial = Position("VEN·050"); var driver = await Play(initial, "VEN·050");
        await driver.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); JsonRestore(initial, driver);
        var item = Assert.Single(driver.State.StackItems); var context = item.EmpowerActivation!;
        Assert.Equal(0, context.ControlledRunes); Assert.Equal(12, context.PaidMana); Assert.Equal(24, driver.State.RunePools["P1"].Mana);
        var changed = mutation == "paid-mana" ? context with { PaidMana = 13 }
            : mutation == "controlled-runes" ? context with { ControlledRunes = 1 }
            : context with { ControlledRunes = 1, PaidMana = 11 };
        var forged = driver.State with { StackItems = [item with { EmpowerActivation = changed }] };
        if (mutation == "coordinated-cost-and-pool") forged = forged with { RunePools = new Dictionary<string, RunePool>(forged.RunePools) {
            ["P1"] = forged.RunePools["P1"] with { Mana = 25 } } };
        else Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("empower", StringComparison.OrdinalIgnoreCase));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), forged, new CoreRuleEngine(), default, driver.Events());
        Assert.False(replay.IsMatch); Assert.NotEmpty(replay.Errors);
        Assert.Equal(12, driver.State.StackItems.Single().EmpowerActivation!.PaidMana); Assert.Equal(24, driver.State.RunePools["P1"].Mana);
        await driver.Replay(); Export("discount-forgery-rejected-" + mutation, initial, driver);
    }

    [Fact]
    public async Task ARealCrabDeathRevealsTheReturnedHandTemporarilyWithoutChangingTheOldEmpowerBinding()
    {
        const string no = "VEN·043";
        var initial = Add(Add(Position(no), "RETURN", "OGN·104/298", "P1"), "REMOVE", "SFD·163/221", "P2");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["CRAB"] = Printed("CRAB", "UNL-053/219", "P2") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["CRAB"] = new("P2", "BASE") },
            PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P2" ? p.Value with { Base = [..p.Value.Base, "CRAB"] } : p.Value) };
        var driver = await Play(initial, no); await driver.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
        var originalGeneration = driver.State.StackItems.Single().EmpowerActivation!.Source.Generation;
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await driver.Top();
        PendingSourceViews(driver.State, no, "HIDDEN");
        if (driver.State.PriorityPlayerId == "P1") await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("REMOVE", "SFD·163/221", ["CRAB", "ENEMY"]));
        await driver.Top();
        await driver.Top();
        var reveal = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal("REVEALED_HAND_EFFECT", reveal.ChoiceWindow); Assert.Equal("P2", reveal.PlayerId);
        Assert.Equal(driver.State.CardObjects["UNIT"].ObjectGeneration, reveal.HandContext!.Cards["UNIT"]);
        PendingSourceViews(driver.State, no, "UNIT"); JsonRestore(initial, driver);
        await driver.Act("P2", new ChooseCardsCommand(reveal.ChoiceId, reveal.ChoiceWindow, []));
        PendingSourceViews(driver.State, no, "HIDDEN"); JsonRestore(initial, driver);
        Assert.Equal(originalGeneration, driver.State.StackItems.Single().EmpowerActivation!.Source.Generation);
        Assert.True(driver.State.CardObjects["UNIT"].ObjectGeneration > originalGeneration);
        await driver.Top(); Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Hand); Assert.False(driver.State.CardObjects["UNIT"].IsEmpowered);
        await driver.Replay(); Export("real-hand-reveal-during-pending-empower", initial, driver);
    }

    internal static MatchState Position(string no)
    {
        var s = VenUnitEntryBasicTests.Position(no); return s with { RoomId = "VEN-EMPOWERED-SELF-CONSUMER",
            RunePools = new Dictionary<string, RunePool>(s.RunePools) {
                ["P1"] = new(40, 0, new Dictionary<string, int> { [RuneTrait.Orange] = 4, [RuneTrait.Red] = 4 }),
                ["P2"] = new(20, 20, new Dictionary<string, int> { [RuneTrait.Green] = 4 }) } };
    }
    private static CardObjectState Printed(string id, string no, string owner) => OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner);
    private static MatchState Add(MatchState s, string id, string no, string owner) => s with {
        CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { [id] = Printed(id, no, owner) },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { [id] = new(owner, "HAND") },
        PlayerZones = s.PlayerZones.ToDictionary(p => p.Key, p => p.Key == owner ? p.Value with { Hand = [..p.Value.Hand, id] } : p.Value) };
    private static async Task<Driver> Play(MatchState initial, string no, string destination = "BASE")
    { var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: destination)); return driver; }
    private static async Task Empower(Driver driver)
    { await driver.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await driver.Top(); }
    private static int Might(MatchState s) => CleaveTemporaryAssaultTests.Might(s, "P1", "UNIT");
    private static void PendingSourceViews(MatchState state, string no, string opponentSource)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var own = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)["P1"], options).GetProperty("stack")
            .EnumerateArray().Single(s => s.GetProperty("effectKind").GetString() == "EMPOWER_ACTIVATION");
        Assert.Equal("UNIT", own.GetProperty("sourceObjectId").GetString());
        foreach (var view in new[] { ResolutionResult.BuildSnapshots(state)["P2"], ResolutionResult.BuildSpectatorSnapshot(state) }) {
            var row = JsonSerializer.SerializeToElement(view, options).GetProperty("stack").EnumerateArray()
                .Single(s => s.GetProperty("effectKind").GetString() == "EMPOWER_ACTIVATION");
            Assert.Equal(opponentSource, row.GetProperty("sourceObjectId").GetString());
            Assert.Equal(opponentSource == "HIDDEN" ? "HIDDEN" : "VISIBLE", row.GetProperty("sourceVisibility").GetString());
            Assert.Equal(no, row.GetProperty("cardNo").GetString()); Assert.False(row.TryGetProperty("empowerActivation", out _));
        }
    }
    private static void RejectForgedSourceViews(Driver driver)
    {
        var s = driver.State; var sequence = driver.Commands().Last().CompletedEventSequence; var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        SnapshotDto Forged(SnapshotDto view, string mutation) {
            var node = JsonSerializer.SerializeToNode(view, json)!;
            if (mutation == "revealed-source") { node["stack"]![0]!["sourceObjectId"] = "UNIT"; node["stack"]![0]!["sourceVisibility"] = "VISIBLE"; }
            if (mutation == "missing-id") node["stack"]![0]!.AsObject().Remove("sourceObjectId");
            if (mutation == "null-id") node["stack"]![0]!["sourceObjectId"] = null;
            if (mutation == "duplicate-row") node["stack"]!.AsArray().Add(node["stack"]![0]!.DeepClone());
            if (mutation == "missing-row") node["stack"]!.AsArray().Clear();
            if (mutation == "null-stack") node["stack"] = null;
            if (mutation == "null-visibility") node["stack"]![0]!["sourceVisibility"] = null;
            return JsonSerializer.Deserialize<SnapshotDto>(node.ToJsonString(), json)!;
        }
        var mutations = s.StackItems.Single().CardNo == "VEN·043"
            ? new[] { "revealed-source", "missing-id", "null-id", "duplicate-row", "missing-row", "null-stack", "null-visibility" }
            : new[] { "revealed-source" };
        foreach (var target in new[] { "P2", "spectator" }) {
          foreach (var mutation in mutations) {
            var snapshots = ResolutionResult.BuildSnapshots(s).ToDictionary(); var prompts = ResolutionResult.BuildPrompts(s);
            if (target == "P2") snapshots["P2"] = Forged(snapshots["P2"], mutation);
            var views = s.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, s.Tick, sequence, snapshots[p], s.Tick, sequence, prompts[p]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(s.RoomId, s.Tick, sequence, [], s);
            if (target == "spectator") spectator = spectator with { SpectatorSnapshot = Forged(spectator.SpectatorSnapshot, mutation) };
            var errors = MatchRecoveryValidator.Validate(s.RoomId, sequence, driver.Commands(), driver.Events(), views, s, s.Tick, spectator);
            Assert.Contains(errors, e => e.Contains(mutation == "null-stack" ? "stack" : "source", StringComparison.OrdinalIgnoreCase));
          }
        }
    }
    private static void JsonRestore(MatchState initial, Driver driver)
    {
        var state = driver.State; var sequence = driver.Commands().Last().CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, driver.Commands(), driver.Events(), views, [], state, initial, spectator) { OpeningSeed = initial.Seed };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, options), options)!;
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(decoded.AuthoritativeState!));
        Assert.Empty(MatchRecoveryValidator.Validate(decoded.RoomId, decoded.LastEventSequence, decoded.Commands, decoded.Events,
            decoded.PlayerViews, decoded.AuthoritativeState, decoded.CurrentTick, decoded.SpectatorReplayFrame));
        var restored = MatchSession.Restore(decoded, new CoreRuleEngine(), driver);
        foreach (var player in state.Seats.Keys) {
            Assert.Equal(MatchStateHasher.HashValue(snapshots[player]), MatchStateHasher.HashValue(restored.SnapshotFor(player)));
            Assert.Equal(MatchStateHasher.HashValue(prompts[player]), MatchStateHasher.HashValue(restored.PromptFor(player)));
        }
    }
    private static void Export(string name, MatchState initial, Driver driver)
    {
        SaveJournal(name, initial, driver);
        JsonRestore(initial, driver);
    }
    private static void SaveJournal(string name, MatchState initial, Driver driver)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN_EMPOWERED_SELF_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, name.Replace('·', '-') + ".json"), JsonSerializer.Serialize(new {
            initial, final = driver.State, commands = driver.Commands(), events = driver.Events()
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
}
