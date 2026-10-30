using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadAlternativeAssembleCostTests
{
    private const string Blade = "RAD·150/167";
    private const string PowerCost = "ASSEMBLE_ANY_POWER";
    private static string Exhaust(string id) => "EXHAUST_FRIENDLY_UNIT:" + id;

    private static MatchState Position()
    {
        var state = ArmamentMightTests.Position(gear: Blade);
        return state with { RoomId = "RAD-ALTERNATIVE-ASSEMBLE", CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", Blade, "P1") } };
    }

    [Fact]
    public async Task TheOfficialPrintingPaysItsPrintedPlayCostAndItsArmamentAddsThreeMight()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Blade);
        Assert.Equal(39658, card.Id); Assert.Equal("放逐之锋", card.CardName);
        Assert.Equal(3, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Equal(3, card.Power);
        Assert.Contains("支付{{A}}或让一名友方单位变为休眠状态", card.CardEffect);
        var initial = OutsidePlay(Position(), "GEAR", "HAND");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(3, 1) } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("GEAR", Blade, [], Destination: "BASE"));
        Assert.Equal(RunePool.Empty, game.State.RunePools["P1"]);
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(4, ArmamentMightTests.Might(game.State));
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", [Exhaust("HOST")]));
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        await game.Top();
        Assert.Equal("HOST", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(7, ArmamentMightTests.Might(game.State));
    }

    [Theory]
    [InlineData("mana")] [InlineData("power")] [InlineData("target")]
    public async Task InvalidPrintedPlayCostsAndTargetsCannotChangeTheMatch(string branch)
    {
        var state = OutsidePlay(Position(), "GEAR", "HAND");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = branch == "mana" ? new(2, 1) : new(3, branch == "power" ? 0 : 1) } };
        await Reject(state, new PlayCardCommand("GEAR", Blade, branch == "target" ? ["HOST"] : [], Destination: "BASE"));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ExactlyTheSelectedBaseCostIsPaidBeforeEitherPlayerCanRespond(bool exhaust)
    {
        var initial = Position(); var before = initial.RunePools["P1"].TotalPower;
        var game = new Driver(initial);
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", [exhaust ? Exhaust("HOST") : PowerCost]));
        Assert.Equal(before - (exhaust ? 0 : 1), game.State.RunePools["P1"].TotalPower);
        Assert.Equal(exhaust, game.State.CardObjects["HOST"].IsExhausted);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.NotNull(game.State.PriorityPlayerId);
        Assert.Equal(exhaust ? 1 : 0, game.Events().Count(e => e.Event.Kind == "UNIT_EXHAUSTED"));
        await game.Top(); Assert.Equal("HOST", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(7, ArmamentMightTests.Might(game.State)); await game.Replay();
    }

    [Theory]
    [InlineData("base")] [InlineData("battlefield")] [InlineData("opponent-owned")]
    public async Task AControlledUnitAtAnyLocationCanPayAndWardAddsNoTargetTax(string branch)
    {
        var state = OfficialHoldSequenceTests.AddUnit(Position(), "COST", "OGN·001/298", "F", "P1");
        if (branch != "battlefield") state = LocalDestructionRecallTests.At(state, "COST", "P1", "BASE");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["COST"] = state.CardObjects["COST"] with { OwnerId = branch == "opponent-owned" ? "P2" : "P1",
                    IsExhausted = false, Tags = [CardObjectTags.UnitCard, "法盾2"] } } };
        var game = new Driver(state);
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", [Exhaust("COST")]));
        Assert.True(game.State.CardObjects["COST"].IsExhausted); Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(RunePool.Empty, game.State.RunePools["P1"]);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "WARD_COST_REQUIRED");
        await game.Top(); Assert.Equal("HOST", game.State.CardObjects["GEAR"].AttachedToObjectId);
    }

    [Theory]
    [InlineData("exhausted")] [InlineData("enemy")] [InlineData("gear")] [InlineData("rune")]
    [InlineData("hidden")] [InlineData("hand")] [InlineData("graveyard")] [InlineData("invented")]
    public async Task CostsCannotExhaustAnIneligibleOrHiddenObject(string branch)
    {
        var state = Position(); var id = branch == "invented" ? "INVENTED" : "HOST";
        if (branch is "hand" or "graveyard") state = OutsidePlay(state, id, branch.ToUpperInvariant());
        if (branch != "invented") state = state with { CardObjects = state.CardObjects.ToDictionary(p => p.Key, p => p.Key != id ? p.Value
            : p.Value with { IsExhausted = branch == "exhausted", IsFaceDown = branch == "hidden",
                ControllerId = branch == "enemy" ? "P2" : "P1", Tags = branch switch {
                    "gear" => [CardObjectTags.EquipmentCard], "rune" => [CardObjectTags.RuneCard], _ => p.Value.Tags } }) };
        // Keep a separate legal host when probing a non-unit or a unit outside play.
        state = OfficialHoldSequenceTests.AddUnit(state, "OTHERHOST", "OGN·001/298", "F", "P1");
        await Reject(state, new AssembleEquipmentCommand("GEAR", "OTHERHOST", [Exhaust(id)]));
    }

    [Theory]
    [InlineData("none")] [InlineData("both")] [InlineData("duplicate-power")] [InlineData("duplicate-unit")]
    [InlineData("two-units")] [InlineData("unknown")] [InlineData("unfunded-power")]
    public async Task MissingMixedDuplicateAndUnfundedCostChoicesAreAtomicRejections(string branch)
    {
        var state = Position();
        if (branch == "unfunded-power") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty } };
        string[] costs = branch switch { "none" => [], "both" => [PowerCost, Exhaust("HOST")],
            "duplicate-power" => [PowerCost, PowerCost], "duplicate-unit" => [Exhaust("HOST"), Exhaust("HOST")],
            "two-units" => [Exhaust("HOST"), Exhaust("OTHER")], "unknown" => ["ASSEMBLE_FOR_FREE"], _ => [PowerCost] };
        await Reject(state, new AssembleEquipmentCommand("GEAR", "HOST", costs));
    }

    [Fact]
    public void PromptsOfferOneBaseCostChoiceIncludingTheHostWithItsOwnAmounts()
    {
        var state = Position() with { RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var candidate = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.AssembleEquipment);
        Assert.True(candidate.Enabled);
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var source = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            r => Equals(r["sourceObjectId"], "GEAR"));
        var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(source["optionalCostChoices"]);
        Assert.Equal(new[] { PowerCost, Exhaust("HOST") }, choices.Select(c => c.Id));
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<string>>(source["requiredOptionalCosts"]));
        Assert.Equal(1, source["requiredOptionalCostChoiceCount"]);
        var amounts = JsonSerializer.SerializeToElement(source["costsByChoice"]);
        Assert.Equal(1, amounts.GetProperty(PowerCost).GetProperty("power").GetInt32());
        Assert.Equal(0, amounts.GetProperty(Exhaust("HOST")).GetProperty("power").GetInt32());
        Assert.DoesNotContain(Exhaust("HOST"), JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state)["P2"]));
    }

    [Fact]
    public void WithoutResourcesOrAnActiveUnitTheEquipmentIsNotAnEnabledSource()
    {
        var state = Position();
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = state.CardObjects["HOST"] with { IsExhausted = true } } };
        Assert.DoesNotContain(ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? [],
            c => c.Action == CommandTypes.AssembleEquipment && c.Enabled);
    }

    [Fact]
    public async Task ARealRuneCanFundTheResourceOptionWithoutExhaustingAUnit()
    {
        var state = Position();
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["RUNE"] = new("RUNE", cardNo: "OGN·007/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.RuneCard, "COLOR:red"]) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = [..state.PlayerZones["P1"].Base, "RUNE"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["RUNE"] = new("P1", "BASE") } };
        var game = new Driver(state);
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", [PowerCost, "RECYCLE_RUNE:RUNE"]));
        Assert.Contains("RUNE", game.State.PlayerZones["P1"].RuneDeck);
        Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(RunePool.Empty, game.State.RunePools["P1"]);
        await game.Top(); Assert.Equal("HOST", game.State.CardObjects["GEAR"].AttachedToObjectId);
    }

    [Fact]
    public async Task AHostReturningInResponseDoesNotRefundItsExhaustionCost()
    {
        var state = AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", "P1");
        var game = new Driver(state);
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", [Exhaust("HOST")]));
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        await game.Top(); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Single(game.Events(), e => e.Event.Kind == "UNIT_EXHAUSTED");
    }

    [Fact]
    public async Task ACostUnitReturningInResponseCannotExposeItsNewPrivateIdentityInTheAttachmentEvent()
    {
        var state = OfficialHoldSequenceTests.AddUnit(Position(), "COST", "OGN·001/298", "F", "P1");
        state = LocalDestructionRecallTests.At(state, "COST", "P1", "BASE");
        state = AttackDamageDistributionTests.Spell(state, "RETURN", "OGN·104/298", "P1");
        var game = new Driver(state);
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", [Exhaust("COST")]));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["COST"]));
        await game.Top(); await game.Top();
        Assert.Contains("COST", game.State.PlayerZones["P1"].Hand);
        Assert.Equal("HOST", game.State.CardObjects["GEAR"].AttachedToObjectId);
        var attached = Assert.Single(game.Events(), e => e.Event.Kind == "EQUIPMENT_ATTACHED");
        Assert.DoesNotContain(Exhaust("COST"), JsonSerializer.Serialize(attached.Event));
        Assert.Contains(Exhaust("HIDDEN"), JsonSerializer.Serialize(attached.Event));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TemperedLetsThePlayerChooseFreeAdjustedPowerOrExhaustionAfterResponses(bool exhaust)
    {
        var state = TemperedPlayTriggerAuditTests.PositionFor(gear: Blade);
        state = OfficialHoldSequenceTests.AddUnit(state, "COST", "OGN·001/298", "F", "P1");
        state = LocalDestructionRecallTests.At(state, "COST", "P1", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", Blade, "P1"),
            ["COST"] = state.CardObjects["COST"] with { IsExhausted = false } } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("HOST", "SFD·008/221", []));
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GEAR"]));
        var pool = game.State.RunePools["P1"];
        Assert.False(game.State.CardObjects["COST"].IsExhausted);
        await game.Top();
        choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TEMPERED_BASE_COST", choice.ChoiceWindow);
        Assert.Equal(new[] { PowerCost, Exhaust("COST") }, choice.LegalObjectIds);
        var candidate = Assert.Single(game.PromptFor("P1").Candidates!, c => c.Action == CommandTypes.ChooseCards);
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var labels = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(metadata["cardChoices"]);
        Assert.Equal("支付调整后的装配符能费用", labels.First().Label);
        Assert.Contains("变为休眠状态", labels.Last().Label);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        await Reject(game.State, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [PowerCost, Exhaust("COST")]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [exhaust ? Exhaust("COST") : PowerCost]));
        Assert.Equal(exhaust, game.State.CardObjects["COST"].IsExhausted);
        Assert.Equal(pool, game.State.RunePools["P1"]);
        Assert.Equal("HOST", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Null(game.State.PendingCardChoice); Assert.Null(game.State.PendingPayment);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task ForgedTemperedBaseCostChoicesAndUnboundExhaustionFailRecovery()
    {
        var game = new Driver(TemperedPlayTriggerAuditTests.PositionFor(gear: Blade));
        await game.Act("P1", new PlayCardCommand("HOST", "SFD·008/221", []));
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GEAR"])); await game.Top();
        choice = game.State.PendingCardChoice!;
        var forged = game.State with { PendingCardChoice = choice with { LegalObjectIds = [PowerCost, Exhaust("HOST")] } };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
        var item = game.State.StackItems[^1];
        forged = game.State with { StackItems = [item with { Tempered = item.Tempered! with {
            Costs = new(ExhaustInstead: true, ExhaustedUnit: new("INVENTED", 0)) } }] };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [PowerCost]));
        Assert.Equal("HOST", game.State.CardObjects["GEAR"].AttachedToObjectId);
    }

    [Fact]
    public async Task AForeignLegendStillCannotIncludeThisSignatureEquipment()
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var deck = PreconstructedDeckCatalog.Build(catalog).First().Decklist;
        deck = deck with { MainDeck = [Blade, ..deck.MainDeck.Skip(1)] };
        Assert.Contains(OfficialDeckValidator.Validate(deck, catalog).Errors,
            e => e.Contains(Blade, StringComparison.Ordinal) && e.Contains("传奇英雄标签不匹配", StringComparison.Ordinal));
        var state = MatchReplayInitialStateBuilder.FromSeats("rad-blade-deck-restriction",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var session = new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        var raw = JsonSerializer.SerializeToElement(command, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var result = await session.SubmitDeckAsync("P1", "invalid-blade-deck", command, raw, default);
        Assert.False(result.Accepted); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    private static MatchState OutsidePlay(MatchState state, string id, string zone)
    {
        var zones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Value with {
            Base = p.Value.Base.Where(x => x != id).ToArray(), Battlefields = p.Value.Battlefields.Where(x => x != id).ToArray(),
            Hand = p.Value.Hand.Where(x => x != id).ToArray(), Graveyard = p.Value.Graveyard.Where(x => x != id).ToArray() });
        zones["P1"] = zone == "HAND" ? zones["P1"] with { Hand = [..zones["P1"].Hand, id] }
            : zones["P1"] with { Graveyard = [..zones["P1"].Graveyard, id] };
        return state with { PlayerZones = zones, ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
            [id] = new("P1", zone) } };
    }

    private static async Task Reject(MatchState state, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-alternative-assemble", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
