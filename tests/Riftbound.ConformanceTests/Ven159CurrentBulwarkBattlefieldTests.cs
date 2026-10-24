using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven159CurrentBulwarkBattlefieldTests
{
    private const string Temple = "VEN·159";
    private const string Wind = "SFD·138/221";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task ExactPrintingRegistersOnlyTheBattlefieldDomainAndCurrentBulwarkFilter()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == Temple);
        Assert.Equal(37862, card.Id);
        Assert.Equal("均衡寺院", card.CardName);
        Assert.Equal("此处拥有{{壁垒}}的单位获得{{S}}+1。", card.CardEffect);
        Assert.True(OfficialRuleDomainBehaviorCatalog.IsImplementedBattlefieldRuleCard(card));
        Assert.False(CardBehaviorRegistry.TryGetByCardNo(Temple, out _));
        var aura = Assert.Single(StaticAuraParser.Parse(card.CardEffect));
        Assert.Equal(StaticAuraKinds.BattlefieldFilteredUnitsPower, aura.Kind);
        Assert.Equal(ContinuousEffectLayers.StaticAura, aura.Layer);
        Assert.Equal(StaticAuraTargetScopes.SameBattlefieldFilteredUnits, aura.TargetScope);
        Assert.Equal(StaticAuraParticipantScopes.SameBattlefieldFilteredPublicUnits, aura.ParticipantScope);
        Assert.Equal("CURRENT_KEYWORD:壁垒", aura.TargetFilter);
        Assert.Equal(1, aura.PowerDeltaPerParticipant);
    }

    [Theory]
    [InlineData("此处拥有{{后排}}的单位获得{{S}}+1。")]
    [InlineData("你的此处拥有{{壁垒}}的单位获得{{S}}+1。")]
    [InlineData("此处拥有{{壁垒}}的单位获得{{S}}+1且改为+2")]
    public void ParserDoesNotInventAnotherKeywordOrControllerScope(string text)
        => Assert.DoesNotContain(StaticAuraParser.Parse(text), a => a.TargetFilter == "CURRENT_KEYWORD:壁垒");

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    [InlineData(null)]
    public void RegisteredAuraIncludesBothPlayersRegardlessOfBattlefieldControl(string? controller)
    {
        // Finite public component frame; no accepted opening is claimed here.
        var state = Component();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { ["F"] = state.CardObjects["F"] with { ControllerId = controller } } };
        AssertMight(state, "P1-TARGET", 4);
        AssertMight(state, "P2-TARGET", 4);
        Assert.Equal(["P1-TARGET", "P2-TARGET"], Effects(state).Select(e => e.TargetObjectId!).Order().ToArray());
        foreach (var effect in Effects(state))
        {
            Assert.Equal(1, effect.PowerDelta);
            Assert.Equal(["F"], effect.SourceDependencyObjectIds);
            Assert.Equal(["P1-TARGET", "P2-TARGET"], effect.ParticipantObjectIds);
        }
    }

    [Theory]
    [InlineData("BASE")]
    [InlineData("HAND")]
    [InlineData("GRAVEYARD")]
    [InlineData("OTHER")]
    [InlineData("FACE_DOWN")]
    public void AUnitOutsideThePublicBattlefieldDoesNotReceiveTheBonus(string boundary)
    {
        var state = Component();
        if (boundary == "FACE_DOWN") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { ["P1-TARGET"] = state.CardObjects["P1-TARGET"] with { IsFaceDown = true } } };
        else state = LocalDestructionRecallTests.At(state, "P1-TARGET", "P1",
            boundary == "OTHER" ? "BATTLEFIELD" : boundary, boundary == "OTHER" ? "OTHER" : null);
        Assert.DoesNotContain(Effects(state), e => e.TargetObjectId == "P1-TARGET");
        AssertMight(state, "P2-TARGET", 4);
    }

    [Fact]
    public void DuplicateBulwarkIsOneBonusAndDamagePriorityIsNotPossession()
    {
        var state = Component();
        var target = state.CardObjects["P1-TARGET"];
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { [target.ObjectId] = target with { UntilEndOfTurnEffects = ["COMBAT_KEYWORD:壁垒:1", "COMBAT_KEYWORD:壁垒:1"] } } };
        AssertMight(state, target.ObjectId, 4);
        Assert.Single(Effects(state), e => e.TargetObjectId == target.ObjectId);
        var immune = Printed(target.ObjectId, "OGN·189/298", "P1");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { [target.ObjectId] = immune with { Tags = immune.Tags.Append(CardCombatKeywordNames.BackRow).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() } } };
        Assert.DoesNotContain(Effects(state), e => e.TargetObjectId == target.ObjectId);
        AssertMight(state, target.ObjectId, 6);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { [target.ObjectId] = immune with { MovementCountThisTurn = 2, UntilEndOfTurnEffects = ["COMBAT_KEYWORD:壁垒:1"] } } };
        AssertMight(state, target.ObjectId, 7);
    }

    [Theory]
    [InlineData("live")]
    [InlineData("generation")]
    [InlineData("revision")]
    [InlineData("private")]
    public void CopiedGrantUsesOnlyTheCurrentPublicRuleInstance(string boundary)
    {
        // Explicit attached-copy component, not a copy-command birth witness.
        var state = CurrentKeywordAuraParticipantTests.Position("continuous");
        var host = Printed("SOURCE", "OGN·210/298", "P1") with { CopyFaceTransitions = [new(1, 1, "UNL-058/219", "OGN·210/298")] };
        var gear = Printed("SONG", "SFD·059/221", "P1") with { AttachedToObjectId = "SOURCE", AttachmentRevision = 1,
            AttachedRuleText = new(new("SOURCE", 1), "UNL-058/219", 1, 0, 0) };
        state = state with { Tick = 1, CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { ["F"] = Printed("F", Temple, "P1"), ["SOURCE"] = host, ["SONG"] = gear },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
                { ["P1"] = state.PlayerZones["P1"] with { Base = ["SOURCE", "SONG"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["SONG"] = new("P1", "BASE") } };
        if (boundary == "generation") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { ["SOURCE"] = host with { ObjectGeneration = 2 } } };
        if (boundary == "revision") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { ["SONG"] = gear with { AttachmentRevision = 2 } } };
        if (boundary == "private") state = LocalDestructionRecallTests.At(state, "SONG", "P1", "HAND");
        if (boundary == "live")
        {
            AssertMight(state, "P1-TARGET", 4);
            Assert.Equal(["F", "SONG", "SOURCE"], Assert.Single(Effects(state)).SourceDependencyObjectIds);
        }
        else
        {
            AssertMight(state, "P1-TARGET", 3);
            Assert.Empty(Effects(state));
        }
    }

    public static IEnumerable<object[]> OrdinaryCases() => new[] { "P1", "P2" }.SelectMany(player =>
        new[] { "intrinsic", "temporary", "UNL-058/219", "UNL-058a/219" }.Select(mode => new object[] { player, mode }));

    [Theory]
    [MemberData(nameof(OrdinaryCases))]
    public async Task OrdinaryPlayGrantLossAndWindTargetSelectionRetainTheOriginalHistory(string player, string mode)
    {
        var initial = Opening(player, mode);
        var run = new Driver(initial);
        var samples = new List<object>();
        var continuous = mode.StartsWith("UNL-", StringComparison.Ordinal);
        string target;
        if (continuous)
        {
            await run.Act(player, new PlayCardCommand("SOURCE", mode, [], Destination: "BASE"));
            await run.Act(player, new PlayCardCommand("TOKEN-SPELL", "OGN·094/298", []));
            await Drain(run);
            target = Assert.Single(run.State.CardObjects.Values, c => c.TokenFactoryCardNo is not null && c.Tags.Contains(CardObjectTags.UnitCard)).ObjectId;
            AssertMight(run.State, target, 3);
            Assert.DoesNotContain(Effects(run.State), e => e.TargetObjectId == target);
            await run.Act(player, new MoveUnitCommand(target, "BASE", "BATTLEFIELD:F", []));
        }
        else
        {
            target = "TARGET";
            await run.Act(player, new PlayCardCommand(target, mode == "intrinsic" ? "OGN·054/298" : "OGN·175/298", [], Destination: "BATTLEFIELD:F"));
            if (mode == "temporary")
            {
                AssertMight(run.State, target, 3);
                Assert.True(Quote(run.State, player, target).IsValid);
                await run.Act(player, new PlayCardCommand("GRANT", "OGN·057/298", [target]));
                await Drain(run);
            }
        }
        AssertMight(run.State, target, 4);
        var effect = Assert.Single(Effects(run.State), e => e.TargetObjectId == target);
        Assert.Equal(continuous ? new[] { "F", "SOURCE" } : ["F"], effect.SourceDependencyObjectIds);
        Assert.Equal(1, effect.PowerDelta);
        Assert.Equal(4, effect.EffectivePower);
        samples.Add(new { stage = "current-bulwark", target, expectedMight = 4, effect });
        Assert.False(Quote(run.State, player, target).IsValid);
        await Reject(run, player, new PlayCardCommand("SELECTOR", Wind, [target]), "preselected-high");
        await run.Act(player, new PlayCardCommand("SELECTOR", Wind, []));
        var choice = Assert.IsType<PendingCardChoiceState>(run.State.PendingCardChoice);
        Assert.DoesNotContain(target, choice.LegalObjectIds);
        Assert.Contains("ANCHOR", choice.LegalObjectIds);
        await Reject(run, player, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]), "chosen-high");
        await run.Act(player, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        await Drain(run);

        // Acquiring Bulwark again still gives one battlefield bonus.
        if (mode == "intrinsic")
        {
            await run.Act(player, new PlayCardCommand("GRANT", "OGN·057/298", [target]));
            await Drain(run);
            AssertMight(run.State, target, 4);
            Assert.Single(Effects(run.State), e => e.TargetObjectId == target);
        }
        if (continuous || mode == "temporary")
        {
            if (continuous)
            {
                await run.Act(player, new PlayCardCommand("RETURN", "OGN·104/298", ["SOURCE"]));
                await Drain(run);
                Assert.Contains("SOURCE", run.State.PlayerZones[player].Hand);
            }
            else await run.Act(player, new EndTurnCommand());
            AssertMight(run.State, target, 3);
            Assert.DoesNotContain(Effects(run.State), e => e.TargetObjectId == target);
            samples.Add(new { stage = "keyword-lost", target, expectedMight = 3 });
            var chooser = run.State.TurnPlayerId!;
            var lateSource = "LATE-" + chooser;
            if (mode == "temporary")
            {
                await run.Act(chooser, new TapRuneCommand(chooser + "-PUBLIC-RUNE-1"));
                await run.Act(chooser, new TapRuneCommand(chooser + "-PUBLIC-RUNE-2"));
            }
            await run.Act(chooser, new PlayCardCommand(lateSource, Wind, []));
            choice = Assert.IsType<PendingCardChoiceState>(run.State.PendingCardChoice);
            Assert.Contains(target, choice.LegalObjectIds);
            await run.Act(chooser, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
            await Drain(run);
            Assert.DoesNotContain(target, run.State.PlayerZones[player].Battlefields);
            if (!continuous) Assert.Contains(target, run.State.PlayerZones[player].Hand);
        }
        await run.Replay();
        var sequence = run.Events()[^1].Sequence;
        var snapshots = ResolutionResult.BuildSnapshots(run.State); var prompts = ResolutionResult.BuildPrompts(run.State);
        var views = run.State.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, run.State.Tick, sequence,
            snapshots[p], run.State.Tick, sequence, prompts[p]));
        var frame = new MatchRecoveryFrame(run.State.RoomId, run.State.Tick, sequence, run.Commands(), run.Events(), views, [], run.State, initial,
            MatchReplayRedactor.BuildSpectatorFrame(run.State.RoomId, run.State.Tick, sequence, [], run.State)) { OpeningSeed = initial.Seed };
        var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(decoded.ReplayInitialState!));
        Assert.Equal(MatchStateHasher.Hash(run.State), MatchStateHasher.Hash(decoded.AuthoritativeState!));
        Assert.Empty(MatchRecoveryValidator.Validate(decoded.RoomId, decoded.LastEventSequence, decoded.Commands, decoded.Events,
            decoded.PlayerViews, decoded.AuthoritativeState, decoded.CurrentTick, decoded.SpectatorReplayFrame));
        var restored = MatchSession.Restore(decoded, new CoreRuleEngine(), run);
        foreach (var viewer in run.State.Seats.Keys)
        {
            Assert.Equal(MatchStateHasher.HashValue(snapshots[viewer]), MatchStateHasher.HashValue(restored.SnapshotFor(viewer)));
            Assert.Equal(MatchStateHasher.HashValue(prompts[viewer]), MatchStateHasher.HashValue(restored.PromptFor(viewer)));
        }
        var jsonReplay = await MatchActionLogReplayer.VerifyFinalStateAsync(decoded.ReplayInitialState!, decoded.Commands,
            decoded.AuthoritativeState!, new CoreRuleEngine(), default, decoded.Events);
        Assert.True(jsonReplay.IsMatch, string.Join("; ", jsonReplay.Errors));
        var forgeries = await RejectForgedHistory(initial, run);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN159_EVIDENCE") is { } root)
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, player + "-" + mode.Replace('/', '_') + ".json"), JsonSerializer.Serialize(new
            {
                scope = "Explicit midgame HAND input; ordinary typed commands, cold restore, duplicate, rejection and original replay; no canonical seating or full game",
                initial, commands = run.Commands(), events = run.Events(), final = run.State, samples, forgeries,
                runtime = new { baseDirectory = AppContext.BaseDirectory, ruleInputs = RuleInputIdentity.Current,
                    modules = new[] { typeof(CoreRuleEngine).Assembly, typeof(OfficialCardCatalog).Assembly, typeof(GameCommand).Assembly }.Select(a => new
                        { path = a.Location, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))).ToLowerInvariant(), mvid = a.ManifestModule.ModuleVersionId }) }
            }, Json));
        }
    }

    private static async Task<object[]> RejectForgedHistory(MatchState initial, Driver run)
    {
        var results = new List<object>();
        foreach (var mutation in new[] { "private-source", "event", "final-power", "coordinated-event-and-final" })
        {
            var commands = run.Commands(); var events = run.Events(); var final = run.State;
            if (mutation == "private-source")
            {
                var raw = JsonSerializer.SerializeToNode(commands[0].RawCommand, Json)!.AsObject();
                raw["sourceObjectId"] = "PRIVATE-" + (commands[0].PlayerId == "P1" ? "P2" : "P1") + "-DRAW-1";
                commands[0] = commands[0] with { RawCommand = JsonSerializer.SerializeToElement(raw, Json) };
            }
            if (mutation is "event" or "coordinated-event-and-final")
                events[0] = events[0] with { Event = events[0].Event with { Kind = "FORGED_" + events[0].Event.Kind } };
            if (mutation is "final-power" or "coordinated-event-and-final")
                final = final with { CardObjects = new Dictionary<string, CardObjectState>(final.CardObjects)
                    { ["ANCHOR"] = final.CardObjects["ANCHOR"] with { Power = 99 } } };
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, final, new CoreRuleEngine(), default, events);
            Assert.False(replay.IsMatch); Assert.NotEmpty(replay.Errors);
            results.Add(new { mutation, replay.IsMatch, replay.Errors });
        }
        return results.ToArray();
    }

    private static Task Reject(Driver run, string player, GameCommand command, string id)
    {
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
        raw["promptId"] = ResolutionResult.BuildPrompts(run.State)[player].PromptId;
        raw["snapshotTick"] = run.State.Tick;
        return run.ActRetained(new(player, "ven159-reject-" + id, JsonSerializer.SerializeToElement(raw, Json)), false);
    }

    private static async Task Drain(Driver run)
    {
        for (var i = 0; i < 20; i++)
        {
            Assert.Null(run.State.PendingCardChoice);
            if (run.State.StackItems.Count > 0) { await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand()); continue; }
            if (run.State.TimingState == TimingStates.SpellDuelOpen && run.State.FocusPlayerId is { } focus)
            { await run.Act(focus, new PassFocusCommand()); continue; }
            Assert.Empty(run.State.TriggerQueue);
            Assert.Equal(TimingStates.NeutralOpen, run.State.TimingState);
            return;
        }
        Assert.Fail("VEN159 command drain did not reach neutral timing.");
    }

    private static PlayCostQuoteDto Quote(MatchState state, string player, string target)
        => new CoreRuleEngine().PreviewPlayCard(state, player, new("ven159-quote", ResolutionResult.BuildPrompts(state)[player].PromptId!, state.Tick,
            new PlayCardCommand("SELECTOR", Wind, [target])));

    private static ContinuousEffectState[] Effects(MatchState state) => state.ContinuousEffects
        .Where(e => e.SourceObjectId == "F" && e.EffectKind == StaticAuraKinds.BattlefieldFilteredUnitsPower).ToArray();

    private static void AssertMight(MatchState state, string id, int expected)
    {
        var hash = MatchStateHasher.Hash(state);
        foreach (var viewer in new[] { "P1", "P2" })
        {
            var view = CurrentPowerTargetTests.Snapshot(state, viewer);
            var controller = state.CardObjects[id].ControllerId!;
            Assert.Equal(expected, view.GetProperty("players").GetProperty(controller).GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32());
        }
        var method = typeof(CoreRuleEngine).GetMethod("ResolveCurrentFieldUnitPower", BindingFlags.Static | BindingFlags.NonPublic,
            [typeof(MatchState), typeof(CardObjectState)])!;
        Assert.Equal(expected, (int)method.Invoke(null, [state, state.CardObjects[id]])!);
        Assert.Equal(hash, MatchStateHasher.Hash(state));
    }

    private static CardObjectState Printed(string id, string no, string owner)
        => CurrentKeywordAuraParticipantTests.Printed(id, no, owner);

    private static MatchState Component()
    {
        var state = CurrentKeywordAuraParticipantTests.Position("intrinsic");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { ["F"] = Printed("F", Temple, "P1"), ["P2-TARGET"] = Printed("P2-TARGET", "OGN·054/298", "P2") } };
    }

    private static MatchState Opening(string player, string mode)
    {
        // Friendly anchors make battlefield control an explicit valid midgame
        // precondition. Every tested target, grant and source begins in HAND.
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string owner, string zone, string? field = null)
        {
            cards[id] = Printed(id, no, owner) with { ObjectGeneration = zone is "HAND" or "MAIN_DECK" or "RUNE_DECK" ? 0 : 1 };
            locations[id] = new(owner, zone, field);
        }
        Put("F", Temple, "P1", "BATTLEFIELD", "F"); cards["F"] = cards["F"] with { ControllerId = player };
        Put("OTHER", "OGN·278/298", "P1", "BATTLEFIELD", "OTHER"); cards["OTHER"] = cards["OTHER"] with { ControllerId = player };
        Put("ANCHOR", "OGN·175/298", player, "BATTLEFIELD", "F");
        Put("OTHER-ANCHOR", "OGN·175/298", player, "BATTLEFIELD", "OTHER");
        if (mode.StartsWith("UNL-", StringComparison.Ordinal))
        { Put("SOURCE", mode, player, "HAND"); Put("TOKEN-SPELL", "OGN·094/298", player, "HAND"); }
        else Put("TARGET", mode == "intrinsic" ? "OGN·054/298" : "OGN·175/298", player, "HAND");
        Put("GRANT", "OGN·057/298", player, "HAND");
        Put("RETURN", "OGN·104/298", player, "HAND");
        Put("SELECTOR", Wind, player, "HAND");
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var owner in new[] { "P1", "P2" })
        {
            Put("LATE-" + owner, Wind, owner, "HAND");
            foreach (var i in Enumerable.Range(1, 4)) Put(owner + "-PUBLIC-RUNE-" + i, "UNL-R03", owner, "BASE");
            foreach (var i in Enumerable.Range(1, 3))
            { Put("PRIVATE-" + owner + "-DRAW-" + i, "OGN·219/298", owner, "MAIN_DECK"); Put("PRIVATE-" + owner + "-RUNE-" + i, "UNL-R03", owner, "RUNE_DECK"); }
            string[] In(string zone) => locations.Where(p => p.Value.PlayerId == owner && p.Value.Zone == zone).Select(p => p.Key).ToArray();
            zones[owner] = PlayerZones.Empty with { Hand = In("HAND"), Base = In("BASE"), Battlefields = In("BATTLEFIELD"), MainDeck = In("MAIN_DECK"), RuneDeck = In("RUNE_DECK") };
        }
        return new MatchState("VEN159-CONSUMER-" + player + "-" + mode, 0, 5, player,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }, status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"], turnPlayerId: player, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: zones, cardObjects: cards, objectLocations: locations, seed: 424242,
            runePools: new Dictionary<string, RunePool> { ["P1"] = Pool(), ["P2"] = Pool() }) with { CardPlays = new(5, [], []) };
        static RunePool Pool() => new(40, 0, new Dictionary<string, int> { [RuneTrait.Green] = 20, [RuneTrait.Blue] = 20 });
    }
}
