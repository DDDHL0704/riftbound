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
        }, catalog, "mainDeck must contain at least 40 cards.");

        AssertInvalid(valid with
        {
            RuneDeck = valid.RuneDeck.Take(11).ToArray()
        }, catalog, "runeDeck must contain exactly 12 rune cards.");

        AssertInvalid(valid with
        {
            ChampionCardNo = "UNL-024/219",
            MainDeck = ReplaceFirst(valid.MainDeck, valid.ChampionCardNo, "UNL-024/219")
        }, catalog, "champion hero tag must match");

        var firstNonChampion = valid.MainDeck.First(cardNo => !string.Equals(cardNo, valid.ChampionCardNo, StringComparison.Ordinal));
        AssertInvalid(valid with
        {
            MainDeck = valid.MainDeck.Concat([firstNonChampion]).ToArray()
        }, catalog, "maximum is 3.");

        AssertInvalid(valid with
        {
            Battlefields = [valid.Battlefields[0], valid.Battlefields[0], valid.Battlefields[1]]
        }, catalog, "battlefields cannot contain duplicate battlefield name");
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
        var activeMulligan = await session.SubmitAsync(
            activePlayerId,
            "mulligan-active",
            new MulliganCommand(activeHandBefore.Take(2).ToArray()),
            RawCommand("MULLIGAN"),
            CancellationToken.None);
        Assert.True(activeMulligan.Accepted);
        Assert.Equal(MatchPhases.Mulligan, activeMulligan.State.Phase);
        Assert.Contains(activePlayerId, activeMulligan.State.MulliganCompletedPlayerIds);
        Assert.True(activeMulligan.Prompts[secondPlayerId].Actionable);

        var secondHandBefore = activeMulligan.State.PlayerZones[secondPlayerId].Hand.ToArray();
        var secondMulligan = await session.SubmitAsync(
            secondPlayerId,
            "mulligan-second",
            new MulliganCommand(secondHandBefore.Take(1).ToArray()),
            RawCommand("MULLIGAN"),
            CancellationToken.None);

        Assert.True(secondMulligan.Accepted);
        Assert.Equal(MatchPhases.Main, secondMulligan.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, secondMulligan.State.TimingState);
        Assert.Equal(activePlayerId, secondMulligan.State.ActivePlayerId);
        Assert.Equal(2, secondMulligan.State.PlayerZones[activePlayerId].Base.Count);
        Assert.Contains(secondMulligan.Events, gameEvent => string.Equals(gameEvent.Kind, "MULLIGAN_PHASE_COMPLETED", StringComparison.Ordinal));
        Assert.Contains(secondMulligan.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
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
            "MOVE_UNIT source precise battlefield location does not match the authoritative location.",
            result.ErrorMessage);
        Assert.Equal("P1-BATTLEFIELD-A", result.State.ObjectLocations["P1-UNIT-ROAM"].BattlefieldObjectId);
    }

    [Fact]
    public async Task MoveUnitRunsLethalCleanupAndReconcilesObjectLocation()
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

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BATTLEFIELD", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));
        Assert.DoesNotContain("P1-DAMAGED-UNIT", result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P1-DAMAGED-UNIT"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("GRAVEYARD", result.State.ObjectLocations["P1-DAMAGED-UNIT"].Zone);
        Assert.DoesNotContain(
            result.State.PendingCleanupTasks,
            task => string.Equals(task.ObjectId, "P1-DAMAGED-UNIT", StringComparison.Ordinal));
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
