using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MatchRewindTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly MatchSessionOptions Room = MatchSessionOptions.Default with { RewindAllowed = true };
    private static readonly string[] Players = ["alice", "bob"];

    [Fact]
    public async Task RewindRestoresExactStateAndRepeatsRandomDrawsInMemory()
    {
        var session = new MatchSession("rewind-memory", new CoreRuleEngine(), NoopMatchJournal.Instance,
            NoopMatchPlayerStore.Instance, Room);
        var before = await OpenConstructed(session);
        var actor = before.State.ActivePlayerId;
        var opponent = Other(actor);
        var oldPrompt = session.PromptFor(actor);
        var mulligan = new MulliganCommand(before.State.PlayerZones[actor].Hand.Take(2).ToArray());
        var played = Accepted(await Send(session, actor, "original", mulligan, oldPrompt));
        Assert.NotEqual(before.State.RngCursor, played.State.RngCursor);
        var target = session.SnapshotFor(opponent).RewindTargets!.Last();
        Assert.Equal(actor, target.PlayerId);
        Assert.Equal("调整起手牌", target.Description);
        var chosenName = OfficialCardSourceIdentityGroups.KnownDisplayName(before.State.CardObjects[mulligan.HandObjectIds[0]].CardNo);
        Assert.NotNull(chosenName);
        Assert.Contains($"「{chosenName}」", played.Snapshots[actor].RewindTargets!.Last().Description);
        Assert.Equal(target, played.Snapshots[opponent].RewindTargets!.Last());

        var request = await Request(session, opponent, "request", target.Step);
        Assert.Equal(new[] { CommandTypes.AcceptRewind, CommandTypes.RejectRewind },
            session.PromptFor(actor).RewindCandidates!.Select(candidate => candidate.Action));
        Assert.Equal(new RewindCandidateDto(CommandTypes.CancelRewind, request.RequestStep),
            Assert.Single(session.PromptFor(opponent).RewindCandidates!));
        var byRequester = await Send(session, opponent, "wrong-seat", new AcceptRewindCommand(request.RequestStep));
        Assert.Equal(ErrorCodes.PhaseNotAllowed, byRequester.ErrorCode);
        Assert.Equal(request, session.SnapshotFor(actor).RewindRequest);

        var answerPrompt = session.PromptFor(actor);
        var rewound = Accepted(await Send(session, actor, "agree", new AcceptRewindCommand(request.RequestStep), answerPrompt));
        Assert.Same(before.State, rewound.State);
        Assert.Equal(MatchStateHasher.Hash(before.State), MatchStateHasher.Hash(rewound.State));
        Assert.True(rewound.ServerTick > played.ServerTick);
        var publicEvent = Assert.Single(rewound.Events);
        Assert.Equal("MATCH_REWOUND", publicEvent.Kind);
        Assert.Null(rewound.Snapshots[opponent].RewindRequest);
        Assert.NotEqual(oldPrompt.PromptId, rewound.Prompts[actor].PromptId);
        var fresh = session.PromptFor(actor);
        var stale = await Send(session, actor, "stale", mulligan, oldPrompt);
        Assert.Equal(ErrorCodes.PromptExpired, stale.ErrorCode);
        var repeat = Accepted(await Send(session, actor, "new-mulligan", mulligan, fresh));
        Assert.Equal(MatchStateHasher.Hash(played.State), MatchStateHasher.Hash(repeat.State));
        Assert.Equal(played.State.PlayerZones[actor].Hand, repeat.State.PlayerZones[actor].Hand);
        Assert.Equal(played.State.PlayerZones[actor].MainDeck, repeat.State.PlayerZones[actor].MainDeck);

        var projected = JsonSerializer.Serialize(new { rewound.Snapshots[opponent].RewindTargets,
            Prompt = rewound.Prompts[opponent], View = rewound.Snapshots[opponent], Event = publicEvent }, Json);
        foreach (var id in before.State.PlayerZones[actor].Hand
            .Concat(before.State.PlayerZones[actor].MainDeck).Concat(before.State.PlayerZones[actor].RuneDeck))
            Assert.DoesNotContain(id, projected);
        Assert.DoesNotContain("rngCursor", projected);
        Assert.DoesNotContain("\"seed\":", projected);

        var oldAgreement = Accepted(await Send(session, actor, "agree",
            new AcceptRewindCommand(request.RequestStep), answerPrompt));
        Assert.True(oldAgreement.IsDuplicate);
        Assert.Equal(rewound.ServerTick, oldAgreement.ServerTick);
        Assert.Equal(repeat.ServerTick, session.SnapshotFor(actor).Tick);
    }

    [Fact]
    public async Task RefusalWithdrawalAndAcceptedGameplayClearOnlyThePendingRequest()
    {
        var session = new MatchSession("rewind-refusal", new CoreRuleEngine(), NoopMatchJournal.Instance,
            NoopMatchPlayerStore.Instance, Room);
        var before = await OpenConstructed(session);
        var actor = before.State.ActivePlayerId;
        var opponent = Other(actor);
        var target = Assert.Single(session.SnapshotFor(actor).RewindTargets!).Step;

        var first = await Request(session, actor, "first", target);
        var second = await Send(session, opponent, "second-pending", new RequestRewindCommand(target));
        Assert.False(second.Accepted);
        Assert.Equal(first, session.SnapshotFor(actor).RewindRequest);
        var refused = Accepted(await Send(session, opponent, "refuse", new RejectRewindCommand(first.RequestStep)));
        Assert.Same(before.State, refused.State);
        Assert.Equal("REWIND_REJECTED", Assert.Single(refused.Events).Kind);
        Assert.Null(session.SnapshotFor(actor).RewindRequest);

        var withdrawn = await Request(session, opponent, "will-withdraw", target);
        var cancelled = Accepted(await Send(session, opponent, "withdraw", new CancelRewindCommand(withdrawn.RequestStep)));
        Assert.Same(before.State, cancelled.State);
        Assert.Equal("REWIND_CANCELLED", Assert.Single(cancelled.Events).Kind);
        Assert.Equal(target, Assert.Single(session.SnapshotFor(actor).RewindTargets!).Step);
        foreach (var controlStep in new[] { first.RequestStep, first.RequestStep + 1, withdrawn.RequestStep, withdrawn.RequestStep + 1 })
        {
            var excluded = await Send(session, actor, $"excluded-{controlStep}", new RequestRewindCommand(controlStep));
            Assert.False(excluded.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, excluded.ErrorCode);
        }

        var pending = await Request(session, opponent, "request-again", target);
        var failed = await Send(session, opponent, "wrong-mulligan", new MulliganCommand([]));
        Assert.False(failed.Accepted);
        Assert.Equal(pending, session.SnapshotFor(actor).RewindRequest);
        Accepted(await Send(session, actor, "mulligan", new MulliganCommand([])));
        Assert.Null(session.SnapshotFor(actor).RewindRequest);
        var expired = await Send(session, actor, "late-agreement", new AcceptRewindCommand(pending.RequestStep));
        Assert.Equal(ErrorCodes.InvalidTarget, expired.ErrorCode);
    }

    [Fact]
    public async Task RewindRestoresAnOpenChainAndProjectsAHiddenStandbyCard()
    {
        const string faceDown = "P1-FACEDOWN-OGN-TEEMO-PURPLE";
        const string cardNo = "OGN·197/298";
        var session = new MatchSession("rewind-standby", new CoreRuleEngine(), NoopMatchJournal.Instance,
            NoopMatchPlayerStore.Instance, Room);
        await session.EnsurePlayerAsync("P1", default);
        await session.EnsurePlayerAsync("P2", default);
        var hidden = Accepted(await session.SeedScenarioAsync("P1", "seed", "standby-reaction", null, default));
        var reveal = new RevealCardCommand(faceDown, cardNo, [], "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "STACK");
        Accepted(await Send(session, "P1", "reveal", reveal));
        var revealTarget = session.SnapshotFor("P2").RewindTargets!.Last();
        var revealedName = OfficialCardSourceIdentityGroups.KnownDisplayName(cardNo);
        Assert.NotNull(revealedName);
        Assert.Equal($"翻开待命牌「{revealedName}」", revealTarget.Description);
        var revealStep = revealTarget.Step;
        var open = Accepted(await Send(session, "P1", "pass-p1", new PassPriorityCommand()));
        Assert.NotEmpty(open.State.StackItems);
        Accepted(await Send(session, "P2", "pass-p2", new PassPriorityCommand()));
        var resolveStep = session.SnapshotFor("P1").RewindTargets!.Last().Step;

        var chainRequest = await Request(session, "P1", "request-chain", resolveStep);
        var restored = Accepted(await Send(session, "P2", "accept-chain", new AcceptRewindCommand(chainRequest.RequestStep)));
        Assert.Equal(MatchStateHasher.Hash(open.State), MatchStateHasher.Hash(restored.State));
        Assert.Contains(CommandTypes.PassPriority, restored.Prompts["P2"].Actions);

        var hideRequest = await Request(session, "P2", "request-hidden", revealStep);
        var again = Accepted(await Send(session, "P1", "accept-hidden", new AcceptRewindCommand(hideRequest.RequestStep)));
        Assert.Equal(MatchStateHasher.Hash(hidden.State), MatchStateHasher.Hash(again.State));
        Assert.True(again.State.CardObjects[faceDown].IsFaceDown);
        var projected = JsonSerializer.Serialize(new { View = again.Snapshots["P2"], Prompt = again.Prompts["P2"], again.Events }, Json);
        Assert.DoesNotContain(faceDown, projected);
        Assert.DoesNotContain(JsonSerializer.Serialize(cardNo, Json), projected);
    }

    [Fact]
    public async Task RewindRestoresTheExactPaymentWindowAndLetsThePlayerPayAgain()
    {
        var session = new MatchSession("rewind-payment", new CoreRuleEngine(), NoopMatchJournal.Instance,
            NoopMatchPlayerStore.Instance, Room);
        await session.EnsurePlayerAsync("P1", default);
        await session.EnsurePlayerAsync("P2", default);
        Accepted(await session.SeedScenarioAsync("P1", "seed", "typed-power-payment", null, default));
        var result = Accepted(await Send(session, "P1", "play",
            new PlayCardCommand("P1-SPELL-BULLET-TIME", "OGN·268/298", [SelectedBattlefieldFixture.Field])));
        for (var pass = 0; pass < 2; pass++)
            result = Accepted(await Send(session, result.State.PriorityPlayerId!, $"pass-{pass}", new PassPriorityCommand()));
        var originalPrompt = session.PromptFor("P1");
        var candidate = Assert.Single(originalPrompt.Candidates!, item => item.Action == CommandTypes.PayCost);
        var payment = new PayCostCommand(Assert.IsType<string>(candidate.Metadata!["paymentId"]),
            Assert.IsType<string>(candidate.Metadata["paymentWindow"]), ["SPEND_POWER:red:2"]);
        var paid = Accepted(await Send(session, "P1", "pay", payment, originalPrompt));
        var target = session.SnapshotFor("P2").RewindTargets!.Last().Step;
        var request = await Request(session, "P2", "request", target);
        var restored = Accepted(await Send(session, "P1", "accept", new AcceptRewindCommand(request.RequestStep)));
        Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(restored.State));
        Assert.NotEqual(originalPrompt.PromptId, restored.Prompts["P1"].PromptId);
        var again = Accepted(await Send(session, "P1", "pay-again", payment, restored.Prompts["P1"]));
        Assert.Equal(MatchStateHasher.Hash(paid.State), MatchStateHasher.Hash(again.State));
    }

    [Fact]
    public async Task QuickMatchAndFinishedRoomsRejectRewind()
    {
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        var queue = new InMemoryMatchmakingQueue(registry, () => "RB-QUICK");
        await queue.EnqueueAsync("alice", default);
        var matched = await queue.EnqueueAsync("bob", default);
        var quick = await registry.GetOrCreatePlayerRoomAsync(matched.RoomId!, default);
        Accepted(await quick.ReadyAsync("alice", "ready", null, default));
        Accepted(await quick.ReadyAsync("bob", "ready", null, default));
        Assert.False(quick.SnapshotFor("alice").RewindAllowed);
        Assert.Null(quick.SnapshotFor("alice").RewindTargets);
        Assert.Equal(ErrorCodes.PhaseNotAllowed,
            (await Send(quick, "alice", "quick-rewind", new RequestRewindCommand(1))).ErrorCode);

        var manual = await registry.GetOrCreatePlayerRoomAsync("RB-MANUAL", default);
        await manual.EnsurePlayerAsync("alice", default);
        await manual.EnsurePlayerAsync("bob", default);
        Accepted(await manual.ReadyAsync("alice", "ready", null, default));
        Accepted(await manual.ReadyAsync("bob", "ready", null, default));
        Assert.True(manual.SnapshotFor("alice").RewindAllowed);
        var target = Assert.Single(manual.SnapshotFor("alice").RewindTargets!);
        var pending = await Request(manual, "alice", "request-finish", target.Step);
        var finished = Accepted(await Send(manual, "alice", "surrender", new SurrenderCommand()));
        Assert.NotNull(finished.State.WinnerPlayerId);
        Assert.Null(manual.SnapshotFor("alice").RewindRequest);
        Assert.Equal(ErrorCodes.MatchFinished,
            (await Send(manual, "bob", "late", new AcceptRewindCommand(pending.RequestStep))).ErrorCode);
        Assert.Equal(ErrorCodes.MatchFinished,
            (await Send(manual, "alice", "finished", new RequestRewindCommand(target.Step))).ErrorCode);
    }

    [Fact]
    public async Task RecoveryRestoresPendingRequestsAndBothBranchesWithoutRewritingTheJournal()
    {
        var engine = new CoreRuleEngine();
        var journal = new SealedSubmittedOpeningTests.Journal();
        var store = new PlayerStore();
        IMatchSession live = new MatchSession("rewind-recovery", engine, journal, store, Room);
        var before = await OpenConstructed(live);
        var actor = before.State.ActivePlayerId;
        var opponent = Other(actor);
        var beforeMulligan = new MulliganCommand(before.State.PlayerZones[actor].Hand.Take(2).ToArray());
        var played = Accepted(await Send(live, actor, "first-branch", beforeMulligan));
        var target = live.SnapshotFor(actor).RewindTargets!.Last();
        var request = await Request(live, opponent, "request", target.Step);
        var token = await live.EnsurePlayerAsync(actor, default);
        live = await Recover(journal, store, engine);
        Assert.Equal(request, live.SnapshotFor(actor).RewindRequest);
        Assert.Equal(token.PlayerId, (await live.ReconnectPlayerAsync(actor, token.ReconnectToken, default)).PlayerId);

        var original = journal.Entries.ToArray();
        var agreement = new AcceptRewindCommand(request.RequestStep);
        journal.FailNext = true;
        await Assert.ThrowsAsync<IOException>(() => Send(live, actor, "agree", agreement));
        Assert.Equal(request, live.SnapshotFor(actor).RewindRequest);
        Assert.Equal(original.Length, journal.Entries.Count);
        var rewound = Accepted(await Send(live, actor, "agree", agreement));
        Assert.Equal(MatchStateHasher.Hash(before.State), MatchStateHasher.Hash(rewound.State));
        Assert.Equal(original, journal.Entries.Take(original.Length));
        var visibleTargets = live.SnapshotFor(actor).RewindTargets!.ToArray();
        Assert.Equal(3, visibleTargets.Length);
        Assert.DoesNotContain(visibleTargets, item => item.Step == request.RequestStep);
        var undo = visibleTargets[^1];
        Assert.Equal($"撤销回到第 {target.TurnNumber} 回合的回退", undo.Description);

        live = await Recover(journal, store, engine);
        Assert.Equal(visibleTargets, live.SnapshotFor(actor).RewindTargets!.ToArray());
        Assert.Equal(rewound.Snapshots[opponent].RewindTargets!.ToArray(),
            live.SnapshotFor(opponent).RewindTargets!.ToArray());
        var retry = Accepted(await Send(live, actor, "agree", agreement));
        Assert.True(retry.IsDuplicate);
        Assert.Equal(rewound.ServerTick, retry.ServerTick);
        Assert.Equal(MatchStateHasher.Hash(rewound.State), MatchStateHasher.Hash(retry.State));
        Assert.Equal("MATCH_REWOUND", Assert.Single(retry.Events).Kind);
        var alternative = Accepted(await Send(live, actor, "alternative", new MulliganCommand([])));
        Assert.NotEqual(MatchStateHasher.Hash(played.State), MatchStateHasher.Hash(alternative.State));
        live = await Recover(journal, store, engine);

        var revisit = await Request(live, opponent, "revisit", undo.Step);
        var returned = Accepted(await Send(live, actor, "return", new AcceptRewindCommand(revisit.RequestStep)));
        Assert.Equal(MatchStateHasher.Hash(played.State), MatchStateHasher.Hash(returned.State));
        await Recover(journal, store, engine);
    }

    [Fact]
    public async Task PreviouslyAcceptedRequestForAControlStepStillReplays()
    {
        var engine = new CoreRuleEngine();
        var journal = new SealedSubmittedOpeningTests.Journal();
        var store = new PlayerStore();
        var session = new MatchSession("rewind-legacy", engine, journal, store, Room);
        var before = await OpenConstructed(session);
        var actor = before.State.ActivePlayerId;
        var opponent = Other(actor);
        var target = Assert.Single(session.SnapshotFor(actor).RewindTargets!);
        var first = await Request(session, actor, "request", target.Step);
        Accepted(await Send(session, opponent, "reject", new RejectRewindCommand(first.RequestStep)));

        // Reproduce a request that was valid before control steps were removed from the menu.
        var legacy = Accepted(await session.SubmitAsync(actor, "legacy-request",
            new RequestRewindCommand(first.RequestStep), null, default, replayAcceptedCommand: true));
        var pending = Assert.IsType<RewindRequestDto>(legacy.Snapshots[opponent].RewindRequest);
        var recovered = await Recover(journal, store, engine);
        Assert.Equal(pending, recovered.SnapshotFor(opponent).RewindRequest);
        Assert.Equal(target, Assert.Single(recovered.SnapshotFor(opponent).RewindTargets!));
        var result = Accepted(await Send(recovered, opponent, "accept-legacy", new AcceptRewindCommand(pending.RequestStep)));
        Assert.Equal(MatchStateHasher.Hash(before.State), MatchStateHasher.Hash(result.State));
        await Recover(journal, store, engine);
    }

    [Fact]
    public async Task TargetDescriptionsShowPublicPlayAndHideInsightAndStandbyChoicesFromOpponent()
    {
        const string playCard = "UNL-063/219";
        var initial = OfficialInsightAndSpellLockTests.Position(playCard) with { SessionOptions = Room };
        var game = new MatchSession(initial, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var play = Accepted(await Send(game, "P1", "play", OfficialInsightAndSpellLockTests.Play(playCard)));
        var publicName = OfficialCardSourceIdentityGroups.KnownDisplayName(playCard);
        Assert.NotNull(publicName);
        Assert.All(play.Snapshots.Values, view => Assert.Equal($"打出「{publicName}」", view.RewindTargets!.Last().Description));

        var pass = Accepted(await Send(game, play.State.PriorityPlayerId!, "pass", new PassPriorityCommand()));
        var open = Accepted(await Send(game, pass.State.PriorityPlayerId!, "resolve", new PassPriorityCommand()));
        var choice = Assert.IsType<PendingCardChoiceState>(open.State.PendingCardChoice);
        var chosenId = Assert.Single(choice.LegalObjectIds);
        var privateName = OfficialCardSourceIdentityGroups.KnownDisplayName(open.State.CardObjects[chosenId].CardNo);
        Assert.NotNull(privateName);
        var chosen = Accepted(await Send(game, "P1", "insight",
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [chosenId])));
        Assert.Equal($"洞察回收「{privateName}」", chosen.Snapshots["P1"].RewindTargets!.Last().Description);
        Assert.Equal("完成选牌", chosen.Snapshots["P2"].RewindTargets!.Last().Description);

        const string standbyCard = "OGN·057/298";
        var standby = new MatchSession(StandbySourceRulesTests.Position(standbyCard) with { SessionOptions = Room },
            new CoreRuleEngine(), NoopMatchJournal.Instance);
        var hide = Accepted(await Send(standby, "P1", "hide", new HideCardCommand("SHADOW", standbyCard, "BATTLEFIELD:F", ["STANDBY_A"])));
        var hiddenName = OfficialCardSourceIdentityGroups.KnownDisplayName(standbyCard);
        Assert.NotNull(hiddenName);
        Assert.Equal($"设置待命牌「{hiddenName}」", hide.Snapshots["P1"].RewindTargets!.Last().Description);
        Assert.Equal("设置待命牌", standby.SnapshotFor("P2").RewindTargets!.Last().Description);
    }

    [Fact]
    public async Task MovementAndActivationDescribeTheirPublicSources()
    {
        var moveState = OfficialInsightSourceTests.Diana() with { SessionOptions = Room };
        var moveGame = new MatchSession(moveState, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var move = Accepted(await Send(moveGame, "P1", "move",
            new MoveUnitCommand("UNIT", Destination: "BATTLEFIELD:HILL", SourceObjectIds: ["UNIT"])));
        var unit = OfficialCardSourceIdentityGroups.KnownDisplayName(moveState.CardObjects["UNIT"].CardNo);
        var field = OfficialCardSourceIdentityGroups.KnownDisplayName(moveState.CardObjects["HILL"].CardNo);
        Assert.NotNull(unit);
        Assert.NotNull(field);
        Assert.All(move.Snapshots.Values, view => Assert.Equal($"移动「{unit}」到「{field}」", view.RewindTargets!.Last().Description));

        const string card = P4ActivatedAbilityCatalog.BlackMarketCardNo;
        var abilityGame = new MatchSession(RadGearTestPosition.Create(card) with { SessionOptions = Room },
            new CoreRuleEngine(), NoopMatchJournal.Instance);
        var activation = Accepted(await Send(abilityGame, "P1", "activate",
            new ActivateAbilityCommand("BF", P4ActivatedAbilityCatalog.BlackMarketAbilityId, [])));
        var source = OfficialCardSourceIdentityGroups.KnownDisplayName(card);
        Assert.NotNull(source);
        Assert.All(activation.Snapshots.Values, view => Assert.Equal($"发动「{source}」的能力", view.RewindTargets!.Last().Description));
    }

    [Fact]
    public async Task BatchedRuneActionsDescribeThePublicCount()
    {
        var game = new MatchSession(RuneBatchTests.Position() with { SessionOptions = Room },
            new CoreRuleEngine(), NoopMatchJournal.Instance);
        var tap = Accepted(await Send(game, "P1", "tap", new TapRuneCommand("RED", ["RED", "BLUE"])));
        Assert.All(tap.Snapshots.Values, view => Assert.Equal("横置 2 枚符文", Assert.Single(view.RewindTargets!).Description));
    }

    [Fact]
    public async Task BestOfThreeCannotTargetStepsFromAnEarlierGame()
    {
        var run = new SealedSubmittedOpeningTests.Run(bestOf: 3, rewindAllowed: true);
        await run.Join("alice");
        await run.Join("bob");
        await run.Choose();
        await SealedSubmittedOpeningTests.OpenCurrentGame(run, 3);
        var old = Assert.Single(run.Live.SnapshotFor("alice").RewindTargets!);
        Assert.Equal(1, old.GameNumber);
        await run.Act("alice", new SurrenderCommand());
        Assert.Equal(ErrorCodes.MatchNotStarted,
            (await Send(run.Live, "alice", "between-games", new RequestRewindCommand(old.Step))).ErrorCode);
        await run.Choose();
        await SealedSubmittedOpeningTests.OpenCurrentGame(run, 3);
        var current = Assert.Single(run.Live.SnapshotFor("alice").RewindTargets!);
        Assert.Equal(2, current.GameNumber);
        var before = run.Journal.Entries[^2].AuthoritativeState;
        Assert.Equal(ErrorCodes.InvalidTarget,
            (await Send(run.Live, "alice", "cross-game", new RequestRewindCommand(old.Step))).ErrorCode);
        var pending = await Request(run.Live, "alice", "current-game", current.Step);
        var rewound = Accepted(await Send(run.Live, "bob", "agree-current", new AcceptRewindCommand(pending.RequestStep)));
        Assert.Equal(MatchStateHasher.Hash(before), MatchStateHasher.Hash(rewound.State));
        Assert.Equal(2, rewound.Snapshots["alice"].Series!.GameNumber);
        await run.Cold();
    }

    private static async Task<ResolutionResult> OpenConstructed(IMatchSession session)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var deck = PreconstructedDeckCatalog.Build(catalog).First().Decklist;
        foreach (var player in Players)
        {
            await session.EnsurePlayerAsync(player, default);
            Accepted(await session.SubmitDeckAsync(player, "deck",
                new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields),
                null, default));
        }
        Accepted(await session.ReadyAsync("alice", "ready", null, default));
        return Accepted(await session.ReadyAsync("bob", "ready", null, default));
    }

    private static string Other(string player) => Players.Single(id => id != player);

    private static ResolutionResult Accepted(ResolutionResult result)
    {
        Assert.True(result.Accepted, result.ErrorMessage);
        return result;
    }

    private static async Task<RewindRequestDto> Request(IMatchSession match, string actor, string id, long step)
        => Accepted(await Send(match, actor, id, new RequestRewindCommand(step), match.PromptFor(actor)))
            .Snapshots[actor].RewindRequest!;

    private static async Task<ResolutionResult> Send(IMatchSession match, string actor, string id, GameCommand command,
        ActionPromptDto? prompt = null)
    {
        JsonElement? raw = null;
        if (prompt is not null)
        {
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            node["promptId"] = prompt.PromptId;
            node["snapshotTick"] = prompt.SnapshotTick;
            raw = JsonSerializer.SerializeToElement(node, Json);
        }
        return await match.SubmitAsync(actor, id, command, raw, default);
    }

    private static async Task<IMatchSession> Recover(SealedSubmittedOpeningTests.Journal journal,
        PlayerStore players, IRuleEngine engine)
    {
        var last = journal.Entries[^1];
        var entries = journal.Entries;
        var commands = entries.Select(entry => new RecoveredCommand(entry.PlayerId, entry.ClientIntentId,
            entry.CommandType, entry.RawCommand, entry.StartedTick, entry.CompletedTick, entry.StartedEventSequence,
            entry.CompletedEventSequence, entry.Accepted, entry.ErrorMessage)
            { TypedCommand = entry.TypedCommand, ErrorCode = entry.ErrorCode }).ToArray();
        var events = entries.SelectMany(entry => entry.Events.Select((item, index) => new RecoveredEvent(
            entry.StartedEventSequence + index + 1, entry.CompletedTick, index, item))).ToArray();
        var views = last.Snapshots.ToDictionary(pair => pair.Key, pair => new RecoveredPlayerView(pair.Key,
            last.CompletedTick, last.CompletedEventSequence, pair.Value, last.CompletedTick, last.CompletedEventSequence,
            last.Prompts[pair.Key]));
        var initial = MatchState.Create(last.RoomId) with { Seed = last.OpeningSeed, SessionOptions = Room };
        var payload = JsonSerializer.Serialize(new MatchRecoveryFrame(last.RoomId, last.CompletedTick,
            last.CompletedEventSequence, commands, events, views, [], last.AuthoritativeState, initial,
            MatchReplayRedactor.BuildSpectatorFrame(last.RoomId, last.CompletedTick, last.CompletedEventSequence,
                events.Select(record => record.Event).ToArray(), last.AuthoritativeState)) { OpeningSeed = last.OpeningSeed }, Json);
        var frame = JsonSerializer.Deserialize<MatchRecoveryFrame>(payload, Json)!;
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(frame.ReplayInitialState!, frame.Commands,
            frame.AuthoritativeState!, engine, default, frame.Events);
        Assert.True(replay.IsMatch, string.Join("\n", replay.Errors));
        return await new InMemoryMatchSessionRegistry(engine, journal, new RecoveryStore(frame), players)
            .GetOrCreateAsync(last.RoomId, default);
    }

    private sealed class RecoveryStore(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken cancellationToken)
            => ValueTask.FromResult<MatchRecoveryFrame?>(frame);
    }

    private sealed class PlayerStore : IMatchPlayerStore
    {
        private readonly Dictionary<(string Room, string Player), string> hashes = [];
        public ValueTask SavePlayerSessionAsync(string roomId, string playerId, string seat, string reconnectTokenHash,
            CancellationToken cancellationToken, long openingSeed = 0)
        {
            hashes[(roomId, playerId)] = reconnectTokenHash;
            return ValueTask.CompletedTask;
        }
        public async ValueTask CommitRoomSeatAsync(MatchJournalEntry entry, string reconnectTokenHash, IMatchJournal journal,
            CancellationToken cancellationToken)
        {
            await journal.RecordAsync(entry, cancellationToken);
            await SavePlayerSessionAsync(entry.RoomId, entry.PlayerId, entry.AuthoritativeState.Seats[entry.PlayerId],
                reconnectTokenHash, cancellationToken);
        }
        public ValueTask<bool> HasReconnectTokenHashAsync(string roomId, string playerId, string reconnectTokenHash,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(hashes.GetValueOrDefault((roomId, playerId)) == reconnectTokenHash);
    }
}
