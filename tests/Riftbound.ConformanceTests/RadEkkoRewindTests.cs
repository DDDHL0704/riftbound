using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadEkkoRewindTests
{
    private const string Ability = "RAD_021_SWIFT_DISCARD_BANISH_REPLAY";
    private static ActivateAbilityCommand Activate(string cost = "COST")
        => new("UNIT", Ability, [], OptionalCosts: ["DISCARD_HAND_CARD:" + cost]);

    private static MatchState Position(string no = "RAD·021/167", string owner = "P1")
    {
        var s = VenUnitEntryBasicTests.Position(no);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["UNIT"] = s.CardObjects["UNIT"] with { OwnerId = owner, IsExhausted = true, Damage = 1 } },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                ["P1"] = s.PlayerZones["P1"] with { Hand = [], Base = [..s.PlayerZones["P1"].Base, "UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["UNIT"] = new("P1", "BASE") },
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(2, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }) } };
        return OfficialCopyIdentityTests.AddSpell(s, "COST", "UNL-009/219") with { RunePools = s.RunePools };
    }

    [Theory]
    [InlineData("RAD·021/167", 39512L)] [InlineData("RAD·021a/167", 39513L)] [InlineData("RAD·177/167", 39694L)]
    public async Task EveryOfficialPrintPaysFiveAndRedAndEntersReadyWithAssault(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("逆时而行", card.SubTitle);
        var g = new Driver(VenUnitEntryBasicTests.Position(no));
        await g.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.Equal(15, g.State.RunePools["P1"].Mana); Assert.Equal(19, g.State.RunePools["P1"].TotalPower);
        Assert.False(g.State.CardObjects["UNIT"].IsExhausted); Assert.Equal(4, g.State.CardObjects["UNIT"].Power);
        Assert.Contains("强攻", g.State.CardObjects["UNIT"].Tags); await g.Replay();
    }

    [Theory]
    [InlineData("RAD·021/167", "P1")] [InlineData("RAD·021a/167", "P1")] [InlineData("RAD·177/167", "P1")]
    [InlineData("RAD·021/167", "P2")] [InlineData("RAD·177/167", "P2")]
    public async Task DiscardAndRuneCostsPrecedeBanishmentAndTheSkillControllerReplaysEvenAStolenCard(string no, string owner)
    {
        var g = new Driver(Position(no, owner)); var generation = g.State.CardObjects["UNIT"].ObjectGeneration;
        await g.Act("P1", Activate());
        Assert.Contains("COST", g.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("UNIT", g.State.PlayerZones["P1"].Base); Assert.True(g.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(1, g.State.RunePools["P1"].Mana); Assert.Equal(0, g.State.RunePools["P1"].TotalPower);
        await Reject(g.State, "P1", Activate()); // Swift cannot respond to an existing chain.
        await g.Top(); var p = Assert.IsType<PendingEffectPlayState>(g.State.PendingEffectPlay);
        Assert.Equal("P1", p.PlayerId); Assert.Contains("UNIT", g.State.PlayerZones[owner].Banished);
        Assert.Equal(generation + 1, p.Sources["UNIT"]);
        Assert.True(p.IgnoreBaseMana); Assert.True(p.IgnoreBasePower); Assert.False(p.IgnoreAllCosts);
        await Reject(g.State, "P2", new PlayCardCommand("UNIT", no, []));
        await Reject(g.State, "P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:BF"));
        await Reject(g.State, "P1", new ChooseCardsCommand(p.ChoiceId, "EFFECT_PLAY", []));
        foreach (var forged in new[] { p with { PlayerId = "P2" }, p with { IgnoreAllCosts = true },
            p with { Sources = new Dictionary<string, long> { ["UNIT"] = generation } }, p with { SourcePlayerId = owner == "P1" ? "P2" : null } })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(g.State with { PendingEffectPlay = forged }));
        await g.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.Null(g.State.PendingEffectPlay); Assert.Empty(g.State.StackItems);
        Assert.DoesNotContain("UNIT", g.State.PlayerZones[owner].Banished); Assert.Contains("UNIT", g.State.PlayerZones["P1"].Base);
        var unit = g.State.CardObjects["UNIT"];
        Assert.Equal(owner, unit.OwnerId); Assert.Equal("P1", unit.ControllerId); Assert.Equal(generation + 2, unit.ObjectGeneration);
        Assert.False(unit.IsExhausted); Assert.Equal(0, unit.Damage); Assert.Equal(4, unit.Power);
        Assert.Equal(1, g.State.RunePools["P1"].Mana); Assert.Equal(0, g.State.RunePools["P1"].TotalPower); await g.Replay();
    }

    [Theory]
    [InlineData("missing")] [InlineData("enemy")] [InlineData("field")] [InlineData("mana")] [InlineData("power")]
    public async Task InvalidDiscardAndInsufficientResourcesLeaveTheSourceAndHandUnchanged(string branch)
    {
        var s = Position();
        if (branch is "mana" or "power") s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) {
            ["P1"] = branch == "mana" ? new(0, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 })
                : new(2, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 10 }) } };
        await Reject(s, "P1", Activate(branch switch { "missing" => "missing", "enemy" => "P2-DRAW-1", "field" => "ALLY", _ => "COST" }));
    }

    [Fact]
    public async Task ARealImageCopyCeasesOnSelfBanishmentAndCompletesTheSkillWithoutAReplay()
    {
        var initial = OfficialCopyIdentityTests.AddSpell(OfficialCopyIdentityTests.Position("RAD·021/167"), "COST", "UNL-009/219") with { Tick = 0 };
        var g = new Driver(initial);
        await g.Act("P1", new PlayCardCommand("C", "UNL-200/219", ["TARGET"]));
        await g.Top(); await g.Top();
        var token = Assert.Single(g.State.CardObjects.Values, card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo);
        Assert.Equal("RAD·021/167", token.CardNo);
        await g.Act("P1", new ActivateAbilityCommand(token.ObjectId, Ability, [], OptionalCosts: ["DISCARD_HAND_CARD:COST"]));
        await g.Top();
        Assert.False(g.State.CardObjects.ContainsKey(token.ObjectId));
        Assert.False(g.State.ObjectLocations.ContainsKey(token.ObjectId));
        Assert.DoesNotContain(token.ObjectId, g.State.PlayerZones.Values.SelectMany(z => z.Base.Concat(z.Battlefields).Concat(z.Banished)));
        Assert.Null(g.State.PendingEffectPlay); Assert.Empty(g.State.StackItems);
        Assert.Contains("COST", g.State.PlayerZones["P1"].Graveyard);
        Assert.Single(g.Events(), row => row.Event.Kind == "TOKEN_CEASED_TO_EXIST"
            && Equals(row.Event.Payload.GetValueOrDefault("targetObjectId"), token.ObjectId));
        await g.Replay();
    }

    [Fact]
    public async Task ARealCopiedEkkoReplaysThePhysicalUnitAfterItsCopiedFaceEnds()
    {
        var initial = OfficialCopyIdentityTests.AddSpell(ShadySpectaclesTests.Position("RAD·021/167"), "COST", "UNL-009/219");
        var g = new Driver(initial);
        await g.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_YELLOW"]));
        await g.Top();
        var copy = g.State.PendingRuleChoice!.Request;
        await g.Act("P1", new PayCostCommand(copy.Id, "RULE_REPLACEMENT", ["COPY:COPY-SOURCE"]));
        Assert.Equal("RAD·021/167", g.State.CardObjects["HOST"].CardNo);
        Assert.NotNull(g.State.CardObjects["HOST"].AttachmentUnitCopies);
        await g.Act("P1", new ActivateAbilityCommand("HOST", Ability, [], OptionalCosts: ["DISCARD_HAND_CARD:COST"]));
        await g.Top();
        Assert.Equal("OGN·096/298", g.State.CardObjects["HOST"].CardNo);
        Assert.Null(g.State.CardObjects["HOST"].AttachmentUnitCopies);
        Assert.Null(g.State.CardObjects["GEAR"].AttachedToObjectId);
        var pending = Assert.IsType<PendingEffectPlayState>(g.State.PendingEffectPlay);
        Assert.Equal("P1", pending.PlayerId);
        Assert.Equal("OGN·096/298", pending.Parent.SimpleActivation!.ReplayCardNo);
        foreach (var forged in new[] { null, "RAD·021/167", "OGN·001/298" })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(g.State with { PendingEffectPlay = pending with {
                Parent = pending.Parent with { SimpleActivation = pending.Parent.SimpleActivation with { ReplayCardNo = forged } } } }));
        var resources = g.State.RunePools["P1"];
        await g.Act("P1", new PlayCardCommand("HOST", "OGN·096/298", []));
        Assert.Equal(resources, g.State.RunePools["P1"]);
        Assert.Contains("HOST", g.State.PlayerZones["P1"].Base);
        Assert.Equal("OGN·096/298", g.State.CardObjects["HOST"].CardNo);
        await g.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ARealAnimatedArmamentThatCopiesEkkoCanReplayAsItsPhysicalEquipment(bool detachCopyFirst)
    {
        const string equipmentNo = "SFD·016/221";
        var initial = ShadySpectaclesTests.Position("RAD·021/167");
        initial = initial with { Tick = 0,
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", equipmentNo, "P1"),
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "RAD·145/167", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["LEGEND"] = new("P1", "LEGEND") } };
        initial = OfficialCopyIdentityTests.AddSpell(initial, "COST", "UNL-009/219");
        initial = OfficialCopyIdentityTests.AddSpell(initial, "DETACH", "SFD·011/221");
        initial = AnimatedArmamentAttachmentTests.WithMainPhaseResources(initial);
        initial = RadGearTestPosition.Put(initial, "ORI-POWER-0", "UNL-R06", "P1", "BASE");
        foreach (var player in new[] { "P1", "P2" })
            for (var i = 0; i < 4; i++)
                initial = LastBreathDrawBoundaryTests.Add(initial, player + "-DRAW-" + i, "OGN·219/298", player, "MAIN_DECK");
        var g = new Driver(initial);
        await AnimatedArmamentAttachmentTests.TransformAtNextOwnMain(g, "HOST");
        await g.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_YELLOW"])); await g.Top();
        var copy = g.State.PendingRuleChoice!.Request;
        await g.Act("P1", new PayCostCommand(copy.Id, "RULE_REPLACEMENT", ["COPY:COPY-SOURCE"]));
        Assert.Equal("RAD·021/167", g.State.CardObjects["HOST"].CardNo);
        Assert.Contains(CardObjectTags.EquipmentCard, g.State.CardObjects["HOST"].Tags);
        Assert.Equal(5, g.State.CardObjects["HOST"].Power);
        if (detachCopyFirst)
        {
            await g.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"])); await g.Top();
            Assert.Equal(equipmentNo, g.State.CardObjects["HOST"].CardNo);
            Assert.Equal(2, g.State.CardObjects["HOST"].Power);
            Assert.Contains(CardObjectTags.UnitCard, g.State.CardObjects["HOST"].Tags);
            Assert.Contains(CardObjectTags.EquipmentCard, g.State.CardObjects["HOST"].Tags);
            Assert.NotNull(g.State.CardObjects["HOST"].LegendEquipmentUnit);
            Assert.Null(g.State.CardObjects["HOST"].AttachmentUnitCopies);
            await g.Replay(); return;
        }
        await g.Act("P1", new ActivateAbilityCommand("HOST", Ability, [], OptionalCosts: ["DISCARD_HAND_CARD:COST"]));
        await g.Top();
        Assert.Equal(equipmentNo, g.State.CardObjects["HOST"].CardNo);
        Assert.Null(g.State.CardObjects["HOST"].LegendEquipmentUnit);
        Assert.Null(g.State.CardObjects["HOST"].AttachmentUnitCopies);
        Assert.Null(g.State.CardObjects["GEAR"].AttachedToObjectId);
        var pending = Assert.IsType<PendingEffectPlayState>(g.State.PendingEffectPlay);
        Assert.Equal(equipmentNo, pending.Parent.SimpleActivation!.ReplayCardNo);
        Assert.Equal("HOST", Assert.Single(pending.Sources).Key);
        await Reject(g.State, "P1", new PlayCardCommand("GEAR", "VEN·137", []));
        var resources = g.State.RunePools["P1"];
        await g.Act("P1", new PlayCardCommand("HOST", equipmentNo, []));
        Assert.Contains("HOST", g.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(CardObjectTags.UnitCard, g.State.CardObjects["HOST"].Tags);
        Assert.Equal(resources, g.State.RunePools["P1"]);
        Assert.Null(g.State.PendingEffectPlay); Assert.Empty(g.State.StackItems);
        await g.Replay();
    }

    [Fact]
    public async Task ReturningTheSourceInResponseDoesNotBanishTheNewHandObject()
    {
        var s = OfficialCopyIdentityTests.AddSpell(Position(), "RETURN", "OGN·104/298");
        s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(20, 20, new Dictionary<string, int> { [RuneTrait.Red] = 2 }) } };
        var g = new Driver(s); await g.Act("P1", Activate());
        await g.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await g.Top(); await g.Top();
        Assert.Contains("UNIT", g.State.PlayerZones["P1"].Hand); Assert.Null(g.State.PendingEffectPlay);
        Assert.Empty(g.State.PlayerZones["P1"].Banished); await g.Replay();
    }

    [Fact]
    public async Task SwiftActivationAtShowdownFocusReturnsToThatShowdownAfterReplay()
    {
        var g = new Driver(Position());
        await g.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER"));
        Assert.Equal(TimingStates.SpellDuelOpen, g.State.TimingState);
        await g.Act("P1", Activate()); await g.Top();
        await g.Act("P1", new PlayCardCommand("UNIT", "RAD·021/167", []));
        Assert.Equal(TimingStates.SpellDuelOpen, g.State.TimingState); await g.Replay();
    }

    private static async Task Reject(MatchState s, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(s);
        var r = await new CoreRuleEngine().ResolveAsync(s, new("invalid-ekko", player, command.CmdType), command, default);
        Assert.False(r.Accepted); Assert.Empty(r.Events); Assert.Equal(hash, MatchStateHasher.Hash(r.State));
    }
}
