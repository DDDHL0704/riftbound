using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OfficialOpeningTests
{
    [Fact]
    public void GameCommandMapperParsesOfficialDeckAndMulliganPayloads()
    {
        var deckCommand = Assert.IsType<SubmitDeckCommand>(GameCommandJsonMapper.Map(JsonDocument.Parse("""
        {
          "cmdType": "SUBMIT_DECK",
          "legendCardNo": "UNL-181/219",
          "championCardNo": "UNL-022/219",
          "mainDeck": ["UNL-022/219"],
          "runeDeck": ["UNL-R01"],
          "battlefields": ["UNL-205/219"]
        }
        """).RootElement));

        Assert.Equal("UNL-181/219", deckCommand.LegendCardNo);
        Assert.Equal("UNL-022/219", deckCommand.ChampionCardNo);
        Assert.Equal(["UNL-022/219"], deckCommand.MainDeck);

        var mulliganCommand = Assert.IsType<MulliganCommand>(GameCommandJsonMapper.Map(JsonDocument.Parse("""
        {
          "cmdType": "MULLIGAN",
          "handObjectIds": ["P1-MAIN-001", "P1-MAIN-002"]
        }
        """).RootElement));

        Assert.Equal(["P1-MAIN-001", "P1-MAIN-002"], mulliganCommand.HandObjectIds);
    }

    [Fact]
    public async Task OfficialDeckValidatorRejectsCoreConstructionErrors()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var valid = BuildValidDeck(catalog);

        AssertValid(valid, catalog);

        AssertInvalid(valid with
        {
            MainDeck = valid.MainDeck.Take(39).ToArray()
        }, catalog, "主牌堆至少需要 40 张牌。");

        AssertInvalid(valid with
        {
            RuneDeck = valid.RuneDeck.Take(11).ToArray()
        }, catalog, "符文牌堆必须正好包含 12 张符文牌。");

        AssertInvalid(valid with
        {
            ChampionCardNo = "UNL-024/219",
            MainDeck = ReplaceFirst(valid.MainDeck, valid.ChampionCardNo, "UNL-024/219")
        }, catalog, "英雄牌的英雄标签必须与所选传奇一致。");

        var firstNonChampion = valid.MainDeck.First(cardNo => !string.Equals(cardNo, valid.ChampionCardNo, StringComparison.Ordinal));
        AssertInvalid(valid with
        {
            MainDeck = valid.MainDeck.Concat([firstNonChampion]).ToArray()
        }, catalog, "上限为 3 张。");

        AssertInvalid(valid with
        {
            Battlefields = [valid.Battlefields[0], valid.Battlefields[0], valid.Battlefields[1]]
        }, catalog, "战场牌组不能包含重名战场");
    }

    [Fact]
    public async Task OfficialDeckValidatorRejectsOfficialNegativeMatrix()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var valid = BuildValidDeck(catalog);
        var legend = catalog.Cards.Single(card => string.Equals(card.CardNo, valid.LegendCardNo, StringComparison.Ordinal));
        var allowedColors = legend.CardColorList.ToHashSet(StringComparer.Ordinal);
        var firstNonChampion = valid.MainDeck.First(cardNo => !string.Equals(cardNo, valid.ChampionCardNo, StringComparison.Ordinal));

        AssertInvalid(valid with
        {
            MainDeck = ReplaceFirst(valid.MainDeck, valid.ChampionCardNo, firstNonChampion)
        }, catalog, "主牌堆必须包含 1 张所选英雄牌");

        var runeCard = catalog.Cards.First(card => string.Equals(card.CardCategoryName, "符文", StringComparison.Ordinal));
        AssertInvalid(valid with
        {
            MainDeck = ReplaceFirst(valid.MainDeck, firstNonChampion, runeCard.CardNo)
        }, catalog, "不能使用类别：符文。");

        AssertInvalid(valid with
        {
            MainDeck = ReplaceFirst(valid.MainDeck, firstNonChampion, "UNKNOWN-000")
        }, catalog, "主牌堆引用未知牌号 UNKNOWN-000。");

        AssertInvalid(valid with
        {
            RuneDeck = ReplaceFirst(valid.RuneDeck, valid.RuneDeck[0], valid.ChampionCardNo)
        }, catalog, "必须是符文牌。");

        AssertInvalid(valid with
        {
            Battlefields = ReplaceFirst(valid.Battlefields, valid.Battlefields[0], valid.ChampionCardNo)
        }, catalog, "必须是战场牌。");

        var offTraitMainDeckCard = catalog.Cards
            .Where(card => card.CardCategoryName is "单位" or "英雄单位" or "装备" or "法术")
            .First(card => HasTraitsOutside(card, allowedColors));
        AssertInvalid(valid with
        {
            MainDeck = ReplaceFirst(valid.MainDeck, firstNonChampion, offTraitMainDeckCard.CardNo)
        }, catalog, "包含所选传奇不支持的特性");

        var offTraitRune = catalog.Cards
            .Where(card => string.Equals(card.CardCategoryName, "符文", StringComparison.Ordinal))
            .First(card => HasTraitsOutside(card, allowedColors));
        AssertInvalid(valid with
        {
            RuneDeck = ReplaceFirst(valid.RuneDeck, valid.RuneDeck[0], offTraitRune.CardNo)
        }, catalog, "包含所选传奇不支持的特性");
    }

    [Fact]
    public async Task SubmitDeckRejectsInvalidOfficialDeckWithChineseMessage()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var invalid = BuildValidDeck(catalog) with
        {
            MainDeck = []
        };
        var session = new MatchSession("official-invalid-deck-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var result = await session.SubmitDeckAsync(
            "P1",
            "submit-invalid-deck",
            ToSubmitCommand(invalid),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidDeck, result.ErrorCode);
        Assert.Contains("卡组不合法：", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("主牌堆至少需要 40 张牌。", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("invalid deck", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("mainDeck must", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(MatchStatuses.Seating, result.State.Status);
        Assert.Equal(MatchPhases.Room, result.State.Phase);
        Assert.Equal(TimingStates.Room, result.State.TimingState);
        Assert.Empty(result.Events);
        Assert.Empty(result.State.PlayerDecklists);
        Assert.Empty(result.State.ReadyPlayerIds);
        AssertRoomSetupIdlePromptQueue(result);

        foreach (var playerId in new[] { "P1", "P2" })
        {
            var prompt = result.Prompts[playerId];
            Assert.Equal(playerId, prompt.PlayerId);
            Assert.True(prompt.Actionable);
            Assert.Equal(PromptTypes.RoomSetup, prompt.View?.Type);
            Assert.Equal(["SUBMIT_DECK"], prompt.Actions);
            Assert.DoesNotContain("READY", prompt.Actions);
            Assert.Equal(result.State.Tick, prompt.SnapshotTick);
        }
    }

    [Fact]
    public async Task InvalidPromptScopedSubmitDeckKeepsRoomPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var invalidP1Deck = p1Deck with
        {
            MainDeck = []
        };
        var session = new MatchSession("official-invalid-prompt-submit-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var p2Submitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(p2Submitted.Accepted, p2Submitted.ErrorMessage);
        AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(p2Submitted, "P2", "P1", p2Deck);

        var prompt = p2Submitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["SUBMIT_DECK"], prompt.Actions);

        var p2SubmittedHash = MatchStateHasher.Hash(p2Submitted.State);
        var rejected = await session.SubmitDeckAsync(
            "P1",
            "submit-p1-invalid-prompt",
            ToSubmitCommand(invalidP1Deck),
            PromptScopedSubmitDeckRawCommand(invalidP1Deck, prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.InvalidDeck, rejected.ErrorCode);
        Assert.Contains("卡组不合法：", rejected.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("主牌堆至少需要 40 张牌。", rejected.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(rejected.Events);
        Assert.Equal(p2SubmittedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(p2Submitted.State.Tick, rejected.State.Tick);
        Assert.Empty(rejected.State.ReadyPlayerIds);
        Assert.False(rejected.State.PlayerDecklists.ContainsKey("P1"));
        Assert.Equal(p2Submitted.State.PlayerDecklists["P2"].MainDeck, rejected.State.PlayerDecklists["P2"].MainDeck);
        Assert.Equal(p2Submitted.State.PlayerDecklists["P2"].RuneDeck, rejected.State.PlayerDecklists["P2"].RuneDeck);
        Assert.Equal(p2Submitted.State.PlayerDecklists["P2"].Battlefields, rejected.State.PlayerDecklists["P2"].Battlefields);
        AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(rejected, "P2", "P1", p2Deck);

        var accepted = await session.SubmitDeckAsync(
            "P1",
            "submit-p1-valid-after-invalid-prompt",
            ToSubmitCommand(p1Deck),
            PromptScopedSubmitDeckRawCommand(p1Deck, prompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DECK_SUBMITTED", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(accepted, p1Deck, p2Deck);
    }

    [Fact]
    public async Task SnapshotMismatchPromptScopedSubmitDeckKeepsRoomPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-snapshot-mismatch-prompt-submit-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var p2Submitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(p2Submitted.Accepted, p2Submitted.ErrorMessage);
        AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(p2Submitted, "P2", "P1", p2Deck);

        var prompt = p2Submitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["SUBMIT_DECK"], prompt.Actions);
        var p2SubmittedHash = MatchStateHasher.Hash(p2Submitted.State);

        var staleSnapshotTick = prompt.SnapshotTick - 1;
        var rejected = await session.SubmitDeckAsync(
            "P1",
            "submit-p1-stale-snapshot-prompt",
            ToSubmitCommand(p1Deck),
            PromptScopedSubmitDeckRawCommand(p1Deck, prompt, staleSnapshotTick),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动快照已过期，请按最新状态重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(p2SubmittedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(p2Submitted.State.Tick, rejected.State.Tick);
        Assert.Empty(rejected.State.ReadyPlayerIds);
        Assert.False(rejected.State.PlayerDecklists.ContainsKey("P1"));
        Assert.Equal(p2Submitted.State.PlayerDecklists["P2"].MainDeck, rejected.State.PlayerDecklists["P2"].MainDeck);
        Assert.Equal(p2Submitted.State.PlayerDecklists["P2"].RuneDeck, rejected.State.PlayerDecklists["P2"].RuneDeck);
        Assert.Equal(p2Submitted.State.PlayerDecklists["P2"].Battlefields, rejected.State.PlayerDecklists["P2"].Battlefields);
        AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(rejected, "P2", "P1", p2Deck);
        Assert.Equal(prompt.PromptId, rejected.Prompts["P1"].PromptId);
        Assert.Equal(prompt.SnapshotTick, rejected.Prompts["P1"].SnapshotTick);

        var accepted = await session.SubmitDeckAsync(
            "P1",
            "submit-p1-valid-after-stale-snapshot-prompt",
            ToSubmitCommand(p1Deck),
            PromptScopedSubmitDeckRawCommand(p1Deck, prompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DECK_SUBMITTED", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(accepted, p1Deck, p2Deck);
    }

    [Fact]
    public async Task WrongPlayerPromptScopedSubmitDeckKeepsRoomPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-wrong-player-prompt-submit-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var p2Submitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(p2Submitted.Accepted, p2Submitted.ErrorMessage);
        AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(p2Submitted, "P2", "P1", p2Deck);

        var prompt = p2Submitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["SUBMIT_DECK"], prompt.Actions);
        var p2SubmittedHash = MatchStateHasher.Hash(p2Submitted.State);

        var rejected = await session.SubmitDeckAsync(
            "P2",
            "submit-p2-with-p1-prompt",
            ToSubmitCommand(p1Deck),
            PromptScopedSubmitDeckRawCommand(p1Deck, prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(p2SubmittedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(p2Submitted.State.Tick, rejected.State.Tick);
        Assert.Equal(p2Submitted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.False(rejected.State.PlayerDecklists.ContainsKey("P1"));
        AssertOfficialDecklist(rejected.State.PlayerDecklists["P2"], p2Deck);
        AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(rejected, "P2", "P1", p2Deck);
        Assert.Equal(prompt.PromptId, rejected.Prompts["P1"].PromptId);
        Assert.Equal(prompt.SnapshotTick, rejected.Prompts["P1"].SnapshotTick);

        var accepted = await session.SubmitDeckAsync(
            "P1",
            "submit-p1-valid-after-wrong-player-prompt",
            ToSubmitCommand(p1Deck),
            PromptScopedSubmitDeckRawCommand(p1Deck, prompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DECK_SUBMITTED", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(accepted, p1Deck, p2Deck);
    }

    [Fact]
    public async Task SubmitDeckRejectsAcceptedReplayWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var decklist = BuildValidDeck(catalog);
        var session = new MatchSession("official-submit-deck-replay-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var accepted = await session.SubmitDeckAsync(
            "P1",
            "submit-p1-accepted",
            ToSubmitCommand(decklist),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(MatchStatuses.Seating, accepted.State.Status);
        Assert.Equal(MatchPhases.Room, accepted.State.Phase);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DECK_SUBMITTED", StringComparison.Ordinal)));
        Assert.True(accepted.State.PlayerDecklists.ContainsKey("P1"));
        Assert.False(accepted.State.PlayerDecklists.ContainsKey("P2"));
        Assert.True(accepted.Prompts["P1"].Actionable);
        Assert.Equal(["READY"], accepted.Prompts["P1"].Actions);
        Assert.True(accepted.Prompts["P2"].Actionable);
        Assert.Equal(["SUBMIT_DECK"], accepted.Prompts["P2"].Actions);
        AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(accepted, "P1", "P2", decklist);

        var acceptedHash = MatchStateHasher.Hash(accepted.State);
        var replay = await session.SubmitDeckAsync(
            "P1",
            "submit-p1-replay",
            ToSubmitCommand(decklist),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, replay.ErrorCode);
        Assert.Equal("玩家已经提交相同卡组。", replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(accepted.State.PlayerDecklists["P1"].MainDeck, replay.State.PlayerDecklists["P1"].MainDeck);
        Assert.Equal(accepted.State.PlayerDecklists["P1"].RuneDeck, replay.State.PlayerDecklists["P1"].RuneDeck);
        Assert.Equal(accepted.State.PlayerDecklists["P1"].Battlefields, replay.State.PlayerDecklists["P1"].Battlefields);
        Assert.True(replay.Prompts["P1"].Actionable);
        Assert.Equal(["READY"], replay.Prompts["P1"].Actions);
        Assert.True(replay.Prompts["P2"].Actionable);
        Assert.Equal(["SUBMIT_DECK"], replay.Prompts["P2"].Actions);
        AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(replay, "P1", "P2", decklist);
    }

    [Fact]
    public async Task SubmitDeckStalePromptReplayAfterReadyPromptStartsRejectsWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-submit-deck-stale-prompt-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var p1Submit = await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        Assert.True(p1Submit.Accepted, p1Submit.ErrorMessage);

        var prompt = p1Submit.Prompts["P2"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["SUBMIT_DECK"], prompt.Actions);
        var rawCommand = PromptScopedSubmitDeckRawCommand(p2Deck, prompt);

        var accepted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2-stale-prompt-accepted",
            ToSubmitCommand(p2Deck),
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(MatchStatuses.Seating, accepted.State.Status);
        Assert.Equal(MatchPhases.Room, accepted.State.Phase);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DECK_SUBMITTED", StringComparison.Ordinal)));
        Assert.True(accepted.State.PlayerDecklists.ContainsKey("P1"));
        Assert.True(accepted.State.PlayerDecklists.ContainsKey("P2"));
        Assert.True(accepted.Prompts["P1"].Actionable);
        Assert.Equal(["READY"], accepted.Prompts["P1"].Actions);
        Assert.True(accepted.Prompts["P2"].Actionable);
        Assert.Equal(["READY"], accepted.Prompts["P2"].Actions);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(accepted, p1Deck, p2Deck);

        var acceptedHash = MatchStateHasher.Hash(accepted.State);
        var replay = await session.SubmitDeckAsync(
            "P2",
            "submit-p2-stale-prompt-replay",
            ToSubmitCommand(p2Deck),
            rawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(accepted.State.PlayerDecklists["P1"].MainDeck, replay.State.PlayerDecklists["P1"].MainDeck);
        Assert.Equal(accepted.State.PlayerDecklists["P2"].MainDeck, replay.State.PlayerDecklists["P2"].MainDeck);
        Assert.Equal(accepted.State.PlayerDecklists["P2"].RuneDeck, replay.State.PlayerDecklists["P2"].RuneDeck);
        Assert.Equal(accepted.State.PlayerDecklists["P2"].Battlefields, replay.State.PlayerDecklists["P2"].Battlefields);
        Assert.True(replay.Prompts["P1"].Actionable);
        Assert.Equal(["READY"], replay.Prompts["P1"].Actions);
        Assert.True(replay.Prompts["P2"].Actionable);
        Assert.Equal(["READY"], replay.Prompts["P2"].Actions);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(replay, p1Deck, p2Deck);
    }

    [Fact]
    public async Task OfficialReadyRejectsMissingSubmittedDeckWithoutPromptQueueMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-ready-missing-deck-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var submitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(submitted.Accepted, submitted.ErrorMessage);
        AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(submitted, "P2", "P1", p2Deck);

        var submittedHash = MatchStateHasher.Hash(submitted.State);
        var rejected = await session.ReadyAsync(
            "P1",
            "ready-p1-without-deck",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.InvalidDeck, rejected.ErrorCode);
        Assert.Equal("正式卡组房间需要先提交合法卡组才能准备。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(submittedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(submitted.State.Tick, rejected.State.Tick);
        Assert.Equal(submitted.State.PlayerDecklists["P2"].MainDeck, rejected.State.PlayerDecklists["P2"].MainDeck);
        Assert.Equal(submitted.State.PlayerDecklists["P2"].RuneDeck, rejected.State.PlayerDecklists["P2"].RuneDeck);
        Assert.Equal(submitted.State.PlayerDecklists["P2"].Battlefields, rejected.State.PlayerDecklists["P2"].Battlefields);
        AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(rejected, "P2", "P1", p2Deck);
    }

    [Fact]
    public async Task PromptScopedReadyMissingDeckKeepsSubmitPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-prompt-ready-missing-deck-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var submitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(submitted.Accepted, submitted.ErrorMessage);
        AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(submitted, "P2", "P1", p2Deck);

        var submitPrompt = submitted.Prompts["P1"];
        Assert.True(submitPrompt.Actionable);
        Assert.Equal(["SUBMIT_DECK"], submitPrompt.Actions);
        var readyPrompt = submitted.Prompts["P2"];
        Assert.True(readyPrompt.Actionable);
        Assert.Equal(["READY"], readyPrompt.Actions);
        var submittedHash = MatchStateHasher.Hash(submitted.State);

        var rejected = await session.ReadyAsync(
            "P1",
            "ready-p1-without-deck-using-submit-prompt",
            PromptScopedReadyRawCommand(submitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.InvalidDeck, rejected.ErrorCode);
        Assert.Equal("正式卡组房间需要先提交合法卡组才能准备。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(submittedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(submitted.State.Tick, rejected.State.Tick);
        Assert.Equal(submitted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.False(rejected.State.PlayerDecklists.ContainsKey("P1"));
        AssertOfficialDecklist(rejected.State.PlayerDecklists["P2"], p2Deck);
        AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(rejected, "P2", "P1", p2Deck);
        Assert.Equal(submitPrompt.PromptId, rejected.Prompts["P1"].PromptId);
        Assert.Equal(submitPrompt.SnapshotTick, rejected.Prompts["P1"].SnapshotTick);
        Assert.Equal(readyPrompt.PromptId, rejected.Prompts["P2"].PromptId);
        Assert.Equal(readyPrompt.SnapshotTick, rejected.Prompts["P2"].SnapshotTick);

        var accepted = await session.SubmitDeckAsync(
            "P1",
            "submit-p1-valid-after-prompt-scoped-ready-reject",
            ToSubmitCommand(p1Deck),
            PromptScopedSubmitDeckRawCommand(p1Deck, submitPrompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DECK_SUBMITTED", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(accepted, p1Deck, p2Deck);
    }

    [Fact]
    public async Task WrongPlayerPromptScopedReadyMissingDeckKeepsSubmitPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-wrong-player-prompt-ready-missing-deck-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var submitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(submitted.Accepted, submitted.ErrorMessage);
        AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(submitted, "P2", "P1", p2Deck);

        var submitPrompt = submitted.Prompts["P1"];
        Assert.True(submitPrompt.Actionable);
        Assert.Equal(["SUBMIT_DECK"], submitPrompt.Actions);
        var readyPrompt = submitted.Prompts["P2"];
        Assert.True(readyPrompt.Actionable);
        Assert.Equal(["READY"], readyPrompt.Actions);
        var submittedHash = MatchStateHasher.Hash(submitted.State);

        var rejected = await session.ReadyAsync(
            "P2",
            "ready-p2-with-p1-submit-prompt",
            PromptScopedReadyRawCommand(submitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(submittedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(submitted.State.Tick, rejected.State.Tick);
        Assert.Equal(submitted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.False(rejected.State.PlayerDecklists.ContainsKey("P1"));
        AssertOfficialDecklist(rejected.State.PlayerDecklists["P2"], p2Deck);
        AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(rejected, "P2", "P1", p2Deck);
        Assert.Equal(submitPrompt.PromptId, rejected.Prompts["P1"].PromptId);
        Assert.Equal(submitPrompt.SnapshotTick, rejected.Prompts["P1"].SnapshotTick);
        Assert.Equal(readyPrompt.PromptId, rejected.Prompts["P2"].PromptId);
        Assert.Equal(readyPrompt.SnapshotTick, rejected.Prompts["P2"].SnapshotTick);

        var accepted = await session.SubmitDeckAsync(
            "P1",
            "submit-p1-valid-after-wrong-player-ready-missing-deck-prompt",
            ToSubmitCommand(p1Deck),
            PromptScopedSubmitDeckRawCommand(p1Deck, submitPrompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DECK_SUBMITTED", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(accepted, p1Deck, p2Deck);
    }

    [Fact]
    public async Task SnapshotMismatchPromptScopedReadyMissingDeckKeepsSubmitPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-snapshot-mismatch-prompt-ready-missing-deck-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var submitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(submitted.Accepted, submitted.ErrorMessage);
        AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(submitted, "P2", "P1", p2Deck);

        var submitPrompt = submitted.Prompts["P1"];
        Assert.True(submitPrompt.Actionable);
        Assert.Equal(["SUBMIT_DECK"], submitPrompt.Actions);
        var readyPrompt = submitted.Prompts["P2"];
        Assert.True(readyPrompt.Actionable);
        Assert.Equal(["READY"], readyPrompt.Actions);
        var submittedHash = MatchStateHasher.Hash(submitted.State);

        var staleSnapshotTick = submitPrompt.SnapshotTick - 1;
        var rejected = await session.ReadyAsync(
            "P1",
            "ready-p1-without-deck-using-stale-snapshot-submit-prompt",
            PromptScopedReadyRawCommand(submitPrompt, staleSnapshotTick),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动快照已过期，请按最新状态重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(submittedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(submitted.State.Tick, rejected.State.Tick);
        Assert.Equal(submitted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.False(rejected.State.PlayerDecklists.ContainsKey("P1"));
        AssertOfficialDecklist(rejected.State.PlayerDecklists["P2"], p2Deck);
        AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(rejected, "P2", "P1", p2Deck);
        Assert.Equal(submitPrompt.PromptId, rejected.Prompts["P1"].PromptId);
        Assert.Equal(submitPrompt.SnapshotTick, rejected.Prompts["P1"].SnapshotTick);
        Assert.Equal(readyPrompt.PromptId, rejected.Prompts["P2"].PromptId);
        Assert.Equal(readyPrompt.SnapshotTick, rejected.Prompts["P2"].SnapshotTick);

        var accepted = await session.SubmitDeckAsync(
            "P1",
            "submit-p1-valid-after-stale-snapshot-ready-missing-deck-prompt",
            ToSubmitCommand(p1Deck),
            PromptScopedSubmitDeckRawCommand(p1Deck, submitPrompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DECK_SUBMITTED", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(accepted, p1Deck, p2Deck);
    }

    [Fact]
    public async Task OfficialSingleReadyBeforeOpponentDeckReplayPreservesRoomPromptQueue()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-single-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var submitted = await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(submitted.Accepted, submitted.ErrorMessage);
        AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(submitted, "P1", "P2", p1Deck);

        var accepted = await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-deck",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(accepted, "P1", "P2", p1Deck);

        var acceptedHash = MatchStateHasher.Hash(accepted.State);
        var replay = await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-deck-replay",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(replay.Accepted, replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(accepted.State.ReadyPlayerIds, replay.State.ReadyPlayerIds);
        Assert.Equal(accepted.State.PlayerDecklists["P1"].MainDeck, replay.State.PlayerDecklists["P1"].MainDeck);
        Assert.Equal(accepted.State.PlayerDecklists["P1"].RuneDeck, replay.State.PlayerDecklists["P1"].RuneDeck);
        Assert.Equal(accepted.State.PlayerDecklists["P1"].Battlefields, replay.State.PlayerDecklists["P1"].Battlefields);
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(replay, "P1", "P2", p1Deck);
    }

    [Fact]
    public async Task PromptScopedSingleReadyReplayBeforeOpponentDeckKeepsSubmitPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-prompt-single-ready-replay-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var submitted = await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(submitted.Accepted, submitted.ErrorMessage);
        AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(submitted, "P1", "P2", p1Deck);

        var readyPrompt = submitted.Prompts["P1"];
        Assert.True(readyPrompt.Actionable);
        Assert.Equal(["READY"], readyPrompt.Actions);

        var accepted = await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-deck-prompt-accepted",
            PromptScopedReadyRawCommand(readyPrompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(accepted, "P1", "P2", p1Deck);

        var waitPrompt = accepted.Prompts["P1"];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(["WAIT"], waitPrompt.Actions);
        var submitPrompt = accepted.Prompts["P2"];
        Assert.True(submitPrompt.Actionable);
        Assert.Equal(["SUBMIT_DECK"], submitPrompt.Actions);
        var acceptedHash = MatchStateHasher.Hash(accepted.State);

        var replay = await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-deck-prompt-replay",
            PromptScopedReadyRawCommand(readyPrompt),
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(accepted.State.ReadyPlayerIds, replay.State.ReadyPlayerIds);
        AssertOfficialDecklist(replay.State.PlayerDecklists["P1"], p1Deck);
        Assert.False(replay.State.PlayerDecklists.ContainsKey("P2"));
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(replay, "P1", "P2", p1Deck);
        Assert.Equal(waitPrompt.PromptId, replay.Prompts["P1"].PromptId);
        Assert.Equal(waitPrompt.SnapshotTick, replay.Prompts["P1"].SnapshotTick);
        Assert.Equal(submitPrompt.PromptId, replay.Prompts["P2"].PromptId);
        Assert.Equal(submitPrompt.SnapshotTick, replay.Prompts["P2"].SnapshotTick);

        var p2Submitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2-valid-after-stale-single-ready-prompt-replay",
            ToSubmitCommand(p2Deck),
            PromptScopedSubmitDeckRawCommand(p2Deck, submitPrompt),
            CancellationToken.None);

        Assert.True(p2Submitted.Accepted, p2Submitted.ErrorMessage);
        Assert.Equal(1, p2Submitted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DECK_SUBMITTED", StringComparison.Ordinal)));
        Assert.DoesNotContain(p2Submitted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Submitted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(p2Submitted, "P1", "P2", p1Deck, p2Deck);
    }

    [Fact]
    public async Task SnapshotMismatchPromptScopedSingleReadyKeepsReadyPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-snapshot-mismatch-prompt-single-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var submitted = await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(submitted.Accepted, submitted.ErrorMessage);
        AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(submitted, "P1", "P2", p1Deck);

        var readyPrompt = submitted.Prompts["P1"];
        Assert.True(readyPrompt.Actionable);
        Assert.Equal(["READY"], readyPrompt.Actions);
        var submitPrompt = submitted.Prompts["P2"];
        Assert.True(submitPrompt.Actionable);
        Assert.Equal(["SUBMIT_DECK"], submitPrompt.Actions);
        var submittedHash = MatchStateHasher.Hash(submitted.State);

        var staleSnapshotTick = readyPrompt.SnapshotTick - 1;
        var rejected = await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-deck-stale-snapshot-prompt",
            PromptScopedReadyRawCommand(readyPrompt, staleSnapshotTick),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动快照已过期，请按最新状态重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(submittedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(submitted.State.Tick, rejected.State.Tick);
        Assert.Equal(submitted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        AssertOfficialDecklist(rejected.State.PlayerDecklists["P1"], p1Deck);
        Assert.False(rejected.State.PlayerDecklists.ContainsKey("P2"));
        AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(rejected, "P1", "P2", p1Deck);
        Assert.Equal(readyPrompt.PromptId, rejected.Prompts["P1"].PromptId);
        Assert.Equal(readyPrompt.SnapshotTick, rejected.Prompts["P1"].SnapshotTick);
        Assert.Equal(submitPrompt.PromptId, rejected.Prompts["P2"].PromptId);
        Assert.Equal(submitPrompt.SnapshotTick, rejected.Prompts["P2"].SnapshotTick);

        var accepted = await session.ReadyAsync(
            "P1",
            "ready-p1-valid-after-stale-snapshot-single-ready-prompt",
            PromptScopedReadyRawCommand(readyPrompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(accepted, "P1", "P2", p1Deck);
    }

    [Fact]
    public async Task WrongPlayerPromptScopedSingleReadyKeepsReadyPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-wrong-player-prompt-single-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var submitted = await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(submitted.Accepted, submitted.ErrorMessage);
        AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(submitted, "P1", "P2", p1Deck);

        var readyPrompt = submitted.Prompts["P1"];
        Assert.True(readyPrompt.Actionable);
        Assert.Equal(["READY"], readyPrompt.Actions);
        var submitPrompt = submitted.Prompts["P2"];
        Assert.True(submitPrompt.Actionable);
        Assert.Equal(["SUBMIT_DECK"], submitPrompt.Actions);
        var submittedHash = MatchStateHasher.Hash(submitted.State);

        var rejected = await session.ReadyAsync(
            "P2",
            "ready-p2-with-p1-single-ready-prompt",
            PromptScopedReadyRawCommand(readyPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(submittedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(submitted.State.Tick, rejected.State.Tick);
        Assert.Equal(submitted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        AssertOfficialDecklist(rejected.State.PlayerDecklists["P1"], p1Deck);
        Assert.False(rejected.State.PlayerDecklists.ContainsKey("P2"));
        AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(rejected, "P1", "P2", p1Deck);
        Assert.Equal(readyPrompt.PromptId, rejected.Prompts["P1"].PromptId);
        Assert.Equal(readyPrompt.SnapshotTick, rejected.Prompts["P1"].SnapshotTick);
        Assert.Equal(submitPrompt.PromptId, rejected.Prompts["P2"].PromptId);
        Assert.Equal(submitPrompt.SnapshotTick, rejected.Prompts["P2"].SnapshotTick);

        var accepted = await session.ReadyAsync(
            "P1",
            "ready-p1-valid-after-wrong-player-single-ready-prompt",
            PromptScopedReadyRawCommand(readyPrompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(accepted, "P1", "P2", p1Deck);
    }

    [Fact]
    public async Task OfficialSubmitDeckAfterReadyRejectsWithoutPromptQueueMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-submit-deck-after-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var ready = await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-deck",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(ready.Accepted, ready.ErrorMessage);
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(ready, "P1", "P2", p1Deck);

        var readyHash = MatchStateHasher.Hash(ready.State);
        var rejected = await session.SubmitDeckAsync(
            "P1",
            "submit-p1-after-ready",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, rejected.ErrorCode);
        Assert.Equal("玩家准备后不能更改卡组。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(readyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(ready.State.Tick, rejected.State.Tick);
        Assert.Equal(ready.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(ready.State.PlayerDecklists["P1"].MainDeck, rejected.State.PlayerDecklists["P1"].MainDeck);
        Assert.Equal(ready.State.PlayerDecklists["P1"].RuneDeck, rejected.State.PlayerDecklists["P1"].RuneDeck);
        Assert.Equal(ready.State.PlayerDecklists["P1"].Battlefields, rejected.State.PlayerDecklists["P1"].Battlefields);
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(rejected, "P1", "P2", p1Deck);
    }

    [Fact]
    public async Task PromptScopedSubmitDeckAfterReadyKeepsOpponentSubmitPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-prompt-submit-deck-after-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var ready = await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-deck",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(ready.Accepted, ready.ErrorMessage);
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(ready, "P1", "P2", p1Deck);

        var readyPrompt = ready.Prompts["P1"];
        Assert.False(readyPrompt.Actionable);
        Assert.Equal(["WAIT"], readyPrompt.Actions);
        var submitPrompt = ready.Prompts["P2"];
        Assert.True(submitPrompt.Actionable);
        Assert.Equal(["SUBMIT_DECK"], submitPrompt.Actions);
        var readyHash = MatchStateHasher.Hash(ready.State);

        var rejected = await session.SubmitDeckAsync(
            "P1",
            "submit-p1-after-ready-with-current-wait-prompt",
            ToSubmitCommand(p1Deck),
            PromptScopedSubmitDeckRawCommand(p1Deck, readyPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, rejected.ErrorCode);
        Assert.Equal("玩家准备后不能更改卡组。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(readyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(ready.State.Tick, rejected.State.Tick);
        Assert.Equal(ready.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        AssertOfficialDecklist(rejected.State.PlayerDecklists["P1"], p1Deck);
        Assert.False(rejected.State.PlayerDecklists.ContainsKey("P2"));
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(rejected, "P1", "P2", p1Deck);
        Assert.Equal(readyPrompt.PromptId, rejected.Prompts["P1"].PromptId);
        Assert.Equal(readyPrompt.SnapshotTick, rejected.Prompts["P1"].SnapshotTick);
        Assert.Equal(submitPrompt.PromptId, rejected.Prompts["P2"].PromptId);
        Assert.Equal(submitPrompt.SnapshotTick, rejected.Prompts["P2"].SnapshotTick);

        var accepted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2-valid-after-p1-ready-prompt-reject",
            ToSubmitCommand(p2Deck),
            PromptScopedSubmitDeckRawCommand(p2Deck, submitPrompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DECK_SUBMITTED", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(accepted, "P1", "P2", p1Deck, p2Deck);
    }

    [Fact]
    public async Task SnapshotMismatchPromptScopedSubmitDeckAfterReadyKeepsOpponentSubmitPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-snapshot-mismatch-prompt-submit-deck-after-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var ready = await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-deck",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(ready.Accepted, ready.ErrorMessage);
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(ready, "P1", "P2", p1Deck);

        var readyPrompt = ready.Prompts["P1"];
        Assert.False(readyPrompt.Actionable);
        Assert.Equal(["WAIT"], readyPrompt.Actions);
        var submitPrompt = ready.Prompts["P2"];
        Assert.True(submitPrompt.Actionable);
        Assert.Equal(["SUBMIT_DECK"], submitPrompt.Actions);
        var readyHash = MatchStateHasher.Hash(ready.State);

        var staleSnapshotTick = readyPrompt.SnapshotTick - 1;
        var rejected = await session.SubmitDeckAsync(
            "P1",
            "submit-p1-after-ready-stale-snapshot-wait-prompt",
            ToSubmitCommand(p1Deck),
            PromptScopedSubmitDeckRawCommand(p1Deck, readyPrompt, staleSnapshotTick),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动快照已过期，请按最新状态重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(readyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(ready.State.Tick, rejected.State.Tick);
        Assert.Equal(ready.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        AssertOfficialDecklist(rejected.State.PlayerDecklists["P1"], p1Deck);
        Assert.False(rejected.State.PlayerDecklists.ContainsKey("P2"));
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(rejected, "P1", "P2", p1Deck);
        Assert.Equal(readyPrompt.PromptId, rejected.Prompts["P1"].PromptId);
        Assert.Equal(readyPrompt.SnapshotTick, rejected.Prompts["P1"].SnapshotTick);
        Assert.Equal(submitPrompt.PromptId, rejected.Prompts["P2"].PromptId);
        Assert.Equal(submitPrompt.SnapshotTick, rejected.Prompts["P2"].SnapshotTick);

        var accepted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2-valid-after-stale-snapshot-wait-prompt",
            ToSubmitCommand(p2Deck),
            PromptScopedSubmitDeckRawCommand(p2Deck, submitPrompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DECK_SUBMITTED", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(accepted, "P1", "P2", p1Deck, p2Deck);
    }

    [Fact]
    public async Task WrongPlayerPromptScopedSubmitDeckAfterReadyKeepsOpponentSubmitPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-wrong-player-prompt-submit-deck-after-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var ready = await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-deck",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(ready.Accepted, ready.ErrorMessage);
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(ready, "P1", "P2", p1Deck);

        var readyPrompt = ready.Prompts["P1"];
        Assert.False(readyPrompt.Actionable);
        Assert.Equal(["WAIT"], readyPrompt.Actions);
        var submitPrompt = ready.Prompts["P2"];
        Assert.True(submitPrompt.Actionable);
        Assert.Equal(["SUBMIT_DECK"], submitPrompt.Actions);
        var readyHash = MatchStateHasher.Hash(ready.State);

        var rejected = await session.SubmitDeckAsync(
            "P2",
            "submit-p2-with-p1-wait-prompt",
            ToSubmitCommand(p2Deck),
            PromptScopedSubmitDeckRawCommand(p2Deck, readyPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(readyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(ready.State.Tick, rejected.State.Tick);
        Assert.Equal(ready.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        AssertOfficialDecklist(rejected.State.PlayerDecklists["P1"], p1Deck);
        Assert.False(rejected.State.PlayerDecklists.ContainsKey("P2"));
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(rejected, "P1", "P2", p1Deck);
        Assert.Equal(readyPrompt.PromptId, rejected.Prompts["P1"].PromptId);
        Assert.Equal(readyPrompt.SnapshotTick, rejected.Prompts["P1"].SnapshotTick);
        Assert.Equal(submitPrompt.PromptId, rejected.Prompts["P2"].PromptId);
        Assert.Equal(submitPrompt.SnapshotTick, rejected.Prompts["P2"].SnapshotTick);

        var accepted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2-valid-after-wrong-player-wait-prompt",
            ToSubmitCommand(p2Deck),
            PromptScopedSubmitDeckRawCommand(p2Deck, submitPrompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DECK_SUBMITTED", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(accepted, "P1", "P2", p1Deck, p2Deck);
    }

    [Fact]
    public async Task PromptScopedReadyAfterReadyKeepsOpponentSubmitPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-prompt-ready-after-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var ready = await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-deck",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(ready.Accepted, ready.ErrorMessage);
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(ready, "P1", "P2", p1Deck);

        var waitPrompt = ready.Prompts["P1"];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(["WAIT"], waitPrompt.Actions);
        var submitPrompt = ready.Prompts["P2"];
        Assert.True(submitPrompt.Actionable);
        Assert.Equal(["SUBMIT_DECK"], submitPrompt.Actions);
        var readyHash = MatchStateHasher.Hash(ready.State);

        var replay = await session.ReadyAsync(
            "P1",
            "ready-p1-after-ready-current-wait-prompt",
            PromptScopedReadyRawCommand(waitPrompt),
            CancellationToken.None);

        Assert.True(replay.Accepted, replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(readyHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(ready.State.Tick, replay.State.Tick);
        Assert.Equal(ready.State.ReadyPlayerIds, replay.State.ReadyPlayerIds);
        AssertOfficialDecklist(replay.State.PlayerDecklists["P1"], p1Deck);
        Assert.False(replay.State.PlayerDecklists.ContainsKey("P2"));
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(replay, "P1", "P2", p1Deck);
        Assert.Equal(waitPrompt.PromptId, replay.Prompts["P1"].PromptId);
        Assert.Equal(waitPrompt.SnapshotTick, replay.Prompts["P1"].SnapshotTick);
        Assert.Equal(submitPrompt.PromptId, replay.Prompts["P2"].PromptId);
        Assert.Equal(submitPrompt.SnapshotTick, replay.Prompts["P2"].SnapshotTick);

        var accepted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2-valid-after-ready-wait-prompt-replay",
            ToSubmitCommand(p2Deck),
            PromptScopedSubmitDeckRawCommand(p2Deck, submitPrompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DECK_SUBMITTED", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(accepted, "P1", "P2", p1Deck, p2Deck);
    }

    [Fact]
    public async Task SnapshotMismatchPromptScopedReadyAfterReadyKeepsOpponentSubmitPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-snapshot-mismatch-ready-after-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var ready = await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-deck",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(ready.Accepted, ready.ErrorMessage);
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(ready, "P1", "P2", p1Deck);

        var waitPrompt = ready.Prompts["P1"];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(["WAIT"], waitPrompt.Actions);
        var submitPrompt = ready.Prompts["P2"];
        Assert.True(submitPrompt.Actionable);
        Assert.Equal(["SUBMIT_DECK"], submitPrompt.Actions);
        var readyHash = MatchStateHasher.Hash(ready.State);

        var staleSnapshotTick = waitPrompt.SnapshotTick - 1;
        var rejected = await session.ReadyAsync(
            "P1",
            "ready-p1-after-ready-stale-snapshot-wait-prompt",
            PromptScopedReadyRawCommand(waitPrompt, staleSnapshotTick),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动快照已过期，请按最新状态重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(readyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(ready.State.Tick, rejected.State.Tick);
        Assert.Equal(ready.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        AssertOfficialDecklist(rejected.State.PlayerDecklists["P1"], p1Deck);
        Assert.False(rejected.State.PlayerDecklists.ContainsKey("P2"));
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(rejected, "P1", "P2", p1Deck);
        Assert.Equal(waitPrompt.PromptId, rejected.Prompts["P1"].PromptId);
        Assert.Equal(waitPrompt.SnapshotTick, rejected.Prompts["P1"].SnapshotTick);
        Assert.Equal(submitPrompt.PromptId, rejected.Prompts["P2"].PromptId);
        Assert.Equal(submitPrompt.SnapshotTick, rejected.Prompts["P2"].SnapshotTick);

        var accepted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2-valid-after-stale-snapshot-ready-wait-prompt",
            ToSubmitCommand(p2Deck),
            PromptScopedSubmitDeckRawCommand(p2Deck, submitPrompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DECK_SUBMITTED", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(accepted, "P1", "P2", p1Deck, p2Deck);
    }

    [Fact]
    public async Task WrongPlayerPromptScopedReadyAfterReadyKeepsOpponentSubmitPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-wrong-player-ready-after-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var ready = await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-deck",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(ready.Accepted, ready.ErrorMessage);
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(ready, "P1", "P2", p1Deck);

        var waitPrompt = ready.Prompts["P1"];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(["WAIT"], waitPrompt.Actions);
        var submitPrompt = ready.Prompts["P2"];
        Assert.True(submitPrompt.Actionable);
        Assert.Equal(["SUBMIT_DECK"], submitPrompt.Actions);
        var readyHash = MatchStateHasher.Hash(ready.State);

        var rejected = await session.ReadyAsync(
            "P2",
            "ready-p2-with-p1-wait-prompt-after-p1-ready",
            PromptScopedReadyRawCommand(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(readyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(ready.State.Tick, rejected.State.Tick);
        Assert.Equal(ready.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        AssertOfficialDecklist(rejected.State.PlayerDecklists["P1"], p1Deck);
        Assert.False(rejected.State.PlayerDecklists.ContainsKey("P2"));
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(rejected, "P1", "P2", p1Deck);
        Assert.Equal(waitPrompt.PromptId, rejected.Prompts["P1"].PromptId);
        Assert.Equal(waitPrompt.SnapshotTick, rejected.Prompts["P1"].SnapshotTick);
        Assert.Equal(submitPrompt.PromptId, rejected.Prompts["P2"].PromptId);
        Assert.Equal(submitPrompt.SnapshotTick, rejected.Prompts["P2"].SnapshotTick);

        var accepted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2-valid-after-wrong-player-ready-wait-prompt",
            ToSubmitCommand(p2Deck),
            PromptScopedSubmitDeckRawCommand(p2Deck, submitPrompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DECK_SUBMITTED", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(accepted, "P1", "P2", p1Deck, p2Deck);
    }

    [Fact]
    public async Task OfficialSingleReadyAfterBothDecksSubmittedPreservesRoomPromptQueue()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-single-ready-both-decks-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var accepted = await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(accepted, "P1", "P2", p1Deck, p2Deck);

        var acceptedHash = MatchStateHasher.Hash(accepted.State);
        var replay = await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-ready-replay",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(replay.Accepted, replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(accepted.State.ReadyPlayerIds, replay.State.ReadyPlayerIds);
        Assert.Equal(accepted.State.PlayerDecklists["P1"].MainDeck, replay.State.PlayerDecklists["P1"].MainDeck);
        Assert.Equal(accepted.State.PlayerDecklists["P1"].RuneDeck, replay.State.PlayerDecklists["P1"].RuneDeck);
        Assert.Equal(accepted.State.PlayerDecklists["P1"].Battlefields, replay.State.PlayerDecklists["P1"].Battlefields);
        Assert.Equal(accepted.State.PlayerDecklists["P2"].MainDeck, replay.State.PlayerDecklists["P2"].MainDeck);
        Assert.Equal(accepted.State.PlayerDecklists["P2"].RuneDeck, replay.State.PlayerDecklists["P2"].RuneDeck);
        Assert.Equal(accepted.State.PlayerDecklists["P2"].Battlefields, replay.State.PlayerDecklists["P2"].Battlefields);
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(replay, "P1", "P2", p1Deck, p2Deck);
    }

    [Fact]
    public async Task PromptScopedReadyAfterBothDecksSingleReadyKeepsOpponentReadyPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-prompt-ready-after-both-decks-single-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var ready = await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(ready.Accepted, ready.ErrorMessage);
        Assert.Equal(1, ready.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(ready.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(ready.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(ready, "P1", "P2", p1Deck, p2Deck);

        var waitPrompt = ready.Prompts["P1"];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(["WAIT"], waitPrompt.Actions);
        var readyPrompt = ready.Prompts["P2"];
        Assert.True(readyPrompt.Actionable);
        Assert.Equal(["READY"], readyPrompt.Actions);
        var readyHash = MatchStateHasher.Hash(ready.State);

        var replay = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-single-ready-current-wait-prompt",
            PromptScopedReadyRawCommand(waitPrompt),
            CancellationToken.None);

        Assert.True(replay.Accepted, replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(readyHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(ready.State.Tick, replay.State.Tick);
        Assert.Equal(ready.State.ReadyPlayerIds, replay.State.ReadyPlayerIds);
        Assert.Equal(ready.State.PlayerDecklists["P1"].MainDeck, replay.State.PlayerDecklists["P1"].MainDeck);
        Assert.Equal(ready.State.PlayerDecklists["P1"].RuneDeck, replay.State.PlayerDecklists["P1"].RuneDeck);
        Assert.Equal(ready.State.PlayerDecklists["P1"].Battlefields, replay.State.PlayerDecklists["P1"].Battlefields);
        Assert.Equal(ready.State.PlayerDecklists["P2"].MainDeck, replay.State.PlayerDecklists["P2"].MainDeck);
        Assert.Equal(ready.State.PlayerDecklists["P2"].RuneDeck, replay.State.PlayerDecklists["P2"].RuneDeck);
        Assert.Equal(ready.State.PlayerDecklists["P2"].Battlefields, replay.State.PlayerDecklists["P2"].Battlefields);
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(replay, "P1", "P2", p1Deck, p2Deck);
        Assert.Equal(waitPrompt.PromptId, replay.Prompts["P1"].PromptId);
        Assert.Equal(waitPrompt.SnapshotTick, replay.Prompts["P1"].SnapshotTick);
        Assert.Equal(readyPrompt.PromptId, replay.Prompts["P2"].PromptId);
        Assert.Equal(readyPrompt.SnapshotTick, replay.Prompts["P2"].SnapshotTick);

        var accepted = await session.ReadyAsync(
            "P2",
            "ready-p2-valid-after-p1-wait-prompt-replay",
            PromptScopedReadyRawCommand(readyPrompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, accepted.State.Status);
        Assert.Equal(MatchPhases.Mulligan, accepted.State.Phase);
        Assert.Equal(TimingStates.Mulligan, accepted.State.TimingState);
        Assert.Equal(["P1", "P2"], accepted.State.ReadyPlayerIds);
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = accepted.State.ActivePlayerId;
        var secondPlayerId = accepted.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(accepted, activePlayerId, secondPlayerId);

        foreach (var acceptedPrompt in accepted.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, acceptedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, acceptedPrompt.Actions);
            Assert.DoesNotContain(
                acceptedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task SnapshotMismatchPromptScopedReadyAfterBothDecksSingleReadyKeepsOpponentReadyPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-snapshot-ready-after-both-decks-single-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var ready = await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(ready.Accepted, ready.ErrorMessage);
        Assert.Equal(1, ready.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(ready.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(ready.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(ready, "P1", "P2", p1Deck, p2Deck);

        var waitPrompt = ready.Prompts["P1"];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(["WAIT"], waitPrompt.Actions);
        var readyPrompt = ready.Prompts["P2"];
        Assert.True(readyPrompt.Actionable);
        Assert.Equal(["READY"], readyPrompt.Actions);
        var readyHash = MatchStateHasher.Hash(ready.State);

        var staleSnapshotTick = waitPrompt.SnapshotTick - 1;
        var rejected = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-single-ready-stale-wait-prompt",
            PromptScopedReadyRawCommand(waitPrompt, staleSnapshotTick),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动快照已过期，请按最新状态重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(readyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(ready.State.Tick, rejected.State.Tick);
        Assert.Equal(ready.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(ready.State.PlayerDecklists["P1"].MainDeck, rejected.State.PlayerDecklists["P1"].MainDeck);
        Assert.Equal(ready.State.PlayerDecklists["P1"].RuneDeck, rejected.State.PlayerDecklists["P1"].RuneDeck);
        Assert.Equal(ready.State.PlayerDecklists["P1"].Battlefields, rejected.State.PlayerDecklists["P1"].Battlefields);
        Assert.Equal(ready.State.PlayerDecklists["P2"].MainDeck, rejected.State.PlayerDecklists["P2"].MainDeck);
        Assert.Equal(ready.State.PlayerDecklists["P2"].RuneDeck, rejected.State.PlayerDecklists["P2"].RuneDeck);
        Assert.Equal(ready.State.PlayerDecklists["P2"].Battlefields, rejected.State.PlayerDecklists["P2"].Battlefields);
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(rejected, "P1", "P2", p1Deck, p2Deck);
        Assert.Equal(waitPrompt.PromptId, rejected.Prompts["P1"].PromptId);
        Assert.Equal(waitPrompt.SnapshotTick, rejected.Prompts["P1"].SnapshotTick);
        Assert.Equal(readyPrompt.PromptId, rejected.Prompts["P2"].PromptId);
        Assert.Equal(readyPrompt.SnapshotTick, rejected.Prompts["P2"].SnapshotTick);

        var accepted = await session.ReadyAsync(
            "P2",
            "ready-p2-valid-after-stale-wait-prompt",
            PromptScopedReadyRawCommand(readyPrompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, accepted.State.Status);
        Assert.Equal(MatchPhases.Mulligan, accepted.State.Phase);
        Assert.Equal(TimingStates.Mulligan, accepted.State.TimingState);
        Assert.Equal(["P1", "P2"], accepted.State.ReadyPlayerIds);
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = accepted.State.ActivePlayerId;
        var secondPlayerId = accepted.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(accepted, activePlayerId, secondPlayerId);

        foreach (var acceptedPrompt in accepted.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, acceptedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, acceptedPrompt.Actions);
            Assert.DoesNotContain(
                acceptedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task WrongPlayerPromptScopedReadyAfterBothDecksSingleReadyKeepsOpponentReadyPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-wrong-player-ready-after-both-decks-single-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var ready = await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(ready.Accepted, ready.ErrorMessage);
        Assert.Equal(1, ready.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(ready.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(ready.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(ready, "P1", "P2", p1Deck, p2Deck);

        var waitPrompt = ready.Prompts["P1"];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(["WAIT"], waitPrompt.Actions);
        var readyPrompt = ready.Prompts["P2"];
        Assert.True(readyPrompt.Actionable);
        Assert.Equal(["READY"], readyPrompt.Actions);
        var readyHash = MatchStateHasher.Hash(ready.State);

        var rejected = await session.ReadyAsync(
            "P2",
            "ready-p2-with-p1-wait-prompt-after-both-decks-single-ready",
            PromptScopedReadyRawCommand(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(readyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(ready.State.Tick, rejected.State.Tick);
        Assert.Equal(ready.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(ready.State.PlayerDecklists["P1"].MainDeck, rejected.State.PlayerDecklists["P1"].MainDeck);
        Assert.Equal(ready.State.PlayerDecklists["P1"].RuneDeck, rejected.State.PlayerDecklists["P1"].RuneDeck);
        Assert.Equal(ready.State.PlayerDecklists["P1"].Battlefields, rejected.State.PlayerDecklists["P1"].Battlefields);
        Assert.Equal(ready.State.PlayerDecklists["P2"].MainDeck, rejected.State.PlayerDecklists["P2"].MainDeck);
        Assert.Equal(ready.State.PlayerDecklists["P2"].RuneDeck, rejected.State.PlayerDecklists["P2"].RuneDeck);
        Assert.Equal(ready.State.PlayerDecklists["P2"].Battlefields, rejected.State.PlayerDecklists["P2"].Battlefields);
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(rejected, "P1", "P2", p1Deck, p2Deck);
        Assert.Equal(waitPrompt.PromptId, rejected.Prompts["P1"].PromptId);
        Assert.Equal(waitPrompt.SnapshotTick, rejected.Prompts["P1"].SnapshotTick);
        Assert.Equal(readyPrompt.PromptId, rejected.Prompts["P2"].PromptId);
        Assert.Equal(readyPrompt.SnapshotTick, rejected.Prompts["P2"].SnapshotTick);

        var accepted = await session.ReadyAsync(
            "P2",
            "ready-p2-valid-after-wrong-player-wait-prompt",
            PromptScopedReadyRawCommand(readyPrompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, accepted.State.Status);
        Assert.Equal(MatchPhases.Mulligan, accepted.State.Phase);
        Assert.Equal(TimingStates.Mulligan, accepted.State.TimingState);
        Assert.Equal(["P1", "P2"], accepted.State.ReadyPlayerIds);
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = accepted.State.ActivePlayerId;
        var secondPlayerId = accepted.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(accepted, activePlayerId, secondPlayerId);

        foreach (var acceptedPrompt in accepted.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, acceptedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, acceptedPrompt.Actions);
            Assert.DoesNotContain(
                acceptedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task PromptScopedSubmitDeckAfterBothDecksSingleReadyKeepsOpponentReadyPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-prompt-submit-after-both-decks-single-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var ready = await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(ready.Accepted, ready.ErrorMessage);
        Assert.Equal(1, ready.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(ready.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(ready.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(ready, "P1", "P2", p1Deck, p2Deck);

        var waitPrompt = ready.Prompts["P1"];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(["WAIT"], waitPrompt.Actions);
        var readyPrompt = ready.Prompts["P2"];
        Assert.True(readyPrompt.Actionable);
        Assert.Equal(["READY"], readyPrompt.Actions);
        var readyHash = MatchStateHasher.Hash(ready.State);

        var rejected = await session.SubmitDeckAsync(
            "P1",
            "submit-p1-both-decks-single-ready-current-wait-prompt",
            ToSubmitCommand(p1Deck),
            PromptScopedSubmitDeckRawCommand(p1Deck, waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, rejected.ErrorCode);
        Assert.Equal("玩家准备后不能更改卡组。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(readyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(ready.State.Tick, rejected.State.Tick);
        Assert.Equal(ready.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(ready.State.PlayerDecklists["P1"].MainDeck, rejected.State.PlayerDecklists["P1"].MainDeck);
        Assert.Equal(ready.State.PlayerDecklists["P1"].RuneDeck, rejected.State.PlayerDecklists["P1"].RuneDeck);
        Assert.Equal(ready.State.PlayerDecklists["P1"].Battlefields, rejected.State.PlayerDecklists["P1"].Battlefields);
        Assert.Equal(ready.State.PlayerDecklists["P2"].MainDeck, rejected.State.PlayerDecklists["P2"].MainDeck);
        Assert.Equal(ready.State.PlayerDecklists["P2"].RuneDeck, rejected.State.PlayerDecklists["P2"].RuneDeck);
        Assert.Equal(ready.State.PlayerDecklists["P2"].Battlefields, rejected.State.PlayerDecklists["P2"].Battlefields);
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(rejected, "P1", "P2", p1Deck, p2Deck);
        Assert.Equal(waitPrompt.PromptId, rejected.Prompts["P1"].PromptId);
        Assert.Equal(waitPrompt.SnapshotTick, rejected.Prompts["P1"].SnapshotTick);
        Assert.Equal(readyPrompt.PromptId, rejected.Prompts["P2"].PromptId);
        Assert.Equal(readyPrompt.SnapshotTick, rejected.Prompts["P2"].SnapshotTick);

        var accepted = await session.ReadyAsync(
            "P2",
            "ready-p2-valid-after-p1-submit-deck-wait-prompt-reject",
            PromptScopedReadyRawCommand(readyPrompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, accepted.State.Status);
        Assert.Equal(MatchPhases.Mulligan, accepted.State.Phase);
        Assert.Equal(TimingStates.Mulligan, accepted.State.TimingState);
        Assert.Equal(["P1", "P2"], accepted.State.ReadyPlayerIds);
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = accepted.State.ActivePlayerId;
        var secondPlayerId = accepted.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(accepted, activePlayerId, secondPlayerId);

        foreach (var acceptedPrompt in accepted.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, acceptedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, acceptedPrompt.Actions);
            Assert.DoesNotContain(
                acceptedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task SnapshotMismatchPromptScopedSubmitDeckAfterBothDecksSingleReadyKeepsOpponentReadyPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-snapshot-submit-after-both-decks-single-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var ready = await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(ready.Accepted, ready.ErrorMessage);
        Assert.Equal(1, ready.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(ready.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(ready.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(ready, "P1", "P2", p1Deck, p2Deck);

        var waitPrompt = ready.Prompts["P1"];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(["WAIT"], waitPrompt.Actions);
        var readyPrompt = ready.Prompts["P2"];
        Assert.True(readyPrompt.Actionable);
        Assert.Equal(["READY"], readyPrompt.Actions);
        var readyHash = MatchStateHasher.Hash(ready.State);

        var staleSnapshotTick = waitPrompt.SnapshotTick - 1;
        var rejected = await session.SubmitDeckAsync(
            "P1",
            "submit-p1-both-decks-single-ready-stale-wait-prompt",
            ToSubmitCommand(p1Deck),
            PromptScopedSubmitDeckRawCommand(p1Deck, waitPrompt, staleSnapshotTick),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动快照已过期，请按最新状态重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(readyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(ready.State.Tick, rejected.State.Tick);
        Assert.Equal(ready.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(ready.State.PlayerDecklists["P1"].MainDeck, rejected.State.PlayerDecklists["P1"].MainDeck);
        Assert.Equal(ready.State.PlayerDecklists["P1"].RuneDeck, rejected.State.PlayerDecklists["P1"].RuneDeck);
        Assert.Equal(ready.State.PlayerDecklists["P1"].Battlefields, rejected.State.PlayerDecklists["P1"].Battlefields);
        Assert.Equal(ready.State.PlayerDecklists["P2"].MainDeck, rejected.State.PlayerDecklists["P2"].MainDeck);
        Assert.Equal(ready.State.PlayerDecklists["P2"].RuneDeck, rejected.State.PlayerDecklists["P2"].RuneDeck);
        Assert.Equal(ready.State.PlayerDecklists["P2"].Battlefields, rejected.State.PlayerDecklists["P2"].Battlefields);
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(rejected, "P1", "P2", p1Deck, p2Deck);
        Assert.Equal(waitPrompt.PromptId, rejected.Prompts["P1"].PromptId);
        Assert.Equal(waitPrompt.SnapshotTick, rejected.Prompts["P1"].SnapshotTick);
        Assert.Equal(readyPrompt.PromptId, rejected.Prompts["P2"].PromptId);
        Assert.Equal(readyPrompt.SnapshotTick, rejected.Prompts["P2"].SnapshotTick);

        var accepted = await session.ReadyAsync(
            "P2",
            "ready-p2-valid-after-stale-submit-deck-wait-prompt",
            PromptScopedReadyRawCommand(readyPrompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, accepted.State.Status);
        Assert.Equal(MatchPhases.Mulligan, accepted.State.Phase);
        Assert.Equal(TimingStates.Mulligan, accepted.State.TimingState);
        Assert.Equal(["P1", "P2"], accepted.State.ReadyPlayerIds);
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = accepted.State.ActivePlayerId;
        var secondPlayerId = accepted.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(accepted, activePlayerId, secondPlayerId);

        foreach (var acceptedPrompt in accepted.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, acceptedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, acceptedPrompt.Actions);
            Assert.DoesNotContain(
                acceptedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task WrongPlayerPromptScopedSubmitDeckAfterBothDecksSingleReadyKeepsOpponentReadyPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-wrong-player-submit-after-both-decks-single-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var ready = await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(ready.Accepted, ready.ErrorMessage);
        Assert.Equal(1, ready.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(ready.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(ready.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(ready, "P1", "P2", p1Deck, p2Deck);

        var waitPrompt = ready.Prompts["P1"];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(["WAIT"], waitPrompt.Actions);
        var readyPrompt = ready.Prompts["P2"];
        Assert.True(readyPrompt.Actionable);
        Assert.Equal(["READY"], readyPrompt.Actions);
        var readyHash = MatchStateHasher.Hash(ready.State);

        var rejected = await session.SubmitDeckAsync(
            "P2",
            "submit-p2-with-p1-wait-prompt-after-both-decks-single-ready",
            ToSubmitCommand(p2Deck),
            PromptScopedSubmitDeckRawCommand(p2Deck, waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(readyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(ready.State.Tick, rejected.State.Tick);
        Assert.Equal(ready.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(ready.State.PlayerDecklists["P1"].MainDeck, rejected.State.PlayerDecklists["P1"].MainDeck);
        Assert.Equal(ready.State.PlayerDecklists["P1"].RuneDeck, rejected.State.PlayerDecklists["P1"].RuneDeck);
        Assert.Equal(ready.State.PlayerDecklists["P1"].Battlefields, rejected.State.PlayerDecklists["P1"].Battlefields);
        Assert.Equal(ready.State.PlayerDecklists["P2"].MainDeck, rejected.State.PlayerDecklists["P2"].MainDeck);
        Assert.Equal(ready.State.PlayerDecklists["P2"].RuneDeck, rejected.State.PlayerDecklists["P2"].RuneDeck);
        Assert.Equal(ready.State.PlayerDecklists["P2"].Battlefields, rejected.State.PlayerDecklists["P2"].Battlefields);
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(rejected, "P1", "P2", p1Deck, p2Deck);
        Assert.Equal(waitPrompt.PromptId, rejected.Prompts["P1"].PromptId);
        Assert.Equal(waitPrompt.SnapshotTick, rejected.Prompts["P1"].SnapshotTick);
        Assert.Equal(readyPrompt.PromptId, rejected.Prompts["P2"].PromptId);
        Assert.Equal(readyPrompt.SnapshotTick, rejected.Prompts["P2"].SnapshotTick);

        var accepted = await session.ReadyAsync(
            "P2",
            "ready-p2-valid-after-wrong-player-submit-deck-wait-prompt",
            PromptScopedReadyRawCommand(readyPrompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, accepted.State.Status);
        Assert.Equal(MatchPhases.Mulligan, accepted.State.Phase);
        Assert.Equal(TimingStates.Mulligan, accepted.State.TimingState);
        Assert.Equal(["P1", "P2"], accepted.State.ReadyPlayerIds);
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = accepted.State.ActivePlayerId;
        var secondPlayerId = accepted.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(accepted, activePlayerId, secondPlayerId);

        foreach (var acceptedPrompt in accepted.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, acceptedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, acceptedPrompt.Actions);
            Assert.DoesNotContain(
                acceptedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task OfficialFirstReadyBothDecksPromptReplayAfterFinalReadyRejectsWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-first-ready-both-decks-stale-prompt-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);
        var rawCommand = PromptScopedReadyRawCommand(prompt);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            rawCommand,
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, prompt.SnapshotTick);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var replay = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-replay-after-final-ready",
            rawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(finalReady.State.Tick, replay.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, replay.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, replay.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, replay.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, replay.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, replay.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, replay.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, replay.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, replay.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(replay, activePlayerId, secondPlayerId);

        foreach (var replayPrompt in replay.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, replayPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, replayPrompt.Actions);
            Assert.DoesNotContain(
                replayPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public Task PromptIdOnlyFirstReadyBothDecksPromptReplayAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertFirstReadyBothDecksEnvelopeAfterFinalReadyRejectsWithoutMutation(
            "official-first-ready-prompt-id-only-after-final-ready-room",
            "ready-p1-both-decks-prompt-id-only-replay-after-final-ready",
            PromptIdOnlyReadyRawCommand,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyFirstReadyBothDecksPromptAfterFinalReadyAcceptsWithoutMutation()
    {
        return AssertFirstReadyBothDecksEnvelopeAfterFinalReadyRejectsWithoutMutation(
            "official-first-ready-snapshot-only-after-final-ready-room",
            "ready-p1-both-decks-snapshot-only-after-final-ready",
            prompt =>
            {
                Assert.NotNull(prompt.SnapshotTick);
                return SnapshotOnlyReadyRawCommand(prompt.SnapshotTick.Value);
            },
            expectedAccepted: true);
    }

    private static async Task AssertFirstReadyBothDecksEnvelopeAfterFinalReadyRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> replayCommandFactory,
        string? expectedMessage = null,
        bool expectedAccepted = false)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession(sessionName, new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var result = await session.ReadyAsync(
            "P1",
            intentId,
            replayCommandFactory(prompt),
            CancellationToken.None);

        if (expectedAccepted)
        {
            Assert.True(result.Accepted, result.ErrorMessage);
        }
        else
        {
            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.PromptExpired, result.ErrorCode);
            Assert.Equal(expectedMessage, result.ErrorMessage);
        }

        Assert.Empty(result.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(result.State));
        Assert.Equal(finalReady.State.Tick, result.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, result.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, result.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, result.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, result.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, result.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, result.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, result.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, result.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(result, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, result.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, result.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, result.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, result.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in result.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task WrongPlayerFirstReadyBothDecksPromptAfterFinalReadyRejectsWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-wrong-player-first-ready-after-final-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);
        var rawCommand = PromptScopedReadyRawCommand(prompt);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            rawCommand,
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.ReadyAsync(
            "P2",
            "ready-p2-with-p1-first-ready-prompt-after-final-ready",
            rawCommand,
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task SubmitDeckWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-submit-with-first-ready-prompt-after-final-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitDeckAsync(
            "P1",
            "submit-p1-with-first-ready-prompt-after-final-ready",
            ToSubmitCommand(p1Deck),
            PromptScopedSubmitDeckRawCommand(p1Deck, prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task WrongPlayerSubmitDeckWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-wrong-player-submit-with-first-ready-prompt-after-final-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitDeckAsync(
            "P2",
            "submit-p2-with-p1-first-ready-prompt-after-final-ready",
            ToSubmitCommand(p2Deck),
            PromptScopedSubmitDeckRawCommand(p2Deck, prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public Task PromptIdOnlySubmitDeckWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertSubmitDeckFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
            "official-submit-prompt-id-only-with-first-ready-prompt-after-final-ready-room",
            "submit-p1-prompt-id-only-with-first-ready-prompt-after-final-ready",
            PromptIdOnlySubmitDeckRawCommand,
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlySubmitDeckWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertSubmitDeckFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
            "official-submit-snapshot-only-with-first-ready-prompt-after-final-ready-room",
            "submit-p1-snapshot-only-with-first-ready-prompt-after-final-ready",
            (decklist, prompt) =>
            {
                Assert.NotNull(prompt.SnapshotTick);
                return SnapshotOnlySubmitDeckRawCommand(decklist, prompt.SnapshotTick.Value);
            },
            ErrorCodes.PhaseNotAllowed,
            "对局开始后不能更改卡组。");
    }

    private static async Task AssertSubmitDeckFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<OfficialDecklist, ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession(sessionName, new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts["P1"].SnapshotTick, prompt.SnapshotTick);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitDeckAsync(
            "P1",
            intentId,
            ToSubmitCommand(p1Deck),
            rawCommandFactory(p1Deck, prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Equal(finalReady.State.PlayerDecklists["P1"].MainDeck, rejected.State.PlayerDecklists["P1"].MainDeck);
        Assert.Equal(finalReady.State.PlayerDecklists["P1"].RuneDeck, rejected.State.PlayerDecklists["P1"].RuneDeck);
        Assert.Equal(finalReady.State.PlayerDecklists["P1"].Battlefields, rejected.State.PlayerDecklists["P1"].Battlefields);
        Assert.Equal(finalReady.State.PlayerDecklists["P2"].MainDeck, rejected.State.PlayerDecklists["P2"].MainDeck);
        Assert.Equal(finalReady.State.PlayerDecklists["P2"].RuneDeck, rejected.State.PlayerDecklists["P2"].RuneDeck);
        Assert.Equal(finalReady.State.PlayerDecklists["P2"].Battlefields, rejected.State.PlayerDecklists["P2"].Battlefields);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task MulliganWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-mulligan-with-first-ready-prompt-after-final-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var selectedObjectIds = finalReady.State.PlayerZones[activePlayerId].Hand.Take(1).ToArray();
        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitAsync(
            activePlayerId,
            "mulligan-active-with-first-ready-prompt-after-final-ready",
            new MulliganCommand(selectedObjectIds),
            PromptScopedMulliganRawCommand(selectedObjectIds, prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task WrongPlayerMulliganWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-wrong-player-mulligan-with-first-ready-prompt-after-final-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var selectedObjectIds = finalReady.State.PlayerZones[secondPlayerId].Hand.Take(1).ToArray();
        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitAsync(
            secondPlayerId,
            "mulligan-second-with-first-ready-prompt-after-final-ready",
            new MulliganCommand(selectedObjectIds),
            PromptScopedMulliganRawCommand(selectedObjectIds, prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public Task PromptIdOnlyDuplicateMulliganWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertDuplicateMulliganFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
            "official-duplicate-mulligan-prompt-id-only-with-first-ready-prompt-after-final-ready-room",
            "duplicate-mulligan-prompt-id-only-with-first-ready-prompt-after-final-ready",
            PromptIdOnlyMulliganRawCommand,
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyDuplicateMulliganWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertDuplicateMulliganFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
            "official-duplicate-mulligan-snapshot-only-with-first-ready-prompt-after-final-ready-room",
            "duplicate-mulligan-snapshot-only-with-first-ready-prompt-after-final-ready",
            (selectedObjectIds, prompt) =>
            {
                Assert.NotNull(prompt.SnapshotTick);
                return SnapshotOnlyMulliganRawCommand(selectedObjectIds, prompt.SnapshotTick.Value);
            },
            ErrorCodes.InvalidTarget,
            "起手调整不能重复选择同一张牌。");
    }

    private static async Task AssertDuplicateMulliganFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<IReadOnlyList<string>, ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession(sessionName, new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, prompt.SnapshotTick);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var duplicatedObjectId = finalReady.State.PlayerZones[activePlayerId].Hand[0];
        var selectedObjectIds = new[] { duplicatedObjectId, duplicatedObjectId };
        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitAsync(
            activePlayerId,
            intentId,
            new MulliganCommand(selectedObjectIds),
            rawCommandFactory(selectedObjectIds, prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task PassWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-pass-with-first-ready-prompt-after-final-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitAsync(
            activePlayerId,
            "pass-active-with-first-ready-prompt-after-final-ready",
            new PassCommand(),
            PromptScopedBasicRawCommand(CommandTypes.Pass, prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public Task PromptIdOnlyPassWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertPassFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
            "official-pass-prompt-id-only-with-first-ready-prompt-after-final-ready-room",
            "pass-prompt-id-only-with-first-ready-prompt-after-final-ready",
            prompt => PromptIdOnlyBasicRawCommand(CommandTypes.Pass, prompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyPassWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertPassFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
            "official-pass-snapshot-only-with-first-ready-prompt-after-final-ready-room",
            "pass-snapshot-only-with-first-ready-prompt-after-final-ready",
            prompt =>
            {
                Assert.NotNull(prompt.SnapshotTick);
                return SnapshotOnlyBasicRawCommand(CommandTypes.Pass, prompt.SnapshotTick.Value);
            },
            ErrorCodes.PhaseNotAllowed,
            "让过只能由当前玩家在可让过窗口中提交。");
    }

    private static async Task AssertPassFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession(sessionName, new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, prompt.SnapshotTick);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitAsync(
            activePlayerId,
            intentId,
            new PassCommand(),
            rawCommandFactory(prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task EndTurnWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-end-turn-with-first-ready-prompt-after-final-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitAsync(
            activePlayerId,
            "end-turn-active-with-first-ready-prompt-after-final-ready",
            new EndTurnCommand(),
            PromptScopedBasicRawCommand(CommandTypes.EndTurn, prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task SurrenderWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-surrender-with-first-ready-prompt-after-final-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitAsync(
            activePlayerId,
            "surrender-active-with-first-ready-prompt-after-final-ready",
            new SurrenderCommand(),
            PromptScopedBasicRawCommand(CommandTypes.Surrender, prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task PassPriorityWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-pass-priority-with-first-ready-prompt-after-final-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitAsync(
            activePlayerId,
            "pass-priority-active-with-first-ready-prompt-after-final-ready",
            new PassPriorityCommand(),
            PromptScopedBasicRawCommand(CommandTypes.PassPriority, prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public Task PromptIdOnlyPassPriorityWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertPassPriorityFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
            "official-pass-priority-prompt-id-only-with-first-ready-prompt-after-final-ready-room",
            "pass-priority-prompt-id-only-with-first-ready-prompt-after-final-ready",
            prompt => PromptIdOnlyBasicRawCommand(CommandTypes.PassPriority, prompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyPassPriorityWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertPassPriorityFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
            "official-pass-priority-snapshot-only-with-first-ready-prompt-after-final-ready-room",
            "pass-priority-snapshot-only-with-first-ready-prompt-after-final-ready",
            prompt =>
            {
                Assert.NotNull(prompt.SnapshotTick);
                return SnapshotOnlyBasicRawCommand(CommandTypes.PassPriority, prompt.SnapshotTick.Value);
            },
            ErrorCodes.PhaseNotAllowed,
            "让过优先权只能在优先行动窗口中提交。");
    }

    private static async Task AssertPassPriorityFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession(sessionName, new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, prompt.SnapshotTick);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitAsync(
            activePlayerId,
            intentId,
            new PassPriorityCommand(),
            rawCommandFactory(prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task PassFocusWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-pass-focus-with-first-ready-prompt-after-final-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitAsync(
            activePlayerId,
            "pass-focus-active-with-first-ready-prompt-after-final-ready",
            new PassFocusCommand(),
            PromptScopedBasicRawCommand(CommandTypes.PassFocus, prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public Task PromptIdOnlyPassFocusWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertPassFocusFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
            "official-pass-focus-prompt-id-only-with-first-ready-prompt-after-final-ready-room",
            "pass-focus-prompt-id-only-with-first-ready-prompt-after-final-ready",
            prompt => PromptIdOnlyBasicRawCommand(CommandTypes.PassFocus, prompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyPassFocusWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertPassFocusFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
            "official-pass-focus-snapshot-only-with-first-ready-prompt-after-final-ready-room",
            "pass-focus-snapshot-only-with-first-ready-prompt-after-final-ready",
            prompt =>
            {
                Assert.NotNull(prompt.SnapshotTick);
                return SnapshotOnlyBasicRawCommand(CommandTypes.PassFocus, prompt.SnapshotTick.Value);
            },
            ErrorCodes.PhaseNotAllowed,
            "让过焦点只能在法术对决焦点窗口中提交。");
    }

    private static async Task AssertPassFocusFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession(sessionName, new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, prompt.SnapshotTick);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitAsync(
            activePlayerId,
            intentId,
            new PassFocusCommand(),
            rawCommandFactory(prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task MoveUnitWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-move-unit-with-first-ready-prompt-after-final-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitAsync(
            activePlayerId,
            "move-unit-active-with-first-ready-prompt-after-final-ready",
            new MoveUnitCommand("missing-unit", "BASE", "BATTLEFIELD:missing", []),
            PromptScopedBasicRawCommand(CommandTypes.MoveUnit, prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public Task PromptIdOnlyMoveUnitWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertMoveUnitFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
            "official-move-unit-prompt-id-only-with-first-ready-prompt-after-final-ready-room",
            "move-unit-prompt-id-only-with-first-ready-prompt-after-final-ready",
            prompt => PromptIdOnlyBasicRawCommand(CommandTypes.MoveUnit, prompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyMoveUnitWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertMoveUnitFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
            "official-move-unit-snapshot-only-with-first-ready-prompt-after-final-ready-room",
            "move-unit-snapshot-only-with-first-ready-prompt-after-final-ready",
            prompt =>
            {
                Assert.NotNull(prompt.SnapshotTick);
                return SnapshotOnlyBasicRawCommand(CommandTypes.MoveUnit, prompt.SnapshotTick.Value);
            },
            ErrorCodes.PhaseNotAllowed,
            "移动单位只能在当前玩家的开放主阶段提交。");
    }

    private static async Task AssertMoveUnitFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession(sessionName, new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, prompt.SnapshotTick);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitAsync(
            activePlayerId,
            intentId,
            new MoveUnitCommand("missing-unit", "BASE", "BATTLEFIELD:missing", []),
            rawCommandFactory(prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task DeclareBattleWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-declare-battle-with-first-ready-prompt-after-final-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitAsync(
            activePlayerId,
            "declare-battle-active-with-first-ready-prompt-after-final-ready",
            new DeclareBattleCommand("missing-battlefield", [], [], []),
            PromptScopedBasicRawCommand(CommandTypes.DeclareBattle, prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public Task PromptIdOnlyDeclareBattleWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertDeclareBattleFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
            "official-declare-battle-prompt-id-only-with-first-ready-prompt-after-final-ready-room",
            "declare-battle-prompt-id-only-with-first-ready-prompt-after-final-ready",
            prompt => PromptIdOnlyBasicRawCommand(CommandTypes.DeclareBattle, prompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyDeclareBattleWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertDeclareBattleFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
            "official-declare-battle-snapshot-only-with-first-ready-prompt-after-final-ready-room",
            "declare-battle-snapshot-only-with-first-ready-prompt-after-final-ready",
            prompt =>
            {
                Assert.NotNull(prompt.SnapshotTick);
                return SnapshotOnlyBasicRawCommand(CommandTypes.DeclareBattle, prompt.SnapshotTick.Value);
            },
            ErrorCodes.UnsupportedCommand,
            "当前声明战斗路径尚未由服务端开放。");
    }

    private static async Task AssertDeclareBattleFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession(sessionName, new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, prompt.SnapshotTick);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitAsync(
            activePlayerId,
            intentId,
            new DeclareBattleCommand("missing-battlefield", [], [], []),
            rawCommandFactory(prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task PlayCardWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-play-card-with-first-ready-prompt-after-final-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitAsync(
            activePlayerId,
            "play-card-active-with-first-ready-prompt-after-final-ready",
            new PlayCardCommand("missing-card", "MISSING-CARD", []),
            PromptScopedBasicRawCommand(CommandTypes.PlayCard, prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public Task PromptIdOnlyPlayCardWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertPlayCardFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
            "official-play-card-prompt-id-only-with-first-ready-prompt-after-final-ready-room",
            "play-card-prompt-id-only-with-first-ready-prompt-after-final-ready",
            prompt => PromptIdOnlyBasicRawCommand(CommandTypes.PlayCard, prompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyPlayCardWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertPlayCardFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
            "official-play-card-snapshot-only-with-first-ready-prompt-after-final-ready-room",
            "play-card-snapshot-only-with-first-ready-prompt-after-final-ready",
            prompt =>
            {
                Assert.NotNull(prompt.SnapshotTick);
                return SnapshotOnlyBasicRawCommand(CommandTypes.PlayCard, prompt.SnapshotTick.Value);
            },
            ErrorCodes.UnsupportedCardBehavior,
            "Unsupported card behavior or mode: MISSING-CARD ");
    }

    private static async Task AssertPlayCardFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession(sessionName, new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, prompt.SnapshotTick);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitAsync(
            activePlayerId,
            intentId,
            new PlayCardCommand("missing-card", "MISSING-CARD", []),
            rawCommandFactory(prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task ActivateAbilityWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-activate-ability-with-first-ready-prompt-after-final-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitAsync(
            activePlayerId,
            "activate-ability-active-with-first-ready-prompt-after-final-ready",
            new ActivateAbilityCommand("missing-source", "missing-ability", []),
            PromptScopedBasicRawCommand(CommandTypes.ActivateAbility, prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public Task PromptIdOnlyActivateAbilityWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertActivateAbilityFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
            "official-activate-ability-prompt-id-only-with-first-ready-prompt-after-final-ready-room",
            "activate-ability-prompt-id-only-with-first-ready-prompt-after-final-ready",
            prompt => PromptIdOnlyBasicRawCommand(CommandTypes.ActivateAbility, prompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyActivateAbilityWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertActivateAbilityFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
            "official-activate-ability-snapshot-only-with-first-ready-prompt-after-final-ready-room",
            "activate-ability-snapshot-only-with-first-ready-prompt-after-final-ready",
            prompt =>
            {
                Assert.NotNull(prompt.SnapshotTick);
                return SnapshotOnlyBasicRawCommand(CommandTypes.ActivateAbility, prompt.SnapshotTick.Value);
            },
            ErrorCodes.UnsupportedCommand,
            "当前启动技能路径尚未由服务端开放。");
    }

    private static async Task AssertActivateAbilityFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession(sessionName, new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, prompt.SnapshotTick);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitAsync(
            activePlayerId,
            intentId,
            new ActivateAbilityCommand("missing-source", "missing-ability", []),
            rawCommandFactory(prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task TapRuneWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-tap-rune-with-first-ready-prompt-after-final-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitAsync(
            activePlayerId,
            "tap-rune-active-with-first-ready-prompt-after-final-ready",
            new TapRuneCommand("missing-rune"),
            PromptScopedBasicRawCommand(CommandTypes.TapRune, prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public Task PromptIdOnlyTapRuneWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertTapRuneFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
            "official-tap-rune-prompt-id-only-with-first-ready-prompt-after-final-ready-room",
            "tap-rune-prompt-id-only-with-first-ready-prompt-after-final-ready",
            prompt => PromptIdOnlyBasicRawCommand(CommandTypes.TapRune, prompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyTapRuneWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertTapRuneFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
            "official-tap-rune-snapshot-only-with-first-ready-prompt-after-final-ready-room",
            "tap-rune-snapshot-only-with-first-ready-prompt-after-final-ready",
            prompt =>
            {
                Assert.NotNull(prompt.SnapshotTick);
                return SnapshotOnlyBasicRawCommand(CommandTypes.TapRune, prompt.SnapshotTick.Value);
            },
            ErrorCodes.PhaseNotAllowed,
            "横置符文只能在当前玩家的开放主阶段提交。");
    }

    private static async Task AssertTapRuneFirstReadyEnvelopeAfterFinalReadyRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession(sessionName, new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, prompt.SnapshotTick);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitAsync(
            activePlayerId,
            intentId,
            new TapRuneCommand("missing-rune"),
            rawCommandFactory(prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task RecycleRuneWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-recycle-rune-with-first-ready-prompt-after-final-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitAsync(
            activePlayerId,
            "recycle-rune-active-with-first-ready-prompt-after-final-ready",
            new RecycleRuneCommand("missing-rune"),
            PromptScopedBasicRawCommand(CommandTypes.RecycleRune, prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task HideCardWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-hide-card-with-first-ready-prompt-after-final-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitAsync(
            activePlayerId,
            "hide-card-active-with-first-ready-prompt-after-final-ready",
            new HideCardCommand("missing-source", "missing-card"),
            PromptScopedBasicRawCommand(CommandTypes.HideCard, prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task RevealCardWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-reveal-card-with-first-ready-prompt-after-final-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitAsync(
            activePlayerId,
            "reveal-card-active-with-first-ready-prompt-after-final-ready",
            new RevealCardCommand("missing-source", "missing-card", []),
            PromptScopedBasicRawCommand(CommandTypes.RevealCard, prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public Task LegendActWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertCommandWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation(
            "official-legend-act-with-first-ready-prompt-after-final-ready-room",
            "legend-act-active-with-first-ready-prompt-after-final-ready",
            new LegendActCommand("missing-source", "missing-ability", []),
            CommandTypes.LegendAct);
    }

    [Fact]
    public Task AssembleEquipmentWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertCommandWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation(
            "official-assemble-equipment-with-first-ready-prompt-after-final-ready-room",
            "assemble-equipment-active-with-first-ready-prompt-after-final-ready",
            new AssembleEquipmentCommand("missing-source", "missing-target"),
            CommandTypes.AssembleEquipment);
    }

    [Fact]
    public Task PayCostWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertCommandWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation(
            "official-pay-cost-with-first-ready-prompt-after-final-ready-room",
            "pay-cost-active-with-first-ready-prompt-after-final-ready",
            new PayCostCommand("missing-payment", "missing-window", []),
            CommandTypes.PayCost);
    }

    [Fact]
    public Task AssignCombatDamageWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertCommandWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation(
            "official-assign-combat-damage-with-first-ready-prompt-after-final-ready-room",
            "assign-combat-damage-active-with-first-ready-prompt-after-final-ready",
            new AssignCombatDamageCommand("missing-battle", "missing-battlefield", []),
            CommandTypes.AssignCombatDamage);
    }

    [Fact]
    public Task OrderTriggersWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertCommandWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation(
            "official-order-triggers-with-first-ready-prompt-after-final-ready-room",
            "order-triggers-active-with-first-ready-prompt-after-final-ready",
            new OrderTriggersCommand(OrderedTriggerIds: []),
            CommandTypes.OrderTriggers);
    }

    [Fact]
    public Task ChooseHandCardsWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation()
    {
        return AssertCommandWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation(
            "official-choose-hand-cards-with-first-ready-prompt-after-final-ready-room",
            "choose-hand-cards-active-with-first-ready-prompt-after-final-ready",
            new ChooseHandCardsCommand("missing-choice", "missing-window", []),
            CommandTypes.ChooseHandCards);
    }

    private static async Task AssertCommandWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation(
        string sessionName,
        string intentId,
        GameCommand command,
        string commandType)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession(sessionName, new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);

        var firstReady = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-prompt-accepted",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(firstReady.Accepted, firstReady.ErrorMessage);
        Assert.Equal(1, firstReady.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(firstReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(firstReady, "P1", "P2", p1Deck, p2Deck);

        var finalReady = await session.ReadyAsync(
            "P2",
            "ready-p2-after-first-ready",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(finalReady.Accepted, finalReady.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, finalReady.State.Status);
        Assert.Equal(MatchPhases.Mulligan, finalReady.State.Phase);
        Assert.Equal(TimingStates.Mulligan, finalReady.State.TimingState);
        Assert.Equal(["P1", "P2"], finalReady.State.ReadyPlayerIds);
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(finalReady.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = finalReady.State.ActivePlayerId;
        var secondPlayerId = finalReady.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(finalReady, activePlayerId, secondPlayerId);

        foreach (var finalReadyPrompt in finalReady.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, finalReadyPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, finalReadyPrompt.Actions);
            Assert.DoesNotContain(
                finalReadyPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var finalReadyHash = MatchStateHasher.Hash(finalReady.State);
        var rejected = await session.SubmitAsync(
            activePlayerId,
            intentId,
            command,
            PromptScopedBasicRawCommand(commandType, prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(finalReadyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(finalReady.State.Tick, rejected.State.Tick);
        Assert.Equal(finalReady.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(finalReady.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(finalReady.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(finalReady.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(finalReady.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(finalReady.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(rejected, activePlayerId, secondPlayerId);
        Assert.Equal(finalReady.Prompts[activePlayerId].PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[activePlayerId].SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);
        Assert.Equal(finalReady.Prompts[secondPlayerId].PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(finalReady.Prompts[secondPlayerId].SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        foreach (var rejectedPrompt in rejected.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, rejectedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, rejectedPrompt.Actions);
            Assert.DoesNotContain(
                rejectedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task SnapshotMismatchPromptScopedReadyKeepsRoomPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-snapshot-mismatch-prompt-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);
        var bothSubmittedHash = MatchStateHasher.Hash(bothSubmitted.State);

        var staleSnapshotTick = prompt.SnapshotTick - 1;
        var rejected = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-stale-snapshot-prompt",
            PromptScopedReadyRawCommand(prompt, staleSnapshotTick),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动快照已过期，请按最新状态重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(bothSubmittedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(bothSubmitted.State.Tick, rejected.State.Tick);
        Assert.Equal(bothSubmitted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(bothSubmitted.State.PlayerDecklists["P1"].MainDeck, rejected.State.PlayerDecklists["P1"].MainDeck);
        Assert.Equal(bothSubmitted.State.PlayerDecklists["P1"].RuneDeck, rejected.State.PlayerDecklists["P1"].RuneDeck);
        Assert.Equal(bothSubmitted.State.PlayerDecklists["P1"].Battlefields, rejected.State.PlayerDecklists["P1"].Battlefields);
        Assert.Equal(bothSubmitted.State.PlayerDecklists["P2"].MainDeck, rejected.State.PlayerDecklists["P2"].MainDeck);
        Assert.Equal(bothSubmitted.State.PlayerDecklists["P2"].RuneDeck, rejected.State.PlayerDecklists["P2"].RuneDeck);
        Assert.Equal(bothSubmitted.State.PlayerDecklists["P2"].Battlefields, rejected.State.PlayerDecklists["P2"].Battlefields);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(rejected, p1Deck, p2Deck);
        Assert.Equal(prompt.PromptId, rejected.Prompts["P1"].PromptId);
        Assert.Equal(prompt.SnapshotTick, rejected.Prompts["P1"].SnapshotTick);

        var accepted = await session.ReadyAsync(
            "P1",
            "ready-p1-both-decks-valid-after-stale-snapshot-prompt",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(accepted, "P1", "P2", p1Deck, p2Deck);
    }

    [Fact]
    public async Task WrongPlayerPromptScopedReadyKeepsRoomPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-wrong-player-prompt-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var bothSubmitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(bothSubmitted.Accepted, bothSubmitted.ErrorMessage);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(bothSubmitted, p1Deck, p2Deck);

        var prompt = bothSubmitted.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);
        var bothSubmittedHash = MatchStateHasher.Hash(bothSubmitted.State);

        var rejected = await session.ReadyAsync(
            "P2",
            "ready-p2-with-p1-prompt",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(bothSubmittedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(bothSubmitted.State.Tick, rejected.State.Tick);
        Assert.Equal(bothSubmitted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(bothSubmitted.State.PlayerDecklists["P1"].MainDeck, rejected.State.PlayerDecklists["P1"].MainDeck);
        Assert.Equal(bothSubmitted.State.PlayerDecklists["P1"].RuneDeck, rejected.State.PlayerDecklists["P1"].RuneDeck);
        Assert.Equal(bothSubmitted.State.PlayerDecklists["P1"].Battlefields, rejected.State.PlayerDecklists["P1"].Battlefields);
        Assert.Equal(bothSubmitted.State.PlayerDecklists["P2"].MainDeck, rejected.State.PlayerDecklists["P2"].MainDeck);
        Assert.Equal(bothSubmitted.State.PlayerDecklists["P2"].RuneDeck, rejected.State.PlayerDecklists["P2"].RuneDeck);
        Assert.Equal(bothSubmitted.State.PlayerDecklists["P2"].Battlefields, rejected.State.PlayerDecklists["P2"].Battlefields);
        AssertOfficialSubmitDeckBothReadyPromptQueueAudit(rejected, p1Deck, p2Deck);
        Assert.Equal(prompt.PromptId, rejected.Prompts["P1"].PromptId);
        Assert.Equal(prompt.SnapshotTick, rejected.Prompts["P1"].SnapshotTick);

        var accepted = await session.ReadyAsync(
            "P1",
            "ready-p1-valid-after-wrong-player-prompt",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(accepted, "P1", "P2", p1Deck, p2Deck);
    }

    [Fact]
    public async Task OfficialSubmitDeckAfterOpponentReadyPreservesRoomPromptQueue()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-submit-deck-after-opponent-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var ready = await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-deck",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(ready.Accepted, ready.ErrorMessage);
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(ready, "P1", "P2", p1Deck);

        var accepted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2-after-p1-ready",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DECK_SUBMITTED", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(accepted, "P1", "P2", p1Deck, p2Deck);

        var acceptedHash = MatchStateHasher.Hash(accepted.State);
        var replay = await session.SubmitDeckAsync(
            "P2",
            "submit-p2-after-p1-ready-replay",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, replay.ErrorCode);
        Assert.Equal("玩家已经提交相同卡组。", replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(accepted.State.ReadyPlayerIds, replay.State.ReadyPlayerIds);
        Assert.Equal(accepted.State.PlayerDecklists["P1"].MainDeck, replay.State.PlayerDecklists["P1"].MainDeck);
        Assert.Equal(accepted.State.PlayerDecklists["P1"].RuneDeck, replay.State.PlayerDecklists["P1"].RuneDeck);
        Assert.Equal(accepted.State.PlayerDecklists["P1"].Battlefields, replay.State.PlayerDecklists["P1"].Battlefields);
        Assert.Equal(accepted.State.PlayerDecklists["P2"].MainDeck, replay.State.PlayerDecklists["P2"].MainDeck);
        Assert.Equal(accepted.State.PlayerDecklists["P2"].RuneDeck, replay.State.PlayerDecklists["P2"].RuneDeck);
        Assert.Equal(accepted.State.PlayerDecklists["P2"].Battlefields, replay.State.PlayerDecklists["P2"].Battlefields);
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(replay, "P1", "P2", p1Deck, p2Deck);
    }

    [Fact]
    public async Task OfficialSubmitDeckAfterOpponentReadyStalePromptReplayRejectsWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-submit-deck-after-opponent-ready-stale-prompt-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var ready = await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-deck",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(ready.Accepted, ready.ErrorMessage);
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(ready, "P1", "P2", p1Deck);

        var prompt = ready.Prompts["P2"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["SUBMIT_DECK"], prompt.Actions);
        var rawCommand = PromptScopedSubmitDeckRawCommand(p2Deck, prompt);

        var accepted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2-after-p1-ready-prompt-accepted",
            ToSubmitCommand(p2Deck),
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DECK_SUBMITTED", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(accepted, "P1", "P2", p1Deck, p2Deck);

        var acceptedHash = MatchStateHasher.Hash(accepted.State);
        var replay = await session.SubmitDeckAsync(
            "P2",
            "submit-p2-after-p1-ready-prompt-replay",
            ToSubmitCommand(p2Deck),
            rawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(accepted.State.ReadyPlayerIds, replay.State.ReadyPlayerIds);
        Assert.Equal(accepted.State.PlayerDecklists["P1"].MainDeck, replay.State.PlayerDecklists["P1"].MainDeck);
        Assert.Equal(accepted.State.PlayerDecklists["P1"].RuneDeck, replay.State.PlayerDecklists["P1"].RuneDeck);
        Assert.Equal(accepted.State.PlayerDecklists["P1"].Battlefields, replay.State.PlayerDecklists["P1"].Battlefields);
        Assert.Equal(accepted.State.PlayerDecklists["P2"].MainDeck, replay.State.PlayerDecklists["P2"].MainDeck);
        Assert.Equal(accepted.State.PlayerDecklists["P2"].RuneDeck, replay.State.PlayerDecklists["P2"].RuneDeck);
        Assert.Equal(accepted.State.PlayerDecklists["P2"].Battlefields, replay.State.PlayerDecklists["P2"].Battlefields);
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(replay, "P1", "P2", p1Deck, p2Deck);
    }

    [Fact]
    public async Task SnapshotMismatchPromptScopedSubmitDeckAfterOpponentReadyKeepsRoomPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-snapshot-mismatch-prompt-submit-after-opponent-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var ready = await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-deck",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(ready.Accepted, ready.ErrorMessage);
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(ready, "P1", "P2", p1Deck);

        var prompt = ready.Prompts["P2"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["SUBMIT_DECK"], prompt.Actions);
        var readyHash = MatchStateHasher.Hash(ready.State);

        var staleSnapshotTick = prompt.SnapshotTick - 1;
        var rejected = await session.SubmitDeckAsync(
            "P2",
            "submit-p2-after-p1-ready-stale-snapshot-prompt",
            ToSubmitCommand(p2Deck),
            PromptScopedSubmitDeckRawCommand(p2Deck, prompt, staleSnapshotTick),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动快照已过期，请按最新状态重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(readyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(ready.State.Tick, rejected.State.Tick);
        Assert.Equal(ready.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        AssertOfficialDecklist(rejected.State.PlayerDecklists["P1"], p1Deck);
        Assert.False(rejected.State.PlayerDecklists.ContainsKey("P2"));
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(rejected, "P1", "P2", p1Deck);
        Assert.Equal(prompt.PromptId, rejected.Prompts["P2"].PromptId);
        Assert.Equal(prompt.SnapshotTick, rejected.Prompts["P2"].SnapshotTick);

        var accepted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2-valid-after-stale-snapshot-prompt",
            ToSubmitCommand(p2Deck),
            PromptScopedSubmitDeckRawCommand(p2Deck, prompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DECK_SUBMITTED", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(accepted, "P1", "P2", p1Deck, p2Deck);
    }

    [Fact]
    public async Task WrongPlayerPromptScopedSubmitDeckAfterOpponentReadyKeepsRoomPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-wrong-player-prompt-submit-after-opponent-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var ready = await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-deck",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(ready.Accepted, ready.ErrorMessage);
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(ready, "P1", "P2", p1Deck);

        var prompt = ready.Prompts["P2"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["SUBMIT_DECK"], prompt.Actions);
        var readyHash = MatchStateHasher.Hash(ready.State);

        var rejected = await session.SubmitDeckAsync(
            "P1",
            "submit-p1-with-p2-prompt-after-ready",
            ToSubmitCommand(p2Deck),
            PromptScopedSubmitDeckRawCommand(p2Deck, prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(readyHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(ready.State.Tick, rejected.State.Tick);
        Assert.Equal(ready.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        AssertOfficialDecklist(rejected.State.PlayerDecklists["P1"], p1Deck);
        Assert.False(rejected.State.PlayerDecklists.ContainsKey("P2"));
        AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(rejected, "P1", "P2", p1Deck);
        Assert.Equal(prompt.PromptId, rejected.Prompts["P2"].PromptId);
        Assert.Equal(prompt.SnapshotTick, rejected.Prompts["P2"].SnapshotTick);

        var accepted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2-valid-after-wrong-player-prompt",
            ToSubmitCommand(p2Deck),
            PromptScopedSubmitDeckRawCommand(p2Deck, prompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DECK_SUBMITTED", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(accepted, "P1", "P2", p1Deck, p2Deck);
    }

    [Fact]
    public async Task OfficialFinalReadyAfterLateDeckSubmissionStartsMulliganWithoutRoomPromptResidue()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-final-ready-after-late-deck-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-deck",
            RawCommand("READY"),
            CancellationToken.None);
        var p2Submitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2-after-p1-ready",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(p2Submitted.Accepted, p2Submitted.ErrorMessage);
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(p2Submitted, "P1", "P2", p1Deck, p2Deck);

        var accepted = await session.ReadyAsync(
            "P2",
            "ready-p2-after-late-deck",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, accepted.State.Status);
        Assert.Equal(MatchPhases.Mulligan, accepted.State.Phase);
        Assert.Equal(TimingStates.Mulligan, accepted.State.TimingState);
        Assert.Equal(["P1", "P2"], accepted.State.ReadyPlayerIds);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal)));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = accepted.State.ActivePlayerId;
        var secondPlayerId = accepted.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(accepted, activePlayerId, secondPlayerId);

        foreach (var prompt in accepted.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, prompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, prompt.Actions);
            Assert.DoesNotContain(
                prompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var acceptedHash = MatchStateHasher.Hash(accepted.State);
        var replay = await session.ReadyAsync(
            "P2",
            "ready-p2-after-late-deck-replay",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(replay.Accepted, replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(accepted.State.RngCursor, replay.State.RngCursor);
        Assert.Equal(accepted.State.ReadyPlayerIds, replay.State.ReadyPlayerIds);
        Assert.Equal(accepted.State.PlayerZones[activePlayerId].Hand, replay.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(accepted.State.PlayerZones[secondPlayerId].Hand, replay.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(accepted.State.PlayerZones[activePlayerId].MainDeck, replay.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(accepted.State.PlayerZones[secondPlayerId].MainDeck, replay.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(accepted.State.MulliganCompletedPlayerIds, replay.State.MulliganCompletedPlayerIds);
        Assert.Equal(accepted.State.OpeningSecondActionPlayerId, replay.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(replay, activePlayerId, secondPlayerId);
    }

    [Fact]
    public async Task OfficialFinalReadyLateDeckPromptReplayRejectsWithoutRoomPromptResidue()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-final-ready-late-deck-stale-prompt-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-deck",
            RawCommand("READY"),
            CancellationToken.None);
        var p2Submitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2-after-p1-ready",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(p2Submitted.Accepted, p2Submitted.ErrorMessage);
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(p2Submitted, "P1", "P2", p1Deck, p2Deck);

        var prompt = p2Submitted.Prompts["P2"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);
        var rawCommand = PromptScopedReadyRawCommand(prompt);

        var accepted = await session.ReadyAsync(
            "P2",
            "ready-p2-late-prompt-accepted",
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, accepted.State.Status);
        Assert.Equal(MatchPhases.Mulligan, accepted.State.Phase);
        Assert.Equal(TimingStates.Mulligan, accepted.State.TimingState);
        Assert.Equal(["P1", "P2"], accepted.State.ReadyPlayerIds);
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = accepted.State.ActivePlayerId;
        var secondPlayerId = accepted.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(accepted, activePlayerId, secondPlayerId);

        foreach (var acceptedPrompt in accepted.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, acceptedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, acceptedPrompt.Actions);
            Assert.DoesNotContain(
                acceptedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }

        var acceptedHash = MatchStateHasher.Hash(accepted.State);
        var replay = await session.ReadyAsync(
            "P2",
            "ready-p2-late-prompt-replay",
            rawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(accepted.State.RngCursor, replay.State.RngCursor);
        Assert.Equal(accepted.State.ReadyPlayerIds, replay.State.ReadyPlayerIds);
        Assert.Equal(accepted.State.PlayerZones[activePlayerId].Hand, replay.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(accepted.State.PlayerZones[secondPlayerId].Hand, replay.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(accepted.State.PlayerZones[activePlayerId].MainDeck, replay.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(accepted.State.PlayerZones[secondPlayerId].MainDeck, replay.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(accepted.State.MulliganCompletedPlayerIds, replay.State.MulliganCompletedPlayerIds);
        Assert.Equal(accepted.State.OpeningSecondActionPlayerId, replay.State.OpeningSecondActionPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(replay, activePlayerId, secondPlayerId);

        foreach (var replayPrompt in replay.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, replayPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, replayPrompt.Actions);
            Assert.DoesNotContain(
                replayPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task SnapshotMismatchPromptScopedFinalReadyKeepsRoomPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-snapshot-mismatch-prompt-final-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-deck",
            RawCommand("READY"),
            CancellationToken.None);
        var p2Submitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2-after-p1-ready",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(p2Submitted.Accepted, p2Submitted.ErrorMessage);
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(p2Submitted, "P1", "P2", p1Deck, p2Deck);

        var prompt = p2Submitted.Prompts["P2"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);
        var p2SubmittedHash = MatchStateHasher.Hash(p2Submitted.State);

        var staleSnapshotTick = prompt.SnapshotTick - 1;
        var rejected = await session.ReadyAsync(
            "P2",
            "ready-p2-late-stale-snapshot-prompt",
            PromptScopedReadyRawCommand(prompt, staleSnapshotTick),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动快照已过期，请按最新状态重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(p2SubmittedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(p2Submitted.State.Tick, rejected.State.Tick);
        Assert.Equal(p2Submitted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(p2Submitted.State.PlayerDecklists["P1"].MainDeck, rejected.State.PlayerDecklists["P1"].MainDeck);
        Assert.Equal(p2Submitted.State.PlayerDecklists["P1"].RuneDeck, rejected.State.PlayerDecklists["P1"].RuneDeck);
        Assert.Equal(p2Submitted.State.PlayerDecklists["P1"].Battlefields, rejected.State.PlayerDecklists["P1"].Battlefields);
        Assert.Equal(p2Submitted.State.PlayerDecklists["P2"].MainDeck, rejected.State.PlayerDecklists["P2"].MainDeck);
        Assert.Equal(p2Submitted.State.PlayerDecklists["P2"].RuneDeck, rejected.State.PlayerDecklists["P2"].RuneDeck);
        Assert.Equal(p2Submitted.State.PlayerDecklists["P2"].Battlefields, rejected.State.PlayerDecklists["P2"].Battlefields);
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(rejected, "P1", "P2", p1Deck, p2Deck);
        Assert.Equal(prompt.PromptId, rejected.Prompts["P2"].PromptId);
        Assert.Equal(prompt.SnapshotTick, rejected.Prompts["P2"].SnapshotTick);

        var accepted = await session.ReadyAsync(
            "P2",
            "ready-p2-late-valid-after-stale-snapshot-prompt",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, accepted.State.Status);
        Assert.Equal(MatchPhases.Mulligan, accepted.State.Phase);
        Assert.Equal(TimingStates.Mulligan, accepted.State.TimingState);
        Assert.Equal(["P1", "P2"], accepted.State.ReadyPlayerIds);
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = accepted.State.ActivePlayerId;
        var secondPlayerId = accepted.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(accepted, activePlayerId, secondPlayerId);

        foreach (var acceptedPrompt in accepted.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, acceptedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, acceptedPrompt.Actions);
            Assert.DoesNotContain(
                acceptedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task WrongPlayerPromptScopedFinalReadyKeepsRoomPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-wrong-player-prompt-final-ready-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.ReadyAsync(
            "P1",
            "ready-p1-before-p2-deck",
            RawCommand("READY"),
            CancellationToken.None);
        var p2Submitted = await session.SubmitDeckAsync(
            "P2",
            "submit-p2-after-p1-ready",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(p2Submitted.Accepted, p2Submitted.ErrorMessage);
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(p2Submitted, "P1", "P2", p1Deck, p2Deck);

        var prompt = p2Submitted.Prompts["P2"];
        Assert.True(prompt.Actionable);
        Assert.Equal(["READY"], prompt.Actions);
        var p2SubmittedHash = MatchStateHasher.Hash(p2Submitted.State);

        var rejected = await session.ReadyAsync(
            "P1",
            "ready-p1-with-p2-final-ready-prompt",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(p2SubmittedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(p2Submitted.State.Tick, rejected.State.Tick);
        Assert.Equal(p2Submitted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(p2Submitted.State.PlayerDecklists["P1"].MainDeck, rejected.State.PlayerDecklists["P1"].MainDeck);
        Assert.Equal(p2Submitted.State.PlayerDecklists["P1"].RuneDeck, rejected.State.PlayerDecklists["P1"].RuneDeck);
        Assert.Equal(p2Submitted.State.PlayerDecklists["P1"].Battlefields, rejected.State.PlayerDecklists["P1"].Battlefields);
        Assert.Equal(p2Submitted.State.PlayerDecklists["P2"].MainDeck, rejected.State.PlayerDecklists["P2"].MainDeck);
        Assert.Equal(p2Submitted.State.PlayerDecklists["P2"].RuneDeck, rejected.State.PlayerDecklists["P2"].RuneDeck);
        Assert.Equal(p2Submitted.State.PlayerDecklists["P2"].Battlefields, rejected.State.PlayerDecklists["P2"].Battlefields);
        AssertOfficialSingleReadyBothDecksPromptQueueAudit(rejected, "P1", "P2", p1Deck, p2Deck);
        Assert.Equal(prompt.PromptId, rejected.Prompts["P2"].PromptId);
        Assert.Equal(prompt.SnapshotTick, rejected.Prompts["P2"].SnapshotTick);

        var accepted = await session.ReadyAsync(
            "P2",
            "ready-p2-final-valid-after-wrong-player-prompt",
            PromptScopedReadyRawCommand(prompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, accepted.State.Status);
        Assert.Equal(MatchPhases.Mulligan, accepted.State.Phase);
        Assert.Equal(TimingStates.Mulligan, accepted.State.TimingState);
        Assert.Equal(["P1", "P2"], accepted.State.ReadyPlayerIds);
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = accepted.State.ActivePlayerId;
        var secondPlayerId = accepted.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        AssertOfficialReadyMulliganPromptQueueAudit(accepted, activePlayerId, secondPlayerId);

        foreach (var acceptedPrompt in accepted.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.Ready, acceptedPrompt.Actions);
            Assert.DoesNotContain(CommandTypes.SubmitDeck, acceptedPrompt.Actions);
            Assert.DoesNotContain(
                acceptedPrompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.Ready, StringComparison.Ordinal)
                    || string.Equals(candidate.Action, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task OfficialSubmittedDecksStartMulliganThenEnterFirstTurn()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-opening-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var p1Submit = await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var p2Submit = await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);

        Assert.True(p1Submit.Accepted);
        Assert.True(p2Submit.Accepted);

        await session.ReadyAsync("P1", "ready-p1", RawCommand("READY"), CancellationToken.None);
        var ready = await session.ReadyAsync("P2", "ready-p2", RawCommand("READY"), CancellationToken.None);

        Assert.True(ready.Accepted);
        Assert.Equal(MatchStatuses.InProgress, ready.State.Status);
        Assert.Equal(MatchPhases.Mulligan, ready.State.Phase);
        Assert.Equal(TimingStates.Mulligan, ready.State.TimingState);
        Assert.Contains(ready.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));

        foreach (var playerId in new[] { "P1", "P2" })
        {
            var zones = ready.State.PlayerZones[playerId];
            Assert.Equal(35, zones.MainDeck.Count);
            Assert.Equal(12, zones.RuneDeck.Count);
            Assert.Equal(4, zones.Hand.Count);
            Assert.Single(zones.Battlefields);
            Assert.Single(zones.LegendZone);
            Assert.Single(zones.ChampionZone);
        }

        var activePlayerId = ready.State.ActivePlayerId;
        var secondPlayerId = ready.State.OpeningSecondActionPlayerId!;
        Assert.NotEqual(activePlayerId, secondPlayerId);
        Assert.True(ready.Prompts[activePlayerId].Actionable);
        Assert.Contains("MULLIGAN", ready.Prompts[activePlayerId].Actions);
        Assert.False(ready.Prompts[secondPlayerId].Actionable);

        var activeHandBefore = ready.State.PlayerZones[activePlayerId].Hand.ToArray();
        var activeSelectedObjectIds = activeHandBefore.Take(2).ToArray();
        var activeDrawnObjectIds = ready.State.PlayerZones[activePlayerId].MainDeck
            .Take(activeSelectedObjectIds.Length)
            .ToArray();
        var activeMulligan = await session.SubmitAsync(
            activePlayerId,
            "mulligan-active",
            new MulliganCommand(activeSelectedObjectIds),
            RawCommand("MULLIGAN"),
            CancellationToken.None);
        Assert.True(activeMulligan.Accepted);
        Assert.Equal(MatchPhases.Mulligan, activeMulligan.State.Phase);
        Assert.Contains(activePlayerId, activeMulligan.State.MulliganCompletedPlayerIds);
        Assert.True(activeMulligan.Prompts[secondPlayerId].Actionable);

        var secondHandBefore = activeMulligan.State.PlayerZones[secondPlayerId].Hand.ToArray();
        var secondSelectedObjectIds = secondHandBefore.Take(1).ToArray();
        var secondDrawnObjectIds = activeMulligan.State.PlayerZones[secondPlayerId].MainDeck
            .Take(secondSelectedObjectIds.Length)
            .ToArray();
        var calledRuneObjectIds = activeMulligan.State.PlayerZones[activePlayerId].RuneDeck.Take(2).ToArray();
        var turnDrawnObjectIds = activeMulligan.State.PlayerZones[activePlayerId].MainDeck.Take(1).ToArray();
        var secondMulligan = await session.SubmitAsync(
            secondPlayerId,
            "mulligan-second",
            new MulliganCommand(secondSelectedObjectIds),
            RawCommand("MULLIGAN"),
            CancellationToken.None);

        Assert.True(secondMulligan.Accepted);
        Assert.Equal(MatchPhases.Main, secondMulligan.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, secondMulligan.State.TimingState);
        Assert.Equal(activePlayerId, secondMulligan.State.ActivePlayerId);
        Assert.Equal(2, secondMulligan.State.PlayerZones[activePlayerId].Base.Count);
        Assert.Contains(secondMulligan.Events, gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_PHASE_COMPLETED", StringComparison.Ordinal));
        Assert.Contains(secondMulligan.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            secondMulligan,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds,
            activeDrawnObjectIds,
            secondSelectedObjectIds,
            secondDrawnObjectIds,
            calledRuneObjectIds,
            turnDrawnObjectIds);
    }

    [Fact]
    public async Task OfficialReadyAcceptsAcceptedReplayWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-ready-replay-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.ReadyAsync("P1", "ready-p1", RawCommand("READY"), CancellationToken.None);
        var accepted = await session.ReadyAsync("P2", "ready-p2-accepted", RawCommand("READY"), CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, accepted.State.Status);
        Assert.Equal(MatchPhases.Mulligan, accepted.State.Phase);
        Assert.Equal(TimingStates.Mulligan, accepted.State.TimingState);
        Assert.Equal(["P1", "P2"], accepted.State.ReadyPlayerIds);
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = accepted.State.ActivePlayerId;
        var secondPlayerId = accepted.State.OpeningSecondActionPlayerId!;
        Assert.True(accepted.Prompts[activePlayerId].Actionable);
        Assert.Contains("MULLIGAN", accepted.Prompts[activePlayerId].Actions);
        Assert.False(accepted.Prompts[secondPlayerId].Actionable);
        AssertOfficialReadyMulliganPromptQueueAudit(accepted, activePlayerId, secondPlayerId);

        var acceptedHash = MatchStateHasher.Hash(accepted.State);
        var replay = await session.ReadyAsync(
            "P2",
            "ready-p2-replay",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.True(replay.Accepted, replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(accepted.State.RngCursor, replay.State.RngCursor);
        Assert.Equal(accepted.State.ReadyPlayerIds, replay.State.ReadyPlayerIds);
        Assert.Equal(accepted.State.PlayerZones[activePlayerId].Hand, replay.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(accepted.State.PlayerZones[secondPlayerId].Hand, replay.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(accepted.State.PlayerZones[activePlayerId].MainDeck, replay.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(accepted.State.PlayerZones[secondPlayerId].MainDeck, replay.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(accepted.State.MulliganCompletedPlayerIds, replay.State.MulliganCompletedPlayerIds);
        Assert.Equal(accepted.State.OpeningSecondActionPlayerId, replay.State.OpeningSecondActionPlayerId);
        Assert.True(replay.Prompts[activePlayerId].Actionable);
        Assert.Contains("MULLIGAN", replay.Prompts[activePlayerId].Actions);
        Assert.False(replay.Prompts[secondPlayerId].Actionable);
        Assert.DoesNotContain("MULLIGAN", replay.Prompts[secondPlayerId].Actions);
        AssertOfficialReadyMulliganPromptQueueAudit(replay, activePlayerId, secondPlayerId);
    }

    [Fact]
    public async Task OfficialReadyStalePromptReplayAfterMulliganStartsRejectsWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-ready-stale-prompt-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        var p1Ready = await session.ReadyAsync("P1", "ready-p1", RawCommand("READY"), CancellationToken.None);
        Assert.True(p1Ready.Accepted, p1Ready.ErrorMessage);

        var prompt = p1Ready.Prompts["P2"];
        Assert.True(prompt.Actionable);
        Assert.Contains("READY", prompt.Actions);
        var rawCommand = PromptScopedReadyRawCommand(prompt);

        var accepted = await session.ReadyAsync(
            "P2",
            "ready-p2-stale-prompt-accepted",
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, accepted.State.Status);
        Assert.Equal(MatchPhases.Mulligan, accepted.State.Phase);
        Assert.Equal(TimingStates.Mulligan, accepted.State.TimingState);
        Assert.Equal(["P1", "P2"], accepted.State.ReadyPlayerIds);
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "PLAYER_READY", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "OFFICIAL_OPENING_STARTED", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));

        var activePlayerId = accepted.State.ActivePlayerId;
        var secondPlayerId = accepted.State.OpeningSecondActionPlayerId!;
        Assert.True(accepted.Prompts[activePlayerId].Actionable);
        Assert.Contains("MULLIGAN", accepted.Prompts[activePlayerId].Actions);
        Assert.False(accepted.Prompts[secondPlayerId].Actionable);
        Assert.DoesNotContain("READY", accepted.Prompts["P2"].Actions);
        AssertOfficialReadyMulliganPromptQueueAudit(accepted, activePlayerId, secondPlayerId);

        var acceptedHash = MatchStateHasher.Hash(accepted.State);
        var replay = await session.ReadyAsync(
            "P2",
            "ready-p2-stale-prompt-replay",
            rawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(accepted.State.RngCursor, replay.State.RngCursor);
        Assert.Equal(accepted.State.ReadyPlayerIds, replay.State.ReadyPlayerIds);
        Assert.Equal(accepted.State.PlayerZones[activePlayerId].Hand, replay.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(accepted.State.PlayerZones[secondPlayerId].Hand, replay.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(accepted.State.PlayerZones[activePlayerId].MainDeck, replay.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(accepted.State.PlayerZones[secondPlayerId].MainDeck, replay.State.PlayerZones[secondPlayerId].MainDeck);
        Assert.Equal(accepted.State.MulliganCompletedPlayerIds, replay.State.MulliganCompletedPlayerIds);
        Assert.Equal(accepted.State.OpeningSecondActionPlayerId, replay.State.OpeningSecondActionPlayerId);
        Assert.True(replay.Prompts[activePlayerId].Actionable);
        Assert.Contains("MULLIGAN", replay.Prompts[activePlayerId].Actions);
        Assert.False(replay.Prompts[secondPlayerId].Actionable);
        Assert.DoesNotContain("READY", replay.Prompts["P2"].Actions);
        AssertOfficialReadyMulliganPromptQueueAudit(replay, activePlayerId, secondPlayerId);
    }

    [Fact]
    public async Task OfficialMulliganRejectsAcceptedReplayWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-mulligan-replay-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.ReadyAsync("P1", "ready-p1", RawCommand("READY"), CancellationToken.None);
        var ready = await session.ReadyAsync("P2", "ready-p2", RawCommand("READY"), CancellationToken.None);

        var activePlayerId = ready.State.ActivePlayerId;
        var secondPlayerId = ready.State.OpeningSecondActionPlayerId!;
        var activeHandBefore = ready.State.PlayerZones[activePlayerId].Hand.ToArray();
        var selectedObjectIds = activeHandBefore.Take(2).ToArray();

        var accepted = await session.SubmitAsync(
            activePlayerId,
            "mulligan-active-accepted",
            new MulliganCommand(selectedObjectIds),
            RawCommand("MULLIGAN"),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(MatchPhases.Mulligan, accepted.State.Phase);
        Assert.Equal(TimingStates.Mulligan, accepted.State.TimingState);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_COMPLETED", StringComparison.Ordinal)));
        Assert.Contains(activePlayerId, accepted.State.MulliganCompletedPlayerIds);
        Assert.DoesNotContain(secondPlayerId, accepted.State.MulliganCompletedPlayerIds);
        Assert.Equal(4, accepted.State.PlayerZones[activePlayerId].Hand.Count);
        Assert.Equal(35, accepted.State.PlayerZones[activePlayerId].MainDeck.Count);
        foreach (var objectId in selectedObjectIds)
        {
            Assert.DoesNotContain(objectId, accepted.State.PlayerZones[activePlayerId].Hand);
            Assert.Contains(objectId, accepted.State.PlayerZones[activePlayerId].MainDeck);
            Assert.Equal("MAIN_DECK", accepted.State.ObjectLocations[objectId].Zone);
        }

        Assert.False(accepted.Prompts[activePlayerId].Actionable);
        Assert.DoesNotContain("MULLIGAN", accepted.Prompts[activePlayerId].Actions);
        Assert.True(accepted.Prompts[secondPlayerId].Actionable);
        Assert.Contains("MULLIGAN", accepted.Prompts[secondPlayerId].Actions);
        AssertOfficialMulliganSecondPlayerPromptQueueAudit(accepted, activePlayerId, secondPlayerId, selectedObjectIds);

        var acceptedHash = MatchStateHasher.Hash(accepted.State);
        var replay = await session.SubmitAsync(
            activePlayerId,
            "mulligan-active-replay",
            new MulliganCommand(selectedObjectIds),
            RawCommand("MULLIGAN"),
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, replay.ErrorCode);
        Assert.Equal("现在不是该玩家的起手调整时机。", replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(accepted.State.RngCursor, replay.State.RngCursor);
        Assert.Equal(accepted.State.MulliganCompletedPlayerIds, replay.State.MulliganCompletedPlayerIds);
        Assert.Equal(accepted.State.PlayerZones[activePlayerId].Hand, replay.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(accepted.State.PlayerZones[activePlayerId].MainDeck, replay.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(accepted.State.PlayerZones[secondPlayerId].Hand, replay.State.PlayerZones[secondPlayerId].Hand);
        Assert.False(replay.Prompts[activePlayerId].Actionable);
        Assert.DoesNotContain("MULLIGAN", replay.Prompts[activePlayerId].Actions);
        Assert.True(replay.Prompts[secondPlayerId].Actionable);
        Assert.Contains("MULLIGAN", replay.Prompts[secondPlayerId].Actions);
        AssertOfficialMulliganSecondPlayerPromptQueueAudit(replay, activePlayerId, secondPlayerId, selectedObjectIds);
    }

    [Fact]
    public async Task OfficialMulliganStalePromptReplayAfterSecondPlayerWindowStartsRejectsWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-mulligan-stale-prompt-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.ReadyAsync("P1", "ready-p1", RawCommand("READY"), CancellationToken.None);
        var ready = await session.ReadyAsync("P2", "ready-p2", RawCommand("READY"), CancellationToken.None);

        var activePlayerId = ready.State.ActivePlayerId;
        var secondPlayerId = ready.State.OpeningSecondActionPlayerId!;
        var prompt = ready.Prompts[activePlayerId];
        Assert.True(prompt.Actionable);
        Assert.Contains("MULLIGAN", prompt.Actions);

        var activeHandBefore = ready.State.PlayerZones[activePlayerId].Hand.ToArray();
        var selectedObjectIds = activeHandBefore.Take(2).ToArray();
        var rawCommand = PromptScopedMulliganRawCommand(selectedObjectIds, prompt);

        var accepted = await session.SubmitAsync(
            activePlayerId,
            "mulligan-active-stale-prompt-accepted",
            new MulliganCommand(selectedObjectIds),
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(MatchPhases.Mulligan, accepted.State.Phase);
        Assert.Equal(TimingStates.Mulligan, accepted.State.TimingState);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_COMPLETED", StringComparison.Ordinal)));
        Assert.Contains(activePlayerId, accepted.State.MulliganCompletedPlayerIds);
        Assert.DoesNotContain(secondPlayerId, accepted.State.MulliganCompletedPlayerIds);
        Assert.False(accepted.Prompts[activePlayerId].Actionable);
        Assert.DoesNotContain("MULLIGAN", accepted.Prompts[activePlayerId].Actions);
        Assert.True(accepted.Prompts[secondPlayerId].Actionable);
        Assert.Contains("MULLIGAN", accepted.Prompts[secondPlayerId].Actions);
        AssertOfficialMulliganSecondPlayerPromptQueueAudit(accepted, activePlayerId, secondPlayerId, selectedObjectIds);
        foreach (var objectId in selectedObjectIds)
        {
            Assert.DoesNotContain(objectId, accepted.State.PlayerZones[activePlayerId].Hand);
            Assert.Contains(objectId, accepted.State.PlayerZones[activePlayerId].MainDeck);
            Assert.Equal("MAIN_DECK", accepted.State.ObjectLocations[objectId].Zone);
        }

        var acceptedHash = MatchStateHasher.Hash(accepted.State);
        var replay = await session.SubmitAsync(
            activePlayerId,
            "mulligan-active-stale-prompt-replay",
            new MulliganCommand(selectedObjectIds),
            rawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(accepted.State.RngCursor, replay.State.RngCursor);
        Assert.Equal(accepted.State.MulliganCompletedPlayerIds, replay.State.MulliganCompletedPlayerIds);
        Assert.Equal(accepted.State.PlayerZones[activePlayerId].Hand, replay.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(accepted.State.PlayerZones[activePlayerId].MainDeck, replay.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(accepted.State.PlayerZones[secondPlayerId].Hand, replay.State.PlayerZones[secondPlayerId].Hand);
        Assert.False(replay.Prompts[activePlayerId].Actionable);
        Assert.DoesNotContain("MULLIGAN", replay.Prompts[activePlayerId].Actions);
        Assert.True(replay.Prompts[secondPlayerId].Actionable);
        Assert.Contains("MULLIGAN", replay.Prompts[secondPlayerId].Actions);
        AssertOfficialMulliganSecondPlayerPromptQueueAudit(replay, activePlayerId, secondPlayerId, selectedObjectIds);
    }

    [Fact]
    public async Task OfficialFinalMulliganReplaysAfterFirstTurnStartsRejectWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-final-mulligan-replay-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.ReadyAsync("P1", "ready-p1", RawCommand("READY"), CancellationToken.None);
        var ready = await session.ReadyAsync("P2", "ready-p2", RawCommand("READY"), CancellationToken.None);

        var activePlayerId = ready.State.ActivePlayerId;
        var secondPlayerId = ready.State.OpeningSecondActionPlayerId!;
        var activeHandBefore = ready.State.PlayerZones[activePlayerId].Hand.ToArray();
        var activeSelectedObjectIds = activeHandBefore.Take(2).ToArray();
        var activeDrawnObjectIds = ready.State.PlayerZones[activePlayerId].MainDeck
            .Take(activeSelectedObjectIds.Length)
            .ToArray();
        var activeMulligan = await session.SubmitAsync(
            activePlayerId,
            "mulligan-active",
            new MulliganCommand(activeSelectedObjectIds),
            RawCommand("MULLIGAN"),
            CancellationToken.None);
        Assert.True(activeMulligan.Accepted, activeMulligan.ErrorMessage);

        var secondPrompt = activeMulligan.Prompts[secondPlayerId];
        Assert.True(secondPrompt.Actionable);
        Assert.Contains(CommandTypes.Mulligan, secondPrompt.Actions);
        var secondHandBefore = activeMulligan.State.PlayerZones[secondPlayerId].Hand.ToArray();
        var secondSelectedObjectIds = secondHandBefore.Take(1).ToArray();
        var secondDrawnObjectIds = activeMulligan.State.PlayerZones[secondPlayerId].MainDeck
            .Take(secondSelectedObjectIds.Length)
            .ToArray();
        var calledRuneObjectIds = activeMulligan.State.PlayerZones[activePlayerId].RuneDeck.Take(2).ToArray();
        var turnDrawnObjectIds = activeMulligan.State.PlayerZones[activePlayerId].MainDeck.Take(1).ToArray();
        var rawCommand = PromptScopedMulliganRawCommand(secondSelectedObjectIds, secondPrompt);

        var accepted = await session.SubmitAsync(
            secondPlayerId,
            "mulligan-second-final-accepted",
            new MulliganCommand(secondSelectedObjectIds),
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            accepted,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds,
            activeDrawnObjectIds,
            secondSelectedObjectIds,
            secondDrawnObjectIds,
            calledRuneObjectIds,
            turnDrawnObjectIds);

        var acceptedHash = MatchStateHasher.Hash(accepted.State);
        var stalePromptReplay = await session.SubmitAsync(
            secondPlayerId,
            "mulligan-second-final-stale-prompt-replay",
            new MulliganCommand(secondSelectedObjectIds),
            rawCommand,
            CancellationToken.None);

        Assert.False(stalePromptReplay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, stalePromptReplay.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", stalePromptReplay.ErrorMessage);
        Assert.Empty(stalePromptReplay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(stalePromptReplay.State));
        Assert.Equal(accepted.State.Tick, stalePromptReplay.State.Tick);
        Assert.Equal(accepted.State.RngCursor, stalePromptReplay.State.RngCursor);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            stalePromptReplay,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds,
            activeDrawnObjectIds,
            secondSelectedObjectIds,
            secondDrawnObjectIds,
            calledRuneObjectIds,
            turnDrawnObjectIds,
            assertEvents: false);

        var unstampedReplay = await session.SubmitAsync(
            secondPlayerId,
            "mulligan-second-final-unstamped-replay",
            new MulliganCommand(secondSelectedObjectIds),
            RawCommand(CommandTypes.Mulligan),
            CancellationToken.None);

        Assert.False(unstampedReplay.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, unstampedReplay.ErrorCode);
        Assert.Equal("起手调整只能在开局调度阶段提交。", unstampedReplay.ErrorMessage);
        Assert.Empty(unstampedReplay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(unstampedReplay.State));
        Assert.Equal(accepted.State.Tick, unstampedReplay.State.Tick);
        Assert.Equal(accepted.State.RngCursor, unstampedReplay.State.RngCursor);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            unstampedReplay,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds,
            activeDrawnObjectIds,
            secondSelectedObjectIds,
            secondDrawnObjectIds,
            calledRuneObjectIds,
            turnDrawnObjectIds,
            assertEvents: false);
    }

    [Fact]
    public async Task OfficialFirstMulliganStalePromptReplayAfterFirstTurnStartsRejectsWithoutMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-first-mulligan-first-turn-stale-prompt-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.ReadyAsync("P1", "ready-p1", RawCommand("READY"), CancellationToken.None);
        var ready = await session.ReadyAsync("P2", "ready-p2", RawCommand("READY"), CancellationToken.None);

        var activePlayerId = ready.State.ActivePlayerId;
        var secondPlayerId = ready.State.OpeningSecondActionPlayerId!;
        var activePrompt = ready.Prompts[activePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Contains(CommandTypes.Mulligan, activePrompt.Actions);

        var activeHandBefore = ready.State.PlayerZones[activePlayerId].Hand.ToArray();
        var activeSelectedObjectIds = activeHandBefore.Take(2).ToArray();
        var activeDrawnObjectIds = ready.State.PlayerZones[activePlayerId].MainDeck
            .Take(activeSelectedObjectIds.Length)
            .ToArray();
        var activeRawCommand = PromptScopedMulliganRawCommand(activeSelectedObjectIds, activePrompt);
        var activeMulligan = await session.SubmitAsync(
            activePlayerId,
            "mulligan-active-first-turn-stale-accepted",
            new MulliganCommand(activeSelectedObjectIds),
            activeRawCommand,
            CancellationToken.None);

        Assert.True(activeMulligan.Accepted, activeMulligan.ErrorMessage);
        AssertOfficialMulliganSecondPlayerPromptQueueAudit(
            activeMulligan,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds);

        var secondHandBefore = activeMulligan.State.PlayerZones[secondPlayerId].Hand.ToArray();
        var secondSelectedObjectIds = secondHandBefore.Take(1).ToArray();
        var secondDrawnObjectIds = activeMulligan.State.PlayerZones[secondPlayerId].MainDeck
            .Take(secondSelectedObjectIds.Length)
            .ToArray();
        var calledRuneObjectIds = activeMulligan.State.PlayerZones[activePlayerId].RuneDeck.Take(2).ToArray();
        var turnDrawnObjectIds = activeMulligan.State.PlayerZones[activePlayerId].MainDeck.Take(1).ToArray();

        var accepted = await session.SubmitAsync(
            secondPlayerId,
            "mulligan-second-final",
            new MulliganCommand(secondSelectedObjectIds),
            RawCommand(CommandTypes.Mulligan),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            accepted,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds,
            activeDrawnObjectIds,
            secondSelectedObjectIds,
            secondDrawnObjectIds,
            calledRuneObjectIds,
            turnDrawnObjectIds);

        var acceptedHash = MatchStateHasher.Hash(accepted.State);
        var stalePromptReplay = await session.SubmitAsync(
            activePlayerId,
            "mulligan-active-first-turn-stale-prompt-replay",
            new MulliganCommand(activeSelectedObjectIds),
            activeRawCommand,
            CancellationToken.None);

        Assert.False(stalePromptReplay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, stalePromptReplay.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", stalePromptReplay.ErrorMessage);
        Assert.Empty(stalePromptReplay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(stalePromptReplay.State));
        Assert.Equal(accepted.State.Tick, stalePromptReplay.State.Tick);
        Assert.Equal(accepted.State.RngCursor, stalePromptReplay.State.RngCursor);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            stalePromptReplay,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds,
            activeDrawnObjectIds,
            secondSelectedObjectIds,
            secondDrawnObjectIds,
            calledRuneObjectIds,
            turnDrawnObjectIds,
            assertEvents: false);

        var unstampedReplay = await session.SubmitAsync(
            activePlayerId,
            "mulligan-active-first-turn-unstamped-replay",
            new MulliganCommand(activeSelectedObjectIds),
            RawCommand(CommandTypes.Mulligan),
            CancellationToken.None);

        Assert.False(unstampedReplay.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, unstampedReplay.ErrorCode);
        Assert.Equal("起手调整只能在开局调度阶段提交。", unstampedReplay.ErrorMessage);
        Assert.Empty(unstampedReplay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(unstampedReplay.State));
        Assert.Equal(accepted.State.Tick, unstampedReplay.State.Tick);
        Assert.Equal(accepted.State.RngCursor, unstampedReplay.State.RngCursor);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            unstampedReplay,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds,
            activeDrawnObjectIds,
            secondSelectedObjectIds,
            secondDrawnObjectIds,
            calledRuneObjectIds,
            turnDrawnObjectIds,
            assertEvents: false);
    }

    [Fact]
    public async Task RoomCommandsWithFinalMulliganPromptAfterFirstTurnRejectWithoutMutation()
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(
            "official-room-commands-with-final-mulligan-prompt-after-first-turn-room");

        var readyRejected = await context.Session.ReadyAsync(
            context.SecondPlayerId,
            "ready-with-final-mulligan-prompt-after-first-turn",
            PromptScopedReadyRawCommand(context.FinalMulliganPrompt),
            CancellationToken.None);

        AssertFinalMulliganPromptAfterFirstTurnRejection(context, readyRejected);

        var submitDeckRejected = await context.Session.SubmitDeckAsync(
            context.SecondPlayerId,
            "submit-deck-with-final-mulligan-prompt-after-first-turn",
            ToSubmitCommand(context.SecondDeck),
            PromptScopedSubmitDeckRawCommand(context.SecondDeck, context.FinalMulliganPrompt),
            CancellationToken.None);

        AssertFinalMulliganPromptAfterFirstTurnRejection(context, submitDeckRejected);
    }

    [Theory]
    [InlineData(CommandTypes.PassPriority)]
    [InlineData(CommandTypes.PassFocus)]
    [InlineData(CommandTypes.Pass)]
    [InlineData(CommandTypes.EndTurn)]
    [InlineData(CommandTypes.Surrender)]
    [InlineData(CommandTypes.PlayCard)]
    [InlineData(CommandTypes.ActivateAbility)]
    [InlineData(CommandTypes.LegendAct)]
    [InlineData(CommandTypes.HideCard)]
    [InlineData(CommandTypes.TapRune)]
    [InlineData(CommandTypes.RecycleRune)]
    [InlineData(CommandTypes.RevealCard)]
    [InlineData(CommandTypes.MoveUnit)]
    [InlineData(CommandTypes.AssembleEquipment)]
    [InlineData(CommandTypes.DeclareBattle)]
    [InlineData(CommandTypes.PayCost)]
    [InlineData(CommandTypes.AssignCombatDamage)]
    [InlineData(CommandTypes.OrderTriggers)]
    [InlineData(CommandTypes.ChooseHandCards)]
    public Task SubmitCommandWithFinalMulliganPromptAfterFirstTurnRejectsWithoutMutation(string commandType)
    {
        var slug = commandType.ToLowerInvariant().Replace('_', '-');
        return AssertSubmitCommandWithFinalMulliganPromptAfterFirstTurnRejectsWithoutMutation(
            $"official-{slug}-with-final-mulligan-prompt-after-first-turn-room",
            $"{slug}-with-final-mulligan-prompt-after-first-turn",
            CreateMulliganPromptAfterFirstTurnCommand(commandType),
            commandType);
    }

    private static async Task AssertSubmitCommandWithFinalMulliganPromptAfterFirstTurnRejectsWithoutMutation(
        string sessionName,
        string intentId,
        GameCommand command,
        string commandType)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            command,
            PromptScopedBasicRawCommand(commandType, context.FinalMulliganPrompt),
            CancellationToken.None);

        AssertFinalMulliganPromptAfterFirstTurnRejection(context, rejected);
    }

    [Fact]
    public async Task PromptIdOnlyRoomCommandsWithFinalMulliganPromptAfterFirstTurnRejectWithoutMutation()
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(
            "official-room-commands-with-final-mulligan-prompt-id-after-first-turn-room");

        var readyRejected = await context.Session.ReadyAsync(
            context.SecondPlayerId,
            "ready-with-final-mulligan-prompt-id-after-first-turn",
            PromptIdOnlyReadyRawCommand(context.FinalMulliganPrompt),
            CancellationToken.None);

        AssertFinalMulliganPromptAfterFirstTurnRejection(context, readyRejected);

        var submitDeckRejected = await context.Session.SubmitDeckAsync(
            context.SecondPlayerId,
            "submit-deck-with-final-mulligan-prompt-id-after-first-turn",
            ToSubmitCommand(context.SecondDeck),
            PromptIdOnlySubmitDeckRawCommand(context.SecondDeck, context.FinalMulliganPrompt),
            CancellationToken.None);

        AssertFinalMulliganPromptAfterFirstTurnRejection(context, submitDeckRejected);
    }

    [Theory]
    [InlineData(CommandTypes.PassPriority)]
    [InlineData(CommandTypes.PassFocus)]
    [InlineData(CommandTypes.Pass)]
    [InlineData(CommandTypes.EndTurn)]
    [InlineData(CommandTypes.Surrender)]
    [InlineData(CommandTypes.PlayCard)]
    [InlineData(CommandTypes.ActivateAbility)]
    [InlineData(CommandTypes.LegendAct)]
    [InlineData(CommandTypes.HideCard)]
    [InlineData(CommandTypes.TapRune)]
    [InlineData(CommandTypes.RecycleRune)]
    [InlineData(CommandTypes.RevealCard)]
    [InlineData(CommandTypes.MoveUnit)]
    [InlineData(CommandTypes.AssembleEquipment)]
    [InlineData(CommandTypes.DeclareBattle)]
    [InlineData(CommandTypes.PayCost)]
    [InlineData(CommandTypes.AssignCombatDamage)]
    [InlineData(CommandTypes.OrderTriggers)]
    [InlineData(CommandTypes.ChooseHandCards)]
    public Task SubmitCommandWithFinalMulliganPromptIdAfterFirstTurnRejectsWithoutMutation(string commandType)
    {
        var slug = commandType.ToLowerInvariant().Replace('_', '-');
        return AssertSubmitCommandWithFinalMulliganPromptIdAfterFirstTurnRejectsWithoutMutation(
            $"official-{slug}-with-final-mulligan-prompt-id-after-first-turn-room",
            $"{slug}-with-final-mulligan-prompt-id-after-first-turn",
            CreateMulliganPromptAfterFirstTurnCommand(commandType),
            commandType);
    }

    private static async Task AssertSubmitCommandWithFinalMulliganPromptIdAfterFirstTurnRejectsWithoutMutation(
        string sessionName,
        string intentId,
        GameCommand command,
        string commandType)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            command,
            PromptIdOnlyBasicRawCommand(commandType, context.FinalMulliganPrompt),
            CancellationToken.None);

        AssertFinalMulliganPromptAfterFirstTurnRejection(context, rejected);
    }

    [Fact]
    public async Task PromptIdOnlyFinalMulliganCommandAfterFirstTurnRejectsWithoutMutation()
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(
            "official-final-mulligan-command-with-prompt-id-after-first-turn-room");

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            "mulligan-second-final-with-prompt-id-after-first-turn",
            new MulliganCommand(context.SecondSelectedObjectIds),
            PromptIdOnlyMulliganRawCommand(context.SecondSelectedObjectIds, context.FinalMulliganPrompt),
            CancellationToken.None);

        AssertFinalMulliganPromptAfterFirstTurnRejection(context, rejected);
    }

    [Fact]
    public async Task SnapshotOnlyFinalMulliganCommandAfterFirstTurnRejectsWithoutMutation()
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(
            "official-final-mulligan-command-with-snapshot-after-first-turn-room");

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            "mulligan-second-final-with-snapshot-after-first-turn",
            new MulliganCommand(context.SecondSelectedObjectIds),
            SnapshotOnlyMulliganRawCommand(
                context.SecondSelectedObjectIds,
                context.FinalMulliganPrompt.SnapshotTick.GetValueOrDefault()),
            CancellationToken.None);

        AssertFinalMulliganPromptAfterFirstTurnRejection(
            context,
            rejected,
            "行动快照已过期，请按最新状态重新提交。");
    }

    private static async Task<FinalMulliganFirstTurnAuditContext> BuildFinalMulliganFirstTurnAuditContext(string sessionName)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession(sessionName, new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.ReadyAsync("P1", "ready-p1", RawCommand("READY"), CancellationToken.None);
        var ready = await session.ReadyAsync("P2", "ready-p2", RawCommand("READY"), CancellationToken.None);

        var activePlayerId = ready.State.ActivePlayerId;
        var secondPlayerId = ready.State.OpeningSecondActionPlayerId!;
        var activeHandBefore = ready.State.PlayerZones[activePlayerId].Hand.ToArray();
        var activeSelectedObjectIds = activeHandBefore.Take(2).ToArray();
        var activeDrawnObjectIds = ready.State.PlayerZones[activePlayerId].MainDeck
            .Take(activeSelectedObjectIds.Length)
            .ToArray();
        var activeMulligan = await session.SubmitAsync(
            activePlayerId,
            "mulligan-active-before-final-prompt-stale-command",
            new MulliganCommand(activeSelectedObjectIds),
            RawCommand(CommandTypes.Mulligan),
            CancellationToken.None);

        Assert.True(activeMulligan.Accepted, activeMulligan.ErrorMessage);
        AssertOfficialMulliganSecondPlayerPromptQueueAudit(
            activeMulligan,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds);

        var finalMulliganPrompt = activeMulligan.Prompts[secondPlayerId];
        Assert.True(finalMulliganPrompt.Actionable);
        Assert.Contains(CommandTypes.Mulligan, finalMulliganPrompt.Actions);

        var secondHandBefore = activeMulligan.State.PlayerZones[secondPlayerId].Hand.ToArray();
        var secondSelectedObjectIds = secondHandBefore.Take(1).ToArray();
        var secondDrawnObjectIds = activeMulligan.State.PlayerZones[secondPlayerId].MainDeck
            .Take(secondSelectedObjectIds.Length)
            .ToArray();
        var calledRuneObjectIds = activeMulligan.State.PlayerZones[activePlayerId].RuneDeck.Take(2).ToArray();
        var turnDrawnObjectIds = activeMulligan.State.PlayerZones[activePlayerId].MainDeck.Take(1).ToArray();
        var accepted = await session.SubmitAsync(
            secondPlayerId,
            "mulligan-second-final-before-stale-command",
            new MulliganCommand(secondSelectedObjectIds),
            PromptScopedMulliganRawCommand(secondSelectedObjectIds, finalMulliganPrompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            accepted,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds,
            activeDrawnObjectIds,
            secondSelectedObjectIds,
            secondDrawnObjectIds,
            calledRuneObjectIds,
            turnDrawnObjectIds);

        var activeDeck = string.Equals(activePlayerId, "P1", StringComparison.Ordinal)
            ? p1Deck
            : p2Deck;
        var secondDeck = string.Equals(secondPlayerId, "P1", StringComparison.Ordinal)
            ? p1Deck
            : p2Deck;

        return new FinalMulliganFirstTurnAuditContext(
            session,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds,
            activeDrawnObjectIds,
            secondSelectedObjectIds,
            secondDrawnObjectIds,
            calledRuneObjectIds,
            turnDrawnObjectIds,
            finalMulliganPrompt,
            accepted,
            MatchStateHasher.Hash(accepted.State),
            activeDeck,
            secondDeck);
    }

    private static void AssertFinalMulliganPromptAfterFirstTurnRejection(
        FinalMulliganFirstTurnAuditContext context,
        ResolutionResult rejected,
        string expectedMessage = "行动窗口已过期，请按最新提示重新提交。")
    {
        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static void AssertOfficialFirstTurnEndTurnNextPlayerPromptQueueAudit(
        FinalMulliganFirstTurnAuditContext context,
        ResolutionResult result,
        IReadOnlyList<string> nextTurnCalledRuneObjectIds,
        IReadOnlyList<string> nextTurnDrawnObjectIds,
        bool assertEvents = true)
    {
        Assert.Equal(MatchStatuses.InProgress, result.State.Status);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.Equal(context.SecondPlayerId, result.State.ActivePlayerId);
        Assert.Equal(context.SecondPlayerId, result.State.TurnPlayerId);
        Assert.Equal(context.SecondPlayerId, result.State.OpeningSecondActionPlayerId);
        Assert.Equal(["P1", "P2"], result.State.ReadyPlayerIds);
        Assert.Equal(2, result.State.MulliganCompletedPlayerIds.Count);
        Assert.Contains(context.ActivePlayerId, result.State.MulliganCompletedPlayerIds);
        Assert.Contains(context.SecondPlayerId, result.State.MulliganCompletedPlayerIds);
        Assert.Null(result.State.PriorityPlayerId);
        Assert.Null(result.State.FocusPlayerId);
        Assert.Empty(result.State.StackItems);
        Assert.Empty(result.State.BattlefieldTasks);
        Assert.False(result.State.PendingTaskQueue.HasTasks);
        Assert.False(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.Null(result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Empty(result.State.PendingTaskQueue.Tasks);

        var oldTurnZones = result.State.PlayerZones[context.ActivePlayerId];
        var acceptedOldTurnZones = context.Accepted.State.PlayerZones[context.ActivePlayerId];
        Assert.Equal(acceptedOldTurnZones.Hand, oldTurnZones.Hand);
        Assert.Equal(acceptedOldTurnZones.MainDeck, oldTurnZones.MainDeck);
        Assert.Equal(acceptedOldTurnZones.RuneDeck, oldTurnZones.RuneDeck);
        Assert.Equal(acceptedOldTurnZones.Base, oldTurnZones.Base);
        Assert.Equal(acceptedOldTurnZones.Graveyard, oldTurnZones.Graveyard);

        var nextTurnZones = result.State.PlayerZones[context.SecondPlayerId];
        var acceptedNextTurnZones = context.Accepted.State.PlayerZones[context.SecondPlayerId];
        Assert.Equal(acceptedNextTurnZones.Hand.Count + nextTurnDrawnObjectIds.Count, nextTurnZones.Hand.Count);
        Assert.Equal(acceptedNextTurnZones.MainDeck.Count - nextTurnDrawnObjectIds.Count, nextTurnZones.MainDeck.Count);
        Assert.Equal(acceptedNextTurnZones.RuneDeck.Count - nextTurnCalledRuneObjectIds.Count, nextTurnZones.RuneDeck.Count);
        Assert.Equal(nextTurnCalledRuneObjectIds, nextTurnZones.Base);
        Assert.Empty(nextTurnZones.Graveyard);
        AssertOfficialObjectLocation(result.State, context.SecondPlayerId, nextTurnCalledRuneObjectIds, "BASE");
        AssertOfficialObjectLocation(result.State, context.SecondPlayerId, nextTurnDrawnObjectIds, "HAND");
        foreach (var objectId in nextTurnCalledRuneObjectIds)
        {
            Assert.DoesNotContain(objectId, nextTurnZones.RuneDeck);
        }

        foreach (var objectId in nextTurnDrawnObjectIds)
        {
            Assert.Contains(objectId, nextTurnZones.Hand);
            Assert.DoesNotContain(objectId, nextTurnZones.MainDeck);
        }

        if (assertEvents)
        {
            Assert.Equal(
                [
                    "TURN_END_DECLARED",
                    "TURN_END_CLEANUP_STARTED",
                    "RUNE_POOL_CLEARED",
                    "TURN_PLAYER_ADVANCED",
                    "TURN_START_BEGAN",
                    "RUNES_CALLED",
                    "CARD_DRAWN",
                    "RUNE_POOL_CLEARED",
                    "MAIN_PHASE_BEGAN"
                ],
                result.Events.Select(gameEvent => gameEvent.Kind).ToArray());

            var turnAdvancedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "TURN_PLAYER_ADVANCED", StringComparison.Ordinal));
            Assert.Equal(context.ActivePlayerId, Assert.IsType<string>(turnAdvancedEvent.Payload["previousTurnPlayerId"]));
            Assert.Equal(context.SecondPlayerId, Assert.IsType<string>(turnAdvancedEvent.Payload["turnPlayerId"]));
            var runeEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
            Assert.Equal(context.SecondPlayerId, Assert.IsType<string>(runeEvent.Payload["playerId"]));
            Assert.Equal(nextTurnCalledRuneObjectIds.Count, Assert.IsType<int>(runeEvent.Payload["count"]));
            var drawEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
            Assert.Equal(context.SecondPlayerId, Assert.IsType<string>(drawEvent.Payload["playerId"]));
            Assert.Equal(nextTurnDrawnObjectIds.Count, Assert.IsType<int>(drawEvent.Payload["count"]));
        }

        foreach (var snapshot in result.Snapshots.Values)
        {
            Assert.Equal(result.State.Tick, snapshot.Tick);
            Assert.Equal(context.SecondPlayerId, snapshot.ActivePlayerId);
            Assert.Equal(MatchPhases.Main, Assert.IsType<string>(snapshot.Timing["phase"]));
            Assert.Equal(TimingStates.NeutralOpen, Assert.IsType<string>(snapshot.Timing["timingState"]));
            Assert.Equal(context.SecondPlayerId, Assert.IsType<string>(snapshot.Timing["turnPlayerId"]));
            Assert.Null(snapshot.Timing["priorityPlayerId"]);
            Assert.Null(snapshot.Timing["focusPlayerId"]);
            Assert.Empty(snapshot.Stack);

            var queue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
            Assert.False(Assert.IsType<bool>(queue["hasTasks"]));
            Assert.False(Assert.IsType<bool>(queue["isBlocking"]));
            Assert.Equal("IDLE", Assert.IsType<string>(queue["phase"]));
            Assert.Null(queue["activeTaskId"]);
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(queue["tasks"]));
        }

        var oldTurnPrompt = result.Prompts[context.ActivePlayerId];
        Assert.Equal(context.ActivePlayerId, oldTurnPrompt.PlayerId);
        Assert.False(oldTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, oldTurnPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], oldTurnPrompt.Actions);
        Assert.Equal(result.State.Tick, oldTurnPrompt.SnapshotTick);
        Assert.DoesNotContain(CommandTypes.EndTurn, oldTurnPrompt.Actions);
        Assert.DoesNotContain(
            oldTurnPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.EndTurn, StringComparison.Ordinal)
                && candidate.Enabled);

        var nextTurnPrompt = result.Prompts[context.SecondPlayerId];
        Assert.Equal(context.SecondPlayerId, nextTurnPrompt.PlayerId);
        Assert.True(nextTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, nextTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, nextTurnPrompt.Actions);
        Assert.Contains(CommandTypes.Surrender, nextTurnPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Mulligan, nextTurnPrompt.Actions);
        Assert.Equal(result.State.Tick, nextTurnPrompt.SnapshotTick);
        Assert.DoesNotContain(
            nextTurnPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.Mulligan, StringComparison.Ordinal)
                && candidate.Enabled);
    }

    private static GameCommand CreateMulliganPromptAfterFirstTurnCommand(string commandType)
    {
        return commandType switch
        {
            CommandTypes.PassPriority => new PassPriorityCommand(),
            CommandTypes.PassFocus => new PassFocusCommand(),
            CommandTypes.Pass => new PassCommand(),
            CommandTypes.EndTurn => new EndTurnCommand(),
            CommandTypes.Surrender => new SurrenderCommand(),
            CommandTypes.PlayCard => new PlayCardCommand("missing-source", "missing-card", []),
            CommandTypes.ActivateAbility => new ActivateAbilityCommand("missing-source", "missing-ability", []),
            CommandTypes.LegendAct => new LegendActCommand("missing-source", "missing-ability", []),
            CommandTypes.HideCard => new HideCardCommand("missing-source", "missing-card"),
            CommandTypes.TapRune => new TapRuneCommand("missing-source"),
            CommandTypes.RecycleRune => new RecycleRuneCommand("missing-source"),
            CommandTypes.RevealCard => new RevealCardCommand("missing-source", "missing-card", []),
            CommandTypes.MoveUnit => new MoveUnitCommand("missing-source", Destination: "missing-destination"),
            CommandTypes.AssembleEquipment => new AssembleEquipmentCommand("missing-source", "missing-target"),
            CommandTypes.DeclareBattle => new DeclareBattleCommand("missing-battlefield"),
            CommandTypes.PayCost => new PayCostCommand("missing-payment", "missing-window", []),
            CommandTypes.AssignCombatDamage => new AssignCombatDamageCommand("missing-battle", "missing-battlefield", []),
            CommandTypes.OrderTriggers => new OrderTriggersCommand(OrderedTriggerIds: []),
            CommandTypes.ChooseHandCards => new ChooseHandCardsCommand("missing-choice", "missing-window", []),
            _ => throw new ArgumentOutOfRangeException(nameof(commandType), commandType, null)
        };
    }

    private sealed record FinalMulliganFirstTurnAuditContext(
        MatchSession Session,
        string ActivePlayerId,
        string SecondPlayerId,
        IReadOnlyList<string> ActiveSelectedObjectIds,
        IReadOnlyList<string> ActiveDrawnObjectIds,
        IReadOnlyList<string> SecondSelectedObjectIds,
        IReadOnlyList<string> SecondDrawnObjectIds,
        IReadOnlyList<string> CalledRuneObjectIds,
        IReadOnlyList<string> TurnDrawnObjectIds,
        ActionPromptDto FinalMulliganPrompt,
        ResolutionResult Accepted,
        string AcceptedHash,
        OfficialDecklist ActiveDeck,
        OfficialDecklist SecondDeck);

    [Fact]
    public Task FirstTurnEndTurnPromptReplayAfterNextPlayerStartsRejectsWithoutMutation()
    {
        return AssertFirstTurnEndTurnPromptReplayAfterNextPlayerStartsRejectsWithoutMutation(
            "official-first-turn-end-turn-prompt-replay-after-next-player-starts-room",
            "first-turn-end-turn",
            firstTurnPrompt => PromptScopedBasicRawCommand(CommandTypes.EndTurn, firstTurnPrompt),
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyFirstTurnEndTurnPromptReplayAfterNextPlayerStartsRejectsWithoutMutation()
    {
        return AssertFirstTurnEndTurnPromptReplayAfterNextPlayerStartsRejectsWithoutMutation(
            "official-first-turn-end-turn-prompt-id-replay-after-next-player-starts-room",
            "first-turn-end-turn-prompt-id",
            firstTurnPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.EndTurn, firstTurnPrompt),
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyFirstTurnEndTurnPromptReplayAfterNextPlayerStartsRejectsWithoutMutation()
    {
        return AssertFirstTurnEndTurnPromptReplayAfterNextPlayerStartsRejectsWithoutMutation(
            "official-first-turn-end-turn-snapshot-replay-after-next-player-starts-room",
            "first-turn-end-turn-snapshot",
            firstTurnPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.EndTurn, firstTurnPrompt.SnapshotTick.GetValueOrDefault()),
            "行动快照已过期，请按最新状态重新提交。");
    }

    [Fact]
    public Task WrongPlayerFirstTurnEndTurnPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnEndTurnPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-end-turn-prompt-room",
            "wrong-player-first-turn-end-turn-prompt",
            firstTurnPrompt => PromptScopedBasicRawCommand(CommandTypes.EndTurn, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyWrongPlayerFirstTurnEndTurnPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnEndTurnPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-end-turn-prompt-id-room",
            "wrong-player-first-turn-end-turn-prompt-id",
            firstTurnPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.EndTurn, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyWrongPlayerFirstTurnEndTurnPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnEndTurnPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-end-turn-snapshot-room",
            "wrong-player-first-turn-end-turn-snapshot",
            firstTurnPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.EndTurn, firstTurnPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.PhaseNotAllowed,
            "结束回合只能由当前玩家在开放主阶段提交。");
    }

    [Fact]
    public Task WrongPlayerFirstTurnMulliganPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnMulliganPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-mulligan-prompt-room",
            "wrong-player-first-turn-mulligan-prompt",
            firstTurnPrompt => PromptScopedMulliganRawCommand([], firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyWrongPlayerFirstTurnMulliganPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnMulliganPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-mulligan-prompt-id-room",
            "wrong-player-first-turn-mulligan-prompt-id",
            firstTurnPrompt => PromptIdOnlyMulliganRawCommand([], firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyWrongPlayerFirstTurnMulliganPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnMulliganPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-mulligan-snapshot-room",
            "wrong-player-first-turn-mulligan-snapshot",
            firstTurnPrompt => SnapshotOnlyMulliganRawCommand([], firstTurnPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.PhaseNotAllowed,
            "起手调整只能在开局调度阶段提交。");
    }

    [Fact]
    public Task WrongPlayerFirstTurnPassPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnPassPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-pass-prompt-room",
            "wrong-player-first-turn-pass-prompt",
            firstTurnPrompt => PromptScopedBasicRawCommand(CommandTypes.Pass, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyWrongPlayerFirstTurnPassPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnPassPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-pass-prompt-id-room",
            "wrong-player-first-turn-pass-prompt-id",
            firstTurnPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.Pass, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyWrongPlayerFirstTurnPassPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnPassPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-pass-snapshot-room",
            "wrong-player-first-turn-pass-snapshot",
            firstTurnPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.Pass, firstTurnPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.PhaseNotAllowed,
            "让过只能由当前玩家在可让过窗口中提交。");
    }

    [Fact]
    public Task WrongPlayerFirstTurnPassPriorityPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnPassPriorityPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-pass-priority-prompt-room",
            "wrong-player-first-turn-pass-priority-prompt",
            firstTurnPrompt => PromptScopedBasicRawCommand(CommandTypes.PassPriority, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyWrongPlayerFirstTurnPassPriorityPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnPassPriorityPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-pass-priority-prompt-id-room",
            "wrong-player-first-turn-pass-priority-prompt-id",
            firstTurnPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.PassPriority, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyWrongPlayerFirstTurnPassPriorityPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnPassPriorityPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-pass-priority-snapshot-room",
            "wrong-player-first-turn-pass-priority-snapshot",
            firstTurnPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.PassPriority, firstTurnPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.PhaseNotAllowed,
            "让过优先权只能在优先行动窗口中提交。");
    }

    [Fact]
    public Task WrongPlayerFirstTurnPassFocusPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnPassFocusPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-pass-focus-prompt-room",
            "wrong-player-first-turn-pass-focus-prompt",
            firstTurnPrompt => PromptScopedBasicRawCommand(CommandTypes.PassFocus, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyWrongPlayerFirstTurnPassFocusPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnPassFocusPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-pass-focus-prompt-id-room",
            "wrong-player-first-turn-pass-focus-prompt-id",
            firstTurnPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.PassFocus, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyWrongPlayerFirstTurnPassFocusPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnPassFocusPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-pass-focus-snapshot-room",
            "wrong-player-first-turn-pass-focus-snapshot",
            firstTurnPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.PassFocus, firstTurnPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.PhaseNotAllowed,
            "让过焦点只能在法术对决焦点窗口中提交。");
    }

    [Fact]
    public Task WrongPlayerFirstTurnMoveUnitPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnMoveUnitPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-move-unit-prompt-room",
            "wrong-player-first-turn-move-unit-prompt",
            firstTurnPrompt => PromptScopedBasicRawCommand(CommandTypes.MoveUnit, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyWrongPlayerFirstTurnMoveUnitPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnMoveUnitPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-move-unit-prompt-id-room",
            "wrong-player-first-turn-move-unit-prompt-id",
            firstTurnPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.MoveUnit, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyWrongPlayerFirstTurnMoveUnitPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnMoveUnitPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-move-unit-snapshot-room",
            "wrong-player-first-turn-move-unit-snapshot",
            firstTurnPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.MoveUnit, firstTurnPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.PhaseNotAllowed,
            "移动单位只能在当前玩家的开放主阶段提交。");
    }

    [Fact]
    public Task WrongPlayerFirstTurnDeclareBattlePromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnDeclareBattlePromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-declare-battle-prompt-room",
            "wrong-player-first-turn-declare-battle-prompt",
            firstTurnPrompt => PromptScopedBasicRawCommand(CommandTypes.DeclareBattle, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyWrongPlayerFirstTurnDeclareBattlePromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnDeclareBattlePromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-declare-battle-prompt-id-room",
            "wrong-player-first-turn-declare-battle-prompt-id",
            firstTurnPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.DeclareBattle, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyWrongPlayerFirstTurnDeclareBattlePromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnDeclareBattlePromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-declare-battle-snapshot-room",
            "wrong-player-first-turn-declare-battle-snapshot",
            firstTurnPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.DeclareBattle, firstTurnPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.UnsupportedCommand,
            "当前声明战斗路径尚未由服务端开放。");
    }

    [Fact]
    public Task WrongPlayerFirstTurnPlayCardPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnPlayCardPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-play-card-prompt-room",
            "wrong-player-first-turn-play-card-prompt",
            firstTurnPrompt => PromptScopedBasicRawCommand(CommandTypes.PlayCard, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyWrongPlayerFirstTurnPlayCardPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnPlayCardPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-play-card-prompt-id-room",
            "wrong-player-first-turn-play-card-prompt-id",
            firstTurnPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.PlayCard, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyWrongPlayerFirstTurnPlayCardPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnPlayCardPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-play-card-snapshot-room",
            "wrong-player-first-turn-play-card-snapshot",
            firstTurnPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.PlayCard, firstTurnPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.UnsupportedCardBehavior,
            "Unsupported card behavior or mode: missing-card ");
    }

    [Fact]
    public Task WrongPlayerFirstTurnActivateAbilityPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnActivateAbilityPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-activate-ability-prompt-room",
            "wrong-player-first-turn-activate-ability-prompt",
            firstTurnPrompt => PromptScopedBasicRawCommand(CommandTypes.ActivateAbility, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyWrongPlayerFirstTurnActivateAbilityPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnActivateAbilityPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-activate-ability-prompt-id-room",
            "wrong-player-first-turn-activate-ability-prompt-id",
            firstTurnPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.ActivateAbility, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyWrongPlayerFirstTurnActivateAbilityPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnActivateAbilityPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-activate-ability-snapshot-room",
            "wrong-player-first-turn-activate-ability-snapshot",
            firstTurnPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.ActivateAbility, firstTurnPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.UnsupportedCommand,
            "当前启动技能路径尚未由服务端开放。");
    }

    [Fact]
    public Task WrongPlayerFirstTurnTapRunePromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnTapRunePromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-tap-rune-prompt-room",
            "wrong-player-first-turn-tap-rune-prompt",
            firstTurnPrompt => PromptScopedBasicRawCommand(CommandTypes.TapRune, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyWrongPlayerFirstTurnTapRunePromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnTapRunePromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-tap-rune-prompt-id-room",
            "wrong-player-first-turn-tap-rune-prompt-id",
            firstTurnPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.TapRune, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyWrongPlayerFirstTurnTapRunePromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnTapRunePromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-tap-rune-snapshot-room",
            "wrong-player-first-turn-tap-rune-snapshot",
            firstTurnPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.TapRune, firstTurnPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.PhaseNotAllowed,
            "横置符文只能在当前玩家的开放主阶段提交。");
    }

    [Fact]
    public Task WrongPlayerFirstTurnRecycleRunePromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnRecycleRunePromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-recycle-rune-prompt-room",
            "wrong-player-first-turn-recycle-rune-prompt",
            firstTurnPrompt => PromptScopedBasicRawCommand(CommandTypes.RecycleRune, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyWrongPlayerFirstTurnRecycleRunePromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnRecycleRunePromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-recycle-rune-prompt-id-room",
            "wrong-player-first-turn-recycle-rune-prompt-id",
            firstTurnPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.RecycleRune, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyWrongPlayerFirstTurnRecycleRunePromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnRecycleRunePromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-recycle-rune-snapshot-room",
            "wrong-player-first-turn-recycle-rune-snapshot",
            firstTurnPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.RecycleRune, firstTurnPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.PhaseNotAllowed,
            "回收符文只能在当前玩家的开放主阶段提交。");
    }

    [Fact]
    public Task WrongPlayerFirstTurnHideCardPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnHideCardPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-hide-card-prompt-room",
            "wrong-player-first-turn-hide-card-prompt",
            firstTurnPrompt => PromptScopedBasicRawCommand(CommandTypes.HideCard, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyWrongPlayerFirstTurnHideCardPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnHideCardPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-hide-card-prompt-id-room",
            "wrong-player-first-turn-hide-card-prompt-id",
            firstTurnPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.HideCard, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyWrongPlayerFirstTurnHideCardPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnHideCardPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-hide-card-snapshot-room",
            "wrong-player-first-turn-hide-card-snapshot",
            firstTurnPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.HideCard, firstTurnPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.PhaseNotAllowed,
            "待命埋伏只能在当前玩家的开放主阶段提交。");
    }

    [Fact]
    public Task WrongPlayerFirstTurnRevealCardPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnRevealCardPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-reveal-card-prompt-room",
            "wrong-player-first-turn-reveal-card-prompt",
            firstTurnPrompt => PromptScopedBasicRawCommand(CommandTypes.RevealCard, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyWrongPlayerFirstTurnRevealCardPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnRevealCardPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-reveal-card-prompt-id-room",
            "wrong-player-first-turn-reveal-card-prompt-id",
            firstTurnPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.RevealCard, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyWrongPlayerFirstTurnRevealCardPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnRevealCardPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-reveal-card-snapshot-room",
            "wrong-player-first-turn-reveal-card-snapshot",
            firstTurnPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.RevealCard, firstTurnPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.UnsupportedCardBehavior,
            "暂不支持该牌的待命翻开行为：missing-card");
    }

    [Fact]
    public Task WrongPlayerFirstTurnLegendActPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnLegendActPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-legend-act-prompt-room",
            "wrong-player-first-turn-legend-act-prompt",
            firstTurnPrompt => PromptScopedBasicRawCommand(CommandTypes.LegendAct, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyWrongPlayerFirstTurnLegendActPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnLegendActPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-legend-act-prompt-id-room",
            "wrong-player-first-turn-legend-act-prompt-id",
            firstTurnPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.LegendAct, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyWrongPlayerFirstTurnLegendActPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnLegendActPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-legend-act-snapshot-room",
            "wrong-player-first-turn-legend-act-snapshot",
            firstTurnPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.LegendAct, firstTurnPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.UnsupportedCardBehavior,
            "当前传奇行动尚未由服务端开放。");
    }

    [Fact]
    public Task WrongPlayerFirstTurnAssembleEquipmentPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnAssembleEquipmentPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-assemble-equipment-prompt-room",
            "wrong-player-first-turn-assemble-equipment-prompt",
            firstTurnPrompt => PromptScopedBasicRawCommand(CommandTypes.AssembleEquipment, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyWrongPlayerFirstTurnAssembleEquipmentPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnAssembleEquipmentPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-assemble-equipment-prompt-id-room",
            "wrong-player-first-turn-assemble-equipment-prompt-id",
            firstTurnPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.AssembleEquipment, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyWrongPlayerFirstTurnAssembleEquipmentPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnAssembleEquipmentPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-assemble-equipment-snapshot-room",
            "wrong-player-first-turn-assemble-equipment-snapshot",
            firstTurnPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.AssembleEquipment, firstTurnPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.UnsupportedCommand,
            "当前装备装配路径尚未由服务端开放。");
    }

    [Fact]
    public Task WrongPlayerFirstTurnPayCostPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnPayCostPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-pay-cost-prompt-room",
            "wrong-player-first-turn-pay-cost-prompt",
            firstTurnPrompt => PromptScopedBasicRawCommand(CommandTypes.PayCost, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyWrongPlayerFirstTurnPayCostPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnPayCostPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-pay-cost-prompt-id-room",
            "wrong-player-first-turn-pay-cost-prompt-id",
            firstTurnPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.PayCost, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyWrongPlayerFirstTurnPayCostPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnPayCostPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-pay-cost-snapshot-room",
            "wrong-player-first-turn-pay-cost-snapshot",
            firstTurnPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.PayCost, firstTurnPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.PhaseNotAllowed,
            "当前没有服务端支付窗口可处理 PAY_COST。");
    }

    [Fact]
    public Task WrongPlayerFirstTurnAssignCombatDamagePromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnAssignCombatDamagePromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-assign-combat-damage-prompt-room",
            "wrong-player-first-turn-assign-combat-damage-prompt",
            firstTurnPrompt => PromptScopedBasicRawCommand(CommandTypes.AssignCombatDamage, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyWrongPlayerFirstTurnAssignCombatDamagePromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnAssignCombatDamagePromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-assign-combat-damage-prompt-id-room",
            "wrong-player-first-turn-assign-combat-damage-prompt-id",
            firstTurnPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.AssignCombatDamage, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyWrongPlayerFirstTurnAssignCombatDamagePromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnAssignCombatDamagePromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-assign-combat-damage-snapshot-room",
            "wrong-player-first-turn-assign-combat-damage-snapshot",
            firstTurnPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.AssignCombatDamage, firstTurnPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.InvalidPayload,
            "ASSIGN_COMBAT_DAMAGE 需要 battleId、battlefieldId 与非空 assignments。");
    }

    [Fact]
    public Task WrongPlayerFirstTurnOrderTriggersPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnOrderTriggersPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-order-triggers-prompt-room",
            "wrong-player-first-turn-order-triggers-prompt",
            firstTurnPrompt => PromptScopedBasicRawCommand(CommandTypes.OrderTriggers, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyWrongPlayerFirstTurnOrderTriggersPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnOrderTriggersPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-order-triggers-prompt-id-room",
            "wrong-player-first-turn-order-triggers-prompt-id",
            firstTurnPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.OrderTriggers, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyWrongPlayerFirstTurnOrderTriggersPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnOrderTriggersPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-order-triggers-snapshot-room",
            "wrong-player-first-turn-order-triggers-snapshot",
            firstTurnPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.OrderTriggers, firstTurnPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.InvalidPayload,
            "ORDER_TRIGGERS 需要非空且不重复的 orderedTriggerIds。");
    }

    [Fact]
    public Task WrongPlayerFirstTurnChooseHandCardsPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnChooseHandCardsPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-choose-hand-cards-prompt-room",
            "wrong-player-first-turn-choose-hand-cards-prompt",
            firstTurnPrompt => PromptScopedBasicRawCommand(CommandTypes.ChooseHandCards, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyWrongPlayerFirstTurnChooseHandCardsPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnChooseHandCardsPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-choose-hand-cards-prompt-id-room",
            "wrong-player-first-turn-choose-hand-cards-prompt-id",
            firstTurnPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.ChooseHandCards, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyWrongPlayerFirstTurnChooseHandCardsPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnChooseHandCardsPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-choose-hand-cards-snapshot-room",
            "wrong-player-first-turn-choose-hand-cards-snapshot",
            firstTurnPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.ChooseHandCards, firstTurnPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.PhaseNotAllowed,
            "当前没有服务端手牌选择窗口可处理 CHOOSE_HAND_CARDS。");
    }

    [Fact]
    public Task WrongPlayerFirstTurnReadyPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnReadyPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-ready-prompt-room",
            "wrong-player-first-turn-ready-prompt",
            firstTurnPrompt => PromptScopedReadyRawCommand(firstTurnPrompt));
    }

    [Fact]
    public Task PromptIdOnlyWrongPlayerFirstTurnReadyPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnReadyPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-ready-prompt-id-room",
            "wrong-player-first-turn-ready-prompt-id",
            firstTurnPrompt => PromptIdOnlyReadyRawCommand(firstTurnPrompt));
    }

    [Fact]
    public Task SnapshotOnlyWrongPlayerFirstTurnReadyPromptAcceptsWithoutMutation()
    {
        return AssertSnapshotOnlyWrongPlayerFirstTurnReadyPromptAcceptsWithoutMutation(
            "official-wrong-player-first-turn-ready-snapshot-room",
            "wrong-player-first-turn-ready-snapshot");
    }

    [Fact]
    public Task WrongPlayerFirstTurnSubmitDeckPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnSubmitDeckPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-submit-deck-prompt-room",
            "wrong-player-first-turn-submit-deck-prompt",
            (context, firstTurnPrompt) => PromptScopedSubmitDeckRawCommand(context.SecondDeck, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyWrongPlayerFirstTurnSubmitDeckPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnSubmitDeckPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-submit-deck-prompt-id-room",
            "wrong-player-first-turn-submit-deck-prompt-id",
            (context, firstTurnPrompt) => PromptIdOnlySubmitDeckRawCommand(context.SecondDeck, firstTurnPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyWrongPlayerFirstTurnSubmitDeckPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnSubmitDeckPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-submit-deck-snapshot-room",
            "wrong-player-first-turn-submit-deck-snapshot",
            (context, firstTurnPrompt) => SnapshotOnlySubmitDeckRawCommand(context.SecondDeck, firstTurnPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.PhaseNotAllowed,
            "对局开始后不能更改卡组。");
    }

    [Fact]
    public Task WrongPlayerFirstTurnSurrenderPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnSurrenderPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-surrender-prompt-room",
            "wrong-player-first-turn-surrender-prompt",
            firstTurnPrompt => PromptScopedBasicRawCommand(CommandTypes.Surrender, firstTurnPrompt));
    }

    [Fact]
    public Task PromptIdOnlyWrongPlayerFirstTurnSurrenderPromptRejectsWithoutMutation()
    {
        return AssertWrongPlayerFirstTurnSurrenderPromptRejectsWithoutMutation(
            "official-wrong-player-first-turn-surrender-prompt-id-room",
            "wrong-player-first-turn-surrender-prompt-id",
            firstTurnPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.Surrender, firstTurnPrompt));
    }

    [Fact]
    public Task SnapshotOnlyWrongPlayerFirstTurnSurrenderPromptReplayAfterMatchFinishedRejectsWithoutMutation()
    {
        return AssertSnapshotOnlyWrongPlayerFirstTurnSurrenderPromptReplayAfterMatchFinishedRejectsWithoutMutation(
            "official-wrong-player-first-turn-surrender-snapshot-room",
            "wrong-player-first-turn-surrender-snapshot");
    }

    [Fact]
    public Task FirstTurnSurrenderPromptReplayAfterMatchFinishedRejectsWithoutMutation()
    {
        return AssertFirstTurnSurrenderPromptReplayAfterMatchFinishedRejectsWithoutMutation(
            "official-first-turn-surrender-prompt-replay-after-match-finished-room",
            "first-turn-surrender",
            firstTurnPrompt => PromptScopedBasicRawCommand(CommandTypes.Surrender, firstTurnPrompt),
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyFirstTurnSurrenderPromptReplayAfterMatchFinishedRejectsWithoutMutation()
    {
        return AssertFirstTurnSurrenderPromptReplayAfterMatchFinishedRejectsWithoutMutation(
            "official-first-turn-surrender-prompt-id-replay-after-match-finished-room",
            "first-turn-surrender-prompt-id",
            firstTurnPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.Surrender, firstTurnPrompt),
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyFirstTurnSurrenderPromptReplayAfterMatchFinishedRejectsWithoutMutation()
    {
        return AssertFirstTurnSurrenderPromptReplayAfterMatchFinishedRejectsWithoutMutation(
            "official-first-turn-surrender-snapshot-replay-after-match-finished-room",
            "first-turn-surrender-snapshot",
            firstTurnPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.Surrender, firstTurnPrompt.SnapshotTick.GetValueOrDefault()),
            "行动快照已过期，请按最新状态重新提交。");
    }

    [Fact]
    public Task OppositePlayerFirstTurnSurrenderWaitPromptReplayAfterMatchFinishedRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnSurrenderWaitPromptReplayAfterMatchFinishedRejectsWithoutMutation(
            "official-opposite-player-first-turn-surrender-wait-prompt-replay-after-match-finished-room",
            "opposite-player-first-turn-surrender",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.Surrender, waitPrompt),
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyOppositePlayerFirstTurnSurrenderWaitPromptReplayAfterMatchFinishedRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnSurrenderWaitPromptReplayAfterMatchFinishedRejectsWithoutMutation(
            "official-opposite-player-first-turn-surrender-wait-prompt-id-replay-after-match-finished-room",
            "opposite-player-first-turn-surrender-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.Surrender, waitPrompt),
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyOppositePlayerFirstTurnSurrenderWaitPromptReplayAfterMatchFinishedRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnSurrenderWaitPromptReplayAfterMatchFinishedRejectsWithoutMutation(
            "official-opposite-player-first-turn-surrender-wait-snapshot-replay-after-match-finished-room",
            "opposite-player-first-turn-surrender-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.Surrender, waitPrompt.SnapshotTick.GetValueOrDefault()),
            "行动快照已过期，请按最新状态重新提交。");
    }

    [Fact]
    public Task OppositePlayerFirstTurnEndTurnWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnEndTurnWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-end-turn-wait-prompt-room",
            "opposite-player-first-turn-end-turn-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.EndTurn, waitPrompt));
    }

    [Fact]
    public Task PromptIdOnlyOppositePlayerFirstTurnEndTurnWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnEndTurnWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-end-turn-wait-prompt-id-room",
            "opposite-player-first-turn-end-turn-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.EndTurn, waitPrompt));
    }

    [Fact]
    public Task SnapshotOnlyOppositePlayerFirstTurnEndTurnWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnEndTurnWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-end-turn-wait-snapshot-room",
            "opposite-player-first-turn-end-turn-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.EndTurn, waitPrompt.SnapshotTick.GetValueOrDefault()));
    }

    [Fact]
    public Task ActivePlayerFirstTurnEndTurnWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnEndTurnWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-end-turn-with-opposite-wait-prompt-room",
            "active-player-first-turn-end-turn-with-opposite-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.EndTurn, waitPrompt));
    }

    [Fact]
    public Task PromptIdOnlyActivePlayerFirstTurnEndTurnWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnEndTurnWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-end-turn-with-opposite-wait-prompt-id-room",
            "active-player-first-turn-end-turn-with-opposite-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.EndTurn, waitPrompt));
    }

    [Fact]
    public Task SnapshotOnlyActivePlayerFirstTurnEndTurnWithOppositeWaitPromptReplayAfterNextPlayerStartsRejectsWithoutMutation()
    {
        return AssertSnapshotOnlyActivePlayerFirstTurnEndTurnWithOppositeWaitPromptReplayAfterNextPlayerStartsRejectsWithoutMutation(
            "official-active-player-first-turn-end-turn-with-opposite-wait-snapshot-room",
            "active-player-first-turn-end-turn-with-opposite-wait-snapshot");
    }

    [Fact]
    public Task ActivePlayerFirstTurnSurrenderWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnSurrenderWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-surrender-with-opposite-wait-prompt-room",
            "active-player-first-turn-surrender-with-opposite-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.Surrender, waitPrompt));
    }

    [Fact]
    public Task PromptIdOnlyActivePlayerFirstTurnSurrenderWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnSurrenderWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-surrender-with-opposite-wait-prompt-id-room",
            "active-player-first-turn-surrender-with-opposite-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.Surrender, waitPrompt));
    }

    [Fact]
    public Task SnapshotOnlyActivePlayerFirstTurnSurrenderWithOppositeWaitPromptReplayAfterMatchFinishedRejectsWithoutMutation()
    {
        return AssertSnapshotOnlyActivePlayerFirstTurnSurrenderWithOppositeWaitPromptReplayAfterMatchFinishedRejectsWithoutMutation(
            "official-active-player-first-turn-surrender-with-opposite-wait-snapshot-room",
            "active-player-first-turn-surrender-with-opposite-wait-snapshot");
    }

    [Fact]
    public Task ActivePlayerFirstTurnMulliganWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnMulliganWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-mulligan-with-opposite-wait-prompt-room",
            "active-player-first-turn-mulligan-with-opposite-wait-prompt",
            waitPrompt => PromptScopedMulliganRawCommand([], waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyActivePlayerFirstTurnMulliganWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnMulliganWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-mulligan-with-opposite-wait-prompt-id-room",
            "active-player-first-turn-mulligan-with-opposite-wait-prompt-id",
            waitPrompt => PromptIdOnlyMulliganRawCommand([], waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyActivePlayerFirstTurnMulliganWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnMulliganWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-mulligan-with-opposite-wait-snapshot-room",
            "active-player-first-turn-mulligan-with-opposite-wait-snapshot",
            waitPrompt => SnapshotOnlyMulliganRawCommand([], waitPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.PhaseNotAllowed,
            "起手调整只能在开局调度阶段提交。");
    }

    [Fact]
    public Task OppositePlayerFirstTurnMulliganWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnMulliganWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-mulligan-wait-prompt-room",
            "opposite-player-first-turn-mulligan-wait-prompt",
            waitPrompt => PromptScopedMulliganRawCommand([], waitPrompt));
    }

    [Fact]
    public Task PromptIdOnlyOppositePlayerFirstTurnMulliganWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnMulliganWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-mulligan-wait-prompt-id-room",
            "opposite-player-first-turn-mulligan-wait-prompt-id",
            waitPrompt => PromptIdOnlyMulliganRawCommand([], waitPrompt));
    }

    [Fact]
    public Task SnapshotOnlyOppositePlayerFirstTurnMulliganWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnMulliganWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-mulligan-wait-snapshot-room",
            "opposite-player-first-turn-mulligan-wait-snapshot",
            waitPrompt => SnapshotOnlyMulliganRawCommand([], waitPrompt.SnapshotTick.GetValueOrDefault()));
    }

    [Fact]
    public Task OppositePlayerFirstTurnPassWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnPassWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-pass-wait-prompt-room",
            "opposite-player-first-turn-pass-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.Pass, waitPrompt));
    }

    [Fact]
    public Task PromptIdOnlyOppositePlayerFirstTurnPassWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnPassWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-pass-wait-prompt-id-room",
            "opposite-player-first-turn-pass-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.Pass, waitPrompt));
    }

    [Fact]
    public Task SnapshotOnlyOppositePlayerFirstTurnPassWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnPassWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-pass-wait-snapshot-room",
            "opposite-player-first-turn-pass-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.Pass, waitPrompt.SnapshotTick.GetValueOrDefault()));
    }

    [Fact]
    public Task OppositePlayerFirstTurnPassPriorityWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnPassPriorityWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-pass-priority-wait-prompt-room",
            "opposite-player-first-turn-pass-priority-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.PassPriority, waitPrompt));
    }

    [Fact]
    public Task PromptIdOnlyOppositePlayerFirstTurnPassPriorityWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnPassPriorityWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-pass-priority-wait-prompt-id-room",
            "opposite-player-first-turn-pass-priority-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.PassPriority, waitPrompt));
    }

    [Fact]
    public Task SnapshotOnlyOppositePlayerFirstTurnPassPriorityWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnPassPriorityWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-pass-priority-wait-snapshot-room",
            "opposite-player-first-turn-pass-priority-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.PassPriority, waitPrompt.SnapshotTick.GetValueOrDefault()));
    }

    [Fact]
    public Task OppositePlayerFirstTurnPassFocusWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnPassFocusWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-pass-focus-wait-prompt-room",
            "opposite-player-first-turn-pass-focus-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.PassFocus, waitPrompt));
    }

    [Fact]
    public Task PromptIdOnlyOppositePlayerFirstTurnPassFocusWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnPassFocusWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-pass-focus-wait-prompt-id-room",
            "opposite-player-first-turn-pass-focus-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.PassFocus, waitPrompt));
    }

    [Fact]
    public Task SnapshotOnlyOppositePlayerFirstTurnPassFocusWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnPassFocusWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-pass-focus-wait-snapshot-room",
            "opposite-player-first-turn-pass-focus-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.PassFocus, waitPrompt.SnapshotTick.GetValueOrDefault()));
    }

    [Fact]
    public Task OppositePlayerFirstTurnMoveUnitWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnMoveUnitWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-move-unit-wait-prompt-room",
            "opposite-player-first-turn-move-unit-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.MoveUnit, waitPrompt));
    }

    [Fact]
    public Task PromptIdOnlyOppositePlayerFirstTurnMoveUnitWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnMoveUnitWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-move-unit-wait-prompt-id-room",
            "opposite-player-first-turn-move-unit-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.MoveUnit, waitPrompt));
    }

    [Fact]
    public Task SnapshotOnlyOppositePlayerFirstTurnMoveUnitWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnMoveUnitWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-move-unit-wait-snapshot-room",
            "opposite-player-first-turn-move-unit-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.MoveUnit, waitPrompt.SnapshotTick.GetValueOrDefault()));
    }

    [Fact]
    public Task OppositePlayerFirstTurnDeclareBattleWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnDeclareBattleWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-declare-battle-wait-prompt-room",
            "opposite-player-first-turn-declare-battle-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.DeclareBattle, waitPrompt));
    }

    [Fact]
    public Task PromptIdOnlyOppositePlayerFirstTurnDeclareBattleWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnDeclareBattleWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-declare-battle-wait-prompt-id-room",
            "opposite-player-first-turn-declare-battle-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.DeclareBattle, waitPrompt));
    }

    [Fact]
    public Task SnapshotOnlyOppositePlayerFirstTurnDeclareBattleWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnDeclareBattleWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-declare-battle-wait-snapshot-room",
            "opposite-player-first-turn-declare-battle-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.DeclareBattle, waitPrompt.SnapshotTick.GetValueOrDefault()));
    }

    [Fact]
    public Task OppositePlayerFirstTurnPlayCardWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnPlayCardWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-play-card-wait-prompt-room",
            "opposite-player-first-turn-play-card-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.PlayCard, waitPrompt));
    }

    [Fact]
    public Task PromptIdOnlyOppositePlayerFirstTurnPlayCardWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnPlayCardWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-play-card-wait-prompt-id-room",
            "opposite-player-first-turn-play-card-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.PlayCard, waitPrompt));
    }

    [Fact]
    public Task SnapshotOnlyOppositePlayerFirstTurnPlayCardWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnPlayCardWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-play-card-wait-snapshot-room",
            "opposite-player-first-turn-play-card-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.PlayCard, waitPrompt.SnapshotTick.GetValueOrDefault()));
    }

    [Fact]
    public Task OppositePlayerFirstTurnActivateAbilityWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnActivateAbilityWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-activate-ability-wait-prompt-room",
            "opposite-player-first-turn-activate-ability-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.ActivateAbility, waitPrompt));
    }

    [Fact]
    public Task PromptIdOnlyOppositePlayerFirstTurnActivateAbilityWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnActivateAbilityWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-activate-ability-wait-prompt-id-room",
            "opposite-player-first-turn-activate-ability-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.ActivateAbility, waitPrompt));
    }

    [Fact]
    public Task SnapshotOnlyOppositePlayerFirstTurnActivateAbilityWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnActivateAbilityWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-activate-ability-wait-snapshot-room",
            "opposite-player-first-turn-activate-ability-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.ActivateAbility, waitPrompt.SnapshotTick.GetValueOrDefault()));
    }

    [Fact]
    public Task OppositePlayerFirstTurnTapRuneWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnTapRuneWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-tap-rune-wait-prompt-room",
            "opposite-player-first-turn-tap-rune-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.TapRune, waitPrompt));
    }

    [Fact]
    public Task PromptIdOnlyOppositePlayerFirstTurnTapRuneWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnTapRuneWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-tap-rune-wait-prompt-id-room",
            "opposite-player-first-turn-tap-rune-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.TapRune, waitPrompt));
    }

    [Fact]
    public Task SnapshotOnlyOppositePlayerFirstTurnTapRuneWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnTapRuneWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-tap-rune-wait-snapshot-room",
            "opposite-player-first-turn-tap-rune-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.TapRune, waitPrompt.SnapshotTick.GetValueOrDefault()));
    }

    [Fact]
    public Task OppositePlayerFirstTurnRecycleRuneWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnRecycleRuneWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-recycle-rune-wait-prompt-room",
            "opposite-player-first-turn-recycle-rune-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.RecycleRune, waitPrompt));
    }

    [Fact]
    public Task PromptIdOnlyOppositePlayerFirstTurnRecycleRuneWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnRecycleRuneWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-recycle-rune-wait-prompt-id-room",
            "opposite-player-first-turn-recycle-rune-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.RecycleRune, waitPrompt));
    }

    [Fact]
    public Task SnapshotOnlyOppositePlayerFirstTurnRecycleRuneWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnRecycleRuneWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-recycle-rune-wait-snapshot-room",
            "opposite-player-first-turn-recycle-rune-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.RecycleRune, waitPrompt.SnapshotTick.GetValueOrDefault()));
    }

    [Fact]
    public Task OppositePlayerFirstTurnHideCardWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnHideCardWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-hide-card-wait-prompt-room",
            "opposite-player-first-turn-hide-card-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.HideCard, waitPrompt));
    }

    [Fact]
    public Task PromptIdOnlyOppositePlayerFirstTurnHideCardWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnHideCardWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-hide-card-wait-prompt-id-room",
            "opposite-player-first-turn-hide-card-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.HideCard, waitPrompt));
    }

    [Fact]
    public Task SnapshotOnlyOppositePlayerFirstTurnHideCardWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnHideCardWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-hide-card-wait-snapshot-room",
            "opposite-player-first-turn-hide-card-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.HideCard, waitPrompt.SnapshotTick.GetValueOrDefault()));
    }

    [Fact]
    public Task OppositePlayerFirstTurnRevealCardWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnRevealCardWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-reveal-card-wait-prompt-room",
            "opposite-player-first-turn-reveal-card-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.RevealCard, waitPrompt));
    }

    [Fact]
    public Task PromptIdOnlyOppositePlayerFirstTurnRevealCardWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnRevealCardWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-reveal-card-wait-prompt-id-room",
            "opposite-player-first-turn-reveal-card-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.RevealCard, waitPrompt));
    }

    [Fact]
    public Task SnapshotOnlyOppositePlayerFirstTurnRevealCardWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnRevealCardWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-reveal-card-wait-snapshot-room",
            "opposite-player-first-turn-reveal-card-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.RevealCard, waitPrompt.SnapshotTick.GetValueOrDefault()));
    }

    [Fact]
    public Task OppositePlayerFirstTurnLegendActWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnLegendActWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-legend-act-wait-prompt-room",
            "opposite-player-first-turn-legend-act-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.LegendAct, waitPrompt));
    }

    [Fact]
    public Task PromptIdOnlyOppositePlayerFirstTurnLegendActWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnLegendActWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-legend-act-wait-prompt-id-room",
            "opposite-player-first-turn-legend-act-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.LegendAct, waitPrompt));
    }

    [Fact]
    public Task SnapshotOnlyOppositePlayerFirstTurnLegendActWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnLegendActWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-legend-act-wait-snapshot-room",
            "opposite-player-first-turn-legend-act-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.LegendAct, waitPrompt.SnapshotTick.GetValueOrDefault()));
    }

    [Fact]
    public Task OppositePlayerFirstTurnAssembleEquipmentWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnAssembleEquipmentWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-assemble-equipment-wait-prompt-room",
            "opposite-player-first-turn-assemble-equipment-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.AssembleEquipment, waitPrompt));
    }

    [Fact]
    public Task PromptIdOnlyOppositePlayerFirstTurnAssembleEquipmentWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnAssembleEquipmentWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-assemble-equipment-wait-prompt-id-room",
            "opposite-player-first-turn-assemble-equipment-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.AssembleEquipment, waitPrompt));
    }

    [Fact]
    public Task SnapshotOnlyOppositePlayerFirstTurnAssembleEquipmentWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnAssembleEquipmentWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-assemble-equipment-wait-snapshot-room",
            "opposite-player-first-turn-assemble-equipment-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.AssembleEquipment, waitPrompt.SnapshotTick.GetValueOrDefault()));
    }

    [Fact]
    public Task OppositePlayerFirstTurnPayCostWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnPayCostWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-pay-cost-wait-prompt-room",
            "opposite-player-first-turn-pay-cost-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.PayCost, waitPrompt));
    }

    [Fact]
    public Task PromptIdOnlyOppositePlayerFirstTurnPayCostWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnPayCostWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-pay-cost-wait-prompt-id-room",
            "opposite-player-first-turn-pay-cost-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.PayCost, waitPrompt));
    }

    [Fact]
    public Task SnapshotOnlyOppositePlayerFirstTurnPayCostWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnPayCostWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-pay-cost-wait-snapshot-room",
            "opposite-player-first-turn-pay-cost-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.PayCost, waitPrompt.SnapshotTick.GetValueOrDefault()));
    }

    [Fact]
    public Task OppositePlayerFirstTurnAssignCombatDamageWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnAssignCombatDamageWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-assign-combat-damage-wait-prompt-room",
            "opposite-player-first-turn-assign-combat-damage-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.AssignCombatDamage, waitPrompt));
    }

    [Fact]
    public Task PromptIdOnlyOppositePlayerFirstTurnAssignCombatDamageWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnAssignCombatDamageWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-assign-combat-damage-wait-prompt-id-room",
            "opposite-player-first-turn-assign-combat-damage-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.AssignCombatDamage, waitPrompt));
    }

    [Fact]
    public Task SnapshotOnlyOppositePlayerFirstTurnAssignCombatDamageWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnAssignCombatDamageWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-assign-combat-damage-wait-snapshot-room",
            "opposite-player-first-turn-assign-combat-damage-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.AssignCombatDamage, waitPrompt.SnapshotTick.GetValueOrDefault()));
    }

    [Fact]
    public Task OppositePlayerFirstTurnOrderTriggersWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnOrderTriggersWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-order-triggers-wait-prompt-room",
            "opposite-player-first-turn-order-triggers-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.OrderTriggers, waitPrompt));
    }

    [Fact]
    public Task PromptIdOnlyOppositePlayerFirstTurnOrderTriggersWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnOrderTriggersWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-order-triggers-wait-prompt-id-room",
            "opposite-player-first-turn-order-triggers-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.OrderTriggers, waitPrompt));
    }

    [Fact]
    public Task SnapshotOnlyOppositePlayerFirstTurnOrderTriggersWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnOrderTriggersWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-order-triggers-wait-snapshot-room",
            "opposite-player-first-turn-order-triggers-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.OrderTriggers, waitPrompt.SnapshotTick.GetValueOrDefault()));
    }

    [Fact]
    public Task OppositePlayerFirstTurnChooseHandCardsWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnChooseHandCardsWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-choose-hand-cards-wait-prompt-room",
            "opposite-player-first-turn-choose-hand-cards-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.ChooseHandCards, waitPrompt));
    }

    [Fact]
    public Task PromptIdOnlyOppositePlayerFirstTurnChooseHandCardsWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnChooseHandCardsWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-choose-hand-cards-wait-prompt-id-room",
            "opposite-player-first-turn-choose-hand-cards-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.ChooseHandCards, waitPrompt));
    }

    [Fact]
    public Task SnapshotOnlyOppositePlayerFirstTurnChooseHandCardsWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnChooseHandCardsWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-choose-hand-cards-wait-snapshot-room",
            "opposite-player-first-turn-choose-hand-cards-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.ChooseHandCards, waitPrompt.SnapshotTick.GetValueOrDefault()));
    }

    [Fact]
    public Task OppositePlayerFirstTurnReadyWaitPromptAcceptsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnReadyWaitPromptAcceptsWithoutMutation(
            "official-opposite-player-first-turn-ready-wait-prompt-room",
            "opposite-player-first-turn-ready-wait-prompt",
            waitPrompt => PromptScopedReadyRawCommand(waitPrompt));
    }

    [Fact]
    public Task PromptIdOnlyOppositePlayerFirstTurnReadyWaitPromptAcceptsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnReadyWaitPromptAcceptsWithoutMutation(
            "official-opposite-player-first-turn-ready-wait-prompt-id-room",
            "opposite-player-first-turn-ready-wait-prompt-id",
            waitPrompt => PromptIdOnlyReadyRawCommand(waitPrompt));
    }

    [Fact]
    public Task SnapshotOnlyOppositePlayerFirstTurnReadyWaitPromptAcceptsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnReadyWaitPromptAcceptsWithoutMutation(
            "official-opposite-player-first-turn-ready-wait-snapshot-room",
            "opposite-player-first-turn-ready-wait-snapshot",
            waitPrompt => SnapshotOnlyReadyRawCommand(waitPrompt.SnapshotTick.GetValueOrDefault()));
    }

    [Fact]
    public Task OppositePlayerFirstTurnSubmitDeckWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnSubmitDeckWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-submit-deck-wait-prompt-room",
            "opposite-player-first-turn-submit-deck-wait-prompt",
            (context, waitPrompt) => PromptScopedSubmitDeckRawCommand(context.SecondDeck, waitPrompt));
    }

    [Fact]
    public Task PromptIdOnlyOppositePlayerFirstTurnSubmitDeckWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnSubmitDeckWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-submit-deck-wait-prompt-id-room",
            "opposite-player-first-turn-submit-deck-wait-prompt-id",
            (context, waitPrompt) => PromptIdOnlySubmitDeckRawCommand(context.SecondDeck, waitPrompt));
    }

    [Fact]
    public Task SnapshotOnlyOppositePlayerFirstTurnSubmitDeckWaitPromptRejectsWithoutMutation()
    {
        return AssertOppositePlayerFirstTurnSubmitDeckWaitPromptRejectsWithoutMutation(
            "official-opposite-player-first-turn-submit-deck-wait-snapshot-room",
            "opposite-player-first-turn-submit-deck-wait-snapshot",
            (context, waitPrompt) => SnapshotOnlySubmitDeckRawCommand(context.SecondDeck, waitPrompt.SnapshotTick.GetValueOrDefault()));
    }

    [Fact]
    public Task ActivePlayerFirstTurnPassWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnPassWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-pass-with-opposite-wait-prompt-room",
            "active-player-first-turn-pass-with-opposite-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.Pass, waitPrompt));
    }

    [Fact]
    public Task PromptIdOnlyActivePlayerFirstTurnPassWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnPassWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-pass-with-opposite-wait-prompt-id-room",
            "active-player-first-turn-pass-with-opposite-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.Pass, waitPrompt));
    }

    [Fact]
    public Task SnapshotOnlyActivePlayerFirstTurnPassWithOppositeWaitPromptReplayAfterAcceptedPassRejectsWithoutMutation()
    {
        return AssertSnapshotOnlyActivePlayerFirstTurnPassWithOppositeWaitPromptReplayAfterAcceptedPassRejectsWithoutMutation(
            "official-active-player-first-turn-pass-with-opposite-wait-snapshot-room",
            "active-player-first-turn-pass-with-opposite-wait-snapshot");
    }

    [Fact]
    public Task ActivePlayerFirstTurnPassPriorityWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnPassPriorityWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-pass-priority-with-opposite-wait-prompt-room",
            "active-player-first-turn-pass-priority-with-opposite-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.PassPriority, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyActivePlayerFirstTurnPassPriorityWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnPassPriorityWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-pass-priority-with-opposite-wait-prompt-id-room",
            "active-player-first-turn-pass-priority-with-opposite-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.PassPriority, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyActivePlayerFirstTurnPassPriorityWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnPassPriorityWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-pass-priority-with-opposite-wait-snapshot-room",
            "active-player-first-turn-pass-priority-with-opposite-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.PassPriority, waitPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.PhaseNotAllowed,
            "让过优先权只能在优先行动窗口中提交。");
    }

    [Fact]
    public Task ActivePlayerFirstTurnPassFocusWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnPassFocusWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-pass-focus-with-opposite-wait-prompt-room",
            "active-player-first-turn-pass-focus-with-opposite-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.PassFocus, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyActivePlayerFirstTurnPassFocusWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnPassFocusWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-pass-focus-with-opposite-wait-prompt-id-room",
            "active-player-first-turn-pass-focus-with-opposite-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.PassFocus, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyActivePlayerFirstTurnPassFocusWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnPassFocusWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-pass-focus-with-opposite-wait-snapshot-room",
            "active-player-first-turn-pass-focus-with-opposite-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.PassFocus, waitPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.PhaseNotAllowed,
            "让过焦点只能在法术对决焦点窗口中提交。");
    }

    [Fact]
    public Task ActivePlayerFirstTurnMoveUnitWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnMoveUnitWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-move-unit-with-opposite-wait-prompt-room",
            "active-player-first-turn-move-unit-with-opposite-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.MoveUnit, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyActivePlayerFirstTurnMoveUnitWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnMoveUnitWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-move-unit-with-opposite-wait-prompt-id-room",
            "active-player-first-turn-move-unit-with-opposite-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.MoveUnit, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyActivePlayerFirstTurnMoveUnitWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnMoveUnitWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-move-unit-with-opposite-wait-snapshot-room",
            "active-player-first-turn-move-unit-with-opposite-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.MoveUnit, waitPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.InvalidTarget,
            "移动单位来源不在提交的起点，或不由该玩家控制。");
    }

    [Fact]
    public Task ActivePlayerFirstTurnDeclareBattleWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnDeclareBattleWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-declare-battle-with-opposite-wait-prompt-room",
            "active-player-first-turn-declare-battle-with-opposite-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.DeclareBattle, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyActivePlayerFirstTurnDeclareBattleWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnDeclareBattleWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-declare-battle-with-opposite-wait-prompt-id-room",
            "active-player-first-turn-declare-battle-with-opposite-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.DeclareBattle, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyActivePlayerFirstTurnDeclareBattleWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnDeclareBattleWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-declare-battle-with-opposite-wait-snapshot-room",
            "active-player-first-turn-declare-battle-with-opposite-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.DeclareBattle, waitPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.UnsupportedCommand,
            "当前声明战斗路径尚未由服务端开放。");
    }

    [Fact]
    public Task ActivePlayerFirstTurnPlayCardWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnPlayCardWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-play-card-with-opposite-wait-prompt-room",
            "active-player-first-turn-play-card-with-opposite-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.PlayCard, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyActivePlayerFirstTurnPlayCardWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnPlayCardWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-play-card-with-opposite-wait-prompt-id-room",
            "active-player-first-turn-play-card-with-opposite-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.PlayCard, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyActivePlayerFirstTurnPlayCardWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnPlayCardWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-play-card-with-opposite-wait-snapshot-room",
            "active-player-first-turn-play-card-with-opposite-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.PlayCard, waitPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.UnsupportedCardBehavior,
            "Unsupported card behavior or mode: missing-card ");
    }

    [Fact]
    public Task ActivePlayerFirstTurnActivateAbilityWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnActivateAbilityWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-activate-ability-with-opposite-wait-prompt-room",
            "active-player-first-turn-activate-ability-with-opposite-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.ActivateAbility, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyActivePlayerFirstTurnActivateAbilityWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnActivateAbilityWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-activate-ability-with-opposite-wait-prompt-id-room",
            "active-player-first-turn-activate-ability-with-opposite-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.ActivateAbility, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyActivePlayerFirstTurnActivateAbilityWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnActivateAbilityWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-activate-ability-with-opposite-wait-snapshot-room",
            "active-player-first-turn-activate-ability-with-opposite-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.ActivateAbility, waitPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.UnsupportedCommand,
            "当前启动技能路径尚未由服务端开放。");
    }

    [Fact]
    public Task ActivePlayerFirstTurnTapRuneWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnTapRuneWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-tap-rune-with-opposite-wait-prompt-room",
            "active-player-first-turn-tap-rune-with-opposite-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.TapRune, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyActivePlayerFirstTurnTapRuneWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnTapRuneWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-tap-rune-with-opposite-wait-prompt-id-room",
            "active-player-first-turn-tap-rune-with-opposite-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.TapRune, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyActivePlayerFirstTurnTapRuneWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnTapRuneWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-tap-rune-with-opposite-wait-snapshot-room",
            "active-player-first-turn-tap-rune-with-opposite-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.TapRune, waitPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.InvalidTarget,
            "横置符文需要选择己方基地中的正面符文。");
    }

    [Fact]
    public Task ActivePlayerFirstTurnRecycleRuneWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnRecycleRuneWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-recycle-rune-with-opposite-wait-prompt-room",
            "active-player-first-turn-recycle-rune-with-opposite-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.RecycleRune, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyActivePlayerFirstTurnRecycleRuneWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnRecycleRuneWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-recycle-rune-with-opposite-wait-prompt-id-room",
            "active-player-first-turn-recycle-rune-with-opposite-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.RecycleRune, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyActivePlayerFirstTurnRecycleRuneWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnRecycleRuneWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-recycle-rune-with-opposite-wait-snapshot-room",
            "active-player-first-turn-recycle-rune-with-opposite-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.RecycleRune, waitPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.InvalidTarget,
            "回收符文需要选择己方基地中的正面特性符文。");
    }

    [Fact]
    public Task ActivePlayerFirstTurnHideCardWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnHideCardWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-hide-card-with-opposite-wait-prompt-room",
            "active-player-first-turn-hide-card-with-opposite-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.HideCard, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyActivePlayerFirstTurnHideCardWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnHideCardWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-hide-card-with-opposite-wait-prompt-id-room",
            "active-player-first-turn-hide-card-with-opposite-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.HideCard, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyActivePlayerFirstTurnHideCardWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnHideCardWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-hide-card-with-opposite-wait-snapshot-room",
            "active-player-first-turn-hide-card-with-opposite-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.HideCard, waitPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.UnsupportedCardBehavior,
            "暂不支持该牌的待命埋伏行为：missing-card");
    }

    [Fact]
    public Task ActivePlayerFirstTurnRevealCardWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnRevealCardWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-reveal-card-with-opposite-wait-prompt-room",
            "active-player-first-turn-reveal-card-with-opposite-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.RevealCard, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyActivePlayerFirstTurnRevealCardWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnRevealCardWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-reveal-card-with-opposite-wait-prompt-id-room",
            "active-player-first-turn-reveal-card-with-opposite-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.RevealCard, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyActivePlayerFirstTurnRevealCardWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnRevealCardWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-reveal-card-with-opposite-wait-snapshot-room",
            "active-player-first-turn-reveal-card-with-opposite-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.RevealCard, waitPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.UnsupportedCardBehavior,
            "暂不支持该牌的待命翻开行为：missing-card");
    }

    [Fact]
    public Task ActivePlayerFirstTurnLegendActWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnLegendActWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-legend-act-with-opposite-wait-prompt-room",
            "active-player-first-turn-legend-act-with-opposite-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.LegendAct, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyActivePlayerFirstTurnLegendActWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnLegendActWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-legend-act-with-opposite-wait-prompt-id-room",
            "active-player-first-turn-legend-act-with-opposite-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.LegendAct, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyActivePlayerFirstTurnLegendActWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnLegendActWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-legend-act-with-opposite-wait-snapshot-room",
            "active-player-first-turn-legend-act-with-opposite-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.LegendAct, waitPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.UnsupportedCardBehavior,
            "当前传奇行动尚未由服务端开放。");
    }

    [Fact]
    public Task ActivePlayerFirstTurnAssembleEquipmentWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnAssembleEquipmentWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-assemble-equipment-with-opposite-wait-prompt-room",
            "active-player-first-turn-assemble-equipment-with-opposite-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.AssembleEquipment, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyActivePlayerFirstTurnAssembleEquipmentWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnAssembleEquipmentWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-assemble-equipment-with-opposite-wait-prompt-id-room",
            "active-player-first-turn-assemble-equipment-with-opposite-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.AssembleEquipment, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyActivePlayerFirstTurnAssembleEquipmentWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnAssembleEquipmentWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-assemble-equipment-with-opposite-wait-snapshot-room",
            "active-player-first-turn-assemble-equipment-with-opposite-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.AssembleEquipment, waitPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.UnsupportedCommand,
            "当前装备装配路径尚未由服务端开放。");
    }

    [Fact]
    public Task ActivePlayerFirstTurnPayCostWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnPayCostWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-pay-cost-with-opposite-wait-prompt-room",
            "active-player-first-turn-pay-cost-with-opposite-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.PayCost, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyActivePlayerFirstTurnPayCostWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnPayCostWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-pay-cost-with-opposite-wait-prompt-id-room",
            "active-player-first-turn-pay-cost-with-opposite-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.PayCost, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyActivePlayerFirstTurnPayCostWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnPayCostWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-pay-cost-with-opposite-wait-snapshot-room",
            "active-player-first-turn-pay-cost-with-opposite-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.PayCost, waitPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.PhaseNotAllowed,
            "当前没有服务端支付窗口可处理 PAY_COST。");
    }

    [Fact]
    public Task ActivePlayerFirstTurnAssignCombatDamageWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnAssignCombatDamageWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-assign-combat-damage-with-opposite-wait-prompt-room",
            "active-player-first-turn-assign-combat-damage-with-opposite-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.AssignCombatDamage, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyActivePlayerFirstTurnAssignCombatDamageWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnAssignCombatDamageWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-assign-combat-damage-with-opposite-wait-prompt-id-room",
            "active-player-first-turn-assign-combat-damage-with-opposite-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.AssignCombatDamage, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyActivePlayerFirstTurnAssignCombatDamageWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnAssignCombatDamageWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-assign-combat-damage-with-opposite-wait-snapshot-room",
            "active-player-first-turn-assign-combat-damage-with-opposite-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.AssignCombatDamage, waitPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.InvalidPayload,
            "ASSIGN_COMBAT_DAMAGE 需要 battleId、battlefieldId 与非空 assignments。");
    }

    [Fact]
    public Task ActivePlayerFirstTurnOrderTriggersWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnOrderTriggersWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-order-triggers-with-opposite-wait-prompt-room",
            "active-player-first-turn-order-triggers-with-opposite-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.OrderTriggers, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyActivePlayerFirstTurnOrderTriggersWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnOrderTriggersWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-order-triggers-with-opposite-wait-prompt-id-room",
            "active-player-first-turn-order-triggers-with-opposite-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.OrderTriggers, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyActivePlayerFirstTurnOrderTriggersWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnOrderTriggersWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-order-triggers-with-opposite-wait-snapshot-room",
            "active-player-first-turn-order-triggers-with-opposite-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.OrderTriggers, waitPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.InvalidPayload,
            "ORDER_TRIGGERS 需要非空且不重复的 orderedTriggerIds。");
    }

    [Fact]
    public Task ActivePlayerFirstTurnChooseHandCardsWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnChooseHandCardsWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-choose-hand-cards-with-opposite-wait-prompt-room",
            "active-player-first-turn-choose-hand-cards-with-opposite-wait-prompt",
            waitPrompt => PromptScopedBasicRawCommand(CommandTypes.ChooseHandCards, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyActivePlayerFirstTurnChooseHandCardsWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnChooseHandCardsWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-choose-hand-cards-with-opposite-wait-prompt-id-room",
            "active-player-first-turn-choose-hand-cards-with-opposite-wait-prompt-id",
            waitPrompt => PromptIdOnlyBasicRawCommand(CommandTypes.ChooseHandCards, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyActivePlayerFirstTurnChooseHandCardsWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnChooseHandCardsWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-choose-hand-cards-with-opposite-wait-snapshot-room",
            "active-player-first-turn-choose-hand-cards-with-opposite-wait-snapshot",
            waitPrompt => SnapshotOnlyBasicRawCommand(CommandTypes.ChooseHandCards, waitPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.PhaseNotAllowed,
            "当前没有服务端手牌选择窗口可处理 CHOOSE_HAND_CARDS。");
    }

    [Fact]
    public Task ActivePlayerFirstTurnReadyWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnReadyWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-ready-with-opposite-wait-prompt-room",
            "active-player-first-turn-ready-with-opposite-wait-prompt",
            waitPrompt => PromptScopedReadyRawCommand(waitPrompt));
    }

    [Fact]
    public Task PromptIdOnlyActivePlayerFirstTurnReadyWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnReadyWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-ready-with-opposite-wait-prompt-id-room",
            "active-player-first-turn-ready-with-opposite-wait-prompt-id",
            waitPrompt => PromptIdOnlyReadyRawCommand(waitPrompt));
    }

    [Fact]
    public Task SnapshotOnlyActivePlayerFirstTurnReadyWithOppositeWaitPromptAcceptsWithoutMutation()
    {
        return AssertSnapshotOnlyActivePlayerFirstTurnReadyWithOppositeWaitPromptAcceptsWithoutMutation(
            "official-active-player-first-turn-ready-with-opposite-wait-snapshot-room",
            "active-player-first-turn-ready-with-opposite-wait-snapshot");
    }

    [Fact]
    public Task ActivePlayerFirstTurnSubmitDeckWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnSubmitDeckWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-submit-deck-with-opposite-wait-prompt-room",
            "active-player-first-turn-submit-deck-with-opposite-wait-prompt",
            (context, waitPrompt) => PromptScopedSubmitDeckRawCommand(context.ActiveDeck, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task PromptIdOnlyActivePlayerFirstTurnSubmitDeckWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnSubmitDeckWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-submit-deck-with-opposite-wait-prompt-id-room",
            "active-player-first-turn-submit-deck-with-opposite-wait-prompt-id",
            (context, waitPrompt) => PromptIdOnlySubmitDeckRawCommand(context.ActiveDeck, waitPrompt),
            ErrorCodes.PromptExpired,
            "行动窗口已过期，请按最新提示重新提交。");
    }

    [Fact]
    public Task SnapshotOnlyActivePlayerFirstTurnSubmitDeckWithOppositeWaitPromptRejectsWithoutMutation()
    {
        return AssertActivePlayerFirstTurnSubmitDeckWithOppositeWaitPromptRejectsWithoutMutation(
            "official-active-player-first-turn-submit-deck-with-opposite-wait-snapshot-room",
            "active-player-first-turn-submit-deck-with-opposite-wait-snapshot",
            (context, waitPrompt) => SnapshotOnlySubmitDeckRawCommand(context.ActiveDeck, waitPrompt.SnapshotTick.GetValueOrDefault()),
            ErrorCodes.PhaseNotAllowed,
            "对局开始后不能更改卡组。");
    }

    private static async Task AssertFirstTurnEndTurnPromptReplayAfterNextPlayerStartsRejectsWithoutMutation(
        string sessionName,
        string intentPrefix,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedReplayMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, firstTurnPrompt.Actions);
        var rawCommand = rawCommandFactory(firstTurnPrompt);
        var nextTurnCalledRuneObjectIds = context.Accepted.State.PlayerZones[context.SecondPlayerId].RuneDeck
            .Take(3)
            .ToArray();
        var nextTurnDrawnObjectIds = context.Accepted.State.PlayerZones[context.SecondPlayerId].MainDeck
            .Take(1)
            .ToArray();

        var accepted = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            $"{intentPrefix}-before-stale-prompt-replay",
            new EndTurnCommand(),
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        AssertOfficialFirstTurnEndTurnNextPlayerPromptQueueAudit(
            context,
            accepted,
            nextTurnCalledRuneObjectIds,
            nextTurnDrawnObjectIds);
        var acceptedHash = MatchStateHasher.Hash(accepted.State);

        var replay = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            $"{intentPrefix}-stale-prompt-replay",
            new EndTurnCommand(),
            rawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Equal(expectedReplayMessage, replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(accepted.State.RngCursor, replay.State.RngCursor);
        Assert.Equal(accepted.State.ReadyPlayerIds, replay.State.ReadyPlayerIds);
        Assert.Equal(accepted.State.MulliganCompletedPlayerIds, replay.State.MulliganCompletedPlayerIds);
        Assert.Equal(accepted.State.OpeningSecondActionPlayerId, replay.State.OpeningSecondActionPlayerId);
        AssertOfficialFirstTurnEndTurnNextPlayerPromptQueueAudit(
            context,
            replay,
            nextTurnCalledRuneObjectIds,
            nextTurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(accepted.Prompts[context.ActivePlayerId].SnapshotTick, replay.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(accepted.Prompts[context.SecondPlayerId].SnapshotTick, replay.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertWrongPlayerFirstTurnEndTurnPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, firstTurnPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new EndTurnCommand(),
            rawCommandFactory(firstTurnPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertWrongPlayerFirstTurnMulliganPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, firstTurnPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Mulligan, firstTurnPrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Mulligan, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new MulliganCommand([]),
            rawCommandFactory(firstTurnPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertWrongPlayerFirstTurnPassPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, firstTurnPrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Pass, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new PassCommand(),
            rawCommandFactory(firstTurnPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertWrongPlayerFirstTurnPassPriorityPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, firstTurnPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.PassPriority, firstTurnPrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.PassPriority, waitPrompt.Actions);
        Assert.Empty(context.Accepted.State.PassedPriorityPlayerIds);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new PassPriorityCommand(),
            rawCommandFactory(firstTurnPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertWrongPlayerFirstTurnPassFocusPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, firstTurnPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, firstTurnPrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, waitPrompt.Actions);
        Assert.Null(context.Accepted.State.FocusPlayerId);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new PassFocusCommand(),
            rawCommandFactory(firstTurnPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertWrongPlayerFirstTurnMoveUnitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, firstTurnPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.MoveUnit, firstTurnPrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.MoveUnit, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new MoveUnitCommand("missing-unit", "BASE", "BATTLEFIELD", []),
            rawCommandFactory(firstTurnPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertWrongPlayerFirstTurnDeclareBattlePromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, firstTurnPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.DeclareBattle, firstTurnPrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.DeclareBattle, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new DeclareBattleCommand("missing-battlefield", [], [], []),
            rawCommandFactory(firstTurnPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertWrongPlayerFirstTurnPlayCardPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, firstTurnPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.PlayCard, firstTurnPrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.PlayCard, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new PlayCardCommand("missing-source", "missing-card", []),
            rawCommandFactory(firstTurnPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertWrongPlayerFirstTurnActivateAbilityPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, firstTurnPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.ActivateAbility, firstTurnPrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.ActivateAbility, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new ActivateAbilityCommand("missing-source", "missing-ability", []),
            rawCommandFactory(firstTurnPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertWrongPlayerFirstTurnTapRunePromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, firstTurnPrompt.Actions);
        Assert.Contains(CommandTypes.TapRune, firstTurnPrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.TapRune, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new TapRuneCommand("missing-rune"),
            rawCommandFactory(firstTurnPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertWrongPlayerFirstTurnRecycleRunePromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, firstTurnPrompt.Actions);
        Assert.Contains(CommandTypes.RecycleRune, firstTurnPrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.RecycleRune, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new RecycleRuneCommand("missing-rune"),
            rawCommandFactory(firstTurnPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertWrongPlayerFirstTurnHideCardPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, firstTurnPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.HideCard, firstTurnPrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.HideCard, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new HideCardCommand("missing-source", "missing-card"),
            rawCommandFactory(firstTurnPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertWrongPlayerFirstTurnRevealCardPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, firstTurnPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.RevealCard, firstTurnPrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.RevealCard, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new RevealCardCommand("missing-source", "missing-card", []),
            rawCommandFactory(firstTurnPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertWrongPlayerFirstTurnLegendActPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, firstTurnPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.LegendAct, firstTurnPrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.LegendAct, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new LegendActCommand("missing-source", "missing-ability", []),
            rawCommandFactory(firstTurnPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertWrongPlayerFirstTurnAssembleEquipmentPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, firstTurnPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.AssembleEquipment, firstTurnPrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.AssembleEquipment, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new AssembleEquipmentCommand("missing-source", "missing-target"),
            rawCommandFactory(firstTurnPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertWrongPlayerFirstTurnPayCostPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, firstTurnPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.PayCost, firstTurnPrompt.Actions);
        Assert.Null(context.Accepted.State.PendingPayment);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.PayCost, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new PayCostCommand("missing-payment", "missing-window", []),
            rawCommandFactory(firstTurnPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        Assert.Null(rejected.State.PendingPayment);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertWrongPlayerFirstTurnAssignCombatDamagePromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, firstTurnPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.AssignCombatDamage, firstTurnPrompt.Actions);
        Assert.False(context.Accepted.State.BattleState.IsActive);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.AssignCombatDamage, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new AssignCombatDamageCommand("missing-battle", "missing-battlefield", []),
            rawCommandFactory(firstTurnPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        Assert.False(rejected.State.BattleState.IsActive);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertWrongPlayerFirstTurnOrderTriggersPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, firstTurnPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.OrderTriggers, firstTurnPrompt.Actions);
        Assert.Empty(context.Accepted.State.TriggerQueue);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.OrderTriggers, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new OrderTriggersCommand(OrderedTriggerIds: []),
            rawCommandFactory(firstTurnPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        Assert.Empty(rejected.State.TriggerQueue);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertWrongPlayerFirstTurnChooseHandCardsPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, firstTurnPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.ChooseHandCards, firstTurnPrompt.Actions);
        Assert.Null(context.Accepted.State.PendingHandChoice);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.ChooseHandCards, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new ChooseHandCardsCommand("missing-choice", "missing-window", []),
            rawCommandFactory(firstTurnPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        Assert.Null(rejected.State.PendingHandChoice);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertFirstTurnSurrenderPromptReplayAfterMatchFinishedRejectsWithoutMutation(
        string sessionName,
        string intentPrefix,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedReplayMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.Surrender, firstTurnPrompt.Actions);
        var rawCommand = rawCommandFactory(firstTurnPrompt);

        var accepted = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            $"{intentPrefix}-before-stale-prompt-replay",
            new SurrenderCommand(),
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        AssertOfficialFirstTurnSurrenderMatchFinishedPromptQueueAudit(context, accepted);
        var acceptedHash = MatchStateHasher.Hash(accepted.State);

        var replay = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            $"{intentPrefix}-stale-prompt-replay",
            new SurrenderCommand(),
            rawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Equal(expectedReplayMessage, replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(accepted.State.RngCursor, replay.State.RngCursor);
        Assert.Equal(accepted.State.ReadyPlayerIds, replay.State.ReadyPlayerIds);
        Assert.Equal(accepted.State.MulliganCompletedPlayerIds, replay.State.MulliganCompletedPlayerIds);
        Assert.Equal(accepted.State.OpeningSecondActionPlayerId, replay.State.OpeningSecondActionPlayerId);
        AssertOfficialFirstTurnSurrenderMatchFinishedPromptQueueAudit(context, replay, assertEvents: false);
        Assert.Equal(accepted.Prompts[context.ActivePlayerId].SnapshotTick, replay.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(accepted.Prompts[context.SecondPlayerId].SnapshotTick, replay.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertOppositePlayerFirstTurnSurrenderWaitPromptReplayAfterMatchFinishedRejectsWithoutMutation(
        string sessionName,
        string intentPrefix,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedReplayMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        var rawCommand = rawCommandFactory(waitPrompt);

        var accepted = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            $"{intentPrefix}-before-stale-prompt-replay",
            new SurrenderCommand(),
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        AssertOfficialFirstTurnSurrenderMatchFinishedPromptQueueAudit(
            context,
            accepted,
            surrenderedPlayerId: context.SecondPlayerId,
            winnerPlayerId: context.ActivePlayerId);
        var acceptedHash = MatchStateHasher.Hash(accepted.State);

        var replay = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            $"{intentPrefix}-stale-prompt-replay",
            new SurrenderCommand(),
            rawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Equal(expectedReplayMessage, replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(accepted.State.RngCursor, replay.State.RngCursor);
        Assert.Equal(accepted.State.ReadyPlayerIds, replay.State.ReadyPlayerIds);
        Assert.Equal(accepted.State.MulliganCompletedPlayerIds, replay.State.MulliganCompletedPlayerIds);
        Assert.Equal(accepted.State.OpeningSecondActionPlayerId, replay.State.OpeningSecondActionPlayerId);
        AssertOfficialFirstTurnSurrenderMatchFinishedPromptQueueAudit(
            context,
            replay,
            surrenderedPlayerId: context.SecondPlayerId,
            winnerPlayerId: context.ActivePlayerId,
            assertEvents: false);
        Assert.Equal(accepted.Prompts[context.ActivePlayerId].SnapshotTick, replay.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(accepted.Prompts[context.SecondPlayerId].SnapshotTick, replay.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertOppositePlayerFirstTurnEndTurnWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new EndTurnCommand(),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, rejected.ErrorCode);
        Assert.Equal("结束回合只能由当前玩家在开放主阶段提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertActivePlayerFirstTurnEndTurnWithOppositeWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            new EndTurnCommand(),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertSnapshotOnlyActivePlayerFirstTurnEndTurnWithOppositeWaitPromptReplayAfterNextPlayerStartsRejectsWithoutMutation(
        string sessionName,
        string intentPrefix)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);

        var rawCommand = SnapshotOnlyBasicRawCommand(CommandTypes.EndTurn, waitPrompt.SnapshotTick.GetValueOrDefault());
        var nextTurnCalledRuneObjectIds = context.Accepted.State.PlayerZones[context.SecondPlayerId].RuneDeck
            .Take(3)
            .ToArray();
        var nextTurnDrawnObjectIds = context.Accepted.State.PlayerZones[context.SecondPlayerId].MainDeck
            .Take(1)
            .ToArray();

        var accepted = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            $"{intentPrefix}-before-stale-prompt-replay",
            new EndTurnCommand(),
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        AssertOfficialFirstTurnEndTurnNextPlayerPromptQueueAudit(
            context,
            accepted,
            nextTurnCalledRuneObjectIds,
            nextTurnDrawnObjectIds);
        var acceptedHash = MatchStateHasher.Hash(accepted.State);

        var replay = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            $"{intentPrefix}-stale-prompt-replay",
            new EndTurnCommand(),
            rawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Equal("行动快照已过期，请按最新状态重新提交。", replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(accepted.State.RngCursor, replay.State.RngCursor);
        Assert.Equal(accepted.State.ReadyPlayerIds, replay.State.ReadyPlayerIds);
        Assert.Equal(accepted.State.MulliganCompletedPlayerIds, replay.State.MulliganCompletedPlayerIds);
        Assert.Equal(accepted.State.OpeningSecondActionPlayerId, replay.State.OpeningSecondActionPlayerId);
        AssertOfficialFirstTurnEndTurnNextPlayerPromptQueueAudit(
            context,
            replay,
            nextTurnCalledRuneObjectIds,
            nextTurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(accepted.Prompts[context.ActivePlayerId].SnapshotTick, replay.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(accepted.Prompts[context.SecondPlayerId].SnapshotTick, replay.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertActivePlayerFirstTurnSurrenderWithOppositeWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.Surrender, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            new SurrenderCommand(),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertSnapshotOnlyActivePlayerFirstTurnSurrenderWithOppositeWaitPromptReplayAfterMatchFinishedRejectsWithoutMutation(
        string sessionName,
        string intentPrefix)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.Surrender, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);

        var rawCommand = SnapshotOnlyBasicRawCommand(CommandTypes.Surrender, waitPrompt.SnapshotTick.GetValueOrDefault());

        var accepted = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            $"{intentPrefix}-before-stale-prompt-replay",
            new SurrenderCommand(),
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        AssertOfficialFirstTurnSurrenderMatchFinishedPromptQueueAudit(context, accepted);
        var acceptedHash = MatchStateHasher.Hash(accepted.State);

        var replay = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            $"{intentPrefix}-stale-prompt-replay",
            new SurrenderCommand(),
            rawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Equal("行动快照已过期，请按最新状态重新提交。", replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(accepted.State.RngCursor, replay.State.RngCursor);
        Assert.Equal(accepted.State.ReadyPlayerIds, replay.State.ReadyPlayerIds);
        Assert.Equal(accepted.State.MulliganCompletedPlayerIds, replay.State.MulliganCompletedPlayerIds);
        Assert.Equal(accepted.State.OpeningSecondActionPlayerId, replay.State.OpeningSecondActionPlayerId);
        AssertOfficialFirstTurnSurrenderMatchFinishedPromptQueueAudit(context, replay, assertEvents: false);
        Assert.Equal(accepted.Prompts[context.ActivePlayerId].SnapshotTick, replay.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(accepted.Prompts[context.SecondPlayerId].SnapshotTick, replay.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertActivePlayerFirstTurnMulliganWithOppositeWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Mulligan, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            new MulliganCommand([]),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertOppositePlayerFirstTurnMulliganWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Mulligan, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new MulliganCommand([]),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, rejected.ErrorCode);
        Assert.Equal("起手调整只能在开局调度阶段提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertOppositePlayerFirstTurnPassWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Pass, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new PassCommand(),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, rejected.ErrorCode);
        Assert.Equal("让过只能由当前玩家在可让过窗口中提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertOppositePlayerFirstTurnPassPriorityWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.PassPriority, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.PassPriority, waitPrompt.Actions);
        Assert.Empty(context.Accepted.State.PassedPriorityPlayerIds);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new PassPriorityCommand(),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, rejected.ErrorCode);
        Assert.Equal("让过优先权只能在优先行动窗口中提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertOppositePlayerFirstTurnPassFocusWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, waitPrompt.Actions);
        Assert.Null(context.Accepted.State.FocusPlayerId);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new PassFocusCommand(),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, rejected.ErrorCode);
        Assert.Equal("让过焦点只能在法术对决焦点窗口中提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertOppositePlayerFirstTurnMoveUnitWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.MoveUnit, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.MoveUnit, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new MoveUnitCommand("missing-unit", "BASE", "BATTLEFIELD", []),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, rejected.ErrorCode);
        Assert.Equal("移动单位只能在当前玩家的开放主阶段提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertOppositePlayerFirstTurnDeclareBattleWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.DeclareBattle, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.DeclareBattle, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new DeclareBattleCommand("missing-battlefield", [], [], []),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, rejected.ErrorCode);
        Assert.Equal("当前声明战斗路径尚未由服务端开放。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertOppositePlayerFirstTurnPlayCardWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.PlayCard, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.PlayCard, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new PlayCardCommand("missing-source", "missing-card", []),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, rejected.ErrorCode);
        Assert.Equal("Unsupported card behavior or mode: missing-card ", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertOppositePlayerFirstTurnActivateAbilityWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.ActivateAbility, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.ActivateAbility, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new ActivateAbilityCommand("missing-source", "missing-ability", []),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, rejected.ErrorCode);
        Assert.Equal("当前启动技能路径尚未由服务端开放。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertOppositePlayerFirstTurnTapRuneWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.Contains(CommandTypes.TapRune, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.TapRune, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new TapRuneCommand("missing-rune"),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, rejected.ErrorCode);
        Assert.Equal("横置符文只能在当前玩家的开放主阶段提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertOppositePlayerFirstTurnRecycleRuneWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.Contains(CommandTypes.RecycleRune, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.RecycleRune, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new RecycleRuneCommand("missing-rune"),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, rejected.ErrorCode);
        Assert.Equal("回收符文只能在当前玩家的开放主阶段提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertOppositePlayerFirstTurnHideCardWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.HideCard, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.HideCard, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new HideCardCommand("missing-source", "missing-card"),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, rejected.ErrorCode);
        Assert.Equal("待命埋伏只能在当前玩家的开放主阶段提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertOppositePlayerFirstTurnRevealCardWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.RevealCard, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.RevealCard, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new RevealCardCommand("missing-source", "missing-card", []),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, rejected.ErrorCode);
        Assert.Equal("暂不支持该牌的待命翻开行为：missing-card", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertOppositePlayerFirstTurnLegendActWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.LegendAct, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.LegendAct, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new LegendActCommand("missing-source", "missing-ability", []),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, rejected.ErrorCode);
        Assert.Equal("当前传奇行动尚未由服务端开放。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertOppositePlayerFirstTurnAssembleEquipmentWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.AssembleEquipment, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.AssembleEquipment, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new AssembleEquipmentCommand("missing-source", "missing-target"),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, rejected.ErrorCode);
        Assert.Equal("当前装备装配路径尚未由服务端开放。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertOppositePlayerFirstTurnPayCostWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.PayCost, activePrompt.Actions);
        Assert.Null(context.Accepted.State.PendingPayment);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.PayCost, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new PayCostCommand("missing-payment", "missing-window", []),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, rejected.ErrorCode);
        Assert.Equal("当前没有服务端支付窗口可处理 PAY_COST。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        Assert.Null(rejected.State.PendingPayment);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertOppositePlayerFirstTurnAssignCombatDamageWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.AssignCombatDamage, activePrompt.Actions);
        Assert.False(context.Accepted.State.BattleState.IsActive);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.AssignCombatDamage, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new AssignCombatDamageCommand("missing-battle", "missing-battlefield", []),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.InvalidPayload, rejected.ErrorCode);
        Assert.Equal("ASSIGN_COMBAT_DAMAGE 需要 battleId、battlefieldId 与非空 assignments。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        Assert.False(rejected.State.BattleState.IsActive);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertOppositePlayerFirstTurnOrderTriggersWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.OrderTriggers, activePrompt.Actions);
        Assert.Empty(context.Accepted.State.TriggerQueue);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.OrderTriggers, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new OrderTriggersCommand(OrderedTriggerIds: []),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.InvalidPayload, rejected.ErrorCode);
        Assert.Equal("ORDER_TRIGGERS 需要非空且不重复的 orderedTriggerIds。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        Assert.Empty(rejected.State.TriggerQueue);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertOppositePlayerFirstTurnChooseHandCardsWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.ChooseHandCards, activePrompt.Actions);
        Assert.Null(context.Accepted.State.PendingHandChoice);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.ChooseHandCards, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new ChooseHandCardsCommand("missing-choice", "missing-window", []),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, rejected.ErrorCode);
        Assert.Equal("当前没有服务端手牌选择窗口可处理 CHOOSE_HAND_CARDS。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        Assert.Null(rejected.State.PendingHandChoice);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertWrongPlayerFirstTurnReadyPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, firstTurnPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Ready, firstTurnPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.SubmitDeck, firstTurnPrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Ready, waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.SubmitDeck, waitPrompt.Actions);

        var rejected = await context.Session.ReadyAsync(
            context.SecondPlayerId,
            intentId,
            rawCommandFactory(firstTurnPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertSnapshotOnlyWrongPlayerFirstTurnReadyPromptAcceptsWithoutMutation(
        string sessionName,
        string intentId)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, firstTurnPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Ready, firstTurnPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.SubmitDeck, firstTurnPrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Ready, waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.SubmitDeck, waitPrompt.Actions);

        var accepted = await context.Session.ReadyAsync(
            context.SecondPlayerId,
            intentId,
            SnapshotOnlyReadyRawCommand(firstTurnPrompt.SnapshotTick.GetValueOrDefault()),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Null(accepted.ErrorMessage);
        Assert.Empty(accepted.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(accepted.State));
        Assert.Equal(context.Accepted.State.Tick, accepted.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, accepted.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, accepted.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, accepted.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, accepted.State.OpeningSecondActionPlayerId);
        Assert.Null(accepted.State.FocusPlayerId);
        Assert.Empty(accepted.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            accepted,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, accepted.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, accepted.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertWrongPlayerFirstTurnSubmitDeckPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<FinalMulliganFirstTurnAuditContext, ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, firstTurnPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Ready, firstTurnPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.SubmitDeck, firstTurnPrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Ready, waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.SubmitDeck, waitPrompt.Actions);

        var rejected = await context.Session.SubmitDeckAsync(
            context.SecondPlayerId,
            intentId,
            ToSubmitCommand(context.SecondDeck),
            rawCommandFactory(context, firstTurnPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertWrongPlayerFirstTurnSurrenderPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.Surrender, firstTurnPrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            new SurrenderCommand(),
            rawCommandFactory(firstTurnPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertSnapshotOnlyWrongPlayerFirstTurnSurrenderPromptReplayAfterMatchFinishedRejectsWithoutMutation(
        string sessionName,
        string intentPrefix)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var firstTurnPrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(firstTurnPrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, firstTurnPrompt.View?.Type);
        Assert.Contains(CommandTypes.Surrender, firstTurnPrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);

        var rawCommand = SnapshotOnlyBasicRawCommand(CommandTypes.Surrender, firstTurnPrompt.SnapshotTick.GetValueOrDefault());

        var accepted = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            $"{intentPrefix}-before-stale-prompt-replay",
            new SurrenderCommand(),
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        AssertOfficialFirstTurnSurrenderMatchFinishedPromptQueueAudit(
            context,
            accepted,
            surrenderedPlayerId: context.SecondPlayerId,
            winnerPlayerId: context.ActivePlayerId);
        var acceptedHash = MatchStateHasher.Hash(accepted.State);

        var replay = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            $"{intentPrefix}-stale-prompt-replay",
            new SurrenderCommand(),
            rawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Equal("行动快照已过期，请按最新状态重新提交。", replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(accepted.State.RngCursor, replay.State.RngCursor);
        Assert.Equal(accepted.State.ReadyPlayerIds, replay.State.ReadyPlayerIds);
        Assert.Equal(accepted.State.MulliganCompletedPlayerIds, replay.State.MulliganCompletedPlayerIds);
        Assert.Equal(accepted.State.OpeningSecondActionPlayerId, replay.State.OpeningSecondActionPlayerId);
        AssertOfficialFirstTurnSurrenderMatchFinishedPromptQueueAudit(
            context,
            replay,
            surrenderedPlayerId: context.SecondPlayerId,
            winnerPlayerId: context.ActivePlayerId,
            assertEvents: false);
        Assert.Equal(accepted.Prompts[context.ActivePlayerId].SnapshotTick, replay.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(accepted.Prompts[context.SecondPlayerId].SnapshotTick, replay.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertOppositePlayerFirstTurnReadyWaitPromptAcceptsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Ready, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.SubmitDeck, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Ready, waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.SubmitDeck, waitPrompt.Actions);

        var accepted = await context.Session.ReadyAsync(
            context.SecondPlayerId,
            intentId,
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Null(accepted.ErrorMessage);
        Assert.Empty(accepted.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(accepted.State));
        Assert.Equal(context.Accepted.State.Tick, accepted.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, accepted.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, accepted.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, accepted.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, accepted.State.OpeningSecondActionPlayerId);
        Assert.Null(accepted.State.FocusPlayerId);
        Assert.Empty(accepted.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            accepted,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, accepted.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, accepted.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertOppositePlayerFirstTurnSubmitDeckWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<FinalMulliganFirstTurnAuditContext, ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Ready, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.SubmitDeck, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Ready, waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.SubmitDeck, waitPrompt.Actions);

        var rejected = await context.Session.SubmitDeckAsync(
            context.SecondPlayerId,
            intentId,
            ToSubmitCommand(context.SecondDeck),
            rawCommandFactory(context, waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, rejected.ErrorCode);
        Assert.Equal("对局开始后不能更改卡组。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertActivePlayerFirstTurnPassWithOppositeWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Pass, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            new PassCommand(),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertSnapshotOnlyActivePlayerFirstTurnPassWithOppositeWaitPromptReplayAfterAcceptedPassRejectsWithoutMutation(
        string sessionName,
        string intentPrefix)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Pass, waitPrompt.Actions);

        var rawCommand = SnapshotOnlyBasicRawCommand(CommandTypes.Pass, waitPrompt.SnapshotTick.GetValueOrDefault());

        var accepted = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            $"{intentPrefix}-before-stale-prompt-replay",
            new PassCommand(),
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        AssertOfficialFirstTurnGenericPassPromptQueueAudit(context, accepted);
        var acceptedHash = MatchStateHasher.Hash(accepted.State);

        var replay = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            $"{intentPrefix}-stale-prompt-replay",
            new PassCommand(),
            rawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Equal("行动快照已过期，请按最新状态重新提交。", replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(accepted.State.RngCursor, replay.State.RngCursor);
        Assert.Equal(accepted.State.ReadyPlayerIds, replay.State.ReadyPlayerIds);
        Assert.Equal(accepted.State.MulliganCompletedPlayerIds, replay.State.MulliganCompletedPlayerIds);
        Assert.Equal(accepted.State.OpeningSecondActionPlayerId, replay.State.OpeningSecondActionPlayerId);
        AssertOfficialFirstTurnGenericPassPromptQueueAudit(context, replay, assertEvents: false);
        Assert.Equal(accepted.Prompts[context.ActivePlayerId].SnapshotTick, replay.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(accepted.Prompts[context.SecondPlayerId].SnapshotTick, replay.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertActivePlayerFirstTurnPassPriorityWithOppositeWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.PassPriority, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.PassPriority, waitPrompt.Actions);
        Assert.Empty(context.Accepted.State.PassedPriorityPlayerIds);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            new PassPriorityCommand(),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertActivePlayerFirstTurnPassFocusWithOppositeWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, waitPrompt.Actions);
        Assert.Null(context.Accepted.State.FocusPlayerId);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            new PassFocusCommand(),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertActivePlayerFirstTurnMoveUnitWithOppositeWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.MoveUnit, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.MoveUnit, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            new MoveUnitCommand("missing-unit", "BASE", "BATTLEFIELD", []),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertActivePlayerFirstTurnDeclareBattleWithOppositeWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.DeclareBattle, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.DeclareBattle, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            new DeclareBattleCommand("missing-battlefield", [], [], []),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertActivePlayerFirstTurnPlayCardWithOppositeWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.PlayCard, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.PlayCard, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            new PlayCardCommand("missing-source", "missing-card", []),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertActivePlayerFirstTurnActivateAbilityWithOppositeWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.ActivateAbility, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.ActivateAbility, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            new ActivateAbilityCommand("missing-source", "missing-ability", []),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertActivePlayerFirstTurnTapRuneWithOppositeWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.Contains(CommandTypes.TapRune, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.TapRune, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            new TapRuneCommand("missing-rune"),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertActivePlayerFirstTurnRecycleRuneWithOppositeWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.Contains(CommandTypes.RecycleRune, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.RecycleRune, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            new RecycleRuneCommand("missing-rune"),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertActivePlayerFirstTurnHideCardWithOppositeWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.HideCard, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.HideCard, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            new HideCardCommand("missing-source", "missing-card"),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertActivePlayerFirstTurnRevealCardWithOppositeWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.RevealCard, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.RevealCard, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            new RevealCardCommand("missing-source", "missing-card", []),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertActivePlayerFirstTurnLegendActWithOppositeWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.LegendAct, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.LegendAct, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            new LegendActCommand("missing-source", "missing-ability", []),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertActivePlayerFirstTurnAssembleEquipmentWithOppositeWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.AssembleEquipment, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.AssembleEquipment, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            new AssembleEquipmentCommand("missing-source", "missing-target"),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertActivePlayerFirstTurnPayCostWithOppositeWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.PayCost, activePrompt.Actions);
        Assert.Null(context.Accepted.State.PendingPayment);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.PayCost, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            new PayCostCommand("missing-payment", "missing-window", []),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        Assert.Null(rejected.State.PendingPayment);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertActivePlayerFirstTurnAssignCombatDamageWithOppositeWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.AssignCombatDamage, activePrompt.Actions);
        Assert.False(context.Accepted.State.BattleState.IsActive);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.AssignCombatDamage, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            new AssignCombatDamageCommand("missing-battle", "missing-battlefield", []),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        Assert.False(rejected.State.BattleState.IsActive);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertActivePlayerFirstTurnOrderTriggersWithOppositeWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.OrderTriggers, activePrompt.Actions);
        Assert.Empty(context.Accepted.State.TriggerQueue);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.OrderTriggers, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            new OrderTriggersCommand(OrderedTriggerIds: []),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        Assert.Empty(rejected.State.TriggerQueue);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertActivePlayerFirstTurnChooseHandCardsWithOppositeWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.ChooseHandCards, activePrompt.Actions);
        Assert.Null(context.Accepted.State.PendingHandChoice);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.ChooseHandCards, waitPrompt.Actions);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            new ChooseHandCardsCommand("missing-choice", "missing-window", []),
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        Assert.Null(rejected.State.PendingHandChoice);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertActivePlayerFirstTurnReadyWithOppositeWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<ActionPromptDto, JsonElement> rawCommandFactory)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Ready, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.SubmitDeck, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Ready, waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.SubmitDeck, waitPrompt.Actions);

        var rejected = await context.Session.ReadyAsync(
            context.ActivePlayerId,
            intentId,
            rawCommandFactory(waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertSnapshotOnlyActivePlayerFirstTurnReadyWithOppositeWaitPromptAcceptsWithoutMutation(
        string sessionName,
        string intentId)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Ready, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.SubmitDeck, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Ready, waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.SubmitDeck, waitPrompt.Actions);

        var accepted = await context.Session.ReadyAsync(
            context.ActivePlayerId,
            intentId,
            SnapshotOnlyReadyRawCommand(waitPrompt.SnapshotTick.GetValueOrDefault()),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Null(accepted.ErrorMessage);
        Assert.Empty(accepted.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(accepted.State));
        Assert.Equal(context.Accepted.State.Tick, accepted.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, accepted.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, accepted.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, accepted.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, accepted.State.OpeningSecondActionPlayerId);
        Assert.Null(accepted.State.FocusPlayerId);
        Assert.Empty(accepted.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            accepted,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, accepted.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, accepted.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static async Task AssertActivePlayerFirstTurnSubmitDeckWithOppositeWaitPromptRejectsWithoutMutation(
        string sessionName,
        string intentId,
        Func<FinalMulliganFirstTurnAuditContext, ActionPromptDto, JsonElement> rawCommandFactory,
        string expectedErrorCode,
        string expectedMessage)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var activePrompt = context.Accepted.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Ready, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.SubmitDeck, activePrompt.Actions);

        var waitPrompt = context.Accepted.Prompts[context.SecondPlayerId];
        Assert.False(waitPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, waitPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Ready, waitPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.SubmitDeck, waitPrompt.Actions);

        var rejected = await context.Session.SubmitDeckAsync(
            context.ActivePlayerId,
            intentId,
            ToSubmitCommand(context.ActiveDeck),
            rawCommandFactory(context, waitPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Null(rejected.State.FocusPlayerId);
        Assert.Empty(rejected.State.PassedPriorityPlayerIds);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private static void AssertOfficialFirstTurnGenericPassPromptQueueAudit(
        FinalMulliganFirstTurnAuditContext context,
        ResolutionResult result,
        bool assertEvents = true)
    {
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            result,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.State.Tick + 1, result.State.Tick);
        Assert.Equal([context.ActivePlayerId], result.State.PassedPriorityPlayerIds);

        var activePrompt = result.Prompts[context.ActivePlayerId];
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.Contains(CommandTypes.Surrender, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Pass, activePrompt.Actions);
        Assert.DoesNotContain(
            activePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.Pass, StringComparison.Ordinal)
                && candidate.Enabled);

        var secondPrompt = result.Prompts[context.SecondPlayerId];
        Assert.False(secondPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, secondPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], secondPrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Pass, secondPrompt.Actions);

        if (assertEvents)
        {
            var passEvent = Assert.Single(result.Events);
            Assert.Equal("TURN_ENDED", passEvent.Kind);
            Assert.Equal($"{context.ActivePlayerId} 选择暂不行动", passEvent.Description);
            Assert.Equal(context.ActivePlayerId, Assert.IsType<string>(passEvent.Payload["playerId"]));
            Assert.Equal(context.ActivePlayerId, Assert.IsType<string>(passEvent.Payload["turnPlayerId"]));
        }
        else
        {
            Assert.Empty(result.Events);
        }
    }

    private static void AssertOfficialFirstTurnSurrenderMatchFinishedPromptQueueAudit(
        FinalMulliganFirstTurnAuditContext context,
        ResolutionResult result,
        string? surrenderedPlayerId = null,
        string? winnerPlayerId = null,
        bool assertEvents = true)
    {
        surrenderedPlayerId ??= context.ActivePlayerId;
        winnerPlayerId ??= context.SecondPlayerId;

        Assert.Equal(MatchStatuses.Finished, result.State.Status);
        Assert.Equal(winnerPlayerId, result.State.WinnerPlayerId);
        Assert.Equal(context.ActivePlayerId, result.State.ActivePlayerId);
        Assert.Equal(context.ActivePlayerId, result.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.Equal(["P1", "P2"], result.State.ReadyPlayerIds);
        Assert.Equal(2, result.State.MulliganCompletedPlayerIds.Count);
        Assert.Contains(context.ActivePlayerId, result.State.MulliganCompletedPlayerIds);
        Assert.Contains(context.SecondPlayerId, result.State.MulliganCompletedPlayerIds);
        Assert.Null(result.State.PriorityPlayerId);
        Assert.Null(result.State.FocusPlayerId);
        Assert.Empty(result.State.StackItems);
        Assert.Empty(result.State.BattlefieldTasks);
        Assert.False(result.State.PendingTaskQueue.HasTasks);
        Assert.False(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.Null(result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Empty(result.State.PendingTaskQueue.Tasks);

        Assert.Equal(context.Accepted.State.PlayerZones[context.ActivePlayerId], result.State.PlayerZones[context.ActivePlayerId]);
        Assert.Equal(context.Accepted.State.PlayerZones[context.SecondPlayerId], result.State.PlayerZones[context.SecondPlayerId]);

        if (assertEvents)
        {
            var winEvent = Assert.Single(result.Events);
            Assert.Equal("MATCH_WON", winEvent.Kind);
            Assert.Equal(winnerPlayerId, Assert.IsType<string>(winEvent.Payload["winnerPlayerId"]));
            Assert.Equal(surrenderedPlayerId, Assert.IsType<string>(winEvent.Payload["surrenderedPlayerId"]));
            Assert.Equal("SURRENDER", Assert.IsType<string>(winEvent.Payload["reason"]));
        }

        foreach (var snapshot in result.Snapshots.Values)
        {
            Assert.Equal(result.State.Tick, snapshot.Tick);
            Assert.Equal(winnerPlayerId, Assert.IsType<string>(snapshot.Timing["winnerPlayerId"]));
            Assert.Equal(MatchPhases.Main, Assert.IsType<string>(snapshot.Timing["phase"]));
            Assert.Equal(TimingStates.NeutralOpen, Assert.IsType<string>(snapshot.Timing["timingState"]));
            Assert.Empty(snapshot.Stack);

            var queue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
            Assert.False(Assert.IsType<bool>(queue["hasTasks"]));
            Assert.False(Assert.IsType<bool>(queue["isBlocking"]));
            Assert.Equal("IDLE", Assert.IsType<string>(queue["phase"]));
            Assert.Null(queue["activeTaskId"]);
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(queue["tasks"]));
        }

        foreach (var prompt in result.Prompts.Values)
        {
            Assert.False(prompt.Actionable);
            Assert.Equal(PromptTypes.MatchResult, prompt.View?.Type);
            Assert.Equal(["WAIT"], prompt.Actions);
            Assert.DoesNotContain(CommandTypes.Surrender, prompt.Actions);
            Assert.DoesNotContain(CommandTypes.EndTurn, prompt.Actions);
            Assert.DoesNotContain(CommandTypes.Mulligan, prompt.Actions);
            Assert.Equal(result.State.Tick, prompt.SnapshotTick);
        }

        var promptJson = JsonSerializer.Serialize(result.Prompts);
        Assert.DoesNotContain(CommandTypes.Surrender, promptJson, StringComparison.Ordinal);
        Assert.DoesNotContain(CommandTypes.EndTurn, promptJson, StringComparison.Ordinal);
        Assert.DoesNotContain(CommandTypes.Mulligan, promptJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WrongPlayerFinalMulliganCommandAfterFirstTurnRejectsWithoutMutation()
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(
            "official-wrong-player-final-mulligan-command-with-prompt-after-first-turn-room");

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            "wrong-player-mulligan-final-with-prompt-after-first-turn",
            new MulliganCommand(context.ActiveSelectedObjectIds),
            PromptScopedMulliganRawCommand(context.ActiveSelectedObjectIds, context.FinalMulliganPrompt),
            CancellationToken.None);

        AssertFinalMulliganPromptAfterFirstTurnRejection(context, rejected);
    }

    [Fact]
    public async Task WrongPlayerRoomCommandsWithFinalMulliganPromptAfterFirstTurnRejectWithoutMutation()
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(
            "official-wrong-player-room-commands-with-final-mulligan-prompt-after-first-turn-room");

        var readyRejected = await context.Session.ReadyAsync(
            context.ActivePlayerId,
            "wrong-player-ready-with-final-mulligan-prompt-after-first-turn",
            PromptScopedReadyRawCommand(context.FinalMulliganPrompt),
            CancellationToken.None);

        AssertFinalMulliganPromptAfterFirstTurnRejection(context, readyRejected);

        var submitDeckRejected = await context.Session.SubmitDeckAsync(
            context.ActivePlayerId,
            "wrong-player-submit-deck-with-final-mulligan-prompt-after-first-turn",
            ToSubmitCommand(context.ActiveDeck),
            PromptScopedSubmitDeckRawCommand(context.ActiveDeck, context.FinalMulliganPrompt),
            CancellationToken.None);

        AssertFinalMulliganPromptAfterFirstTurnRejection(context, submitDeckRejected);
    }

    [Theory]
    [InlineData(CommandTypes.PassPriority)]
    [InlineData(CommandTypes.PassFocus)]
    [InlineData(CommandTypes.Pass)]
    [InlineData(CommandTypes.EndTurn)]
    [InlineData(CommandTypes.Surrender)]
    [InlineData(CommandTypes.PlayCard)]
    [InlineData(CommandTypes.ActivateAbility)]
    [InlineData(CommandTypes.LegendAct)]
    [InlineData(CommandTypes.HideCard)]
    [InlineData(CommandTypes.TapRune)]
    [InlineData(CommandTypes.RecycleRune)]
    [InlineData(CommandTypes.RevealCard)]
    [InlineData(CommandTypes.MoveUnit)]
    [InlineData(CommandTypes.AssembleEquipment)]
    [InlineData(CommandTypes.DeclareBattle)]
    [InlineData(CommandTypes.PayCost)]
    [InlineData(CommandTypes.AssignCombatDamage)]
    [InlineData(CommandTypes.OrderTriggers)]
    [InlineData(CommandTypes.ChooseHandCards)]
    public Task SubmitCommandWithFinalMulliganPromptAfterFirstTurnWrongPlayerRejectsWithoutMutation(string commandType)
    {
        var slug = commandType.ToLowerInvariant().Replace('_', '-');
        return AssertSubmitCommandWithFinalMulliganPromptAfterFirstTurnWrongPlayerRejectsWithoutMutation(
            $"official-wrong-player-{slug}-with-final-mulligan-prompt-after-first-turn-room",
            $"wrong-player-{slug}-with-final-mulligan-prompt-after-first-turn",
            CreateMulliganPromptAfterFirstTurnCommand(commandType),
            commandType);
    }

    private static async Task AssertSubmitCommandWithFinalMulliganPromptAfterFirstTurnWrongPlayerRejectsWithoutMutation(
        string sessionName,
        string intentId,
        GameCommand command,
        string commandType)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            command,
            PromptScopedBasicRawCommand(commandType, context.FinalMulliganPrompt),
            CancellationToken.None);

        AssertFinalMulliganPromptAfterFirstTurnRejection(context, rejected);
    }

    [Fact]
    public async Task PromptIdOnlyWrongPlayerRoomCommandsWithFinalMulliganPromptAfterFirstTurnRejectWithoutMutation()
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(
            "official-wrong-player-room-commands-with-final-mulligan-prompt-id-after-first-turn-room");

        var readyRejected = await context.Session.ReadyAsync(
            context.ActivePlayerId,
            "wrong-player-ready-with-final-mulligan-prompt-id-after-first-turn",
            PromptIdOnlyReadyRawCommand(context.FinalMulliganPrompt),
            CancellationToken.None);

        AssertFinalMulliganPromptAfterFirstTurnRejection(context, readyRejected);

        var submitDeckRejected = await context.Session.SubmitDeckAsync(
            context.ActivePlayerId,
            "wrong-player-submit-deck-with-final-mulligan-prompt-id-after-first-turn",
            ToSubmitCommand(context.ActiveDeck),
            PromptIdOnlySubmitDeckRawCommand(context.ActiveDeck, context.FinalMulliganPrompt),
            CancellationToken.None);

        AssertFinalMulliganPromptAfterFirstTurnRejection(context, submitDeckRejected);
    }

    [Theory]
    [InlineData(CommandTypes.PassPriority)]
    [InlineData(CommandTypes.PassFocus)]
    [InlineData(CommandTypes.Pass)]
    [InlineData(CommandTypes.EndTurn)]
    [InlineData(CommandTypes.Surrender)]
    [InlineData(CommandTypes.PlayCard)]
    [InlineData(CommandTypes.ActivateAbility)]
    [InlineData(CommandTypes.LegendAct)]
    [InlineData(CommandTypes.HideCard)]
    [InlineData(CommandTypes.TapRune)]
    [InlineData(CommandTypes.RecycleRune)]
    [InlineData(CommandTypes.RevealCard)]
    [InlineData(CommandTypes.MoveUnit)]
    [InlineData(CommandTypes.AssembleEquipment)]
    [InlineData(CommandTypes.DeclareBattle)]
    [InlineData(CommandTypes.PayCost)]
    [InlineData(CommandTypes.AssignCombatDamage)]
    [InlineData(CommandTypes.OrderTriggers)]
    [InlineData(CommandTypes.ChooseHandCards)]
    public Task SubmitCommandWithFinalMulliganPromptIdAfterFirstTurnWrongPlayerRejectsWithoutMutation(string commandType)
    {
        var slug = commandType.ToLowerInvariant().Replace('_', '-');
        return AssertSubmitCommandWithFinalMulliganPromptIdAfterFirstTurnWrongPlayerRejectsWithoutMutation(
            $"official-wrong-player-{slug}-with-final-mulligan-prompt-id-after-first-turn-room",
            $"wrong-player-{slug}-with-final-mulligan-prompt-id-after-first-turn",
            CreateMulliganPromptAfterFirstTurnCommand(commandType),
            commandType);
    }

    private static async Task AssertSubmitCommandWithFinalMulliganPromptIdAfterFirstTurnWrongPlayerRejectsWithoutMutation(
        string sessionName,
        string intentId,
        GameCommand command,
        string commandType)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            command,
            PromptIdOnlyBasicRawCommand(commandType, context.FinalMulliganPrompt),
            CancellationToken.None);

        AssertFinalMulliganPromptAfterFirstTurnRejection(context, rejected);
    }

    [Fact]
    public async Task PromptIdOnlyWrongPlayerFinalMulliganCommandAfterFirstTurnRejectsWithoutMutation()
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(
            "official-wrong-player-final-mulligan-command-with-prompt-id-after-first-turn-room");

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            "wrong-player-mulligan-final-with-prompt-id-after-first-turn",
            new MulliganCommand(context.ActiveSelectedObjectIds),
            PromptIdOnlyMulliganRawCommand(context.ActiveSelectedObjectIds, context.FinalMulliganPrompt),
            CancellationToken.None);

        AssertFinalMulliganPromptAfterFirstTurnRejection(context, rejected);
    }

    [Fact]
    public async Task SnapshotOnlyRoomCommandsWithFinalMulliganPromptAfterFirstTurnRejectWithoutMutation()
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(
            "official-room-commands-with-final-mulligan-snapshot-after-first-turn-room");
        const string expectedMessage = "行动快照已过期，请按最新状态重新提交。";

        var readyRejected = await context.Session.ReadyAsync(
            context.SecondPlayerId,
            "ready-with-final-mulligan-snapshot-after-first-turn",
            SnapshotOnlyReadyRawCommand(context.FinalMulliganPrompt.SnapshotTick.GetValueOrDefault()),
            CancellationToken.None);

        AssertFinalMulliganPromptAfterFirstTurnRejection(context, readyRejected, expectedMessage);

        var submitDeckRejected = await context.Session.SubmitDeckAsync(
            context.SecondPlayerId,
            "submit-deck-with-final-mulligan-snapshot-after-first-turn",
            ToSubmitCommand(context.SecondDeck),
            SnapshotOnlySubmitDeckRawCommand(context.SecondDeck, context.FinalMulliganPrompt.SnapshotTick.GetValueOrDefault()),
            CancellationToken.None);

        AssertFinalMulliganPromptAfterFirstTurnRejection(context, submitDeckRejected, expectedMessage);
    }

    [Theory]
    [InlineData(CommandTypes.PassPriority)]
    [InlineData(CommandTypes.PassFocus)]
    [InlineData(CommandTypes.Pass)]
    [InlineData(CommandTypes.EndTurn)]
    [InlineData(CommandTypes.Surrender)]
    [InlineData(CommandTypes.PlayCard)]
    [InlineData(CommandTypes.ActivateAbility)]
    [InlineData(CommandTypes.LegendAct)]
    [InlineData(CommandTypes.HideCard)]
    [InlineData(CommandTypes.TapRune)]
    [InlineData(CommandTypes.RecycleRune)]
    [InlineData(CommandTypes.RevealCard)]
    [InlineData(CommandTypes.MoveUnit)]
    [InlineData(CommandTypes.AssembleEquipment)]
    [InlineData(CommandTypes.DeclareBattle)]
    [InlineData(CommandTypes.PayCost)]
    [InlineData(CommandTypes.AssignCombatDamage)]
    [InlineData(CommandTypes.OrderTriggers)]
    [InlineData(CommandTypes.ChooseHandCards)]
    public Task SubmitCommandWithFinalMulliganSnapshotAfterFirstTurnRejectsWithoutMutation(string commandType)
    {
        var slug = commandType.ToLowerInvariant().Replace('_', '-');
        return AssertSubmitCommandWithFinalMulliganSnapshotAfterFirstTurnRejectsWithoutMutation(
            $"official-{slug}-with-final-mulligan-snapshot-after-first-turn-room",
            $"{slug}-with-final-mulligan-snapshot-after-first-turn",
            CreateMulliganPromptAfterFirstTurnCommand(commandType),
            commandType);
    }

    private static async Task AssertSubmitCommandWithFinalMulliganSnapshotAfterFirstTurnRejectsWithoutMutation(
        string sessionName,
        string intentId,
        GameCommand command,
        string commandType)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            command,
            SnapshotOnlyBasicRawCommand(commandType, context.FinalMulliganPrompt.SnapshotTick.GetValueOrDefault()),
            CancellationToken.None);

        AssertFinalMulliganPromptAfterFirstTurnRejection(
            context,
            rejected,
            "行动快照已过期，请按最新状态重新提交。");
    }

    [Fact]
    public async Task SnapshotOnlyWrongPlayerRoomCommandsWithFinalMulliganPromptAfterFirstTurnRejectWithoutMutation()
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(
            "official-wrong-player-room-commands-with-final-mulligan-snapshot-after-first-turn-room");
        const string expectedMessage = "行动快照已过期，请按最新状态重新提交。";

        var readyRejected = await context.Session.ReadyAsync(
            context.ActivePlayerId,
            "wrong-player-ready-with-final-mulligan-snapshot-after-first-turn",
            SnapshotOnlyReadyRawCommand(context.FinalMulliganPrompt.SnapshotTick.GetValueOrDefault()),
            CancellationToken.None);

        AssertFinalMulliganPromptAfterFirstTurnRejection(context, readyRejected, expectedMessage);

        var submitDeckRejected = await context.Session.SubmitDeckAsync(
            context.ActivePlayerId,
            "wrong-player-submit-deck-with-final-mulligan-snapshot-after-first-turn",
            ToSubmitCommand(context.ActiveDeck),
            SnapshotOnlySubmitDeckRawCommand(context.ActiveDeck, context.FinalMulliganPrompt.SnapshotTick.GetValueOrDefault()),
            CancellationToken.None);

        AssertFinalMulliganPromptAfterFirstTurnRejection(context, submitDeckRejected, expectedMessage);
    }

    [Theory]
    [InlineData(CommandTypes.PassPriority)]
    [InlineData(CommandTypes.PassFocus)]
    [InlineData(CommandTypes.Pass)]
    [InlineData(CommandTypes.EndTurn)]
    [InlineData(CommandTypes.Surrender)]
    [InlineData(CommandTypes.PlayCard)]
    [InlineData(CommandTypes.ActivateAbility)]
    [InlineData(CommandTypes.LegendAct)]
    [InlineData(CommandTypes.HideCard)]
    [InlineData(CommandTypes.TapRune)]
    [InlineData(CommandTypes.RecycleRune)]
    [InlineData(CommandTypes.RevealCard)]
    [InlineData(CommandTypes.MoveUnit)]
    [InlineData(CommandTypes.AssembleEquipment)]
    [InlineData(CommandTypes.DeclareBattle)]
    [InlineData(CommandTypes.PayCost)]
    [InlineData(CommandTypes.AssignCombatDamage)]
    [InlineData(CommandTypes.OrderTriggers)]
    [InlineData(CommandTypes.ChooseHandCards)]
    public Task SubmitCommandWithFinalMulliganSnapshotAfterFirstTurnWrongPlayerRejectsWithoutMutation(string commandType)
    {
        var slug = commandType.ToLowerInvariant().Replace('_', '-');
        return AssertSubmitCommandWithFinalMulliganSnapshotAfterFirstTurnWrongPlayerRejectsWithoutMutation(
            $"official-wrong-player-{slug}-with-final-mulligan-snapshot-after-first-turn-room",
            $"wrong-player-{slug}-with-final-mulligan-snapshot-after-first-turn",
            CreateMulliganPromptAfterFirstTurnCommand(commandType),
            commandType);
    }

    private static async Task AssertSubmitCommandWithFinalMulliganSnapshotAfterFirstTurnWrongPlayerRejectsWithoutMutation(
        string sessionName,
        string intentId,
        GameCommand command,
        string commandType)
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(sessionName);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            command,
            SnapshotOnlyBasicRawCommand(commandType, context.FinalMulliganPrompt.SnapshotTick.GetValueOrDefault()),
            CancellationToken.None);

        AssertFinalMulliganPromptAfterFirstTurnRejection(
            context,
            rejected,
            "行动快照已过期，请按最新状态重新提交。");
    }

    [Fact]
    public async Task SnapshotOnlyWrongPlayerFinalMulliganCommandAfterFirstTurnRejectsWithoutMutation()
    {
        var context = await BuildFinalMulliganFirstTurnAuditContext(
            "official-wrong-player-final-mulligan-command-with-snapshot-after-first-turn-room");

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            "wrong-player-mulligan-final-with-snapshot-after-first-turn",
            new MulliganCommand(context.ActiveSelectedObjectIds),
            SnapshotOnlyMulliganRawCommand(
                context.ActiveSelectedObjectIds,
                context.FinalMulliganPrompt.SnapshotTick.GetValueOrDefault()),
            CancellationToken.None);

        AssertFinalMulliganPromptAfterFirstTurnRejection(
            context,
            rejected,
            "行动快照已过期，请按最新状态重新提交。");
    }

    [Fact]
    public async Task RoomCommandsWithFirstMulliganPromptAfterFirstTurnRejectWithoutMutation()
    {
        var context = await BuildFirstMulliganFirstTurnAuditContext(
            "official-room-commands-with-first-mulligan-prompt-after-first-turn-room");

        var readyRejected = await context.Session.ReadyAsync(
            context.ActivePlayerId,
            "ready-with-first-mulligan-prompt-after-first-turn",
            PromptScopedReadyRawCommand(context.FirstMulliganPrompt),
            CancellationToken.None);

        AssertFirstMulliganPromptAfterFirstTurnRejection(context, readyRejected);

        var submitDeckRejected = await context.Session.SubmitDeckAsync(
            context.ActivePlayerId,
            "submit-deck-with-first-mulligan-prompt-after-first-turn",
            ToSubmitCommand(context.ActiveDeck),
            PromptScopedSubmitDeckRawCommand(context.ActiveDeck, context.FirstMulliganPrompt),
            CancellationToken.None);

        AssertFirstMulliganPromptAfterFirstTurnRejection(context, submitDeckRejected);
    }

    [Theory]
    [InlineData(CommandTypes.PassPriority)]
    [InlineData(CommandTypes.PassFocus)]
    [InlineData(CommandTypes.Pass)]
    [InlineData(CommandTypes.EndTurn)]
    [InlineData(CommandTypes.Surrender)]
    [InlineData(CommandTypes.PlayCard)]
    [InlineData(CommandTypes.ActivateAbility)]
    [InlineData(CommandTypes.LegendAct)]
    [InlineData(CommandTypes.HideCard)]
    [InlineData(CommandTypes.TapRune)]
    [InlineData(CommandTypes.RecycleRune)]
    [InlineData(CommandTypes.RevealCard)]
    [InlineData(CommandTypes.MoveUnit)]
    [InlineData(CommandTypes.AssembleEquipment)]
    [InlineData(CommandTypes.DeclareBattle)]
    [InlineData(CommandTypes.PayCost)]
    [InlineData(CommandTypes.AssignCombatDamage)]
    [InlineData(CommandTypes.OrderTriggers)]
    [InlineData(CommandTypes.ChooseHandCards)]
    public Task SubmitCommandWithFirstMulliganPromptAfterFirstTurnRejectsWithoutMutation(string commandType)
    {
        var slug = commandType.ToLowerInvariant().Replace('_', '-');
        return AssertSubmitCommandWithFirstMulliganPromptAfterFirstTurnRejectsWithoutMutation(
            $"official-{slug}-with-first-mulligan-prompt-after-first-turn-room",
            $"{slug}-with-first-mulligan-prompt-after-first-turn",
            CreateMulliganPromptAfterFirstTurnCommand(commandType),
            commandType);
    }

    private static async Task AssertSubmitCommandWithFirstMulliganPromptAfterFirstTurnRejectsWithoutMutation(
        string sessionName,
        string intentId,
        GameCommand command,
        string commandType)
    {
        var context = await BuildFirstMulliganFirstTurnAuditContext(sessionName);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            command,
            PromptScopedBasicRawCommand(commandType, context.FirstMulliganPrompt),
            CancellationToken.None);

        AssertFirstMulliganPromptAfterFirstTurnRejection(context, rejected);
    }

    [Fact]
    public async Task PromptIdOnlyRoomCommandsWithFirstMulliganPromptAfterFirstTurnRejectWithoutMutation()
    {
        var context = await BuildFirstMulliganFirstTurnAuditContext(
            "official-room-commands-with-first-mulligan-prompt-id-after-first-turn-room");

        var readyRejected = await context.Session.ReadyAsync(
            context.ActivePlayerId,
            "ready-with-first-mulligan-prompt-id-after-first-turn",
            PromptIdOnlyReadyRawCommand(context.FirstMulliganPrompt),
            CancellationToken.None);

        AssertFirstMulliganPromptAfterFirstTurnRejection(context, readyRejected);

        var submitDeckRejected = await context.Session.SubmitDeckAsync(
            context.ActivePlayerId,
            "submit-deck-with-first-mulligan-prompt-id-after-first-turn",
            ToSubmitCommand(context.ActiveDeck),
            PromptIdOnlySubmitDeckRawCommand(context.ActiveDeck, context.FirstMulliganPrompt),
            CancellationToken.None);

        AssertFirstMulliganPromptAfterFirstTurnRejection(context, submitDeckRejected);
    }

    [Theory]
    [InlineData(CommandTypes.PassPriority)]
    [InlineData(CommandTypes.PassFocus)]
    [InlineData(CommandTypes.Pass)]
    [InlineData(CommandTypes.EndTurn)]
    [InlineData(CommandTypes.Surrender)]
    [InlineData(CommandTypes.PlayCard)]
    [InlineData(CommandTypes.ActivateAbility)]
    [InlineData(CommandTypes.LegendAct)]
    [InlineData(CommandTypes.HideCard)]
    [InlineData(CommandTypes.TapRune)]
    [InlineData(CommandTypes.RecycleRune)]
    [InlineData(CommandTypes.RevealCard)]
    [InlineData(CommandTypes.MoveUnit)]
    [InlineData(CommandTypes.AssembleEquipment)]
    [InlineData(CommandTypes.DeclareBattle)]
    [InlineData(CommandTypes.PayCost)]
    [InlineData(CommandTypes.AssignCombatDamage)]
    [InlineData(CommandTypes.OrderTriggers)]
    [InlineData(CommandTypes.ChooseHandCards)]
    public Task SubmitCommandWithFirstMulliganPromptIdAfterFirstTurnRejectsWithoutMutation(string commandType)
    {
        var slug = commandType.ToLowerInvariant().Replace('_', '-');
        return AssertSubmitCommandWithFirstMulliganPromptIdAfterFirstTurnRejectsWithoutMutation(
            $"official-{slug}-with-first-mulligan-prompt-id-after-first-turn-room",
            $"{slug}-with-first-mulligan-prompt-id-after-first-turn",
            CreateMulliganPromptAfterFirstTurnCommand(commandType),
            commandType);
    }

    private static async Task AssertSubmitCommandWithFirstMulliganPromptIdAfterFirstTurnRejectsWithoutMutation(
        string sessionName,
        string intentId,
        GameCommand command,
        string commandType)
    {
        var context = await BuildFirstMulliganFirstTurnAuditContext(sessionName);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            command,
            PromptIdOnlyBasicRawCommand(commandType, context.FirstMulliganPrompt),
            CancellationToken.None);

        AssertFirstMulliganPromptAfterFirstTurnRejection(context, rejected);
    }

    [Fact]
    public async Task PromptIdOnlyFirstMulliganCommandAfterFirstTurnRejectsWithoutMutation()
    {
        var context = await BuildFirstMulliganFirstTurnAuditContext(
            "official-first-mulligan-command-with-prompt-id-after-first-turn-room");

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            "mulligan-active-first-with-prompt-id-after-first-turn",
            new MulliganCommand(context.ActiveSelectedObjectIds),
            PromptIdOnlyMulliganRawCommand(context.ActiveSelectedObjectIds, context.FirstMulliganPrompt),
            CancellationToken.None);

        AssertFirstMulliganPromptAfterFirstTurnRejection(context, rejected);
    }

    [Fact]
    public async Task SnapshotOnlyFirstMulliganCommandAfterFirstTurnRejectsWithoutMutation()
    {
        var context = await BuildFirstMulliganFirstTurnAuditContext(
            "official-first-mulligan-command-with-snapshot-after-first-turn-room");

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            "mulligan-active-first-with-snapshot-after-first-turn",
            new MulliganCommand(context.ActiveSelectedObjectIds),
            SnapshotOnlyMulliganRawCommand(
                context.ActiveSelectedObjectIds,
                context.FirstMulliganPrompt.SnapshotTick.GetValueOrDefault()),
            CancellationToken.None);

        AssertFirstMulliganPromptAfterFirstTurnRejection(
            context,
            rejected,
            "行动快照已过期，请按最新状态重新提交。");
    }

    private static async Task<FirstMulliganFirstTurnAuditContext> BuildFirstMulliganFirstTurnAuditContext(string sessionName)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession(sessionName, new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.ReadyAsync("P1", "ready-p1", RawCommand("READY"), CancellationToken.None);
        var ready = await session.ReadyAsync("P2", "ready-p2", RawCommand("READY"), CancellationToken.None);

        var activePlayerId = ready.State.ActivePlayerId;
        var secondPlayerId = ready.State.OpeningSecondActionPlayerId!;
        var firstMulliganPrompt = ready.Prompts[activePlayerId];
        Assert.True(firstMulliganPrompt.Actionable);
        Assert.Contains(CommandTypes.Mulligan, firstMulliganPrompt.Actions);

        var activeHandBefore = ready.State.PlayerZones[activePlayerId].Hand.ToArray();
        var activeSelectedObjectIds = activeHandBefore.Take(2).ToArray();
        var activeDrawnObjectIds = ready.State.PlayerZones[activePlayerId].MainDeck
            .Take(activeSelectedObjectIds.Length)
            .ToArray();
        var activeMulligan = await session.SubmitAsync(
            activePlayerId,
            "mulligan-active-before-first-prompt-stale-command",
            new MulliganCommand(activeSelectedObjectIds),
            PromptScopedMulliganRawCommand(activeSelectedObjectIds, firstMulliganPrompt),
            CancellationToken.None);

        Assert.True(activeMulligan.Accepted, activeMulligan.ErrorMessage);
        AssertOfficialMulliganSecondPlayerPromptQueueAudit(
            activeMulligan,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds);

        var secondHandBefore = activeMulligan.State.PlayerZones[secondPlayerId].Hand.ToArray();
        var secondSelectedObjectIds = secondHandBefore.Take(1).ToArray();
        var secondDrawnObjectIds = activeMulligan.State.PlayerZones[secondPlayerId].MainDeck
            .Take(secondSelectedObjectIds.Length)
            .ToArray();
        var calledRuneObjectIds = activeMulligan.State.PlayerZones[activePlayerId].RuneDeck.Take(2).ToArray();
        var turnDrawnObjectIds = activeMulligan.State.PlayerZones[activePlayerId].MainDeck.Take(1).ToArray();
        var accepted = await session.SubmitAsync(
            secondPlayerId,
            "mulligan-second-final-before-first-prompt-stale-command",
            new MulliganCommand(secondSelectedObjectIds),
            RawCommand(CommandTypes.Mulligan),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            accepted,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds,
            activeDrawnObjectIds,
            secondSelectedObjectIds,
            secondDrawnObjectIds,
            calledRuneObjectIds,
            turnDrawnObjectIds);

        var activeDeck = string.Equals(activePlayerId, "P1", StringComparison.Ordinal)
            ? p1Deck
            : p2Deck;
        var secondDeck = string.Equals(secondPlayerId, "P1", StringComparison.Ordinal)
            ? p1Deck
            : p2Deck;

        return new FirstMulliganFirstTurnAuditContext(
            session,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds,
            activeDrawnObjectIds,
            secondSelectedObjectIds,
            secondDrawnObjectIds,
            calledRuneObjectIds,
            turnDrawnObjectIds,
            firstMulliganPrompt,
            accepted,
            MatchStateHasher.Hash(accepted.State),
            activeDeck,
            secondDeck);
    }

    private static void AssertFirstMulliganPromptAfterFirstTurnRejection(
        FirstMulliganFirstTurnAuditContext context,
        ResolutionResult rejected,
        string expectedMessage = "行动窗口已过期，请按最新提示重新提交。")
    {
        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal(expectedMessage, rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(context.AcceptedHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(context.Accepted.State.Tick, rejected.State.Tick);
        Assert.Equal(context.Accepted.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(context.Accepted.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(context.Accepted.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(context.Accepted.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            rejected,
            context.ActivePlayerId,
            context.SecondPlayerId,
            context.ActiveSelectedObjectIds,
            context.ActiveDrawnObjectIds,
            context.SecondSelectedObjectIds,
            context.SecondDrawnObjectIds,
            context.CalledRuneObjectIds,
            context.TurnDrawnObjectIds,
            assertEvents: false);
        Assert.Equal(context.Accepted.Prompts[context.ActivePlayerId].SnapshotTick, rejected.Prompts[context.ActivePlayerId].SnapshotTick);
        Assert.Equal(context.Accepted.Prompts[context.SecondPlayerId].SnapshotTick, rejected.Prompts[context.SecondPlayerId].SnapshotTick);
    }

    private sealed record FirstMulliganFirstTurnAuditContext(
        MatchSession Session,
        string ActivePlayerId,
        string SecondPlayerId,
        IReadOnlyList<string> ActiveSelectedObjectIds,
        IReadOnlyList<string> ActiveDrawnObjectIds,
        IReadOnlyList<string> SecondSelectedObjectIds,
        IReadOnlyList<string> SecondDrawnObjectIds,
        IReadOnlyList<string> CalledRuneObjectIds,
        IReadOnlyList<string> TurnDrawnObjectIds,
        ActionPromptDto FirstMulliganPrompt,
        ResolutionResult Accepted,
        string AcceptedHash,
        OfficialDecklist ActiveDeck,
        OfficialDecklist SecondDeck);

    [Fact]
    public async Task WrongPlayerFirstMulliganCommandAfterFirstTurnRejectsWithoutMutation()
    {
        var context = await BuildFirstMulliganFirstTurnAuditContext(
            "official-wrong-player-first-mulligan-command-with-prompt-after-first-turn-room");

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            "wrong-player-mulligan-first-with-prompt-after-first-turn",
            new MulliganCommand(context.SecondSelectedObjectIds),
            PromptScopedMulliganRawCommand(context.SecondSelectedObjectIds, context.FirstMulliganPrompt),
            CancellationToken.None);

        AssertFirstMulliganPromptAfterFirstTurnRejection(context, rejected);
    }

    [Fact]
    public async Task SnapshotOnlyRoomCommandsWithFirstMulliganPromptAfterFirstTurnRejectWithoutMutation()
    {
        var context = await BuildFirstMulliganFirstTurnAuditContext(
            "official-room-commands-with-first-mulligan-snapshot-after-first-turn-room");
        const string expectedMessage = "行动快照已过期，请按最新状态重新提交。";

        var readyRejected = await context.Session.ReadyAsync(
            context.ActivePlayerId,
            "ready-with-first-mulligan-snapshot-after-first-turn",
            SnapshotOnlyReadyRawCommand(context.FirstMulliganPrompt.SnapshotTick.GetValueOrDefault()),
            CancellationToken.None);

        AssertFirstMulliganPromptAfterFirstTurnRejection(context, readyRejected, expectedMessage);

        var submitDeckRejected = await context.Session.SubmitDeckAsync(
            context.ActivePlayerId,
            "submit-deck-with-first-mulligan-snapshot-after-first-turn",
            ToSubmitCommand(context.ActiveDeck),
            SnapshotOnlySubmitDeckRawCommand(context.ActiveDeck, context.FirstMulliganPrompt.SnapshotTick.GetValueOrDefault()),
            CancellationToken.None);

        AssertFirstMulliganPromptAfterFirstTurnRejection(context, submitDeckRejected, expectedMessage);
    }

    [Theory]
    [InlineData(CommandTypes.PassPriority)]
    [InlineData(CommandTypes.PassFocus)]
    [InlineData(CommandTypes.Pass)]
    [InlineData(CommandTypes.EndTurn)]
    [InlineData(CommandTypes.Surrender)]
    [InlineData(CommandTypes.PlayCard)]
    [InlineData(CommandTypes.ActivateAbility)]
    [InlineData(CommandTypes.LegendAct)]
    [InlineData(CommandTypes.HideCard)]
    [InlineData(CommandTypes.TapRune)]
    [InlineData(CommandTypes.RecycleRune)]
    [InlineData(CommandTypes.RevealCard)]
    [InlineData(CommandTypes.MoveUnit)]
    [InlineData(CommandTypes.AssembleEquipment)]
    [InlineData(CommandTypes.DeclareBattle)]
    [InlineData(CommandTypes.PayCost)]
    [InlineData(CommandTypes.AssignCombatDamage)]
    [InlineData(CommandTypes.OrderTriggers)]
    [InlineData(CommandTypes.ChooseHandCards)]
    public Task SubmitCommandWithFirstMulliganSnapshotAfterFirstTurnRejectsWithoutMutation(string commandType)
    {
        var slug = commandType.ToLowerInvariant().Replace('_', '-');
        return AssertSubmitCommandWithFirstMulliganSnapshotAfterFirstTurnRejectsWithoutMutation(
            $"official-{slug}-with-first-mulligan-snapshot-after-first-turn-room",
            $"{slug}-with-first-mulligan-snapshot-after-first-turn",
            CreateMulliganPromptAfterFirstTurnCommand(commandType),
            commandType);
    }

    private static async Task AssertSubmitCommandWithFirstMulliganSnapshotAfterFirstTurnRejectsWithoutMutation(
        string sessionName,
        string intentId,
        GameCommand command,
        string commandType)
    {
        var context = await BuildFirstMulliganFirstTurnAuditContext(sessionName);

        var rejected = await context.Session.SubmitAsync(
            context.ActivePlayerId,
            intentId,
            command,
            SnapshotOnlyBasicRawCommand(commandType, context.FirstMulliganPrompt.SnapshotTick.GetValueOrDefault()),
            CancellationToken.None);

        AssertFirstMulliganPromptAfterFirstTurnRejection(
            context,
            rejected,
            "行动快照已过期，请按最新状态重新提交。");
    }

    [Fact]
    public async Task SnapshotOnlyWrongPlayerRoomCommandsWithFirstMulliganPromptAfterFirstTurnRejectWithoutMutation()
    {
        var context = await BuildFirstMulliganFirstTurnAuditContext(
            "official-wrong-player-room-commands-with-first-mulligan-snapshot-after-first-turn-room");
        const string expectedMessage = "行动快照已过期，请按最新状态重新提交。";

        var readyRejected = await context.Session.ReadyAsync(
            context.SecondPlayerId,
            "wrong-player-ready-with-first-mulligan-snapshot-after-first-turn",
            SnapshotOnlyReadyRawCommand(context.FirstMulliganPrompt.SnapshotTick.GetValueOrDefault()),
            CancellationToken.None);

        AssertFirstMulliganPromptAfterFirstTurnRejection(context, readyRejected, expectedMessage);

        var submitDeckRejected = await context.Session.SubmitDeckAsync(
            context.SecondPlayerId,
            "wrong-player-submit-deck-with-first-mulligan-snapshot-after-first-turn",
            ToSubmitCommand(context.SecondDeck),
            SnapshotOnlySubmitDeckRawCommand(context.SecondDeck, context.FirstMulliganPrompt.SnapshotTick.GetValueOrDefault()),
            CancellationToken.None);

        AssertFirstMulliganPromptAfterFirstTurnRejection(context, submitDeckRejected, expectedMessage);
    }

    [Theory]
    [InlineData(CommandTypes.PassPriority)]
    [InlineData(CommandTypes.PassFocus)]
    [InlineData(CommandTypes.Pass)]
    [InlineData(CommandTypes.EndTurn)]
    [InlineData(CommandTypes.Surrender)]
    [InlineData(CommandTypes.PlayCard)]
    [InlineData(CommandTypes.ActivateAbility)]
    [InlineData(CommandTypes.LegendAct)]
    [InlineData(CommandTypes.HideCard)]
    [InlineData(CommandTypes.TapRune)]
    [InlineData(CommandTypes.RecycleRune)]
    [InlineData(CommandTypes.RevealCard)]
    [InlineData(CommandTypes.MoveUnit)]
    [InlineData(CommandTypes.AssembleEquipment)]
    [InlineData(CommandTypes.DeclareBattle)]
    [InlineData(CommandTypes.PayCost)]
    [InlineData(CommandTypes.AssignCombatDamage)]
    [InlineData(CommandTypes.OrderTriggers)]
    [InlineData(CommandTypes.ChooseHandCards)]
    public Task SubmitCommandWithFirstMulliganSnapshotAfterFirstTurnWrongPlayerRejectsWithoutMutation(string commandType)
    {
        var slug = commandType.ToLowerInvariant().Replace('_', '-');
        return AssertSubmitCommandWithFirstMulliganSnapshotAfterFirstTurnWrongPlayerRejectsWithoutMutation(
            $"official-wrong-player-{slug}-with-first-mulligan-snapshot-after-first-turn-room",
            $"wrong-player-{slug}-with-first-mulligan-snapshot-after-first-turn",
            CreateMulliganPromptAfterFirstTurnCommand(commandType),
            commandType);
    }

    private static async Task AssertSubmitCommandWithFirstMulliganSnapshotAfterFirstTurnWrongPlayerRejectsWithoutMutation(
        string sessionName,
        string intentId,
        GameCommand command,
        string commandType)
    {
        var context = await BuildFirstMulliganFirstTurnAuditContext(sessionName);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            command,
            SnapshotOnlyBasicRawCommand(commandType, context.FirstMulliganPrompt.SnapshotTick.GetValueOrDefault()),
            CancellationToken.None);

        AssertFirstMulliganPromptAfterFirstTurnRejection(
            context,
            rejected,
            "行动快照已过期，请按最新状态重新提交。");
    }

    [Fact]
    public async Task SnapshotOnlyWrongPlayerFirstMulliganCommandAfterFirstTurnRejectsWithoutMutation()
    {
        var context = await BuildFirstMulliganFirstTurnAuditContext(
            "official-wrong-player-first-mulligan-command-with-snapshot-after-first-turn-room");

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            "wrong-player-mulligan-first-with-snapshot-after-first-turn",
            new MulliganCommand(context.SecondSelectedObjectIds),
            SnapshotOnlyMulliganRawCommand(
                context.SecondSelectedObjectIds,
                context.FirstMulliganPrompt.SnapshotTick.GetValueOrDefault()),
            CancellationToken.None);

        AssertFirstMulliganPromptAfterFirstTurnRejection(
            context,
            rejected,
            "行动快照已过期，请按最新状态重新提交。");
    }

    [Fact]
    public async Task WrongPlayerRoomCommandsWithFirstMulliganPromptAfterFirstTurnRejectWithoutMutation()
    {
        var context = await BuildFirstMulliganFirstTurnAuditContext(
            "official-wrong-player-room-commands-with-first-mulligan-prompt-after-first-turn-room");

        var readyRejected = await context.Session.ReadyAsync(
            context.SecondPlayerId,
            "wrong-player-ready-with-first-mulligan-prompt-after-first-turn",
            PromptScopedReadyRawCommand(context.FirstMulliganPrompt),
            CancellationToken.None);

        AssertFirstMulliganPromptAfterFirstTurnRejection(context, readyRejected);

        var submitDeckRejected = await context.Session.SubmitDeckAsync(
            context.SecondPlayerId,
            "wrong-player-submit-deck-with-first-mulligan-prompt-after-first-turn",
            ToSubmitCommand(context.SecondDeck),
            PromptScopedSubmitDeckRawCommand(context.SecondDeck, context.FirstMulliganPrompt),
            CancellationToken.None);

        AssertFirstMulliganPromptAfterFirstTurnRejection(context, submitDeckRejected);
    }

    [Theory]
    [InlineData(CommandTypes.PassPriority)]
    [InlineData(CommandTypes.PassFocus)]
    [InlineData(CommandTypes.Pass)]
    [InlineData(CommandTypes.EndTurn)]
    [InlineData(CommandTypes.Surrender)]
    [InlineData(CommandTypes.PlayCard)]
    [InlineData(CommandTypes.ActivateAbility)]
    [InlineData(CommandTypes.LegendAct)]
    [InlineData(CommandTypes.HideCard)]
    [InlineData(CommandTypes.TapRune)]
    [InlineData(CommandTypes.RecycleRune)]
    [InlineData(CommandTypes.RevealCard)]
    [InlineData(CommandTypes.MoveUnit)]
    [InlineData(CommandTypes.AssembleEquipment)]
    [InlineData(CommandTypes.DeclareBattle)]
    [InlineData(CommandTypes.PayCost)]
    [InlineData(CommandTypes.AssignCombatDamage)]
    [InlineData(CommandTypes.OrderTriggers)]
    [InlineData(CommandTypes.ChooseHandCards)]
    public Task SubmitCommandWithFirstMulliganPromptAfterFirstTurnWrongPlayerRejectsWithoutMutation(string commandType)
    {
        var slug = commandType.ToLowerInvariant().Replace('_', '-');
        return AssertSubmitCommandWithFirstMulliganPromptAfterFirstTurnWrongPlayerRejectsWithoutMutation(
            $"official-wrong-player-{slug}-with-first-mulligan-prompt-after-first-turn-room",
            $"wrong-player-{slug}-with-first-mulligan-prompt-after-first-turn",
            CreateMulliganPromptAfterFirstTurnCommand(commandType),
            commandType);
    }

    private static async Task AssertSubmitCommandWithFirstMulliganPromptAfterFirstTurnWrongPlayerRejectsWithoutMutation(
        string sessionName,
        string intentId,
        GameCommand command,
        string commandType)
    {
        var context = await BuildFirstMulliganFirstTurnAuditContext(sessionName);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            command,
            PromptScopedBasicRawCommand(commandType, context.FirstMulliganPrompt),
            CancellationToken.None);

        AssertFirstMulliganPromptAfterFirstTurnRejection(context, rejected);
    }

    [Fact]
    public async Task PromptIdOnlyWrongPlayerFirstMulliganCommandAfterFirstTurnRejectsWithoutMutation()
    {
        var context = await BuildFirstMulliganFirstTurnAuditContext(
            "official-wrong-player-first-mulligan-command-with-prompt-id-after-first-turn-room");

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            "wrong-player-mulligan-first-with-prompt-id-after-first-turn",
            new MulliganCommand(context.SecondSelectedObjectIds),
            PromptIdOnlyMulliganRawCommand(context.SecondSelectedObjectIds, context.FirstMulliganPrompt),
            CancellationToken.None);

        AssertFirstMulliganPromptAfterFirstTurnRejection(context, rejected);
    }

    [Fact]
    public async Task PromptIdOnlyWrongPlayerRoomCommandsWithFirstMulliganPromptAfterFirstTurnRejectWithoutMutation()
    {
        var context = await BuildFirstMulliganFirstTurnAuditContext(
            "official-wrong-player-room-commands-with-first-mulligan-prompt-id-after-first-turn-room");

        var readyRejected = await context.Session.ReadyAsync(
            context.SecondPlayerId,
            "wrong-player-ready-with-first-mulligan-prompt-id-after-first-turn",
            PromptIdOnlyReadyRawCommand(context.FirstMulliganPrompt),
            CancellationToken.None);

        AssertFirstMulliganPromptAfterFirstTurnRejection(context, readyRejected);

        var submitDeckRejected = await context.Session.SubmitDeckAsync(
            context.SecondPlayerId,
            "wrong-player-submit-deck-with-first-mulligan-prompt-id-after-first-turn",
            ToSubmitCommand(context.SecondDeck),
            PromptIdOnlySubmitDeckRawCommand(context.SecondDeck, context.FirstMulliganPrompt),
            CancellationToken.None);

        AssertFirstMulliganPromptAfterFirstTurnRejection(context, submitDeckRejected);
    }

    [Theory]
    [InlineData(CommandTypes.PassPriority)]
    [InlineData(CommandTypes.PassFocus)]
    [InlineData(CommandTypes.Pass)]
    [InlineData(CommandTypes.EndTurn)]
    [InlineData(CommandTypes.Surrender)]
    [InlineData(CommandTypes.PlayCard)]
    [InlineData(CommandTypes.ActivateAbility)]
    [InlineData(CommandTypes.LegendAct)]
    [InlineData(CommandTypes.HideCard)]
    [InlineData(CommandTypes.TapRune)]
    [InlineData(CommandTypes.RecycleRune)]
    [InlineData(CommandTypes.RevealCard)]
    [InlineData(CommandTypes.MoveUnit)]
    [InlineData(CommandTypes.AssembleEquipment)]
    [InlineData(CommandTypes.DeclareBattle)]
    [InlineData(CommandTypes.PayCost)]
    [InlineData(CommandTypes.AssignCombatDamage)]
    [InlineData(CommandTypes.OrderTriggers)]
    [InlineData(CommandTypes.ChooseHandCards)]
    public Task SubmitCommandWithFirstMulliganPromptIdAfterFirstTurnWrongPlayerRejectsWithoutMutation(string commandType)
    {
        var slug = commandType.ToLowerInvariant().Replace('_', '-');
        return AssertSubmitCommandWithFirstMulliganPromptIdAfterFirstTurnWrongPlayerRejectsWithoutMutation(
            $"official-wrong-player-{slug}-with-first-mulligan-prompt-id-after-first-turn-room",
            $"wrong-player-{slug}-with-first-mulligan-prompt-id-after-first-turn",
            CreateMulliganPromptAfterFirstTurnCommand(commandType),
            commandType);
    }

    private static async Task AssertSubmitCommandWithFirstMulliganPromptIdAfterFirstTurnWrongPlayerRejectsWithoutMutation(
        string sessionName,
        string intentId,
        GameCommand command,
        string commandType)
    {
        var context = await BuildFirstMulliganFirstTurnAuditContext(sessionName);

        var rejected = await context.Session.SubmitAsync(
            context.SecondPlayerId,
            intentId,
            command,
            PromptIdOnlyBasicRawCommand(commandType, context.FirstMulliganPrompt),
            CancellationToken.None);

        AssertFirstMulliganPromptAfterFirstTurnRejection(context, rejected);
    }

    [Fact]
    public async Task OfficialNoSelectionMulligansEnterFirstTurnWithoutDrawReturnMutation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-no-selection-mulligan-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.ReadyAsync("P1", "ready-p1", RawCommand("READY"), CancellationToken.None);
        var ready = await session.ReadyAsync("P2", "ready-p2", RawCommand("READY"), CancellationToken.None);

        var activePlayerId = ready.State.ActivePlayerId;
        var secondPlayerId = ready.State.OpeningSecondActionPlayerId!;
        var activeHandBefore = ready.State.PlayerZones[activePlayerId].Hand.ToArray();
        var activeMainDeckBefore = ready.State.PlayerZones[activePlayerId].MainDeck.ToArray();
        var activeSelectedObjectIds = Array.Empty<string>();
        var activeDrawnObjectIds = Array.Empty<string>();
        var activeMulligan = await session.SubmitAsync(
            activePlayerId,
            "mulligan-active-no-selection",
            new MulliganCommand(activeSelectedObjectIds),
            RawCommand(CommandTypes.Mulligan),
            CancellationToken.None);

        Assert.True(activeMulligan.Accepted, activeMulligan.ErrorMessage);
        Assert.Equal(activeHandBefore, activeMulligan.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(activeMainDeckBefore, activeMulligan.State.PlayerZones[activePlayerId].MainDeck);
        var activeMulliganEvent = Assert.Single(
            activeMulligan.Events,
            gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_COMPLETED", StringComparison.Ordinal));
        Assert.Equal(activePlayerId, Assert.IsType<string>(activeMulliganEvent.Payload["playerId"]));
        Assert.Equal(0, Assert.IsType<int>(activeMulliganEvent.Payload["setAsideCount"]));
        Assert.Equal(0, Assert.IsType<int>(activeMulliganEvent.Payload["drawnCount"]));
        Assert.Equal(0, Assert.IsType<int>(activeMulliganEvent.Payload["returnedCount"]));
        AssertOfficialMulliganSecondPlayerPromptQueueAudit(
            activeMulligan,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds);

        var secondHandBefore = activeMulligan.State.PlayerZones[secondPlayerId].Hand.ToArray();
        var secondMainDeckBefore = activeMulligan.State.PlayerZones[secondPlayerId].MainDeck.ToArray();
        var secondSelectedObjectIds = Array.Empty<string>();
        var secondDrawnObjectIds = Array.Empty<string>();
        var calledRuneObjectIds = activeMulligan.State.PlayerZones[activePlayerId].RuneDeck.Take(2).ToArray();
        var turnDrawnObjectIds = activeMulligan.State.PlayerZones[activePlayerId].MainDeck.Take(1).ToArray();
        var accepted = await session.SubmitAsync(
            secondPlayerId,
            "mulligan-second-final-no-selection",
            new MulliganCommand(secondSelectedObjectIds),
            RawCommand(CommandTypes.Mulligan),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.All(activeHandBefore, objectId => Assert.Contains(objectId, accepted.State.PlayerZones[activePlayerId].Hand));
        Assert.Equal(secondHandBefore, accepted.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(secondMainDeckBefore, accepted.State.PlayerZones[secondPlayerId].MainDeck);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            accepted,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds,
            activeDrawnObjectIds,
            secondSelectedObjectIds,
            secondDrawnObjectIds,
            calledRuneObjectIds,
            turnDrawnObjectIds);
    }

    [Fact]
    public async Task OfficialMaximumSelectionMulligansEnterFirstTurnWithStableDrawReturnCounts()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-maximum-selection-mulligan-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.ReadyAsync("P1", "ready-p1", RawCommand("READY"), CancellationToken.None);
        var ready = await session.ReadyAsync("P2", "ready-p2", RawCommand("READY"), CancellationToken.None);

        var activePlayerId = ready.State.ActivePlayerId;
        var secondPlayerId = ready.State.OpeningSecondActionPlayerId!;
        var activeSelectedObjectIds = ready.State.PlayerZones[activePlayerId].Hand
            .Take(OfficialDeckValidator.MaximumMulliganCount)
            .ToArray();
        var activeDrawnObjectIds = ready.State.PlayerZones[activePlayerId].MainDeck
            .Take(activeSelectedObjectIds.Length)
            .ToArray();
        var activeMulligan = await session.SubmitAsync(
            activePlayerId,
            "mulligan-active-maximum-selection",
            new MulliganCommand(activeSelectedObjectIds),
            RawCommand(CommandTypes.Mulligan),
            CancellationToken.None);

        Assert.True(activeMulligan.Accepted, activeMulligan.ErrorMessage);
        var activeMulliganEvent = Assert.Single(
            activeMulligan.Events,
            gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_COMPLETED", StringComparison.Ordinal));
        Assert.Equal(activePlayerId, Assert.IsType<string>(activeMulliganEvent.Payload["playerId"]));
        Assert.Equal(OfficialDeckValidator.MaximumMulliganCount, Assert.IsType<int>(activeMulliganEvent.Payload["setAsideCount"]));
        Assert.Equal(OfficialDeckValidator.MaximumMulliganCount, Assert.IsType<int>(activeMulliganEvent.Payload["drawnCount"]));
        Assert.Equal(OfficialDeckValidator.MaximumMulliganCount, Assert.IsType<int>(activeMulliganEvent.Payload["returnedCount"]));
        AssertOfficialMulliganSecondPlayerPromptQueueAudit(
            activeMulligan,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds);

        var secondSelectedObjectIds = activeMulligan.State.PlayerZones[secondPlayerId].Hand
            .Take(OfficialDeckValidator.MaximumMulliganCount)
            .ToArray();
        var secondDrawnObjectIds = activeMulligan.State.PlayerZones[secondPlayerId].MainDeck
            .Take(secondSelectedObjectIds.Length)
            .ToArray();
        var calledRuneObjectIds = activeMulligan.State.PlayerZones[activePlayerId].RuneDeck.Take(2).ToArray();
        var turnDrawnObjectIds = activeMulligan.State.PlayerZones[activePlayerId].MainDeck.Take(1).ToArray();
        var accepted = await session.SubmitAsync(
            secondPlayerId,
            "mulligan-second-final-maximum-selection",
            new MulliganCommand(secondSelectedObjectIds),
            RawCommand(CommandTypes.Mulligan),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            accepted,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds,
            activeDrawnObjectIds,
            secondSelectedObjectIds,
            secondDrawnObjectIds,
            calledRuneObjectIds,
            turnDrawnObjectIds);
    }

    [Fact]
    public async Task OfficialMulliganRejectsInvalidSelectionsAndWrongPlayer()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-mulligan-invalid-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.ReadyAsync("P1", "ready-p1", RawCommand("READY"), CancellationToken.None);
        var ready = await session.ReadyAsync("P2", "ready-p2", RawCommand("READY"), CancellationToken.None);

        var activePlayerId = ready.State.ActivePlayerId;
        var secondPlayerId = ready.State.OpeningSecondActionPlayerId!;
        var activeHand = ready.State.PlayerZones[activePlayerId].Hand.ToArray();
        var startedTick = ready.State.Tick;
        var startedHash = MatchStateHasher.Hash(ready.State);
        AssertOfficialReadyMulliganPromptQueueAudit(ready, activePlayerId, secondPlayerId);

        var wrongPlayer = await session.SubmitAsync(
            secondPlayerId,
            "wrong-player-mulligan",
            new MulliganCommand([]),
            RawCommand("MULLIGAN"),
            CancellationToken.None);
        Assert.False(wrongPlayer.Accepted);
        Assert.Equal("现在不是该玩家的起手调整时机。", wrongPlayer.ErrorMessage);
        Assert.DoesNotContain("MULLIGAN", wrongPlayer.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(startedTick, wrongPlayer.State.Tick);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, wrongPlayer.ErrorCode);
        AssertOfficialMulliganInvalidRejectionPromptQueueAudit(
            wrongPlayer,
            ready.State,
            startedHash,
            activePlayerId,
            secondPlayerId);

        var tooMany = await session.SubmitAsync(
            activePlayerId,
            "too-many-mulligan",
            new MulliganCommand(activeHand.Take(3).ToArray()),
            RawCommand("MULLIGAN"),
            CancellationToken.None);
        Assert.False(tooMany.Accepted);
        Assert.Equal("起手调整最多可选择 2 张牌。", tooMany.ErrorMessage);
        Assert.DoesNotContain("MULLIGAN", tooMany.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(startedTick, tooMany.State.Tick);
        Assert.Equal(ErrorCodes.InvalidTarget, tooMany.ErrorCode);
        AssertOfficialMulliganInvalidRejectionPromptQueueAudit(
            tooMany,
            ready.State,
            startedHash,
            activePlayerId,
            secondPlayerId);

        var duplicate = await session.SubmitAsync(
            activePlayerId,
            "duplicate-mulligan",
            new MulliganCommand([activeHand[0], activeHand[0]]),
            RawCommand("MULLIGAN"),
            CancellationToken.None);
        Assert.False(duplicate.Accepted);
        Assert.Equal("起手调整不能重复选择同一张牌。", duplicate.ErrorMessage);
        Assert.DoesNotContain("MULLIGAN", duplicate.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(startedTick, duplicate.State.Tick);
        Assert.Equal(ErrorCodes.InvalidTarget, duplicate.ErrorCode);
        AssertOfficialMulliganInvalidRejectionPromptQueueAudit(
            duplicate,
            ready.State,
            startedHash,
            activePlayerId,
            secondPlayerId);

        var nonHand = await session.SubmitAsync(
            activePlayerId,
            "non-hand-mulligan",
            new MulliganCommand(["NOT-IN-HAND"]),
            RawCommand("MULLIGAN"),
            CancellationToken.None);
        Assert.False(nonHand.Accepted);
        Assert.Equal("起手调整只能选择自己起手手牌中的牌。", nonHand.ErrorMessage);
        Assert.DoesNotContain("MULLIGAN", nonHand.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(startedTick, nonHand.State.Tick);
        Assert.Equal(ErrorCodes.InvalidTarget, nonHand.ErrorCode);
        AssertOfficialMulliganInvalidRejectionPromptQueueAudit(
            nonHand,
            ready.State,
            startedHash,
            activePlayerId,
            secondPlayerId);
    }

    [Fact]
    public async Task InvalidPromptScopedMulliganKeepsActivePromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-invalid-prompt-mulligan-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.ReadyAsync("P1", "ready-p1", RawCommand("READY"), CancellationToken.None);
        var ready = await session.ReadyAsync("P2", "ready-p2", RawCommand("READY"), CancellationToken.None);

        var activePlayerId = ready.State.ActivePlayerId;
        var secondPlayerId = ready.State.OpeningSecondActionPlayerId!;
        var activeHand = ready.State.PlayerZones[activePlayerId].Hand.ToArray();
        var prompt = ready.Prompts[activePlayerId];
        Assert.True(prompt.Actionable);
        Assert.Contains(CommandTypes.Mulligan, prompt.Actions);
        var readyHash = MatchStateHasher.Hash(ready.State);
        AssertOfficialReadyMulliganPromptQueueAudit(ready, activePlayerId, secondPlayerId);

        var invalidSelection = activeHand.Take(3).ToArray();
        var rejected = await session.SubmitAsync(
            activePlayerId,
            "mulligan-too-many-prompt",
            new MulliganCommand(invalidSelection),
            PromptScopedMulliganRawCommand(invalidSelection, prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, rejected.ErrorCode);
        Assert.Equal("起手调整最多可选择 2 张牌。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        AssertOfficialMulliganInvalidRejectionPromptQueueAudit(
            rejected,
            ready.State,
            readyHash,
            activePlayerId,
            secondPlayerId);
        Assert.Equal(prompt.PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(prompt.SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);

        var validSelection = activeHand.Take(2).ToArray();
        var accepted = await session.SubmitAsync(
            activePlayerId,
            "mulligan-valid-after-invalid-prompt",
            new MulliganCommand(validSelection),
            PromptScopedMulliganRawCommand(validSelection, prompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_COMPLETED", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_PHASE_COMPLETED", StringComparison.Ordinal));
        AssertOfficialMulliganSecondPlayerPromptQueueAudit(
            accepted,
            activePlayerId,
            secondPlayerId,
            validSelection);
    }

    [Fact]
    public async Task WrongPlayerPromptScopedMulliganKeepsActivePromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-wrong-player-prompt-mulligan-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.ReadyAsync("P1", "ready-p1", RawCommand("READY"), CancellationToken.None);
        var ready = await session.ReadyAsync("P2", "ready-p2", RawCommand("READY"), CancellationToken.None);

        var activePlayerId = ready.State.ActivePlayerId;
        var secondPlayerId = ready.State.OpeningSecondActionPlayerId!;
        var activeHand = ready.State.PlayerZones[activePlayerId].Hand.ToArray();
        var prompt = ready.Prompts[activePlayerId];
        Assert.True(prompt.Actionable);
        Assert.Contains(CommandTypes.Mulligan, prompt.Actions);
        var readyHash = MatchStateHasher.Hash(ready.State);
        AssertOfficialReadyMulliganPromptQueueAudit(ready, activePlayerId, secondPlayerId);

        var selectedObjectIds = activeHand.Take(2).ToArray();
        var rejected = await session.SubmitAsync(
            secondPlayerId,
            "mulligan-wrong-player-active-prompt",
            new MulliganCommand(selectedObjectIds),
            PromptScopedMulliganRawCommand(selectedObjectIds, prompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        AssertOfficialMulliganInvalidRejectionPromptQueueAudit(
            rejected,
            ready.State,
            readyHash,
            activePlayerId,
            secondPlayerId);
        Assert.Equal(prompt.PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(prompt.SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);

        var accepted = await session.SubmitAsync(
            activePlayerId,
            "mulligan-active-valid-after-wrong-player-prompt",
            new MulliganCommand(selectedObjectIds),
            PromptScopedMulliganRawCommand(selectedObjectIds, prompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_COMPLETED", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_PHASE_COMPLETED", StringComparison.Ordinal));
        AssertOfficialMulliganSecondPlayerPromptQueueAudit(
            accepted,
            activePlayerId,
            secondPlayerId,
            selectedObjectIds);
    }

    [Fact]
    public async Task SnapshotMismatchPromptScopedMulliganKeepsActivePromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-snapshot-mismatch-prompt-mulligan-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.ReadyAsync("P1", "ready-p1", RawCommand("READY"), CancellationToken.None);
        var ready = await session.ReadyAsync("P2", "ready-p2", RawCommand("READY"), CancellationToken.None);

        var activePlayerId = ready.State.ActivePlayerId;
        var secondPlayerId = ready.State.OpeningSecondActionPlayerId!;
        var activeHand = ready.State.PlayerZones[activePlayerId].Hand.ToArray();
        var prompt = ready.Prompts[activePlayerId];
        Assert.True(prompt.Actionable);
        Assert.Contains(CommandTypes.Mulligan, prompt.Actions);
        var readyHash = MatchStateHasher.Hash(ready.State);
        AssertOfficialReadyMulliganPromptQueueAudit(ready, activePlayerId, secondPlayerId);

        var selectedObjectIds = activeHand.Take(2).ToArray();
        var staleSnapshotTick = prompt.SnapshotTick - 1;
        var rejected = await session.SubmitAsync(
            activePlayerId,
            "mulligan-active-stale-snapshot-prompt",
            new MulliganCommand(selectedObjectIds),
            PromptScopedMulliganRawCommand(selectedObjectIds, prompt, staleSnapshotTick),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动快照已过期，请按最新状态重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        AssertOfficialMulliganInvalidRejectionPromptQueueAudit(
            rejected,
            ready.State,
            readyHash,
            activePlayerId,
            secondPlayerId);
        Assert.Equal(prompt.PromptId, rejected.Prompts[activePlayerId].PromptId);
        Assert.Equal(prompt.SnapshotTick, rejected.Prompts[activePlayerId].SnapshotTick);

        var accepted = await session.SubmitAsync(
            activePlayerId,
            "mulligan-active-valid-after-stale-snapshot-prompt",
            new MulliganCommand(selectedObjectIds),
            PromptScopedMulliganRawCommand(selectedObjectIds, prompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_COMPLETED", StringComparison.Ordinal)));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_PHASE_COMPLETED", StringComparison.Ordinal));
        AssertOfficialMulliganSecondPlayerPromptQueueAudit(
            accepted,
            activePlayerId,
            secondPlayerId,
            selectedObjectIds);
    }

    [Fact]
    public async Task InvalidPromptScopedFinalMulliganKeepsSecondPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-invalid-prompt-final-mulligan-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.ReadyAsync("P1", "ready-p1", RawCommand("READY"), CancellationToken.None);
        var ready = await session.ReadyAsync("P2", "ready-p2", RawCommand("READY"), CancellationToken.None);

        var activePlayerId = ready.State.ActivePlayerId;
        var secondPlayerId = ready.State.OpeningSecondActionPlayerId!;
        var activeHand = ready.State.PlayerZones[activePlayerId].Hand.ToArray();
        var activeSelectedObjectIds = activeHand.Take(2).ToArray();
        var activeDrawnObjectIds = ready.State.PlayerZones[activePlayerId].MainDeck.Take(2).ToArray();
        var activeMulligan = await session.SubmitAsync(
            activePlayerId,
            "mulligan-active-before-invalid-final",
            new MulliganCommand(activeSelectedObjectIds),
            RawCommand(CommandTypes.Mulligan),
            CancellationToken.None);

        Assert.True(activeMulligan.Accepted, activeMulligan.ErrorMessage);
        AssertOfficialMulliganSecondPlayerPromptQueueAudit(
            activeMulligan,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds);

        var secondPrompt = activeMulligan.Prompts[secondPlayerId];
        Assert.True(secondPrompt.Actionable);
        Assert.Contains(CommandTypes.Mulligan, secondPrompt.Actions);
        var secondHand = activeMulligan.State.PlayerZones[secondPlayerId].Hand.ToArray();
        var activeMulliganHash = MatchStateHasher.Hash(activeMulligan.State);

        var invalidSelection = secondHand.Take(3).ToArray();
        var rejected = await session.SubmitAsync(
            secondPlayerId,
            "mulligan-final-too-many-prompt",
            new MulliganCommand(invalidSelection),
            PromptScopedMulliganRawCommand(invalidSelection, secondPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, rejected.ErrorCode);
        Assert.Equal("起手调整最多可选择 2 张牌。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(activeMulliganHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(activeMulligan.State.Tick, rejected.State.Tick);
        Assert.Equal(activeMulligan.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(activeMulligan.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(activeMulligan.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(activeMulligan.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Equal(activeMulligan.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(activeMulligan.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(activeMulligan.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(activeMulligan.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        AssertOfficialMulliganSecondPlayerPromptQueueAudit(
            rejected,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds);
        Assert.Equal(secondPrompt.PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(secondPrompt.SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        var secondSelectedObjectIds = secondHand.Take(2).ToArray();
        var secondDrawnObjectIds = activeMulligan.State.PlayerZones[secondPlayerId].MainDeck.Take(2).ToArray();
        var calledRuneObjectIds = activeMulligan.State.PlayerZones[activePlayerId].RuneDeck.Take(2).ToArray();
        var turnDrawnObjectIds = activeMulligan.State.PlayerZones[activePlayerId].MainDeck.Take(1).ToArray();
        var accepted = await session.SubmitAsync(
            secondPlayerId,
            "mulligan-final-valid-after-invalid-prompt",
            new MulliganCommand(secondSelectedObjectIds),
            PromptScopedMulliganRawCommand(secondSelectedObjectIds, secondPrompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_COMPLETED", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_PHASE_COMPLETED", StringComparison.Ordinal));
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            accepted,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds,
            activeDrawnObjectIds,
            secondSelectedObjectIds,
            secondDrawnObjectIds,
            calledRuneObjectIds,
            turnDrawnObjectIds);
    }

    [Fact]
    public async Task WrongPlayerPromptScopedFinalMulliganKeepsSecondPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-wrong-player-prompt-final-mulligan-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.ReadyAsync("P1", "ready-p1", RawCommand("READY"), CancellationToken.None);
        var ready = await session.ReadyAsync("P2", "ready-p2", RawCommand("READY"), CancellationToken.None);

        var activePlayerId = ready.State.ActivePlayerId;
        var secondPlayerId = ready.State.OpeningSecondActionPlayerId!;
        var activeHand = ready.State.PlayerZones[activePlayerId].Hand.ToArray();
        var activeSelectedObjectIds = activeHand.Take(2).ToArray();
        var activeDrawnObjectIds = ready.State.PlayerZones[activePlayerId].MainDeck.Take(2).ToArray();
        var activeMulligan = await session.SubmitAsync(
            activePlayerId,
            "mulligan-active-before-wrong-player-final",
            new MulliganCommand(activeSelectedObjectIds),
            RawCommand(CommandTypes.Mulligan),
            CancellationToken.None);

        Assert.True(activeMulligan.Accepted, activeMulligan.ErrorMessage);
        AssertOfficialMulliganSecondPlayerPromptQueueAudit(
            activeMulligan,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds);

        var secondPrompt = activeMulligan.Prompts[secondPlayerId];
        Assert.True(secondPrompt.Actionable);
        Assert.Contains(CommandTypes.Mulligan, secondPrompt.Actions);
        var secondHand = activeMulligan.State.PlayerZones[secondPlayerId].Hand.ToArray();
        var activeMulliganHash = MatchStateHasher.Hash(activeMulligan.State);

        var secondSelectedObjectIds = secondHand.Take(2).ToArray();
        var rejected = await session.SubmitAsync(
            activePlayerId,
            "mulligan-final-wrong-player-second-prompt",
            new MulliganCommand(secondSelectedObjectIds),
            PromptScopedMulliganRawCommand(secondSelectedObjectIds, secondPrompt),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(activeMulliganHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(activeMulligan.State.Tick, rejected.State.Tick);
        Assert.Equal(activeMulligan.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(activeMulligan.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(activeMulligan.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(activeMulligan.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Equal(activeMulligan.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(activeMulligan.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(activeMulligan.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(activeMulligan.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        AssertOfficialMulliganSecondPlayerPromptQueueAudit(
            rejected,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds);
        Assert.Equal(secondPrompt.PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(secondPrompt.SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        var secondDrawnObjectIds = activeMulligan.State.PlayerZones[secondPlayerId].MainDeck.Take(2).ToArray();
        var calledRuneObjectIds = activeMulligan.State.PlayerZones[activePlayerId].RuneDeck.Take(2).ToArray();
        var turnDrawnObjectIds = activeMulligan.State.PlayerZones[activePlayerId].MainDeck.Take(1).ToArray();
        var accepted = await session.SubmitAsync(
            secondPlayerId,
            "mulligan-final-valid-after-wrong-player-prompt",
            new MulliganCommand(secondSelectedObjectIds),
            PromptScopedMulliganRawCommand(secondSelectedObjectIds, secondPrompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_COMPLETED", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_PHASE_COMPLETED", StringComparison.Ordinal));
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            accepted,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds,
            activeDrawnObjectIds,
            secondSelectedObjectIds,
            secondDrawnObjectIds,
            calledRuneObjectIds,
            turnDrawnObjectIds);
    }

    [Fact]
    public async Task SnapshotMismatchPromptScopedFinalMulliganKeepsSecondPromptReusable()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildValidDeck(catalog);
        var p2Deck = BuildValidDeck(catalog);
        var session = new MatchSession("official-snapshot-mismatch-prompt-final-mulligan-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitDeckAsync(
            "P1",
            "submit-p1",
            ToSubmitCommand(p1Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.SubmitDeckAsync(
            "P2",
            "submit-p2",
            ToSubmitCommand(p2Deck),
            RawCommand("SUBMIT_DECK"),
            CancellationToken.None);
        await session.ReadyAsync("P1", "ready-p1", RawCommand("READY"), CancellationToken.None);
        var ready = await session.ReadyAsync("P2", "ready-p2", RawCommand("READY"), CancellationToken.None);

        var activePlayerId = ready.State.ActivePlayerId;
        var secondPlayerId = ready.State.OpeningSecondActionPlayerId!;
        var activeHand = ready.State.PlayerZones[activePlayerId].Hand.ToArray();
        var activeSelectedObjectIds = activeHand.Take(2).ToArray();
        var activeDrawnObjectIds = ready.State.PlayerZones[activePlayerId].MainDeck.Take(2).ToArray();
        var activeMulligan = await session.SubmitAsync(
            activePlayerId,
            "mulligan-active-before-snapshot-mismatch-final",
            new MulliganCommand(activeSelectedObjectIds),
            RawCommand(CommandTypes.Mulligan),
            CancellationToken.None);

        Assert.True(activeMulligan.Accepted, activeMulligan.ErrorMessage);
        AssertOfficialMulliganSecondPlayerPromptQueueAudit(
            activeMulligan,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds);

        var secondPrompt = activeMulligan.Prompts[secondPlayerId];
        Assert.True(secondPrompt.Actionable);
        Assert.Contains(CommandTypes.Mulligan, secondPrompt.Actions);
        var secondHand = activeMulligan.State.PlayerZones[secondPlayerId].Hand.ToArray();
        var activeMulliganHash = MatchStateHasher.Hash(activeMulligan.State);

        var secondSelectedObjectIds = secondHand.Take(2).ToArray();
        var staleSnapshotTick = secondPrompt.SnapshotTick - 1;
        var rejected = await session.SubmitAsync(
            secondPlayerId,
            "mulligan-final-stale-snapshot-prompt",
            new MulliganCommand(secondSelectedObjectIds),
            PromptScopedMulliganRawCommand(secondSelectedObjectIds, secondPrompt, staleSnapshotTick),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
        Assert.Equal("行动快照已过期，请按最新状态重新提交。", rejected.ErrorMessage);
        Assert.Empty(rejected.Events);
        Assert.Equal(activeMulliganHash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(activeMulligan.State.Tick, rejected.State.Tick);
        Assert.Equal(activeMulligan.State.RngCursor, rejected.State.RngCursor);
        Assert.Equal(activeMulligan.State.ReadyPlayerIds, rejected.State.ReadyPlayerIds);
        Assert.Equal(activeMulligan.State.MulliganCompletedPlayerIds, rejected.State.MulliganCompletedPlayerIds);
        Assert.Equal(activeMulligan.State.OpeningSecondActionPlayerId, rejected.State.OpeningSecondActionPlayerId);
        Assert.Equal(activeMulligan.State.PlayerZones[activePlayerId].Hand, rejected.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(activeMulligan.State.PlayerZones[activePlayerId].MainDeck, rejected.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(activeMulligan.State.PlayerZones[secondPlayerId].Hand, rejected.State.PlayerZones[secondPlayerId].Hand);
        Assert.Equal(activeMulligan.State.PlayerZones[secondPlayerId].MainDeck, rejected.State.PlayerZones[secondPlayerId].MainDeck);
        AssertOfficialMulliganSecondPlayerPromptQueueAudit(
            rejected,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds);
        Assert.Equal(secondPrompt.PromptId, rejected.Prompts[secondPlayerId].PromptId);
        Assert.Equal(secondPrompt.SnapshotTick, rejected.Prompts[secondPlayerId].SnapshotTick);

        var secondDrawnObjectIds = activeMulligan.State.PlayerZones[secondPlayerId].MainDeck.Take(2).ToArray();
        var calledRuneObjectIds = activeMulligan.State.PlayerZones[activePlayerId].RuneDeck.Take(2).ToArray();
        var turnDrawnObjectIds = activeMulligan.State.PlayerZones[activePlayerId].MainDeck.Take(1).ToArray();
        var accepted = await session.SubmitAsync(
            secondPlayerId,
            "mulligan-final-valid-after-stale-snapshot-prompt",
            new MulliganCommand(secondSelectedObjectIds),
            PromptScopedMulliganRawCommand(secondSelectedObjectIds, secondPrompt),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_COMPLETED", StringComparison.Ordinal));
        Assert.Contains(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_PHASE_COMPLETED", StringComparison.Ordinal));
        AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
            accepted,
            activePlayerId,
            secondPlayerId,
            activeSelectedObjectIds,
            activeDrawnObjectIds,
            secondSelectedObjectIds,
            secondDrawnObjectIds,
            calledRuneObjectIds,
            turnDrawnObjectIds);
    }

    [Fact]
    public async Task OfficialMulliganWithShortMainDeckDrawsAvailableCardsAndReturnsSetAside()
    {
        var state = new MatchState(
            "mulligan-short-deck-room",
            1,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Mulligan,
            timingState: TimingStates.Mulligan,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-DECK-1"],
                    Hand = ["P1-HAND-1", "P1-HAND-2", "P1-HAND-3", "P1-HAND-4"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-HAND-1", "P2-HAND-2", "P2-HAND-3", "P2-HAND-4"]
                }
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-DECK-1"] = new("P1", "MAIN_DECK"),
                ["P1-HAND-1"] = new("P1", "HAND"),
                ["P1-HAND-2"] = new("P1", "HAND"),
                ["P1-HAND-3"] = new("P1", "HAND"),
                ["P1-HAND-4"] = new("P1", "HAND")
            },
            openingSecondActionPlayerId: "P2");

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("short-deck-mulligan", "P1", "MULLIGAN"),
            new MulliganCommand(["P1-HAND-1", "P1-HAND-2"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        AssertOfficialShortMainDeckMulliganPromptQueueAudit(
            result,
            "P1",
            "P2",
            ["P1-HAND-1", "P1-HAND-2"],
            ["P1-DECK-1"]);
        Assert.Equal(["P1-HAND-3", "P1-HAND-4", "P1-DECK-1"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-HAND-1", "P1-HAND-2"], result.State.PlayerZones["P1"].MainDeck.OrderBy(objectId => objectId, StringComparer.Ordinal));
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("HAND", result.State.ObjectLocations["P1-DECK-1"].Zone);
        Assert.Equal("MAIN_DECK", result.State.ObjectLocations["P1-HAND-1"].Zone);
        Assert.Equal("MAIN_DECK", result.State.ObjectLocations["P1-HAND-2"].Zone);
        Assert.Contains(
            result.Events,
            gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_COMPLETED", StringComparison.Ordinal)
                && gameEvent.Payload.TryGetValue("drawnCount", out var drawnCount)
                && drawnCount is int drawn
                && drawn == 1);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BURNOUT_APPLIED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PreciseRoamMoveUpdatesAuthoritativeObjectLocation()
    {
        var state = new MatchState(
            "precise-location-room",
            1,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-UNIT-ROAM"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-ROAM"] = new(
                    "P1-UNIT-ROAM",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, "游走"],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-ROAM"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-A")
            });

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-roam", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-UNIT-ROAM",
                "BATTLEFIELD:P1-BATTLEFIELD-A",
                "BATTLEFIELD:P1-BATTLEFIELD-B",
                ["ROAM"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var location = result.State.ObjectLocations["P1-UNIT-ROAM"];
        Assert.Equal("P1", location.PlayerId);
        Assert.Equal("BATTLEFIELD", location.Zone);
        Assert.Equal("P1-BATTLEFIELD-B", location.BattlefieldObjectId);

        var p1Snapshot = result.Snapshots["P1"];
        var p1View = Assert.IsType<Dictionary<string, object?>>(p1Snapshot.Players["P1"]);
        var objects = Assert.IsType<Dictionary<string, object?>>(p1View["objects"]);
        var unit = Assert.IsType<Dictionary<string, object?>>(objects["P1-UNIT-ROAM"]);
        var snapshotLocation = Assert.IsType<Dictionary<string, object?>>(unit["location"]);
        Assert.Equal("BATTLEFIELD", snapshotLocation["zone"]);
        Assert.Equal("P1-BATTLEFIELD-B", snapshotLocation["battlefieldObjectId"]);
    }

    [Fact]
    public async Task PreciseRoamMoveRejectsOriginThatDoesNotMatchAuthoritativeLocation()
    {
        var state = new MatchState(
            "precise-location-room",
            1,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-UNIT-ROAM"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-ROAM"] = new(
                    "P1-UNIT-ROAM",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, "游走"],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-ROAM"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-A")
            });

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-roam-mismatch", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-UNIT-ROAM",
                "BATTLEFIELD:P1-BATTLEFIELD-Z",
                "BATTLEFIELD:P1-BATTLEFIELD-B",
                ["ROAM"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal(
            "移动单位的精确战场起点与服务端权威位置不一致。",
            result.ErrorMessage);
        Assert.DoesNotContain("MOVE_UNIT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal("P1-BATTLEFIELD-A", result.State.ObjectLocations["P1-UNIT-ROAM"].BattlefieldObjectId);
    }

    [Fact]
    public async Task MoveUnitRejectsWhenLethalCleanupTaskIsPending()
    {
        var state = new MatchState(
            "move-cleanup-room",
            1,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-DAMAGED-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-DAMAGED-UNIT"] = new(
                    "P1-DAMAGED-UNIT",
                    damage: 3,
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-DAMAGED-UNIT"] = new("P1", "BASE")
            });

        Assert.Contains(
            state.PendingCleanupTasks,
            task => string.Equals(task.Kind, "DESTROY_LETHAL_UNIT", StringComparison.Ordinal)
                && string.Equals(task.ObjectId, "P1-DAMAGED-UNIT", StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-move-cleanup", "P1", "MOVE_UNIT"),
            new MoveUnitCommand(
                "P1-DAMAGED-UNIT",
                "BASE",
                "BATTLEFIELD",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Contains("致命伤害清理", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("DESTROY_LETHAL_UNIT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(["P1-DAMAGED-UNIT"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("BASE", result.State.ObjectLocations["P1-DAMAGED-UNIT"].Zone);
    }

    private static void AssertValid(OfficialDecklist decklist, OfficialCardCatalog catalog)
    {
        var validation = OfficialDeckValidator.Validate(decklist, catalog);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
    }

    private static void AssertInvalid(OfficialDecklist decklist, OfficialCardCatalog catalog, string expected)
    {
        var validation = OfficialDeckValidator.Validate(decklist, catalog);
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains(expected, StringComparison.Ordinal));
        var combined = string.Join(" | ", validation.Errors);
        Assert.DoesNotContain("mainDeck must", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("runeDeck must", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("champion hero tag", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("battlefields cannot", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("has traits outside", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("references unknown card", combined, StringComparison.Ordinal);
    }

    private static void AssertOfficialMulliganSecondPlayerPromptQueueAudit(
        ResolutionResult result,
        string completedPlayerId,
        string waitingPlayerId,
        IReadOnlyList<string> selectedObjectIds)
    {
        Assert.Equal(MatchStatuses.InProgress, result.State.Status);
        Assert.Equal(MatchPhases.Mulligan, result.State.Phase);
        Assert.Equal(TimingStates.Mulligan, result.State.TimingState);
        Assert.Equal(completedPlayerId, result.State.ActivePlayerId);
        Assert.Equal(completedPlayerId, result.State.TurnPlayerId);
        Assert.Equal(waitingPlayerId, result.State.OpeningSecondActionPlayerId);
        Assert.Contains(completedPlayerId, result.State.MulliganCompletedPlayerIds);
        Assert.DoesNotContain(waitingPlayerId, result.State.MulliganCompletedPlayerIds);
        Assert.Null(result.State.PriorityPlayerId);
        Assert.Null(result.State.FocusPlayerId);
        Assert.Empty(result.State.StackItems);
        Assert.Empty(result.State.BattlefieldTasks);
        Assert.False(result.State.PendingTaskQueue.HasTasks);
        Assert.False(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.Null(result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Empty(result.State.PendingTaskQueue.Tasks);

        Assert.Equal(4, result.State.PlayerZones[completedPlayerId].Hand.Count);
        Assert.Equal(35, result.State.PlayerZones[completedPlayerId].MainDeck.Count);
        Assert.Equal(4, result.State.PlayerZones[waitingPlayerId].Hand.Count);
        Assert.Equal(35, result.State.PlayerZones[waitingPlayerId].MainDeck.Count);
        foreach (var objectId in selectedObjectIds)
        {
            Assert.DoesNotContain(objectId, result.State.PlayerZones[completedPlayerId].Hand);
            Assert.Contains(objectId, result.State.PlayerZones[completedPlayerId].MainDeck);

            var location = result.State.ObjectLocations[objectId];
            Assert.Equal(completedPlayerId, location.PlayerId);
            Assert.Equal("MAIN_DECK", location.Zone);
            Assert.Null(location.BattlefieldObjectId);
        }

        foreach (var snapshot in result.Snapshots.Values)
        {
            Assert.Equal(result.State.Tick, snapshot.Tick);
            Assert.Equal(result.State.ActivePlayerId, snapshot.ActivePlayerId);
            Assert.Equal(MatchPhases.Mulligan, Assert.IsType<string>(snapshot.Timing["phase"]));
            Assert.Equal(TimingStates.Mulligan, Assert.IsType<string>(snapshot.Timing["timingState"]));
            Assert.Equal(completedPlayerId, Assert.IsType<string>(snapshot.Timing["turnPlayerId"]));
            Assert.Null(snapshot.Timing["priorityPlayerId"]);
            Assert.Null(snapshot.Timing["focusPlayerId"]);
            Assert.Empty(snapshot.Stack);

            var queue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
            Assert.False(Assert.IsType<bool>(queue["hasTasks"]));
            Assert.False(Assert.IsType<bool>(queue["isBlocking"]));
            Assert.Equal("IDLE", Assert.IsType<string>(queue["phase"]));
            Assert.Null(queue["activeTaskId"]);
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(queue["tasks"]));
        }

        var completedPrompt = result.Prompts[completedPlayerId];
        Assert.Equal(completedPlayerId, completedPrompt.PlayerId);
        Assert.False(completedPrompt.Actionable);
        Assert.Equal(PromptTypes.Mulligan, completedPrompt.View?.Type);
        Assert.DoesNotContain(CommandTypes.Mulligan, completedPrompt.Actions);
        Assert.DoesNotContain(
            completedPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.Mulligan, StringComparison.Ordinal)
                && candidate.Enabled
                && (candidate.Sources ?? []).Any(source => selectedObjectIds.Contains(source.Id, StringComparer.Ordinal)));
        Assert.Equal(result.State.Tick, completedPrompt.SnapshotTick);

        var waitingPrompt = result.Prompts[waitingPlayerId];
        Assert.Equal(waitingPlayerId, waitingPrompt.PlayerId);
        Assert.True(waitingPrompt.Actionable);
        Assert.Equal(PromptTypes.Mulligan, waitingPrompt.View?.Type);
        Assert.Equal(0, waitingPrompt.View?.MinSelection);
        Assert.Equal(OfficialDeckValidator.MaximumMulliganCount, waitingPrompt.View?.MaxSelection);
        Assert.Contains(CommandTypes.Mulligan, waitingPrompt.Actions);
        Assert.Equal(result.State.Tick, waitingPrompt.SnapshotTick);

        var mulliganCandidate = Assert.Single(
            waitingPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.Mulligan, StringComparison.Ordinal));
        Assert.True(mulliganCandidate.Enabled);
        var sourceIds = (mulliganCandidate.Sources ?? []).Select(source => source.Id).ToArray();
        Assert.Equal(result.State.PlayerZones[waitingPlayerId].Hand, sourceIds);
        Assert.DoesNotContain(sourceIds, sourceId => selectedObjectIds.Contains(sourceId, StringComparer.Ordinal));
    }

    private static void AssertOfficialMulliganInvalidRejectionPromptQueueAudit(
        ResolutionResult result,
        MatchState expectedState,
        string expectedHash,
        string activePlayerId,
        string waitingPlayerId)
    {
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(expectedHash, MatchStateHasher.Hash(result.State));
        Assert.Equal(expectedState.Tick, result.State.Tick);
        Assert.Equal(expectedState.RngCursor, result.State.RngCursor);
        Assert.Equal(expectedState.ReadyPlayerIds, result.State.ReadyPlayerIds);
        Assert.Equal(expectedState.MulliganCompletedPlayerIds, result.State.MulliganCompletedPlayerIds);
        Assert.Equal(expectedState.OpeningSecondActionPlayerId, result.State.OpeningSecondActionPlayerId);
        Assert.Equal(expectedState.PlayerZones[activePlayerId].Hand, result.State.PlayerZones[activePlayerId].Hand);
        Assert.Equal(expectedState.PlayerZones[activePlayerId].MainDeck, result.State.PlayerZones[activePlayerId].MainDeck);
        Assert.Equal(expectedState.PlayerZones[waitingPlayerId].Hand, result.State.PlayerZones[waitingPlayerId].Hand);
        Assert.Equal(expectedState.PlayerZones[waitingPlayerId].MainDeck, result.State.PlayerZones[waitingPlayerId].MainDeck);
        AssertOfficialReadyMulliganPromptQueueAudit(result, activePlayerId, waitingPlayerId);
    }

    private static void AssertOfficialShortMainDeckMulliganPromptQueueAudit(
        ResolutionResult result,
        string completedPlayerId,
        string waitingPlayerId,
        IReadOnlyList<string> selectedObjectIds,
        IReadOnlyList<string> drawnObjectIds)
    {
        Assert.Equal(MatchStatuses.InProgress, result.State.Status);
        Assert.Equal(MatchPhases.Mulligan, result.State.Phase);
        Assert.Equal(TimingStates.Mulligan, result.State.TimingState);
        Assert.Equal(completedPlayerId, result.State.ActivePlayerId);
        Assert.Equal(completedPlayerId, result.State.TurnPlayerId);
        Assert.Equal(waitingPlayerId, result.State.OpeningSecondActionPlayerId);
        Assert.Equal(2, result.State.Tick);
        Assert.Equal(1, result.State.RngCursor);
        Assert.Equal(["P1", "P2"], result.State.ReadyPlayerIds);
        Assert.Contains(completedPlayerId, result.State.MulliganCompletedPlayerIds);
        Assert.DoesNotContain(waitingPlayerId, result.State.MulliganCompletedPlayerIds);
        Assert.Null(result.State.PriorityPlayerId);
        Assert.Null(result.State.FocusPlayerId);
        Assert.Empty(result.State.StackItems);
        Assert.Empty(result.State.BattlefieldTasks);
        Assert.False(result.State.PendingTaskQueue.HasTasks);
        Assert.False(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.Null(result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Empty(result.State.PendingTaskQueue.Tasks);

        var completedZones = result.State.PlayerZones[completedPlayerId];
        Assert.Equal(["P1-HAND-3", "P1-HAND-4", "P1-DECK-1"], completedZones.Hand);
        Assert.Equal(selectedObjectIds.OrderBy(objectId => objectId, StringComparer.Ordinal), completedZones.MainDeck.OrderBy(objectId => objectId, StringComparer.Ordinal));
        Assert.Empty(completedZones.Graveyard);
        foreach (var objectId in drawnObjectIds)
        {
            Assert.Contains(objectId, completedZones.Hand);
            Assert.DoesNotContain(objectId, completedZones.MainDeck);
            var location = result.State.ObjectLocations[objectId];
            Assert.Equal(completedPlayerId, location.PlayerId);
            Assert.Equal("HAND", location.Zone);
            Assert.Null(location.BattlefieldObjectId);
        }

        foreach (var objectId in selectedObjectIds)
        {
            Assert.DoesNotContain(objectId, completedZones.Hand);
            Assert.Contains(objectId, completedZones.MainDeck);
            var location = result.State.ObjectLocations[objectId];
            Assert.Equal(completedPlayerId, location.PlayerId);
            Assert.Equal("MAIN_DECK", location.Zone);
            Assert.Null(location.BattlefieldObjectId);
        }

        var mulliganEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_COMPLETED", StringComparison.Ordinal));
        Assert.Equal(completedPlayerId, Assert.IsType<string>(mulliganEvent.Payload["playerId"]));
        Assert.Equal(selectedObjectIds.Count, Assert.IsType<int>(mulliganEvent.Payload["setAsideCount"]));
        Assert.Equal(drawnObjectIds.Count, Assert.IsType<int>(mulliganEvent.Payload["drawnCount"]));
        Assert.Equal(selectedObjectIds.Count, Assert.IsType<int>(mulliganEvent.Payload["returnedCount"]));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_PHASE_COMPLETED", StringComparison.Ordinal));

        foreach (var snapshot in result.Snapshots.Values)
        {
            Assert.Equal(result.State.Tick, snapshot.Tick);
            Assert.Equal(result.State.ActivePlayerId, snapshot.ActivePlayerId);
            Assert.Equal(MatchPhases.Mulligan, Assert.IsType<string>(snapshot.Timing["phase"]));
            Assert.Equal(TimingStates.Mulligan, Assert.IsType<string>(snapshot.Timing["timingState"]));
            Assert.Equal(completedPlayerId, Assert.IsType<string>(snapshot.Timing["turnPlayerId"]));
            Assert.Null(snapshot.Timing["priorityPlayerId"]);
            Assert.Null(snapshot.Timing["focusPlayerId"]);
            Assert.Empty(snapshot.Stack);

            var queue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
            Assert.False(Assert.IsType<bool>(queue["hasTasks"]));
            Assert.False(Assert.IsType<bool>(queue["isBlocking"]));
            Assert.Equal("IDLE", Assert.IsType<string>(queue["phase"]));
            Assert.Null(queue["activeTaskId"]);
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(queue["tasks"]));
        }

        var completedPrompt = result.Prompts[completedPlayerId];
        Assert.Equal(completedPlayerId, completedPrompt.PlayerId);
        Assert.False(completedPrompt.Actionable);
        Assert.Equal(PromptTypes.Mulligan, completedPrompt.View?.Type);
        Assert.DoesNotContain(CommandTypes.Mulligan, completedPrompt.Actions);
        Assert.Equal(result.State.Tick, completedPrompt.SnapshotTick);

        var waitingPrompt = result.Prompts[waitingPlayerId];
        Assert.Equal(waitingPlayerId, waitingPrompt.PlayerId);
        Assert.True(waitingPrompt.Actionable);
        Assert.Equal(PromptTypes.Mulligan, waitingPrompt.View?.Type);
        Assert.Equal(0, waitingPrompt.View?.MinSelection);
        Assert.Equal(OfficialDeckValidator.MaximumMulliganCount, waitingPrompt.View?.MaxSelection);
        Assert.Contains(CommandTypes.Mulligan, waitingPrompt.Actions);
        Assert.Equal(result.State.Tick, waitingPrompt.SnapshotTick);

        var mulliganCandidate = Assert.Single(
            waitingPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.Mulligan, StringComparison.Ordinal));
        Assert.True(mulliganCandidate.Enabled);
        var sourceIds = (mulliganCandidate.Sources ?? []).Select(source => source.Id).ToArray();
        Assert.Equal(result.State.PlayerZones[waitingPlayerId].Hand, sourceIds);
        Assert.DoesNotContain(sourceIds, sourceId => selectedObjectIds.Contains(sourceId, StringComparer.Ordinal));
        Assert.DoesNotContain(sourceIds, sourceId => drawnObjectIds.Contains(sourceId, StringComparer.Ordinal));
    }

    private static void AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(
        ResolutionResult result,
        string activePlayerId,
        string secondPlayerId,
        IReadOnlyList<string> activeSelectedObjectIds,
        IReadOnlyList<string> activeDrawnObjectIds,
        IReadOnlyList<string> secondSelectedObjectIds,
        IReadOnlyList<string> secondDrawnObjectIds,
        IReadOnlyList<string> calledRuneObjectIds,
        IReadOnlyList<string> turnDrawnObjectIds,
        bool assertEvents = true)
    {
        Assert.Equal(MatchStatuses.InProgress, result.State.Status);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.Equal(activePlayerId, result.State.ActivePlayerId);
        Assert.Equal(activePlayerId, result.State.TurnPlayerId);
        Assert.Equal(secondPlayerId, result.State.OpeningSecondActionPlayerId);
        Assert.Equal(["P1", "P2"], result.State.ReadyPlayerIds);
        Assert.Equal(2, result.State.MulliganCompletedPlayerIds.Count);
        Assert.Contains(activePlayerId, result.State.MulliganCompletedPlayerIds);
        Assert.Contains(secondPlayerId, result.State.MulliganCompletedPlayerIds);
        Assert.Null(result.State.PriorityPlayerId);
        Assert.Null(result.State.FocusPlayerId);
        Assert.Empty(result.State.StackItems);
        Assert.Empty(result.State.BattlefieldTasks);
        Assert.False(result.State.PendingTaskQueue.HasTasks);
        Assert.False(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.Null(result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Empty(result.State.PendingTaskQueue.Tasks);

        var activeZones = result.State.PlayerZones[activePlayerId];
        Assert.Equal(5, activeZones.Hand.Count);
        Assert.Equal(34, activeZones.MainDeck.Count);
        Assert.Equal(10, activeZones.RuneDeck.Count);
        Assert.Equal(calledRuneObjectIds, activeZones.Base);
        Assert.Empty(activeZones.Graveyard);
        AssertOfficialObjectLocation(result.State, activePlayerId, activeDrawnObjectIds, "HAND");
        AssertOfficialObjectLocation(result.State, activePlayerId, turnDrawnObjectIds, "HAND");
        AssertOfficialObjectLocation(result.State, activePlayerId, calledRuneObjectIds, "BASE");
        AssertOfficialObjectLocation(result.State, activePlayerId, activeSelectedObjectIds, "MAIN_DECK");
        foreach (var objectId in activeSelectedObjectIds)
        {
            Assert.DoesNotContain(objectId, activeZones.Hand);
            Assert.Contains(objectId, activeZones.MainDeck);
        }

        var secondZones = result.State.PlayerZones[secondPlayerId];
        Assert.Equal(4, secondZones.Hand.Count);
        Assert.Equal(35, secondZones.MainDeck.Count);
        Assert.Equal(12, secondZones.RuneDeck.Count);
        Assert.Empty(secondZones.Base);
        Assert.Empty(secondZones.Graveyard);
        AssertOfficialObjectLocation(result.State, secondPlayerId, secondDrawnObjectIds, "HAND");
        AssertOfficialObjectLocation(result.State, secondPlayerId, secondSelectedObjectIds, "MAIN_DECK");
        foreach (var objectId in secondSelectedObjectIds)
        {
            Assert.DoesNotContain(objectId, secondZones.Hand);
            Assert.Contains(objectId, secondZones.MainDeck);
        }

        if (assertEvents)
        {
            Assert.Equal(
                [
                    "MULLIGAN_COMPLETED",
                    "MULLIGAN_PHASE_COMPLETED",
                    "TURN_START_BEGAN",
                    "RUNES_CALLED",
                    "CARD_DRAWN",
                    "RUNE_POOL_CLEARED",
                    "MAIN_PHASE_BEGAN"
                ],
                result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
            var mulliganEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_COMPLETED", StringComparison.Ordinal));
            Assert.Equal(secondPlayerId, Assert.IsType<string>(mulliganEvent.Payload["playerId"]));
            Assert.Equal(secondSelectedObjectIds.Count, Assert.IsType<int>(mulliganEvent.Payload["setAsideCount"]));
            Assert.Equal(secondDrawnObjectIds.Count, Assert.IsType<int>(mulliganEvent.Payload["drawnCount"]));
            Assert.Equal(secondSelectedObjectIds.Count, Assert.IsType<int>(mulliganEvent.Payload["returnedCount"]));

            var phaseCompletedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_PHASE_COMPLETED", StringComparison.Ordinal));
            Assert.Equal(activePlayerId, Assert.IsType<string>(phaseCompletedEvent.Payload["activePlayerId"]));
            Assert.Equal(secondPlayerId, Assert.IsType<string>(phaseCompletedEvent.Payload["secondActionPlayerId"]));
            var runeEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
            Assert.Equal(activePlayerId, Assert.IsType<string>(runeEvent.Payload["playerId"]));
            Assert.Equal(calledRuneObjectIds.Count, Assert.IsType<int>(runeEvent.Payload["count"]));
            var drawEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
            Assert.Equal(activePlayerId, Assert.IsType<string>(drawEvent.Payload["playerId"]));
            Assert.Equal(turnDrawnObjectIds.Count, Assert.IsType<int>(drawEvent.Payload["count"]));
            Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BURNOUT_APPLIED", StringComparison.Ordinal));
        }

        foreach (var snapshot in result.Snapshots.Values)
        {
            Assert.Equal(result.State.Tick, snapshot.Tick);
            Assert.Equal(activePlayerId, snapshot.ActivePlayerId);
            Assert.Equal(MatchPhases.Main, Assert.IsType<string>(snapshot.Timing["phase"]));
            Assert.Equal(TimingStates.NeutralOpen, Assert.IsType<string>(snapshot.Timing["timingState"]));
            Assert.Equal(activePlayerId, Assert.IsType<string>(snapshot.Timing["turnPlayerId"]));
            Assert.Null(snapshot.Timing["priorityPlayerId"]);
            Assert.Null(snapshot.Timing["focusPlayerId"]);
            Assert.Empty(snapshot.Stack);

            var queue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
            Assert.False(Assert.IsType<bool>(queue["hasTasks"]));
            Assert.False(Assert.IsType<bool>(queue["isBlocking"]));
            Assert.Equal("IDLE", Assert.IsType<string>(queue["phase"]));
            Assert.Null(queue["activeTaskId"]);
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(queue["tasks"]));
        }

        var activePrompt = result.Prompts[activePlayerId];
        Assert.Equal(activePlayerId, activePrompt.PlayerId);
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, activePrompt.View?.Type);
        Assert.Contains(CommandTypes.EndTurn, activePrompt.Actions);
        Assert.Contains(CommandTypes.Surrender, activePrompt.Actions);
        Assert.DoesNotContain(CommandTypes.Mulligan, activePrompt.Actions);
        Assert.Equal(result.State.Tick, activePrompt.SnapshotTick);
        Assert.DoesNotContain(
            activePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.Mulligan, StringComparison.Ordinal)
                && candidate.Enabled);

        var secondPrompt = result.Prompts[secondPlayerId];
        Assert.Equal(secondPlayerId, secondPrompt.PlayerId);
        Assert.False(secondPrompt.Actionable);
        Assert.Equal(PromptTypes.Wait, secondPrompt.View?.Type);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], secondPrompt.Actions);
        Assert.Equal(result.State.Tick, secondPrompt.SnapshotTick);
        Assert.DoesNotContain(
            secondPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.Mulligan, StringComparison.Ordinal)
                && candidate.Enabled);
    }

    private static void AssertOfficialObjectLocation(
        MatchState state,
        string playerId,
        IReadOnlyList<string> objectIds,
        string zone)
    {
        foreach (var objectId in objectIds)
        {
            var location = state.ObjectLocations[objectId];
            Assert.Equal(playerId, location.PlayerId);
            Assert.Equal(zone, location.Zone);
            Assert.Null(location.BattlefieldObjectId);
        }
    }

    private static void AssertOfficialReadyMulliganPromptQueueAudit(
        ResolutionResult result,
        string activePlayerId,
        string waitingPlayerId)
    {
        Assert.Equal(MatchStatuses.InProgress, result.State.Status);
        Assert.Equal(MatchPhases.Mulligan, result.State.Phase);
        Assert.Equal(TimingStates.Mulligan, result.State.TimingState);
        Assert.Equal(activePlayerId, result.State.ActivePlayerId);
        Assert.Equal(activePlayerId, result.State.TurnPlayerId);
        Assert.Equal(waitingPlayerId, result.State.OpeningSecondActionPlayerId);
        Assert.Equal(["P1", "P2"], result.State.ReadyPlayerIds);
        Assert.Empty(result.State.MulliganCompletedPlayerIds);
        Assert.Null(result.State.PriorityPlayerId);
        Assert.Null(result.State.FocusPlayerId);
        Assert.Empty(result.State.StackItems);
        Assert.Empty(result.State.BattlefieldTasks);
        Assert.False(result.State.PendingTaskQueue.HasTasks);
        Assert.False(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.Null(result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Empty(result.State.PendingTaskQueue.Tasks);

        foreach (var playerId in new[] { activePlayerId, waitingPlayerId })
        {
            var zones = result.State.PlayerZones[playerId];
            Assert.Equal(35, zones.MainDeck.Count);
            Assert.Equal(12, zones.RuneDeck.Count);
            Assert.Equal(4, zones.Hand.Count);
            Assert.Single(zones.Battlefields);
            Assert.Single(zones.LegendZone);
            Assert.Single(zones.ChampionZone);
        }

        foreach (var snapshot in result.Snapshots.Values)
        {
            Assert.Equal(result.State.Tick, snapshot.Tick);
            Assert.Equal(activePlayerId, snapshot.ActivePlayerId);
            Assert.Equal(MatchPhases.Mulligan, Assert.IsType<string>(snapshot.Timing["phase"]));
            Assert.Equal(TimingStates.Mulligan, Assert.IsType<string>(snapshot.Timing["timingState"]));
            Assert.Equal(activePlayerId, Assert.IsType<string>(snapshot.Timing["turnPlayerId"]));
            Assert.Null(snapshot.Timing["priorityPlayerId"]);
            Assert.Null(snapshot.Timing["focusPlayerId"]);
            Assert.Empty(snapshot.Stack);

            var queue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
            Assert.False(Assert.IsType<bool>(queue["hasTasks"]));
            Assert.False(Assert.IsType<bool>(queue["isBlocking"]));
            Assert.Equal("IDLE", Assert.IsType<string>(queue["phase"]));
            Assert.Null(queue["activeTaskId"]);
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(queue["tasks"]));
        }

        var activePrompt = result.Prompts[activePlayerId];
        Assert.Equal(activePlayerId, activePrompt.PlayerId);
        Assert.True(activePrompt.Actionable);
        Assert.Equal(PromptTypes.Mulligan, activePrompt.View?.Type);
        Assert.Equal(0, activePrompt.View?.MinSelection);
        Assert.Equal(OfficialDeckValidator.MaximumMulliganCount, activePrompt.View?.MaxSelection);
        Assert.Contains(CommandTypes.Mulligan, activePrompt.Actions);
        Assert.Equal(result.State.Tick, activePrompt.SnapshotTick);

        var mulliganCandidate = Assert.Single(
            activePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.Mulligan, StringComparison.Ordinal));
        Assert.True(mulliganCandidate.Enabled);
        var sourceIds = (mulliganCandidate.Sources ?? []).Select(source => source.Id).ToArray();
        Assert.Equal(result.State.PlayerZones[activePlayerId].Hand, sourceIds);

        var waitingPrompt = result.Prompts[waitingPlayerId];
        Assert.Equal(waitingPlayerId, waitingPrompt.PlayerId);
        Assert.False(waitingPrompt.Actionable);
        Assert.Equal(PromptTypes.Mulligan, waitingPrompt.View?.Type);
        Assert.DoesNotContain(CommandTypes.Mulligan, waitingPrompt.Actions);
        Assert.DoesNotContain(
            waitingPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.Mulligan, StringComparison.Ordinal)
                && candidate.Enabled);
        Assert.Equal(result.State.Tick, waitingPrompt.SnapshotTick);
    }

    private static void AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(
        ResolutionResult result,
        string submittedPlayerId,
        string waitingPlayerId,
        OfficialDecklist submittedDecklist)
    {
        Assert.Equal(MatchStatuses.Seating, result.State.Status);
        Assert.Equal(MatchPhases.Room, result.State.Phase);
        Assert.Equal(TimingStates.Room, result.State.TimingState);
        Assert.Empty(result.State.ReadyPlayerIds);
        Assert.True(result.State.PlayerDecklists.ContainsKey(submittedPlayerId));
        Assert.False(result.State.PlayerDecklists.ContainsKey(waitingPlayerId));
        AssertOfficialDecklist(result.State.PlayerDecklists[submittedPlayerId], submittedDecklist);
        AssertRoomSetupIdlePromptQueue(result);

        var submittedPrompt = result.Prompts[submittedPlayerId];
        Assert.Equal(submittedPlayerId, submittedPrompt.PlayerId);
        Assert.True(submittedPrompt.Actionable);
        Assert.Equal(PromptTypes.RoomSetup, submittedPrompt.View?.Type);
        Assert.Equal(["READY"], submittedPrompt.Actions);
        Assert.Equal(result.State.Tick, submittedPrompt.SnapshotTick);

        var waitingPrompt = result.Prompts[waitingPlayerId];
        Assert.Equal(waitingPlayerId, waitingPrompt.PlayerId);
        Assert.True(waitingPrompt.Actionable);
        Assert.Equal(PromptTypes.RoomSetup, waitingPrompt.View?.Type);
        Assert.Equal(["SUBMIT_DECK"], waitingPrompt.Actions);
        Assert.Equal(result.State.Tick, waitingPrompt.SnapshotTick);
    }

    private static void AssertOfficialSubmitDeckBothReadyPromptQueueAudit(
        ResolutionResult result,
        OfficialDecklist p1Decklist,
        OfficialDecklist p2Decklist)
    {
        Assert.Equal(MatchStatuses.Seating, result.State.Status);
        Assert.Equal(MatchPhases.Room, result.State.Phase);
        Assert.Equal(TimingStates.Room, result.State.TimingState);
        Assert.Empty(result.State.ReadyPlayerIds);
        AssertOfficialDecklist(result.State.PlayerDecklists["P1"], p1Decklist);
        AssertOfficialDecklist(result.State.PlayerDecklists["P2"], p2Decklist);
        AssertRoomSetupIdlePromptQueue(result);

        foreach (var playerId in new[] { "P1", "P2" })
        {
            var prompt = result.Prompts[playerId];
            Assert.Equal(playerId, prompt.PlayerId);
            Assert.True(prompt.Actionable);
            Assert.Equal(PromptTypes.RoomSetup, prompt.View?.Type);
            Assert.Equal(["READY"], prompt.Actions);
            Assert.DoesNotContain("SUBMIT_DECK", prompt.Actions);
            Assert.Equal(result.State.Tick, prompt.SnapshotTick);
        }
    }

    private static void AssertOfficialSingleReadyWaitingSubmitPromptQueueAudit(
        ResolutionResult result,
        string readyPlayerId,
        string waitingPlayerId,
        OfficialDecklist readyDecklist)
    {
        Assert.Equal(MatchStatuses.Seating, result.State.Status);
        Assert.Equal(MatchPhases.Room, result.State.Phase);
        Assert.Equal(TimingStates.Room, result.State.TimingState);
        var readyPlayer = Assert.Single(result.State.ReadyPlayerIds);
        Assert.Equal(readyPlayerId, readyPlayer);
        Assert.True(result.State.PlayerDecklists.ContainsKey(readyPlayerId));
        Assert.False(result.State.PlayerDecklists.ContainsKey(waitingPlayerId));
        AssertOfficialDecklist(result.State.PlayerDecklists[readyPlayerId], readyDecklist);
        AssertRoomSetupIdlePromptQueue(result);

        var readyPrompt = result.Prompts[readyPlayerId];
        Assert.Equal(readyPlayerId, readyPrompt.PlayerId);
        Assert.False(readyPrompt.Actionable);
        Assert.Equal(PromptTypes.RoomSetup, readyPrompt.View?.Type);
        Assert.Equal(["WAIT"], readyPrompt.Actions);
        Assert.DoesNotContain("READY", readyPrompt.Actions);
        Assert.DoesNotContain("SUBMIT_DECK", readyPrompt.Actions);
        Assert.Equal(result.State.Tick, readyPrompt.SnapshotTick);

        var waitingPrompt = result.Prompts[waitingPlayerId];
        Assert.Equal(waitingPlayerId, waitingPrompt.PlayerId);
        Assert.True(waitingPrompt.Actionable);
        Assert.Equal(PromptTypes.RoomSetup, waitingPrompt.View?.Type);
        Assert.Equal(["SUBMIT_DECK"], waitingPrompt.Actions);
        Assert.DoesNotContain("READY", waitingPrompt.Actions);
        Assert.Equal(result.State.Tick, waitingPrompt.SnapshotTick);
    }

    private static void AssertOfficialSingleReadyBothDecksPromptQueueAudit(
        ResolutionResult result,
        string readyPlayerId,
        string waitingPlayerId,
        OfficialDecklist readyDecklist,
        OfficialDecklist waitingDecklist)
    {
        Assert.Equal(MatchStatuses.Seating, result.State.Status);
        Assert.Equal(MatchPhases.Room, result.State.Phase);
        Assert.Equal(TimingStates.Room, result.State.TimingState);
        var readyPlayer = Assert.Single(result.State.ReadyPlayerIds);
        Assert.Equal(readyPlayerId, readyPlayer);
        AssertOfficialDecklist(result.State.PlayerDecklists[readyPlayerId], readyDecklist);
        AssertOfficialDecklist(result.State.PlayerDecklists[waitingPlayerId], waitingDecklist);
        AssertRoomSetupIdlePromptQueue(result);

        var readyPrompt = result.Prompts[readyPlayerId];
        Assert.Equal(readyPlayerId, readyPrompt.PlayerId);
        Assert.False(readyPrompt.Actionable);
        Assert.Equal(PromptTypes.RoomSetup, readyPrompt.View?.Type);
        Assert.Equal(["WAIT"], readyPrompt.Actions);
        Assert.DoesNotContain("READY", readyPrompt.Actions);
        Assert.DoesNotContain("SUBMIT_DECK", readyPrompt.Actions);
        Assert.Equal(result.State.Tick, readyPrompt.SnapshotTick);

        var waitingPrompt = result.Prompts[waitingPlayerId];
        Assert.Equal(waitingPlayerId, waitingPrompt.PlayerId);
        Assert.True(waitingPrompt.Actionable);
        Assert.Equal(PromptTypes.RoomSetup, waitingPrompt.View?.Type);
        Assert.Equal(["READY"], waitingPrompt.Actions);
        Assert.DoesNotContain("SUBMIT_DECK", waitingPrompt.Actions);
        Assert.Equal(result.State.Tick, waitingPrompt.SnapshotTick);
    }

    private static void AssertRoomSetupIdlePromptQueue(ResolutionResult result)
    {
        Assert.Null(result.State.PriorityPlayerId);
        Assert.Null(result.State.FocusPlayerId);
        Assert.Empty(result.State.StackItems);
        Assert.Empty(result.State.BattlefieldTasks);
        Assert.False(result.State.PendingTaskQueue.HasTasks);
        Assert.False(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.Null(result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Empty(result.State.PendingTaskQueue.Tasks);

        foreach (var snapshot in result.Snapshots.Values)
        {
            Assert.Equal(result.State.Tick, snapshot.Tick);
            Assert.Equal(MatchPhases.Room, Assert.IsType<string>(snapshot.Timing["phase"]));
            Assert.Equal(TimingStates.Room, Assert.IsType<string>(snapshot.Timing["timingState"]));
            Assert.Null(snapshot.Timing["priorityPlayerId"]);
            Assert.Null(snapshot.Timing["focusPlayerId"]);
            Assert.Empty(snapshot.Stack);

            var queue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
            Assert.False(Assert.IsType<bool>(queue["hasTasks"]));
            Assert.False(Assert.IsType<bool>(queue["isBlocking"]));
            Assert.Equal("IDLE", Assert.IsType<string>(queue["phase"]));
            Assert.Null(queue["activeTaskId"]);
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(queue["tasks"]));
        }
    }

    private static void AssertOfficialDecklist(
        OfficialDecklist actual,
        OfficialDecklist expected)
    {
        Assert.Equal(expected.LegendCardNo, actual.LegendCardNo);
        Assert.Equal(expected.ChampionCardNo, actual.ChampionCardNo);
        Assert.Equal(expected.MainDeck, actual.MainDeck);
        Assert.Equal(expected.RuneDeck, actual.RuneDeck);
        Assert.Equal(expected.Battlefields, actual.Battlefields);
    }

    private static SubmitDeckCommand ToSubmitCommand(OfficialDecklist decklist)
    {
        return new SubmitDeckCommand(
            decklist.LegendCardNo,
            decklist.ChampionCardNo,
            decklist.MainDeck,
            decklist.RuneDeck,
            decklist.Battlefields);
    }

    private static JsonElement RawCommand(string cmdType)
    {
        return JsonSerializer.SerializeToElement(new { cmdType });
    }

    private static JsonElement PromptScopedSubmitDeckRawCommand(
        OfficialDecklist decklist,
        ActionPromptDto prompt,
        long? snapshotTick = null)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType = "SUBMIT_DECK",
            legendCardNo = decklist.LegendCardNo,
            championCardNo = decklist.ChampionCardNo,
            mainDeck = decklist.MainDeck,
            runeDeck = decklist.RuneDeck,
            battlefields = decklist.Battlefields,
            promptId = prompt.PromptId,
            snapshotTick = snapshotTick ?? prompt.SnapshotTick
        });
    }

    private static JsonElement PromptScopedReadyRawCommand(ActionPromptDto prompt, long? snapshotTick = null)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType = "READY",
            promptId = prompt.PromptId,
            snapshotTick = snapshotTick ?? prompt.SnapshotTick
        });
    }

    private static JsonElement PromptScopedBasicRawCommand(
        string cmdType,
        ActionPromptDto prompt,
        long? snapshotTick = null)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType,
            promptId = prompt.PromptId,
            snapshotTick = snapshotTick ?? prompt.SnapshotTick
        });
    }

    private static JsonElement PromptScopedMulliganRawCommand(
        IReadOnlyList<string> handObjectIds,
        ActionPromptDto prompt,
        long? snapshotTick = null)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType = "MULLIGAN",
            handObjectIds,
            promptId = prompt.PromptId,
            snapshotTick = snapshotTick ?? prompt.SnapshotTick
        });
    }

    private static JsonElement PromptIdOnlySubmitDeckRawCommand(OfficialDecklist decklist, ActionPromptDto prompt)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType = "SUBMIT_DECK",
            legendCardNo = decklist.LegendCardNo,
            championCardNo = decklist.ChampionCardNo,
            mainDeck = decklist.MainDeck,
            runeDeck = decklist.RuneDeck,
            battlefields = decklist.Battlefields,
            promptId = prompt.PromptId
        });
    }

    private static JsonElement PromptIdOnlyReadyRawCommand(ActionPromptDto prompt)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType = "READY",
            promptId = prompt.PromptId
        });
    }

    private static JsonElement PromptIdOnlyBasicRawCommand(string cmdType, ActionPromptDto prompt)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType,
            promptId = prompt.PromptId
        });
    }

    private static JsonElement PromptIdOnlyMulliganRawCommand(IReadOnlyList<string> handObjectIds, ActionPromptDto prompt)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType = "MULLIGAN",
            handObjectIds,
            promptId = prompt.PromptId
        });
    }

    private static JsonElement SnapshotOnlySubmitDeckRawCommand(OfficialDecklist decklist, long snapshotTick)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType = "SUBMIT_DECK",
            legendCardNo = decklist.LegendCardNo,
            championCardNo = decklist.ChampionCardNo,
            mainDeck = decklist.MainDeck,
            runeDeck = decklist.RuneDeck,
            battlefields = decklist.Battlefields,
            snapshotTick
        });
    }

    private static JsonElement SnapshotOnlyReadyRawCommand(long snapshotTick)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType = "READY",
            snapshotTick
        });
    }

    private static JsonElement SnapshotOnlyBasicRawCommand(string cmdType, long snapshotTick)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType,
            snapshotTick
        });
    }

    private static JsonElement SnapshotOnlyMulliganRawCommand(IReadOnlyList<string> handObjectIds, long snapshotTick)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType = "MULLIGAN",
            handObjectIds,
            snapshotTick
        });
    }

    private static OfficialDecklist BuildValidDeck(OfficialCardCatalog catalog)
    {
        const string legendCardNo = "UNL-181/219";
        const string championCardNo = "UNL-022/219";
        var legend = catalog.Cards.Single(card => string.Equals(card.CardNo, legendCardNo, StringComparison.Ordinal));
        var allowedColors = legend.CardColorList.ToHashSet(StringComparer.Ordinal);
        var mainDeck = new List<string> { championCardNo };
        var nameCounts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [catalog.Cards.Single(card => string.Equals(card.CardNo, championCardNo, StringComparison.Ordinal)).CardName] = 1
        };
        var candidates = catalog.Cards
            .Where(card => IsMainDeckCandidate(card, allowedColors))
            .Where(card => !string.Equals(card.CardNo, championCardNo, StringComparison.Ordinal))
            .OrderBy(card => card.CardNo, StringComparer.Ordinal)
            .ToArray();

        foreach (var card in candidates)
        {
            while (mainDeck.Count < OfficialDeckValidator.MinimumMainDeckCount
                && (!nameCounts.TryGetValue(card.CardName, out var count) || count < OfficialDeckValidator.DefaultMaxCopiesByName))
            {
                mainDeck.Add(card.CardNo);
                nameCounts[card.CardName] = nameCounts.TryGetValue(card.CardName, out var current) ? current + 1 : 1;
            }

            if (mainDeck.Count >= OfficialDeckValidator.MinimumMainDeckCount)
            {
                break;
            }
        }

        Assert.Equal(OfficialDeckValidator.MinimumMainDeckCount, mainDeck.Count);
        var allowedRunes = catalog.Cards
            .Where(card => string.Equals(card.CardCategoryName, "符文", StringComparison.Ordinal))
            .Where(card => TraitsAllowed(card, allowedColors))
            .OrderBy(card => card.CardNo, StringComparer.Ordinal)
            .Select(card => card.CardNo)
            .ToArray();
        Assert.NotEmpty(allowedRunes);
        var runeDeck = Enumerable.Range(0, OfficialDeckValidator.RuneDeckCount)
            .Select(index => allowedRunes[index % allowedRunes.Length])
            .ToArray();
        var battlefields = catalog.Cards
            .Where(card => string.Equals(card.CardCategoryName, "战场", StringComparison.Ordinal))
            .GroupBy(card => card.CardName, StringComparer.Ordinal)
            .Select(group => group.OrderBy(card => card.CardNo, StringComparer.Ordinal).First())
            .OrderBy(card => card.CardNo, StringComparer.Ordinal)
            .Take(OfficialDeckValidator.BattlefieldCount)
            .Select(card => card.CardNo)
            .ToArray();

        return new OfficialDecklist(legendCardNo, championCardNo, mainDeck, runeDeck, battlefields);
    }

    private static bool IsMainDeckCandidate(OfficialCard card, HashSet<string> allowedColors)
    {
        if (card.CardCategoryName.StartsWith("专属", StringComparison.Ordinal)
            || card.CardGroupLimit == 1
            || card.CardEffect.Contains("{{唯我}}", StringComparison.Ordinal))
        {
            return false;
        }

        return card.CardCategoryName is "单位" or "英雄单位" or "装备" or "法术"
            && TraitsAllowed(card, allowedColors);
    }

    private static bool TraitsAllowed(OfficialCard card, HashSet<string> allowedColors)
    {
        return card.CardColorList.All(color => string.Equals(color, "colorless", StringComparison.Ordinal)
            || allowedColors.Contains(color));
    }

    private static bool HasTraitsOutside(OfficialCard card, HashSet<string> allowedColors)
    {
        return card.CardColorList.Any(color => !string.Equals(color, "colorless", StringComparison.Ordinal)
            && !allowedColors.Contains(color));
    }

    private static IReadOnlyList<string> ReplaceFirst(
        IReadOnlyList<string> values,
        string oldValue,
        string newValue)
    {
        var next = values.ToArray();
        var index = Array.FindIndex(next, value => string.Equals(value, oldValue, StringComparison.Ordinal));
        Assert.True(index >= 0);
        next[index] = newValue;
        return next;
    }
}
