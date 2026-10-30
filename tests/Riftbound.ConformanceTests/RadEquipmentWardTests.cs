using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadEquipmentWardTests
{
    private const string Collector = "RAD·037/167";
    private const string Recall = "SFD·135/221";

    [Fact]
    public async Task TheOfficialPrintingPreservesItsWholeTextAndPrintedCharacteristics()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Collector);
        Assert.Equal(39530, card.Id);
        Assert.Equal("警觉的收藏家", card.CardName);
        Assert.Equal(3, card.Energy);
        Assert.Equal(1, card.ReturnEnergy);
        Assert.Equal(4, card.Power);
        Assert.Equal("你的装备具有{{法盾}}。（对手必须支付{{A}}才能将其选作法术或技能的目标。）", card.CardEffect);
        var aura = Assert.Single(RuleTextParser.Parse(card).StaticAuras);
        Assert.Equal(StaticAuraKinds.FriendlyEquipmentKeyword, aura.Kind);
        Assert.Equal(StaticAuraTargetScopes.FriendlyEquipment, aura.TargetScope);
        Assert.Equal(StaticAuraParticipantScopes.FriendlyPublicFieldEquipment, aura.ParticipantScope);
        Assert.Equal(ContinuousEffectLayers.RuleText, aura.Layer);
        Assert.Equal("法盾", aura.GrantedKeyword);
    }

    [Theory]
    [InlineData("具有", "法盾")]
    [InlineData("获得", "法盾2")]
    public void EquipmentKeywordAurasAreParsedFromTheirScopeAndGrantedText(string verb, string keyword)
    {
        var aura = Assert.Single(StaticAuraParser.Parse($"你的装备{verb}{{{{{keyword}}}}}。"));
        Assert.Equal(StaticAuraKinds.FriendlyEquipmentKeyword, aura.Kind);
        Assert.Equal(StaticAuraTargetScopes.FriendlyEquipment, aura.TargetScope);
        Assert.Equal(keyword, aura.GrantedKeyword);
        Assert.Empty(StaticAuraParser.Parse("当我被摧毁时，你的装备具有{{法盾}}。"));
    }

    [Theory]
    [InlineData("BASE")]
    [InlineData("BATTLEFIELD:BF")]
    public async Task PayingThePrintedCostMakesTheAuraActiveImmediatelyOnUnitEntry(string destination)
    {
        var game = new Driver(VenUnitEntryBasicTests.Position(Collector));
        Assert.Empty(Auras(game.State));
        await game.Act("P1", new PlayCardCommand("UNIT", Collector, [], Destination: destination));
        Assert.Equal(17, game.State.RunePools["P1"].Mana);
        Assert.Equal(19, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(destination == "BASE" ? "BASE" : "BATTLEFIELD", game.State.ObjectLocations["UNIT"].Zone);
        Assert.Empty(game.State.StackItems);
        Assert.Null(game.State.PendingCardChoice);
        var aura = Assert.Single(Auras(game.State));
        Assert.Equal("GEAR", aura.TargetObjectId);
        Assert.Equal("UNIT", aura.SourceObjectId);
        Assert.Equal(Collector, aura.SourceCardNo);
        var enemyTurn = WithRecall(game.State, "P2") with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        Assert.Equal(1, TaxQuote(enemyTurn).Cost!.GenericPower);
        await game.Replay();
    }

    public static IEnumerable<object[]> Locations() => new[] { "BASE", "BATTLEFIELD" }
        .SelectMany(source => new[] { "BASE", "BATTLEFIELD" }.Select(target => new object[] { source, target }));

    [Theory]
    [MemberData(nameof(Locations))]
    public async Task EnemyTargetingPaysWardAcrossBothLocationsBeforeTheRecallCanResolve(string source, string target)
    {
        var initial = Position(source, target);
        var quote = TaxQuote(initial);
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(1, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.GenericPower);
        var game = new Driver(initial);
        await game.Act("P2", new PlayCardCommand("SPELL", Recall, ["GEAR"]));
        Assert.Equal(19, game.State.RunePools["P2"].Mana);
        Assert.Equal(19, game.State.RunePools["P2"].TotalPower);
        Assert.Equal(target, game.State.ObjectLocations["GEAR"].Zone);
        Assert.Single(Auras(game.State));
        var paid = Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID");
        Assert.Equal(1, paid.Event.Payload["spellshieldTaxPower"]);
        await game.Top();
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand);
        Assert.Empty(Auras(game.State));
        await game.Replay();
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(2, 2)]
    public void IndependentCollectorsAndTheEquipmentsOwnWardAddTheirValues(int collectors, int printedWard)
    {
        var state = Position();
        if (collectors == 2)
            state = RadUnitEffectTestFixture.Place(HandDiscardObserverTests.Put(state, "SECOND", Collector, "P1", "HAND"),
                "SECOND", "P1", "BATTLEFIELD", "BF");
        if (printedWard > 0) state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { Tags = [..state.CardObjects["GEAR"].Tags, "法盾" + printedWard] } } };
        var quote = TaxQuote(state);
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(collectors + printedWard, quote.Cost!.GenericPower);
        Assert.Equal(collectors, Auras(state).Length);
    }

    [Fact]
    public async Task ActuallyCopyingThePrintedTextAddsAnIndependentWardInstance()
    {
        var state = (await AttachedRuleTextTests.Attach(AttachedRuleTextTests.Position(Collector, 0))).State;
        state = WithRecall(state, "P2") with { TurnPlayerId = "P2", ActivePlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P2"] = new(20, 20) } };
        Assert.Equal(2, TaxQuote(state).Cost!.GenericPower);
        var auras = Auras(state);
        Assert.Equal(2, auras.Length);
        Assert.Equal(2, auras.Select(a => a.EffectId).Distinct().Count());
        Assert.Contains(auras, a => a.EffectId.Contains("ATTACHED:GEAR:", StringComparison.Ordinal));
        Assert.All(auras, a => {
            Assert.Null(a.ParticipantObjectIds);
            Assert.Null(a.SourceDependencyObjectIds);
            Assert.Null(a.TargetDependencyObjectIds);
            Assert.Null(a.ParticipantDependencyObjectIds);
        });
        var game = new Driver(state);
        var power = state.RunePools["P2"].TotalPower;
        await game.Act("P2", new PlayCardCommand("SPELL", Recall, ["GEAR"]));
        Assert.Equal(power - 2, game.State.RunePools["P2"].TotalPower);
        await game.Top();
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedRuleText);
        await game.Replay();
    }

    [Theory]
    [InlineData("hand")]
    [InlineData("graveyard")]
    [InlineData("hidden")]
    [InlineData("enemy")]
    public void AnInactiveOrEnemySourceNeitherProjectsNorChargesTheAura(string reason)
    {
        var state = Position();
        if (reason is "hand" or "graveyard")
            state = RadUnitEffectTestFixture.Place(state, "UNIT", "P1", reason == "hand" ? "HAND" : "GRAVEYARD");
        if (reason == "enemy") state = RadUnitEffectTestFixture.Place(state, "UNIT", "P2", "BASE");
        if (reason == "hidden") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { IsFaceDown = true } } };
        Assert.Empty(Auras(state));
        var quote = TaxQuote(state);
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(0, quote.Cost!.GenericPower);
        if (reason is "hand" or "hidden")
            Assert.DoesNotContain(Collector, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state)["P2"]));
    }

    [Theory]
    [InlineData("HAND")]
    [InlineData("GRAVEYARD")]
    public void EquipmentOutsideTheFieldAndHiddenEquipmentReceiveNoPublicGrant(string zone)
    {
        var state = RadUnitEffectTestFixture.Place(Position(), "GEAR", "P1", zone);
        Assert.Empty(Auras(state));
        state = Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { IsFaceDown = true } } };
        Assert.Empty(Auras(state));
    }

    [Fact]
    public async Task TheCurrentControllerReceivesTheGrantWhilePhysicalOwnershipIsPreserved()
    {
        var state = RadUnitEffectTestFixture.Place(RadUnitEffectTestFixture.Place(Position(), "UNIT", "P2", "BASE"), "GEAR", "P2", "BASE");
        state = WithRecall(state, "P1")
            with { TurnPlayerId = "P1", ActivePlayerId = "P1" };
        var quote = TaxQuote(state, "P1");
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(1, quote.Cost!.GenericPower);
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("SPELL", Recall, ["GEAR"]));
        await game.Top();
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("GEAR", game.State.PlayerZones["P2"].Hand);
        Assert.Equal("P1", game.State.CardObjects["GEAR"].OwnerId);
        Assert.Equal("P2", game.State.CardObjects["UNIT"].ControllerId);
        await game.Replay();
    }

    [Fact]
    public async Task ReturningTheSourceInResponseRemovesTheAuraWithoutRefundingTheAlreadyPaidCost()
    {
        var initial = HandDiscardObserverTests.Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P2", new PlayCardCommand("SPELL", Recall, ["GEAR"]));
        await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"]));
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.Empty(Auras(game.State));
        Assert.Single(game.State.StackItems);
        Assert.Equal(19, game.State.RunePools["P2"].TotalPower);
        await game.Top();
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(19, game.State.RunePools["P2"].TotalPower);
        await game.Replay();
    }

    [Fact]
    public async Task OwnEquipmentAndFriendlyUnitsDoNotPayTheEquipmentOnlyEnemyTax()
    {
        var own = WithRecall(Position(), "P1")
            with { TurnPlayerId = "P1", ActivePlayerId = "P1" };
        Assert.Equal(0, TaxQuote(own, "P1").Cost!.GenericPower);
        var unit = HandDiscardObserverTests.Put(Position(), "BUFF", "OGN·207/298", "P2", "HAND");
        foreach (var target in new[] { "UNIT", "ALLY" })
        {
            var command = new PlayCardCommand("BUFF", "OGN·207/298", [target]);
            var quote = new CoreRuleEngine().PreviewPlayCard(unit, "P2", PlayCostPreviewTests.Request(unit, command));
            Assert.True(quote.CanPay, quote.Message);
            Assert.Equal(0, quote.Cost!.GenericPower);
        }
        var game = new Driver(own);
        await game.Act("P1", new PlayCardCommand("SPELL", Recall, ["GEAR"]));
        Assert.Equal(20, game.State.RunePools["P1"].TotalPower);
        await game.Top();
        await game.Replay();
    }

    [Fact]
    public async Task AnUnpayableWardRejectsBothTheQuoteAndCommandWithoutChangingStateOrViews()
    {
        var state = Position();
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P2"] = new(20, 0) } };
        await Reject(state, "P2", new PlayCardCommand("SPELL", Recall, ["GEAR"]));
    }

    [Theory]
    [InlineData("sourceObjectId")]
    [InlineData("targetObjectId")]
    [InlineData("sourceCardNo")]
    [InlineData("sourcePath")]
    [InlineData("powerDelta")]
    [InlineData("effectId")]
    public void RecoveryRejectsForgedEquipmentAuraValuesInEveryPublicView(string key)
    {
        var state = Position();
        ArmamentMightTests.PublicRecovery(state);
        var snapshots = ResolutionResult.BuildSnapshots(state);
        var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id,
            id => new RecoveredPlayerView(id, state.Tick, 0, snapshots[id], state.Tick, 0, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        foreach (var snapshot in snapshots.Values.Append(spectator.SpectatorSnapshot))
        {
            var effects = Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(snapshot.Timing["continuousEffects"]);
            var aura = Assert.Single(effects, e => Equals(e.GetValueOrDefault("effectKind"), StaticAuraKinds.FriendlyEquipmentKeyword));
            var original = aura[key];
            aura[key] = key == "powerDelta" ? 99 : "FORGED";
            Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(state, views, spectator));
            aura[key] = original;
        }
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(state, views, spectator));
    }

    [Theory]
    [InlineData("mana")]
    [InlineData("power")]
    [InlineData("controller")]
    [InlineData("graveyard")]
    [InlineData("target")]
    public async Task InvalidUnitPlaysPreserveStateAndDoNotActivateTheAura(string reason)
    {
        var state = VenUnitEntryBasicTests.Position(Collector);
        if (reason is "mana" or "power") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = reason == "mana" ? new(2, 20) : new(20, 0) } };
        if (reason == "controller") state = RadUnitEffectTestFixture.Place(state, "UNIT", "P2", "HAND");
        if (reason == "graveyard") state = RadUnitEffectTestFixture.Place(state, "UNIT", "P1", "GRAVEYARD");
        await Reject(state, "P1", new PlayCardCommand("UNIT", Collector, reason == "target" ? ["GEAR"] : []));
        Assert.Empty(Auras(state));
    }

    private static MatchState Position(string source = "BASE", string equipment = "BASE")
    {
        var state = VenUnitEntryBasicTests.Position(Collector);
        state = RadUnitEffectTestFixture.Place(state, "UNIT", "P1", source, source == "BATTLEFIELD" ? "BF" : null);
        state = RadUnitEffectTestFixture.Place(state, "GEAR", "P1", equipment, equipment == "BATTLEFIELD" ? "BF" : null);
        return WithRecall(state, "P2")
            with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
    }

    private static ContinuousEffectState[] Auras(MatchState state)
        => state.ContinuousEffects.Where(e => e.EffectKind == StaticAuraKinds.FriendlyEquipmentKeyword).ToArray();

    private static PlayCostQuoteDto TaxQuote(MatchState state, string player = "P2")
        => new CoreRuleEngine().PreviewPlayCard(state, player, PlayCostPreviewTests.Request(state,
            new PlayCardCommand("SPELL", Recall, ["GEAR"])));

    private static MatchState WithRecall(MatchState state, string player)
        => RadUnitEffectTestFixture.Place(HandDiscardObserverTests.Put(state, "SPELL", Recall, player, "HAND"),
            "SPELL", player, "HAND");

    private static async Task Reject(MatchState state, string player, PlayCardCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var quote = new CoreRuleEngine().PreviewPlayCard(state, player, PlayCostPreviewTests.Request(state, command));
        Assert.False(quote.CanPay);
        Assert.Equal(hash, MatchStateHasher.Hash(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-equipment-ward", player, command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
