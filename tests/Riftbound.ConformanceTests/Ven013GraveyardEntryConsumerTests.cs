using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven013GraveyardEntryConsumerTests
{
    private const string No = "VEN·013";
    private const string Text = "如果你的废牌堆中有一张和我同名的卡牌，则我以活跃状态进场。";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData("BASE", 0)] [InlineData("BASE", 1)] [InlineData("BASE", 2)]
    [InlineData("BATTLEFIELD:BF", 0)] [InlineData("BATTLEFIELD:BF", 1)] [InlineData("BATTLEFIELD:BF", 2)]
    public async Task ActualPrintedEntryChecksCurrentGraveWithoutConsumingAnyCopies(string destination, int copies)
    {
        var initial = Position();
        for (var i = 0; i < copies; i++) initial = Put(initial, "MATCH-" + i, No, "P1", "GRAVEYARD");
        var grave = initial.PlayerZones["P1"].Graveyard.ToArray(); var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: destination));
        await Export($"entry-{destination.Replace(':', '-')}-{copies}", initial, run);
        var unit = run.State.CardObjects["UNIT"];
        Assert.Equal(copies == 0, unit.IsExhausted);
        Assert.Equal(5, unit.Power); Assert.Equal(No, unit.CardNo); Assert.Equal("P1", unit.OwnerId); Assert.Equal("P1", unit.ControllerId);
        Assert.Equal(35, run.State.RunePools["P1"].Mana); Assert.Equal(0, run.State.RunePools["P1"].TotalPower);
        Assert.Equal(grave, run.State.PlayerZones["P1"].Graveyard); Assert.Empty(run.State.StackItems);
        Assert.Contains("UNIT", destination == "BASE" ? run.State.PlayerZones["P1"].Base : run.State.PlayerZones["P1"].Battlefields);
        var entry = Assert.Single(run.Events(), e => e.Event.Kind is "UNIT_PLAYED_TO_BASE" or "UNIT_PLAYED_TO_BATTLEFIELD");
        if (copies > 0) {
            Assert.Equal(StaticAbilityKinds.SourceUnitEnterReady, entry.Event.Payload["entryStaticAbilityKind"]);
            Assert.Equal("UNIT", entry.Event.Payload["entryStaticAbilitySourceObjectId"]); Assert.Equal(No, entry.Event.Payload["entryStaticAbilitySourceCardNo"]);
        }
        else Assert.False(entry.Event.Payload.ContainsKey("entryStaticAbilityKind"));
    }

    [Fact]
    public async Task ExactSourceAndItsSharedConditionalEntryMetadataAreRegistered()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(); var card = Assert.Single(catalog.Cards, c => c.CardNo == No);
        Assert.Equal(37698, card.Id); Assert.Equal("暗影刺客", card.CardName); Assert.Equal(Text, card.CardEffect);
        Assert.Equal(5, card.Energy); Assert.Equal(5, card.Power); Assert.Null(card.ReturnEnergy); Assert.Equal([RuneTrait.Red], card.CardColorList);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior)); Assert.True(behavior.PlaysSourceToBaseAsUnit);
        Assert.Equal(5, behavior.ManaCost); Assert.Equal(5, behavior.SourceUnitPower); Assert.Equal(0, behavior.RequiredTargetCount);
        var implemented = OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(catalog.Cards,
            CardBehaviorRegistry.GetAll().Select(b => new ImplementedCardBehavior(b.CardNo, b.EffectKind, b.DisplayName)).ToArray());
        var spec = Assert.Single(BehaviorSpecCatalogBuilder.Build(catalog.Cards, FunctionalUnitBuilder.Build(catalog.Cards), implemented), c => c.CardNo == No);
        var entry = Assert.Single(spec.StaticAbilities, a => a.Kind == StaticAbilityKinds.SourceUnitEnterReady);
        Assert.Equal(Text.TrimEnd('。'), entry.Text); Assert.True(entry.RequiresSameNameCardInControllerGraveyard);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, entry.Status);
    }

    [Theory]
    [InlineData("opponent-grave")] [InlineData("hand")] [InlineData("deck")] [InlineData("base")] [InlineData("field")]
    public async Task ACopyOutsideTheControllersGraveDoesNotMakeTheUnitEnterReady(string zone)
    {
        var owner = zone == "opponent-grave" ? "P2" : "P1";
        var initial = Put(Position(), "MATCH", No, owner, zone switch { "opponent-grave" => "GRAVEYARD", "hand" => "HAND", "deck" => "MAIN_DECK", _ => "BASE" });
        if (zone == "field") initial = LocalDestructionRecallTests.At(initial, "MATCH", "P1", "BATTLEFIELD", "BF");
        var run = new Driver(initial); await run.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: "BASE"));
        await Export("false-zone-" + zone, initial, run); Assert.True(run.State.CardObjects["UNIT"].IsExhausted);
    }

    [Theory]
    [InlineData("mana")] [InlineData("actor")] [InlineData("zone")] [InlineData("field")] [InlineData("target")] [InlineData("extra")]
    public async Task IllegalDeclarationsCannotPayOrCreateAnEntry(string kind)
    {
        var initial = Put(Position(), "MATCH", No, "P1", "GRAVEYARD");
        if (kind == "mana") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(4, 40) } };
        if (kind == "zone") initial = initial with {
            PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with {
                Hand = p.Value.Hand.Where(id => id != "UNIT").ToArray(), Graveyard = [..p.Value.Graveyard, "UNIT"] } : p.Value),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["UNIT"] = new("P1", "GRAVEYARD") } };
        var run = new Driver(initial);
        await Reject(run, kind == "actor" ? "P2" : "P1", new PlayCardCommand("UNIT", No, kind == "target" ? ["ALLY"] : [],
            OptionalCosts: kind == "extra" ? ["ECHO"] : [], Destination: kind == "field" ? "BATTLEFIELD:OTHER" : "BASE"));
        await Export("illegal-" + kind, initial, run); Assert.Single(run.Commands()); Assert.False(run.Commands()[0].Accepted);
    }

    [Fact]
    public async Task TheOrdinaryUnitCannotBePlayedAsAReactionToAPendingSpell()
    {
        var initial = Put(SpellsPosition(), "OPEN", "OGN·104/298", "P1", "HAND"); var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("OPEN", "OGN·104/298", ["ALLY"]));
        await Reject(run, "P1", new PlayCardCommand("UNIT", No, [], Destination: "BASE")); await run.Top();
        await Export("ordinary-unit-closed-chain-rejection", initial, run); Assert.Contains("UNIT", run.State.PlayerZones["P1"].Hand);
    }

    [Theory]
    [InlineData("add")] [InlineData("recycle")] [InlineData("return")]
    public async Task ActualDiscardRecycleAndReturnChangeTheEntryConditionBeforePlay(string action)
    {
        var initial = Put(SpellsPosition(), "MATCH", No, "P1", action == "add" ? "HAND" : "GRAVEYARD");
        if (action == "add") initial = Put(initial, "CHANGE", "UNL-125/219", "P1", "HAND");
        else if (action == "return") initial = Put(initial, "CHANGE", "VEN·103", "P1", "HAND");
        else { initial = Put(initial, "OPEN", "OGN·104/298", "P1", "HAND"); initial = Put(initial, "RECYCLE", "UNL-103/219", "P2", "HAND"); }
        var run = new Driver(initial);
        if (action == "recycle") {
            await run.Act("P1", new PlayCardCommand("OPEN", "OGN·104/298", ["ALLY"])); await run.Act("P1", new PassPriorityCommand());
            await run.Act("P2", new PlayCardCommand("RECYCLE", "UNL-103/219", ["MATCH"], "RECYCLE_OPPONENT_GRAVEYARD_UP_TO_3")); await run.Top(); await run.Top();
            Assert.Contains("MATCH", run.State.PlayerZones["P1"].MainDeck);
        }
        else { await run.Act("P1", new PlayCardCommand("CHANGE", action == "add" ? "UNL-125/219" : "VEN·103", ["MATCH"])); await run.Top(); }
        await run.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: "BASE")); await Export("actual-grave-change-" + action, initial, run);
        Assert.Equal(action != "add", run.State.CardObjects["UNIT"].IsExhausted);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ChangingTheGraveAfterEntryDoesNotContinuouslyToggleTheUnitsReadiness(bool addAfterEntry)
    {
        var initial = Put(Put(SpellsPosition(), "MATCH", No, "P1", addAfterEntry ? "HAND" : "GRAVEYARD"),
            "CHANGE", addAfterEntry ? "UNL-125/219" : "VEN·103", "P1", "HAND"); var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: "BASE")); Assert.Equal(addAfterEntry, run.State.CardObjects["UNIT"].IsExhausted);
        await run.Act("P1", new PlayCardCommand("CHANGE", addAfterEntry ? "UNL-125/219" : "VEN·103", ["MATCH"])); await run.Top();
        await Export("entry-condition-is-not-continuous-" + addAfterEntry, initial, run); Assert.Equal(addAfterEntry, run.State.CardObjects["UNIT"].IsExhausted);
        Assert.DoesNotContain(run.Events(), e => e.Event.Kind == "UNIT_READIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "UNIT"));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AnActualGraveyardPlayDoesNotCountThePlayingCardAfterItLeavesTheGrave(bool anotherCopy)
    {
        var initial = Put(Put(SpellsPosition(), "REANIMATED", No, "P1", "GRAVEYARD"), "PARENT", "OGN·198/298", "P1", "HAND");
        if (anotherCopy) initial = Put(initial, "MATCH", No, "P1", "GRAVEYARD");
        var run = new Driver(initial); await run.Act("P1", new PlayCardCommand("PARENT", "OGN·198/298", ["REANIMATED"])); await run.Top();
        Assert.Equal("P1", run.State.PendingEffectPlay!.PlayerId);
        var mana = run.State.RunePools["P1"].Mana; var power = run.State.RunePools["P1"].TotalPower;
        await run.Act("P1", new PlayCardCommand("REANIMATED", No, [], Destination: "BASE"));
        await Export("actual-graveyard-play-other-copy-" + anotherCopy, initial, run);
        Assert.Equal(!anotherCopy, run.State.CardObjects["REANIMATED"].IsExhausted);
        Assert.Contains("REANIMATED", run.State.PlayerZones["P1"].Base); Assert.DoesNotContain("REANIMATED", run.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(mana, run.State.RunePools["P1"].Mana); Assert.Equal(power, run.State.RunePools["P1"].TotalPower);
        if (anotherCopy) Assert.Contains("MATCH", run.State.PlayerZones["P1"].Graveyard);
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task PlayingAnotherOwnersActualTopDeckCardChecksTheCurrentControllersGrave(string graveOwner)
    {
        var initial = Put(Put(Put(SpellsPosition(), "TOP", No, "P2", "MAIN_DECK"), "MATCH", No, graveOwner, "GRAVEYARD"),
            "PARENT", "OGN·025/298", "P1", "HAND");
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P2"] = initial.PlayerZones["P2"] with {
            MainDeck = initial.PlayerZones["P2"].MainDeck.Where(id => id != "TOP").Prepend("TOP").ToArray() } } };
        var run = new Driver(initial); await run.Act("P1", new PlayCardCommand("PARENT", "OGN·025/298", [])); await run.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(run.State.PendingCardChoice);
        await run.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["TOP"]));
        var mana = run.State.RunePools["P1"].Mana; var power = run.State.RunePools["P1"].TotalPower;
        await run.Act("P1", new PlayCardCommand("TOP", No, [], Destination: "BASE")); await Export("actual-other-owner-controller-grave-" + graveOwner, initial, run);
        Assert.Equal("P2", run.State.CardObjects["TOP"].OwnerId); Assert.Equal("P1", run.State.CardObjects["TOP"].ControllerId);
        Assert.Equal(graveOwner != "P1", run.State.CardObjects["TOP"].IsExhausted); Assert.Contains("TOP", run.State.PlayerZones["P1"].Base);
        Assert.Equal(mana, run.State.RunePools["P1"].Mana); Assert.Equal(power, run.State.RunePools["P1"].TotalPower);
        Assert.Contains("MATCH", run.State.PlayerZones[graveOwner].Graveyard);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualReturnAndReplayReevaluateTheCurrentGraveForTheNewObject(bool removeCopy)
    {
        var initial = Put(Put(SpellsPosition(), "MATCH", No, "P1", "GRAVEYARD"), "RECALL", "OGN·104/298", "P1", "HAND");
        if (removeCopy) initial = Put(initial, "CHANGE", "VEN·103", "P1", "HAND");
        var run = new Driver(initial); await run.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: "BASE"));
        var firstGeneration = run.State.CardObjects["UNIT"].ObjectGeneration; Assert.False(run.State.CardObjects["UNIT"].IsExhausted);
        if (removeCopy) { await run.Act("P1", new PlayCardCommand("CHANGE", "VEN·103", ["MATCH"])); await run.Top(); Assert.False(run.State.CardObjects["UNIT"].IsExhausted); }
        await run.Act("P1", new PlayCardCommand("RECALL", "OGN·104/298", ["UNIT"])); await run.Top();
        Assert.Contains("UNIT", run.State.PlayerZones["P1"].Hand);
        await run.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: "BASE")); await Export("actual-return-and-replay-" + removeCopy, initial, run);
        Assert.Equal(removeCopy, run.State.CardObjects["UNIT"].IsExhausted); Assert.True(run.State.CardObjects["UNIT"].ObjectGeneration >= firstGeneration + 2);
        Assert.Equal(5, run.State.CardObjects["UNIT"].Power); Assert.Equal(0, run.State.CardObjects["UNIT"].Damage);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualRescueBanishThenOwnerPlayUsesTheSameEntryCondition(bool anotherCopy)
    {
        var initial = Put(SpellsPosition(), "RESCUE", "OGN·102/298", "P1", "HAND");
        if (anotherCopy) initial = Put(initial, "MATCH", No, "P1", "GRAVEYARD");
        var run = new Driver(initial); await run.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: "BASE"));
        var generation = run.State.CardObjects["UNIT"].ObjectGeneration;
        await run.Act("P1", new PlayCardCommand("RESCUE", "OGN·102/298", ["UNIT"])); await run.Top();
        Assert.Equal("P1", run.State.PendingEffectPlay!.PlayerId);
        await run.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: "BASE"));
        await Export("actual-rescue-owner-entry-" + anotherCopy, initial, run);
        Assert.Contains("UNIT", run.State.PlayerZones["P1"].Base); Assert.Equal("P1", run.State.CardObjects["UNIT"].ControllerId);
        Assert.Equal(!anotherCopy, run.State.CardObjects["UNIT"].IsExhausted); Assert.True(run.State.CardObjects["UNIT"].ObjectGeneration >= generation + 2);
        Assert.Equal(5, run.State.CardObjects["UNIT"].Power); Assert.Empty(run.State.StackItems);
    }

    [Theory]
    [InlineData("prompt")] [InlineData("tick")]
    public async Task StaleDeclarationsAreRetainedWithoutEntryAndACurrentNewIntentCanPlay(string kind)
    {
        var initial = Put(Put(Position(), "MATCH", No, "P1", "GRAVEYARD"), "WARMUP", "UNL-R01", "P1", "BASE"); var run = new Driver(initial);
        await run.Act("P1", new TapRuneCommand("WARMUP"));
        var beforeMana = run.State.RunePools["P1"].Mana;
        var command = new PlayCardCommand("UNIT", No, [], Destination: "BASE");
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
        raw["promptId"] = ResolutionResult.BuildPrompts(run.State)["P1"].PromptId + (kind == "prompt" ? "-stale" : "");
        raw["snapshotTick"] = run.State.Tick - (kind == "tick" ? 1 : 0);
        await run.ActRetained(new("P1", "ven013-stale-" + kind, JsonSerializer.SerializeToElement(raw, Json)), false);
        Assert.Contains("UNIT", run.State.PlayerZones["P1"].Hand); Assert.Equal(beforeMana, run.State.RunePools["P1"].Mana);
        await run.Act("P1", command); await Export("stale-then-current-" + kind, initial, run);
        Assert.Equal(3, run.Commands().Length); Assert.False(run.Commands()[1].Accepted); Assert.True(run.Commands()[2].Accepted);
        Assert.False(run.State.CardObjects["UNIT"].IsExhausted);
    }

    [Theory]
    [InlineData("readiness")] [InlineData("grave-and-readiness")] [InlineData("controller-and-zone")] [InlineData("generation")]
    public async Task TheOriginalFullLogRejectsCoordinatedFinalStateForgery(string kind)
    {
        var initial = Put(Position(), "MATCH", No, "P1", "GRAVEYARD"); var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: "BASE")); await Export("forgery-original-" + kind, initial, run);
        var forged = run.State with { CardObjects = new Dictionary<string, CardObjectState>(run.State.CardObjects) {
            ["UNIT"] = run.State.CardObjects["UNIT"] with { IsExhausted = kind is "readiness" or "grave-and-readiness",
                ControllerId = kind == "controller-and-zone" ? "P2" : "P1",
                ObjectGeneration = run.State.CardObjects["UNIT"].ObjectGeneration + (kind == "generation" ? 1 : 0) } } };
        if (kind == "grave-and-readiness") forged = forged with {
            ObjectLocations = new Dictionary<string, ObjectLocationState>(forged.ObjectLocations) { ["MATCH"] = new("P1", "HAND") },
            PlayerZones = new Dictionary<string, PlayerZones>(forged.PlayerZones) { ["P1"] = forged.PlayerZones["P1"] with {
                Graveyard = forged.PlayerZones["P1"].Graveyard.Where(id => id != "MATCH").ToArray(), Hand = [..forged.PlayerZones["P1"].Hand, "MATCH"] } } };
        if (kind == "controller-and-zone") forged = forged with {
            ObjectLocations = new Dictionary<string, ObjectLocationState>(forged.ObjectLocations) { ["UNIT"] = new("P2", "BASE") },
            PlayerZones = forged.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Base = p.Value.Base.Where(id => id != "UNIT").ToArray() }
                : p.Value with { Base = [..p.Value.Base, "UNIT"] }) };
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, run.Commands(), forged, new CoreRuleEngine(), default, run.Events());
        Assert.False(replay.IsMatch); Assert.NotEmpty(replay.Errors); Assert.False(run.State.CardObjects["UNIT"].IsExhausted);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN013_EVIDENCE") is { Length: > 0 } output)
            File.WriteAllText(Path.Combine(output, "forgery-rejected-" + kind + ".json"), JsonSerializer.Serialize(new { forged, replay.IsMatch, replay.Errors }, Json));
    }

    private static MatchState Position()
    {
        var state = VenUnitEntryBasicTests.Position(No) with { RoomId = "VEN013-GRAVEYARD-ENTRY" };
        state = LocalDestructionRecallTests.At(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        return state with { RunePools = new Dictionary<string, RunePool> { ["P1"] = new(40, 0), ["P2"] = new(20, 20) } };
    }

    private static MatchState SpellsPosition() => Position() with {
        RunePools = new Dictionary<string, RunePool> { ["P1"] = new(40, 40), ["P2"] = new(20, 20) } };

    private static async Task Reject(Driver run, string player, GameCommand command)
    {
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
        raw["promptId"] = ResolutionResult.BuildPrompts(run.State)[player].PromptId; raw["snapshotTick"] = run.State.Tick;
        await run.ActRetained(new(player, "ven013-reject-" + run.Commands().Length, JsonSerializer.SerializeToElement(raw, Json)), false);
    }

    private static MatchState Put(MatchState state, string id, string no, string owner, string zone)
    {
        var zones = state.PlayerZones[owner];
        zones = zone switch {
            "HAND" => zones with { Hand = [..zones.Hand, id] },
            "GRAVEYARD" => zones with { Graveyard = [..zones.Graveyard, id] },
            "MAIN_DECK" => zones with { MainDeck = [..zones.MainDeck, id] },
            "BASE" => zones with { Base = [..zones.Base, id] },
            _ => throw new ArgumentOutOfRangeException(nameof(zone)) };
        return state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, zone) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [owner] = zones } };
    }

    private static void JsonRestore(MatchState initial, Driver run)
    {
        var state = run.State; var sequence = run.Commands()[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, run.Commands(), run.Events(), views, [], state, initial,
            MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state)) { OpeningSeed = initial.Seed };
        var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(decoded.AuthoritativeState!));
        Assert.Empty(MatchRecoveryValidator.Validate(decoded.RoomId, decoded.LastEventSequence, decoded.Commands, decoded.Events,
            decoded.PlayerViews, decoded.AuthoritativeState, decoded.CurrentTick, decoded.SpectatorReplayFrame));
        var restored = MatchSession.Restore(decoded, new CoreRuleEngine(), run);
        foreach (var player in state.Seats.Keys) {
            Assert.Equal(MatchStateHasher.HashValue(snapshots[player]), MatchStateHasher.HashValue(restored.SnapshotFor(player)));
            Assert.Equal(MatchStateHasher.HashValue(prompts[player]), MatchStateHasher.HashValue(restored.PromptFor(player)));
        }
    }

    private static async Task Export(string name, MatchState initial, Driver run)
    {
        await run.Replay(); JsonRestore(initial, run);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN013_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        System.Reflection.Assembly[] assemblies = [typeof(CoreRuleEngine).Assembly, typeof(OfficialCardCatalog).Assembly, typeof(GameCommand).Assembly];
        var runtime = new { ruleInputs = RuleInputIdentity.Current, appContextBaseDirectory = AppContext.BaseDirectory,
            catalogPath = OfficialCardCatalog.DefaultCatalogPath(), assemblies = assemblies.Select(a => new {
                name = a.GetName().Name, loadedPath = a.Location, mvid = a.ManifestModule.ModuleVersionId.ToString(),
                sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))).ToLowerInvariant() }) };
        File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(new { initial, final = run.State,
            commands = run.Commands(), events = run.Events(), snapshots = ResolutionResult.BuildSnapshots(run.State), prompts = ResolutionResult.BuildPrompts(run.State), runtime }, Json));
    }
}
