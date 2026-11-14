using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

// CN 370.1.c / 370.2 / 372 / 435.1.c / 479.3.c and the VEN Song-copy FAQ.
public sealed class SharedTargetReplacementChoiceTests
{
    private const string Song = "SFD·059/221";
    private const string Extort = "OGN·095/298";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    public static TheoryData<string, string, string, string> Sources {
        get {
            var data = new TheoryData<string, string, string, string>();
            foreach (var printing in new[] { "VEN·086", "VEN·181" })
                foreach (var spell in new[] { (Extort, "NEGATIVE_MIGHT"), ("UNL-042/219", "STUN"), ("OGN·104/298", "RETURN_TO_HAND") })
                    foreach (var source in new[] { "HOST", "SONG-1", "SONG-2" })
                        data.Add(printing, spell.Item1, spell.Item2, source);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public async Task EachRealPrintedOrAttachedSourceReplacesTheTargetedEventExactlyOnce(
        string printing, string spell, string eventKind, string source)
    {
        var game = new Driver(Put(Position(printing), "SPELL", spell, "P1", "HAND"));
        await Enter(game, printing);
        var before = Might(game.State); var generation = game.State.CardObjects["HOST"].ObjectGeneration;
        var runes = game.State.PlayerZones["P1"].RuneDeck.Count;
        var prefix = game.Events().Length;
        await game.Act("P1", new PlayCardCommand("SPELL", spell, ["HOST"])); await game.Top();
        AssertChoice(game, "P1");
        await Choose(game, source);
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(before + 3, Might(game.State));
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
        Assert.Equal(generation, game.State.CardObjects["HOST"].ObjectGeneration);
        Assert.Equal(runes, game.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        var replaced = Assert.Single(game.Events().Skip(prefix), e => e.Event.Payload.ContainsKey("replacementInstanceId")).Event.Payload;
        Assert.Equal(eventKind, replaced["replacedEvent"]);
        Assert.Equal(printing, replaced["replacementCardNo"]);
        Assert.Equal(source == "HOST" ? $"PRINTED:HOST:{generation}"
            : $"ATTACHED:{source}:{game.State.CardObjects[source].ObjectGeneration}:{game.State.CardObjects[source].AttachmentRevision}",
            replaced["replacementInstanceId"]);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(before, Might(game.State));
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·086")] [InlineData("VEN·181")]
    public async Task ANewEventCanChooseTheSameAttachedReplacementAgain(string printing)
    {
        var initial = Put(Put(Position(printing), "FIRST", Extort, "P1", "HAND"), "SECOND", Extort, "P1", "HAND");
        var game = new Driver(initial); await Enter(game, printing); var before = Might(game.State);
        string? previousId = null;
        foreach (var id in new[] { "FIRST", "SECOND" }) {
            await game.Act("P1", new PlayCardCommand(id, Extort, ["HOST"])); await game.Top();
            var choice = AssertChoice(game, "P1");
            if (previousId is not null) Assert.NotEqual(previousId, choice.Id);
            previousId = choice.Id; await Choose(game, "SONG-2");
        }
        Assert.Equal(before + 6, Might(game.State));
        Assert.Equal(2, game.Events().Count(e => e.Event.Payload.ContainsKey("replacementInstanceId")));
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·086")] [InlineData("VEN·181")]
    public async Task EchoCreatesTwoIndependentChoicesInsideTheSameResolution(string printing)
    {
        var initial = Put(Put(Position(printing), "GATE", "SFD·078/221", "P1", "HAND"), "SPELL", Extort, "P1", "HAND");
        var game = new Driver(initial); await Enter(game, printing); var before = Might(game.State);
        await game.Act("P1", new PlayCardCommand("GATE", "SFD·078/221", [], Destination: "BASE"));
        await game.Act("P1", new ActivateAbilityCommand("GATE", P4ActivatedAbilityCatalog.NextSpellEchoAbilityId, [])); await game.Top();
        await game.Act("P1", new PlayCardCommand("SPELL", Extort, ["HOST"], OptionalCosts: ["ECHO"])); await game.Top();
        var first = AssertChoice(game, "P1"); await Choose(game, "SONG-1");
        var second = AssertChoice(game, "P1"); Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(before + 3, Might(second.Preview!));
        Assert.Single(game.State.PendingRuleChoice!.Answers);
        await Choose(game, "HOST");
        Assert.Null(game.State.PendingRuleChoice); Assert.Empty(game.State.StackItems);
        Assert.Equal(before + 6, Might(game.State));
        Assert.Equal(2, game.Events().Count(e => e.Event.Payload.ContainsKey("replacementInstanceId")));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TheCurrentControllerChoosesEvenWhenTheCasterOrOwnerIsDifferent(bool steal)
    {
        var initial = Put(Put(Position(), "SPELL", Extort, "P2", "HAND"), "STEAL", "SFD·202/221", "P2", "HAND");
        var funding = Enumerable.Range(1, 10).Select(i => "P2-FUND-" + i).ToArray();
        foreach (var (id, index) in funding.Select((id, index) => (id, index)))
            initial = Put(initial, id, index < 6 ? "UNL-R03" : "UNL-R06", "P2", "BASE");
        var game = new Driver(initial); await Enter(game, "VEN·086", battlefield: true);
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new TapRuneCommand(funding[0], funding));
        await game.Act("P2", new RecycleRuneCommand(funding[0], [funding[0], funding[1], funding[6], funding[7]]));
        if (steal) {
            await game.Act("P2", new PlayCardCommand("STEAL", "SFD·202/221", ["HOST"])); await game.Top();
            await HostileTakeoverDelayedControlTests.ToMain(game);
        }
        var controller = steal ? "P2" : "P1"; var before = Might(game.State);
        Assert.Equal("P1", game.State.CardObjects["HOST"].OwnerId);
        Assert.Equal(controller, game.State.CardObjects["HOST"].ControllerId);
        await game.Act("P2", new PlayCardCommand("SPELL", Extort, ["HOST"])); await game.Top();
        var choice = AssertChoice(game, controller);
        await Reject(game.State, controller == "P1" ? "P2" : "P1",
            new PayCostCommand(choice.Id, "RULE_REPLACEMENT", [choice.Options[0].Id]));
        await Choose(game, "SONG-2"); Assert.Equal(before + 3, Might(game.State));
        await game.Replay();
    }

    [Theory]
    [InlineData("STUN")] [InlineData("FLOOR")]
    public async Task APreventedOrNonexistentNegativeEventDoesNotOpenAMultiSourceChoice(string kind)
    {
        var initial = Put(Put(Put(Position(), "SMOKE-1", "OGN·093/298", "P1", "HAND"),
            "SMOKE-2", "OGN·093/298", "P1", "HAND"), "FIRST-STUN", "UNL-042/219", "P1", "HAND");
        var spell = kind == "STUN" ? "UNL-042/219" : Extort;
        var game = new Driver(Put(initial, "SPELL", spell, "P1", "HAND"));
        await Enter(game, "VEN·086", empower: false);
        foreach (var id in kind == "STUN" ? new[] { "FIRST-STUN" } : new[] { "SMOKE-1", "SMOKE-2" }) {
            await game.Act("P1", new PlayCardCommand(id, kind == "STUN" ? "UNL-042/219" : "OGN·093/298", ["HOST"])); await game.Top();
        }
        await game.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", [])); await game.Top();
        var before = Might(game.State);
        await game.Act("P1", new PlayCardCommand("SPELL", spell, ["HOST"])); await game.Top();
        Assert.Null(game.State.PendingRuleChoice); Assert.Equal(before, Might(game.State));
        Assert.DoesNotContain(game.Events(), e => e.Event.Payload.ContainsKey("replacementInstanceId"));
        await game.Replay();
    }

    [Fact]
    public async Task ChoiceBindsTheCurrentSourceAndColdRecoveryAndOriginalReplayPreserveTheSelectedInstance()
    {
        var initial = Put(Position(), "SPELL", Extort, "P1", "HAND");
        var game = new Driver(initial); await Enter(game, "VEN·086");
        await game.Act("P1", new PlayCardCommand("SPELL", Extort, ["HOST"])); await game.Top();
        var request = AssertChoice(game, "P1"); var option = Option(request, "SONG-2");
        await Reject(game.State, "P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["FORGED"]));
        await Reject(game.State, "P1", new PayCostCommand(request.Id + ":STALE", "RULE_REPLACEMENT", [option.Id]));
        var cold = MatchSession.Restore(Frame(initial, game, game.State), new CoreRuleEngine(), NoopMatchJournal.Instance);
        await Choose(game, "SONG-2");
        var accepted = game.Commands()[^1]; var raw = accepted.RawCommand!.Value;
        var resumed = await cold.SubmitAsync(accepted.PlayerId, accepted.ClientIntentId, GameCommandJsonMapper.Map(raw), raw, default);
        Assert.True(resumed.Accepted, resumed.ErrorMessage);
        Assert.Equal(MatchStateHasher.Hash(game.State), MatchStateHasher.Hash(resumed.State));
        var duplicate = await cold.SubmitAsync(accepted.PlayerId, accepted.ClientIntentId, GameCommandJsonMapper.Map(raw), raw, default);
        Assert.True(duplicate.Accepted); Assert.Equal(MatchStateHasher.Hash(resumed.State), MatchStateHasher.Hash(duplicate.State));
        var commands = game.Commands(); var changed = JsonNode.Parse(raw.GetRawText())!.AsObject();
        changed["paymentChoiceIds"] = JsonSerializer.SerializeToNode(new[] { Option(request, "HOST").Id }, Web);
        commands[^1] = accepted with { RawCommand = JsonSerializer.SerializeToElement(changed, Web) };
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, game.State, new CoreRuleEngine(), default, game.Events());
        Assert.False(replay.IsMatch); Assert.NotEmpty(replay.Errors); await game.Replay();
    }

    [Fact]
    public async Task PendingChoicesRejectChangedOptionsPreviewControllerAndCapturedEquipmentBindings()
    {
        var initial = Put(Position(), "SPELL", Extort, "P1", "HAND");
        var game = new Driver(initial); await Enter(game, "VEN·086");
        await game.Act("P1", new PlayCardCommand("SPELL", Extort, ["HOST"])); await game.Top();
        var pending = game.State.PendingRuleChoice!; var request = AssertChoice(game, "P1"); var first = request.Options[0];
        MatchState ChangedOrigin(string id, Func<CardObjectState, CardObjectState> change) => pending.Origin with {
            CardObjects = new Dictionary<string, CardObjectState>(pending.Origin.CardObjects) { [id] = change(pending.Origin.CardObjects[id]) } };
        var variants = new[] {
            pending with { Request = request with { Options = [first with { Id = "FORGED" }, ..request.Options.Skip(1)] } },
            pending with { Request = request with { Options = [first with { Label = "FORGED" }, ..request.Options.Skip(1)] } },
            pending with { Request = request with { Options = [first with { ObjectIds = ["HOST", "P1-D0"] }, ..request.Options.Skip(1)] } },
            pending with { Request = request with { PlayerId = "P2" } },
            pending with { Request = request with { Kind = "DAMAGE_REPLACEMENT_ORDER" } },
            pending with { Request = request with { Preview = request.Preview! with { CardObjects = new Dictionary<string, CardObjectState>(request.Preview!.CardObjects) {
                ["HOST"] = request.Preview.CardObjects["HOST"] with { IsEmpowered = false } } } } },
            pending with { Origin = ChangedOrigin("HOST", c => c with { ObjectGeneration = c.ObjectGeneration + 1 }) },
            pending with { Origin = ChangedOrigin("SONG-2", c => c with { AttachmentRevision = c.AttachmentRevision + 1 }) },
            pending with { Origin = ChangedOrigin("SONG-2", c => c with { AttachedToObjectId = "ALLY" }) },
            pending with { Answers = [new RuleChoiceAnswer(MatchStateHasher.HashValue(request), first.Id)] }
        };
        foreach (var variant in variants) {
            var state = game.State with { PendingRuleChoice = variant };
            var frame = Frame(initial, game, state);
            var errors = MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands, frame.Events,
                frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame);
            Assert.Contains(errors, e => e.Contains("invalid replacement choice continuation", StringComparison.Ordinal));
        }
        await Choose(game, "HOST"); await game.Replay();
    }

    [Fact]
    public async Task TwoRealFiniteShieldsKeepIndependentBalancesAcrossFourRealDamageEvents()
    {
        var initial = Put(Put(Create("OGN·278/298"), "SHIELD-1", "VEN·126", "P1", "HAND"), "SHIELD-2", "VEN·126", "P1", "HAND");
        initial = RadUnitEffectTestFixture.Place(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        for (var i = 1; i <= 4; i++) initial = Put(initial, "HIT-" + i, "VEN·059", "P1", "HAND");
        var game = new Driver(initial);
        foreach (var id in new[] { "SHIELD-1", "SHIELD-2" }) {
            await game.Act("P1", new PlayCardCommand(id, "VEN·126", ["ALLY"])); await game.Top();
        }
        string Budget(string source) => (string)Assert.Single(game.Events(), e => e.Event.Kind == "DAMAGE_PREVENTION_GRANTED"
            && Equals(e.Event.Payload["sourceObjectId"], source)).Event.Payload["budgetId"]!;
        var first = Budget("SHIELD-1"); var second = Budget("SHIELD-2"); Assert.NotEqual(first, second);
        await Hit(1); await Select(second);
        AssertBalances(7, 3); await Hit(2); await Select(second);
        Assert.Null(game.State.PendingRuleChoice); AssertBalances(6, 0);
        await Hit(3); Assert.Null(game.State.PendingRuleChoice); AssertBalances(2, 0);
        await Hit(4); Assert.Null(game.State.PendingRuleChoice); AssertBalances(0, 0, damage: 2);
        Assert.Equal(2, game.State.CardObjects["ALLY"].Damage);
        await game.Act("P1", new EndTurnCommand());
        Assert.Empty(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects); await game.Replay();

        async Task Hit(int index) {
            await game.Act("P1", new PlayCardCommand("HIT-" + index, "VEN·059", ["ALLY"])); await game.Top();
        }
        async Task Select(string id) {
            var p = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
            Assert.Equal("P1", p.PlayerId); Assert.Equal("DAMAGE_REPLACEMENT_ORDER", p.Kind);
            Assert.Equal(new[] { first, second }.Order(StringComparer.Ordinal), p.Options.Select(o => o.Id).Order(StringComparer.Ordinal));
            await game.Act("P1", new PayCostCommand(p.Id, "RULE_REPLACEMENT", [id]));
        }
        void AssertBalances(int left, int right, int damage = 0) {
            var effects = game.State.CardObjects["ALLY"].UntilEndOfTurnEffects;
            foreach (var (id, remaining) in new[] { (first, left), (second, right) }) {
                if (remaining > 0) Assert.Contains(id + ":" + remaining, effects);
                else Assert.DoesNotContain(effects, e => e.StartsWith(id + ":", StringComparison.Ordinal));
            }
            Assert.Equal(damage, game.State.CardObjects["ALLY"].Damage);
        }
    }

    private static MatchState Position(string printing = "VEN·086")
        => Put(Put(Put(Create(), "HOST", printing, "P1", "HAND"), "SONG-1", Song, "P1", "HAND"), "SONG-2", Song, "P1", "HAND");
    private static async Task Enter(Driver game, string printing, bool empower = true, bool battlefield = false)
    {
        await game.Act("P1", new PlayCardCommand("HOST", printing, [], Destination: battlefield ? "BATTLEFIELD:BF" : "BASE"));
        if (game.State.StackItems.Count > 0) await game.Top();
        if (empower) { await game.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", [])); await game.Top(); }
        foreach (var id in new[] { "SONG-1", "SONG-2" }) {
            await game.Act("P1", new PlayCardCommand(id, Song, [], Destination: "BASE"));
            if (game.State.StackItems.Count > 0) await game.Top();
            await game.Act("P1", new AssembleEquipmentCommand(id, "HOST", ["ASSEMBLE_GREEN"])); await game.Top();
            Assert.Equal("HOST", game.State.CardObjects[id].AttachedToObjectId);
        }
    }
    private static int Might(MatchState state)
        => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)["P1"], Web).GetProperty("players")
            .GetProperty(state.CardObjects["HOST"].ControllerId!).GetProperty("objects").GetProperty("HOST").GetProperty("effectivePower").GetInt32();
    private static RuleChoiceRequest AssertChoice(Driver game, string controller)
    {
        var request = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        Assert.Equal(controller, request.PlayerId); Assert.Equal("TARGETED_ACTION_REPLACEMENT", request.Kind);
        Assert.Equal(3, request.Options.Count); Assert.Equal(3, request.Options.Select(o => o.Id).Distinct().Count());
        Assert.All(request.Options, o => { Assert.Matches("^TARGET_REPLACEMENT:[0-9a-f]{64}$", o.Id); Assert.Contains("HOST", o.ObjectIds!); });
        var other = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(game.State)[controller == "P1" ? "P2" : "P1"], Web);
        Assert.All(request.Options, o => Assert.DoesNotContain(o.Id, other));
        return request;
    }
    private static RuleChoiceOption Option(RuleChoiceRequest request, string source)
        => Assert.Single(request.Options, o => source == "HOST" ? o.ObjectIds!.SequenceEqual(new[] { "HOST" }) : o.ObjectIds!.Contains(source));
    private static Task Choose(Driver game, string source)
    {
        var request = game.State.PendingRuleChoice!.Request;
        return game.Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [Option(request, source).Id]));
    }
    private static MatchRecoveryFrame Frame(MatchState initial, Driver game, MatchState state)
    {
        var sequence = game.Events()[^1].Sequence; var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(player, state.Tick, sequence, snapshots[player], state.Tick, sequence, prompts[player]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, game.Commands(), game.Events(), views, [], state, initial, spectator) { OpeningSeed = initial.Seed };
        return JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Web), Web)!;
    }
}
