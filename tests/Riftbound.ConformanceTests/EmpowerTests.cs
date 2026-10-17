using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class EmpowerTests
{
    internal static MatchState Position(string cardNo = "VEN·074")
    {
        var s = AttachedRuleTextTests.Position(cardNo, 0);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(cardNo, out var printed));
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["HOST"] = s.CardObjects["HOST"] with { ManaCost = printed.ManaCost } },
            RunePools = new Dictionary<string, RunePool>(s.RunePools) {
                ["P1"] = new(20, 20, new Dictionary<string, int> { ["red"] = 10, ["orange"] = 10 }) } };
        return s with { PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
            ["P1"] = s.PlayerZones["P1"] with {
                Base = s.PlayerZones["P1"].Base.Where(id => id != "HOST").ToArray(),
                Battlefields = s.PlayerZones["P1"].Battlefields.Where(id => id != "HOST").ToArray(),
                Hand = [..s.PlayerZones["P1"].Hand, "HOST"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["HOST"] = new("P1", "HAND") } };
    }

    internal static async Task<MatchState> Enter(string cardNo = "VEN·074")
    {
        var played = await Act(Position(cardNo), "P1", new PlayCardCommand("HOST", cardNo, []));
        Assert.False(played.State.CardObjects["HOST"].IsEmpowered);
        var entered = played.State.StackItems.Count == 0 ? played.State : (await Top(played.State)).State;
        Assert.Contains("HOST", entered.PlayerZones["P1"].Base);
        Assert.False(entered.CardObjects["HOST"].IsEmpowered);
        return entered;
    }

    internal static Task<ResolutionResult> Activate(MatchState s, int alternative = 0, IReadOnlyList<string>? costs = null)
        => Act(s, "P1", new ActivateAbilityCommand("HOST", $"EMPOWER:0:{alternative}", [], costs));

    internal static MatchState Runes(MatchState s, int count, string trait = "red")
    {
        var cards = s.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var ids = Enumerable.Range(0, count).Select(i => "EMPOWER-RUNE-" + i).ToArray();
        foreach (var id in ids) cards[id] = new(id, cardNo: trait == "red" ? "UNL-R01" : "UNL-R04",
            ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.RuneCard, "COLOR:" + trait]);
        return s with { CardObjects = cards,
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                ["P1"] = s.PlayerZones["P1"] with { Base = s.PlayerZones["P1"].Base
                    .Where(id => !s.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)).Concat(ids).ToArray() } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations
                .Where(p => !p.Key.StartsWith("EMPOWER-RUNE-")))
                .Concat(ids.Select(id => new KeyValuePair<string, ObjectLocationState>(id, new("P1", "BASE")))).ToDictionary(p => p.Key, p => p.Value) };
    }

    [Theory]
    [InlineData("VEN·001", 0, 2, "", 0, "法盾", "强攻2", 6)]
    [InlineData("VEN·014", 0, 2, "red", 1, "强攻3", "", 2)]
    [InlineData("VEN·030", 0, 3, "", 0, "法盾", "坚守3", 3)]
    [InlineData("VEN·074", 0, 1, "", 0, "", "", 3)]
    [InlineData("VEN·074", 1, 0, "orange", 1, "", "", 3)]
    public async Task RealPlayThenActivationPaysTheSelectedCostAndOnlyResolutionEnablesSelfEffects(
        string cardNo, int alternative, int mana, string trait, int power, string first, string second, int might)
    {
        var s = await Enter(cardNo); var before = s.RunePools["P1"];
        Assert.DoesNotContain(CardObjectTags.Spellshield, s.CardObjects["HOST"].Tags);
        Assert.False(CurrentPowerTargetTests.Snapshot(s).GetProperty("players").GetProperty("P1")
            .GetProperty("objects").GetProperty("HOST").TryGetProperty("isEmpowered", out _));
        var prompt = ResolutionResult.BuildPrompts(s)["P1"];
        Assert.Contains(prompt.Candidates!, c => c.Action == CommandTypes.ActivateAbility && c.Sources!.Any(x => x.Id == "HOST"));
        var activated = await Activate(s, alternative);
        Assert.Equal(before.Mana - mana, activated.State.RunePools["P1"].Mana);
        Assert.Equal(before.TotalPower - power, activated.State.RunePools["P1"].TotalPower);
        if (trait.Length > 0) Assert.Equal(before.PowerByTrait[trait] - power, activated.State.RunePools["P1"].PowerByTrait[trait]);
        Assert.False(activated.State.CardObjects["HOST"].IsEmpowered);
        Assert.DoesNotContain(activated.Events, e => e.Kind == "OBJECT_EMPOWERED");
        Assert.Empty(Assert.Single(activated.State.StackItems).TargetObjectIds);
        Assert.Equal(mana, activated.State.StackItems.Single().EmpowerActivation!.PaidMana);
        Restore(activated.State); ArmamentMightTests.PublicRecovery(activated.State);
        var done = await Top(activated.State);
        Assert.True(done.State.CardObjects["HOST"].IsEmpowered);
        Assert.Single(done.Events, e => e.Kind == "OBJECT_EMPOWERED");
        if (first.Length > 0) Assert.Contains(first, done.State.CardObjects["HOST"].Tags);
        if (second.Length > 0) Assert.Contains(second, done.State.CardObjects["HOST"].Tags);
        Assert.DoesNotContain(CardObjectTags.Boon, done.State.CardObjects["HOST"].Tags);
        Assert.Equal(might, ArmamentMightTests.Might(done.State));
        Assert.True(CurrentPowerTargetTests.Snapshot(done.State).GetProperty("players").GetProperty("P1")
            .GetProperty("objects").GetProperty("HOST").GetProperty("isEmpowered").GetBoolean());
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
        Assert.DoesNotContain(ResolutionResult.BuildPrompts(done.State)["P1"].Candidates ?? [], c =>
            c.Action == CommandTypes.ActivateAbility && c.Sources!.Any(x => x.Id == "HOST"));
        var rejected = await new CoreRuleEngine().ResolveAsync(done.State, new("again", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []), default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(done.State), MatchStateHasher.Hash(rejected.State));
    }

    [Theory]
    [InlineData("OGN·104/298", "HAND")]
    [InlineData("OGN·229/298", "GRAVEYARD")]
    public async Task LeavingPlayClearsTheStatusAndConditionalLayer(string spell, string zone)
    {
        var s = (await Top((await Activate(await Enter())).State)).State;
        s = AttackDamageDistributionTests.Spell(s, "LEAVE", spell, "P1");
        var cast = await Act(s, "P1", new PlayCardCommand("LEAVE", spell, ["HOST"]));
        var done = (await Top(cast.State)).State;
        Assert.Contains("HOST", zone == "HAND" ? done.PlayerZones["P1"].Hand : done.PlayerZones["P1"].Graveyard);
        Assert.False(done.CardObjects["HOST"].IsEmpowered); Assert.Equal(2, done.CardObjects["HOST"].Power);
        Assert.Null(done.CardObjects["HOST"].RuleDerivedTags);
        Assert.True(done.CardObjects["HOST"].ObjectGeneration > s.CardObjects["HOST"].ObjectGeneration);
        Restore(done); ArmamentMightTests.PublicRecovery(done);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 5)]
    public async Task RuneThresholdUsesControlledFieldRunesAtActivation(int runes, int expectedMana)
    {
        var s = Runes(await Enter("VEN·001"), runes);
        var activated = await Activate(s);
        Assert.Equal(20 - 6 - expectedMana, activated.State.RunePools["P1"].Mana);
        Assert.Equal(runes, activated.State.StackItems.Single().EmpowerActivation!.ControlledRunes);
        Restore(activated.State); ArmamentMightTests.PublicRecovery(activated.State);
        Assert.True((await Top(activated.State)).State.CardObjects["HOST"].IsEmpowered);
    }

    [Theory]
    [InlineData("mana")]
    [InlineData("color")]
    [InlineData("target")]
    [InlineData("alternative")]
    [InlineData("instance")]
    [InlineData("owner")]
    [InlineData("timing")]
    [InlineData("hidden")]
    [InlineData("recycle")]
    public async Task InvalidActivationNeverChangesStateOrPaysAnything(string branch)
    {
        var s = Runes(await Enter("VEN·014"), 1);
        var command = new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []);
        var actor = "P1";
        if (branch == "mana") s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(0, 1, new Dictionary<string,int>{{"red",1}}) } };
        if (branch == "color") s = Runes(s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(20, 0, new Dictionary<string,int>{{"orange",1}}) } }, 0);
        if (branch == "target") command = command with { TargetObjectIds = ["HOST"] };
        if (branch == "alternative") command = command with { AbilityId = "EMPOWER:0:99" };
        if (branch == "instance") command = command with { AbilityInstanceId = "PRINTED:HOST:0:EMPOWER:0:0" };
        if (branch == "owner") actor = "P2";
        if (branch == "timing") s = s with { TimingState = TimingStates.NeutralClosed };
        if (branch == "hidden") s = s with { CardObjects = new Dictionary<string,CardObjectState>(s.CardObjects) { ["HOST"] = s.CardObjects["HOST"] with { IsFaceDown = true } } };
        if (branch == "recycle") command = command with { OptionalCosts = ["RECYCLE_RUNE:EMPOWER-RUNE-0"] };
        var done = await new CoreRuleEngine().ResolveAsync(s, new("bad", actor, CommandTypes.ActivateAbility), command, default);
        Assert.False(done.Accepted); Assert.Empty(done.Events); Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(done.State));
    }

    [Fact]
    public async Task RequiredTypedRuneRecyclingPaysWithoutTreatingTheSourceAsATarget()
    {
        var s = Runes(await Enter("VEN·014"), 1);
        s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(5, 0) } };
        var activated = await Activate(s, costs: ["RECYCLE_RUNE:EMPOWER-RUNE-0"]);
        Assert.Equal(3, activated.State.RunePools["P1"].Mana);
        Assert.DoesNotContain("EMPOWER-RUNE-0", activated.State.PlayerZones["P1"].Base);
        Assert.Contains("EMPOWER-RUNE-0", activated.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(1, activated.State.StackItems.Single().EmpowerActivation!.ControlledRunes);
        Assert.Empty(activated.State.StackItems.Single().TargetObjectIds);
        Restore(activated.State); ArmamentMightTests.PublicRecovery(activated.State);
        Assert.True((await Top(activated.State)).State.CardObjects["HOST"].IsEmpowered);
    }

    [Fact]
    public async Task QuickCounterCannotCounterEmpowerBecauseItHasNoFriendlyTargets()
    {
        var s = await Enter("VEN·030");
        s = OfficialHoldSequenceTests.AddUnit(s, "FRIEND", "OGN·001/298", "F", "P2");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["F"] = s.CardObjects["F"] with { ControllerId = "P2" } } };
        s = AttackDamageDistributionTests.Spell(s, "RESPONSE", "SFD·045/221", "P2");
        s = s with { RunePools = new Dictionary<string,RunePool>(s.RunePools) { ["P2"] = new(20,20,new Dictionary<string,int>{{"blue",20}}) } };
        var activated = await Activate(s); var item = Assert.Single(activated.State.StackItems);
        var passed = await Act(activated.State, "P1", new PassPriorityCommand());
        var countered = await new CoreRuleEngine().ResolveAsync(passed.State, new("bad-quick-counter", "P2", CommandTypes.PlayCard),
            new PlayCardCommand("RESPONSE", "SFD·045/221", [item.StackItemId]), default);
        Assert.False(countered.Accepted); Assert.Empty(countered.Events);
        Assert.Equal(MatchStateHasher.Hash(passed.State), MatchStateHasher.Hash(countered.State));
        var done = await Act(passed.State, "P2", new PassPriorityCommand());
        Assert.Empty(done.State.StackItems); Assert.Contains("HOST", done.State.PlayerZones["P1"].Base);
        Assert.True(done.State.CardObjects["HOST"].IsEmpowered); Assert.Equal(14, done.State.RunePools["P1"].Mana);
        Assert.DoesNotContain(done.Events, e => e.Kind == "STACK_ITEM_COUNTERED");
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task RepulseCannotCounterEmpowerBecauseItsSourceIsNotATarget()
    {
        var s = await Enter();
        s = OfficialHoldSequenceTests.AddUnit(s, "FRIEND", "OGN·001/298", "F", "P2");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["F"] = s.CardObjects["F"] with { ControllerId = "P2" } },
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P2"] = new(20,20) } };
        s = AttackDamageDistributionTests.Spell(s, "RESPONSE", "UNL-106/219", "P2");
        var activated = await Activate(s); var item = Assert.Single(activated.State.StackItems);
        var passed = await Act(activated.State, "P1", new PassPriorityCommand());
        var done = await new CoreRuleEngine().ResolveAsync(passed.State, new("bad-repulse", "P2", CommandTypes.PlayCard),
            new PlayCardCommand("RESPONSE", "UNL-106/219", ["FRIEND", item.StackItemId]), default);
        Assert.False(done.Accepted); Assert.Empty(done.Events); Assert.Equal(MatchStateHasher.Hash(passed.State), MatchStateHasher.Hash(done.State));
        Assert.True((await Act(passed.State, "P2", new PassPriorityCommand())).State.CardObjects["HOST"].IsEmpowered);
    }

    [Theory]
    [InlineData("mana")]
    [InlineData("runes")]
    [InlineData("generation")]
    [InlineData("tick")]
    [InlineData("revision")]
    [InlineData("card")]
    [InlineData("target")]
    [InlineData("effect")]
    public async Task RecoveryRejectsForgedCapturedEmpowerFields(string field)
    {
        var s = (await Activate(await Enter("VEN·001"))).State;
        var item = s.StackItems.Single(); var c = item.EmpowerActivation!;
        if (field == "mana") c = c with { PaidMana = c.PaidMana + 1 };
        if (field == "runes") c = c with { ControlledRunes = -1 };
        if (field == "generation") c = c with { Source = new("HOST", 999) };
        if (field == "tick") c = c with { AcquiredTick = c.AcquiredTick + 1 };
        if (field == "revision") c = c with { FaceRevision = 999 };
        if (field == "card") c = c with { CardNo = "VEN·014" };
        if (field == "target") item = item with { TargetObjectIds = ["HOST"] };
        if (field == "effect") item = item with { EffectKind = "FORGED" };
        var bad = s with { StackItems = [item with { EmpowerActivation = c }] };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(bad), e => e.Contains("empower"));
        Restore(s); ArmamentMightTests.PublicRecovery(s);
    }

    [Fact]
    public async Task CopyingAnEmpoweredUnitCopiesPrintedTraitsWithoutCopyingItsStatus()
    {
        var s = ShadySpectaclesTests.Position("VEN·074");
        var empoweredSource = await Act(s, "P1", new ActivateAbilityCommand("COPY-SOURCE", "EMPOWER:0:0", []));
        s = (await Top(empoweredSource.State)).State;
        Assert.True(s.CardObjects["COPY-SOURCE"].IsEmpowered);
        s = await ShadySpectaclesTests.Attach(s);
        Assert.Equal("VEN·074", s.CardObjects["HOST"].CardNo);
        Assert.False(s.CardObjects["HOST"].IsEmpowered); Assert.Equal(2, ArmamentMightTests.Might(s));
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(s)["P1"].Candidates!, c => c.Action == CommandTypes.ActivateAbility);
        var rows = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
        var acquired = Assert.Single(rows, row => (string?)row["sourceObjectId"] == "HOST" && (string?)row["abilityId"] == "EMPOWER:0:0");
        var id = Assert.IsType<string>(acquired["abilityInstanceId"]);
        var done = await Top((await Act(s, "P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []) { AbilityInstanceId = id })).State);
        Assert.True(done.State.CardObjects["HOST"].IsEmpowered); Assert.Equal(3, ArmamentMightTests.Might(done.State));
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task AStillUnempoweredUnitCanResolveAnAcquiredAbilityAfterItsTextGearDetaches()
    {
        var s = (await AttachedRuleTextTests.Attach(AttachedRuleTextTests.Position("VEN·074", 0))).State;
        var copy = s.CardObjects["GEAR"].AttachedRuleText!;
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(s)["P1"].Candidates!, c => c.Action == CommandTypes.ActivateAbility);
        var rows = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
        var acquired = Assert.Single(rows, row => row.TryGetValue("copyEquipmentObjectId", out var gear)
            && (string?)gear == "GEAR" && (string?)row["abilityId"] == "EMPOWER:0:0");
        var id = Assert.IsType<string>(acquired["abilityInstanceId"]);
        var activated = await Act(s, "P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []) { AbilityInstanceId = id });
        Assert.Equal(copy.Revision, activated.State.StackItems.Single().EmpowerActivation!.Origin!.Revision);
        var response = AttachedRuleTextTests.WithSpell(activated.State, "P1", "SFD·011/221");
        var detached = await Top((await Act(response, "P1", new PlayCardCommand("SPELL", "SFD·011/221", ["HOST", "GEAR"]))).State);
        Assert.Null(detached.State.CardObjects["GEAR"].AttachedRuleText);
        Assert.False(detached.State.CardObjects["HOST"].IsEmpowered); Assert.Single(detached.State.StackItems);
        Restore(detached.State); ArmamentMightTests.PublicRecovery(detached.State);
        var done = await Top(detached.State);
        Assert.True(done.State.CardObjects["HOST"].IsEmpowered); Assert.Equal(3, ArmamentMightTests.Might(done.State));
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task TurnCleanupPreservesEmpowerStatusAndItsContinuousMight()
    {
        var s = (await Top((await Activate(await Enter())).State)).State;
        var ended = await Act(s, "P1", new EndTurnCommand());
        Assert.True(ended.State.CardObjects["HOST"].IsEmpowered); Assert.Equal(3, ArmamentMightTests.Might(ended.State));
        Restore(ended.State); ArmamentMightTests.PublicRecovery(ended.State);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("missing")]
    [InlineData("string")]
    public async Task RecoveryRejectsIncorrectEmpoweredStatusInPlayerProjection(string mutation)
    {
        var s = (await Top((await Activate(await Enter())).State)).State;
        var snapshots = ResolutionResult.BuildSnapshots(s).ToDictionary(p => p.Key, p => p.Value);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonNode.Parse(JsonSerializer.Serialize(snapshots["P1"], options))!;
        var card = json["players"]!["P1"]!["objects"]!["HOST"]!.AsObject();
        if (mutation == "missing") card.Remove("isEmpowered");
        else card["isEmpowered"] = mutation == "false" ? JsonValue.Create(false) : JsonValue.Create("true");
        snapshots["P1"] = json.Deserialize<SnapshotDto>(options)!;
        var prompts = ResolutionResult.BuildPrompts(s);
        var views = s.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, s.Tick, 0, snapshots[id], s.Tick, 0, prompts[id]));
        Assert.Contains(MatchRecoveryValidator.Validate(s.RoomId, 0, [], [], views, s, s.Tick), e => e.Contains("empowered status"));
        Restore(s); ArmamentMightTests.PublicRecovery(s);
    }

    [Fact]
    public async Task DestroyingTheUnempoweredSourceAsARealReactionCostMakesItsCapturedActivationDoNothing()
    {
        var s = await Enter("VEN·001");
        s = AttachedRuleTextTests.WithSpell(s, "P1", PowerfulSacrificeCostTests.Card);
        var activated = await Activate(s); var mana = activated.State.RunePools["P1"].Mana;
        var paid = await Act(activated.State, "P1", PowerfulSacrificeCostTests.Command());
        Assert.Contains("HOST", paid.State.PlayerZones["P1"].Graveyard);
        Assert.False(paid.State.CardObjects["HOST"].IsEmpowered);
        Assert.True(paid.State.CardObjects["HOST"].ObjectGeneration > activated.State.CardObjects["HOST"].ObjectGeneration);
        Restore(paid.State); ArmamentMightTests.PublicRecovery(paid.State);
        var spell = await Top(paid.State); Assert.Single(spell.State.StackItems);
        var done = await Top(spell.State);
        Assert.False(done.State.CardObjects["HOST"].IsEmpowered);
        Assert.DoesNotContain(done.Events, e => e.Kind == "OBJECT_EMPOWERED");
        Assert.Equal(mana - 1, done.State.RunePools["P1"].Mana);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task ARealExileAndReentryCreatesANewUnempoweredObjectBeforeTheOldAbilityResolves()
    {
        var s = AttachedRuleTextTests.WithSpell(await Enter(), "P1", "UNL-184/219");
        var activated = await Activate(s);
        var played = await Act(activated.State, "P1", new PlayCardCommand("SPELL", "UNL-184/219", ["HOST"]));
        var returned = await Top(played.State);
        Assert.NotNull(returned.State.PendingEffectPlay);
        returned = await Act(returned.State, "P1", new PlayCardCommand("HOST", returned.State.CardObjects["HOST"].CardNo!, [], Destination: "BATTLEFIELD:F"));
        Assert.True(returned.State.CardObjects["HOST"].ObjectGeneration > activated.State.CardObjects["HOST"].ObjectGeneration);
        Assert.Contains("HOST", returned.State.PlayerZones["P1"].Battlefields);
        Assert.False(returned.State.CardObjects["HOST"].IsEmpowered);
        Restore(returned.State); ArmamentMightTests.PublicRecovery(returned.State);
        var done = await Top(returned.State);
        Assert.False(done.State.CardObjects["HOST"].IsEmpowered);
        Assert.DoesNotContain(done.Events, e => e.Kind == "OBJECT_EMPOWERED");
        Assert.Equal(2, ArmamentMightTests.Might(done.State));
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }
}
