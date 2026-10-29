using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{

    private static JsonElement PromptScopedPlayCardRawCommand(PlayCardCommand command, ActionPromptDto prompt)
    {
        var raw = JsonSerializer.SerializeToNode(command, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        raw["promptId"] = prompt.PromptId;
        raw["snapshotTick"] = prompt.SnapshotTick;
        return JsonSerializer.SerializeToElement(raw);
    }

    private static JsonElement PromptScopedRawCommand(string cmdType, ActionPromptDto prompt)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick
        });
    }

    private sealed class RecordingMatchJournal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];

        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }
    }

    private static async Task<ResolutionResult> P4FinishBattleAssignments(ResolutionResult result,
        IReadOnlyList<CombatDamageAssignmentDto> attackers, IReadOnlyList<CombatDamageAssignmentDto> defenders)
    {
        var events = result.Events.ToList();
        foreach (var (player, assignments) in new[] { ("P1", attackers), ("P2", defenders) })
        {
            var battle = result.State.BattleState;
            result = await new CoreRuleEngine().ResolveAsync(result.State,
                new($"p4-assign-{player}", player, CommandTypes.AssignCombatDamage),
                new AssignCombatDamageCommand(battle.BattleId!, battle.BattlefieldObjectId!, assignments), default);
            Assert.True(result.Accepted, result.ErrorMessage);
            events.AddRange(result.Events);
        }
        return result with { Events = events };
    }

    private static MatchState P4SpellDuelFocusState()
    {
        return PunishmentState(mana: 1) with
        {
            TimingState = TimingStates.SpellDuelOpen,
            FocusPlayerId = "P1",
            PassedFocusPlayerIds = [],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-CLEAVE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-001"] = new("P2-UNIT-001", power: 2, isAttacking: true)
            }
        };
    }

    private static MatchState P4SpellshieldTaxState(int mana, int wardPower = 0)
    {
        return PunishmentState(mana) with
        {
            RunePools = new Dictionary<string,RunePool> { ["P1"] = new(mana, wardPower), ["P2"] = RunePool.Empty },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-INCINERATE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-SPELLSHIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-SPELLSHIELD-UNIT-001"] = new(
                    "P2-SPELLSHIELD-UNIT-001",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield])
            }
        };
    }

    private static MatchState P4MultipleSpellshieldTaxState(int mana)
    {
        return PunishmentState(mana) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-SPIRIT-FIRE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields =
                    [
                        "BF",
                        "P2-SPIRIT-FIRE-SPELLSHIELD-001",
                        "P2-SPIRIT-FIRE-SPELLSHIELD2-001"
                    ]
                }
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState> {
                ["BF"] = new("P2", "BATTLEFIELD", "BF"),
                ["P2-SPIRIT-FIRE-SPELLSHIELD-001"] = new("P2", "BATTLEFIELD", "BF"),
                ["P2-SPIRIT-FIRE-SPELLSHIELD2-001"] = new("P2", "BATTLEFIELD", "BF") },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF"] = new("BF", cardNo: "OGN·296/298", ownerId: "P2", controllerId: "P2", tags: [P6TokenFactoryCatalog.BattlefieldCardTag]),
                ["P2-SPIRIT-FIRE-SPELLSHIELD-001"] = new(
                    "P2-SPIRIT-FIRE-SPELLSHIELD-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield]),
                ["P2-SPIRIT-FIRE-SPELLSHIELD2-001"] = new(
                    "P2-SPIRIT-FIRE-SPELLSHIELD2-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "法盾2"])
            }
        };
    }

    private static MatchState JudgmentDayState()
    {
        return PunishmentState(7) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-JUDGMENT", "P1-HAND-KEEP-001", "P1-HAND-KEEP-002", "P1-HAND-RECYCLE-001"],
                    Base =
                    [
                        "P1-UNIT-KEEP-001",
                        "P1-UNIT-RECYCLE-001",
                        "P1-EQUIPMENT-KEEP-001",
                        "P1-EQUIPMENT-RECYCLE-001",
                        "P1-RUNE-KEEP-001",
                        "P1-RUNE-KEEP-002",
                        "P1-RUNE-RECYCLE-001"
                    ],
                    Battlefields = ["P1-UNIT-KEEP-002", "P1-EQUIPMENT-KEEP-002"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-HAND-KEEP-001", "P2-HAND-KEEP-002", "P2-HAND-RECYCLE-001"],
                    Base =
                    [
                        "P2-UNIT-KEEP-001",
                        "P2-UNIT-RECYCLE-001",
                        "P2-EQUIPMENT-KEEP-001",
                        "P2-EQUIPMENT-RECYCLE-001",
                        "P2-RUNE-KEEP-001",
                        "P2-RUNE-KEEP-002",
                        "P2-RUNE-RECYCLE-001"
                    ],
                    Battlefields = ["P2-UNIT-KEEP-002", "P2-EQUIPMENT-KEEP-002"]
                }
            },
            CardObjects = JudgmentDayCardObjects()
        };
    }

    private static string[] JudgmentDayKeepTargets()
    {
        return
        [
            "P1-UNIT-KEEP-001",
            "P1-UNIT-KEEP-002",
            "P1-EQUIPMENT-KEEP-001",
            "P1-EQUIPMENT-KEEP-002",
            "P1-RUNE-KEEP-001",
            "P1-RUNE-KEEP-002",
            "P1-HAND-KEEP-001",
            "P1-HAND-KEEP-002",
            "P2-UNIT-KEEP-001",
            "P2-UNIT-KEEP-002",
            "P2-EQUIPMENT-KEEP-001",
            "P2-EQUIPMENT-KEEP-002",
            "P2-RUNE-KEEP-001",
            "P2-RUNE-KEEP-002",
            "P2-HAND-KEEP-001",
            "P2-HAND-KEEP-002"
        ];
    }

    private static Dictionary<string, CardObjectState> JudgmentDayCardObjects()
    {
        return new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["P1-UNIT-KEEP-001"] = new("P1-UNIT-KEEP-001", tags: [CardObjectTags.UnitCard]),
            ["P1-UNIT-KEEP-002"] = new("P1-UNIT-KEEP-002", tags: [CardObjectTags.UnitCard]),
            ["P1-UNIT-RECYCLE-001"] = new("P1-UNIT-RECYCLE-001", tags: [CardObjectTags.UnitCard]),
            ["P1-EQUIPMENT-KEEP-001"] = new("P1-EQUIPMENT-KEEP-001", tags: [CardObjectTags.EquipmentCard]),
            ["P1-EQUIPMENT-KEEP-002"] = new("P1-EQUIPMENT-KEEP-002", tags: [CardObjectTags.EquipmentCard]),
            ["P1-EQUIPMENT-RECYCLE-001"] = new("P1-EQUIPMENT-RECYCLE-001", tags: [CardObjectTags.EquipmentCard]),
            ["P1-RUNE-KEEP-001"] = new("P1-RUNE-KEEP-001", tags: [CardObjectTags.RuneCard]),
            ["P1-RUNE-KEEP-002"] = new("P1-RUNE-KEEP-002", tags: [CardObjectTags.RuneCard]),
            ["P1-RUNE-RECYCLE-001"] = new("P1-RUNE-RECYCLE-001", tags: [CardObjectTags.RuneCard]),
            ["P2-UNIT-KEEP-001"] = new("P2-UNIT-KEEP-001", tags: [CardObjectTags.UnitCard]),
            ["P2-UNIT-KEEP-002"] = new("P2-UNIT-KEEP-002", tags: [CardObjectTags.UnitCard]),
            ["P2-UNIT-RECYCLE-001"] = new("P2-UNIT-RECYCLE-001", tags: [CardObjectTags.UnitCard]),
            ["P2-EQUIPMENT-KEEP-001"] = new("P2-EQUIPMENT-KEEP-001", tags: [CardObjectTags.EquipmentCard]),
            ["P2-EQUIPMENT-KEEP-002"] = new("P2-EQUIPMENT-KEEP-002", tags: [CardObjectTags.EquipmentCard]),
            ["P2-EQUIPMENT-RECYCLE-001"] = new("P2-EQUIPMENT-RECYCLE-001", tags: [CardObjectTags.EquipmentCard]),
            ["P2-RUNE-KEEP-001"] = new("P2-RUNE-KEEP-001", tags: [CardObjectTags.RuneCard]),
            ["P2-RUNE-KEEP-002"] = new("P2-RUNE-KEEP-002", tags: [CardObjectTags.RuneCard]),
            ["P2-RUNE-RECYCLE-001"] = new("P2-RUNE-RECYCLE-001", tags: [CardObjectTags.RuneCard])
        };
    }

    private static async Task AssertSimpleEquipmentFixtureAsync(
        string fixtureFileName,
        string equipmentObjectId,
        bool expectedIsExhausted = false,
        string[]? expectedTags = null,
        string? expectedAttachedToObjectId = null)
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        if (expectedAttachedToObjectId is null)
        {
            Assert.Equal([equipmentObjectId], result.FinalState.PlayerZones["P1"].Base);
        }
        else
        {
            Assert.Contains(expectedAttachedToObjectId, result.FinalState.PlayerZones["P1"].Base);
            Assert.Contains(equipmentObjectId, result.FinalState.PlayerZones["P1"].Base);
        }

        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        var expectedEquipmentTags = expectedTags
            ?? ExpectedEquipmentTagsForCard(result.FinalState.CardObjects[equipmentObjectId].CardNo);
        Assert.Equal(expectedEquipmentTags, result.FinalState.CardObjects[equipmentObjectId].Tags);
        Assert.Equal(expectedIsExhausted, result.FinalState.CardObjects[equipmentObjectId].IsExhausted);
        Assert.Equal(expectedAttachedToObjectId, result.FinalState.CardObjects[equipmentObjectId].AttachedToObjectId);
    }

    private static IReadOnlyList<string> ExpectedEquipmentTagsForCard(string? cardNo)
    {
        var tags = new List<string> { CardObjectTags.EquipmentCard };
        if (!string.IsNullOrWhiteSpace(cardNo)
            && CardBehaviorRegistry.TryGetByCardNo(cardNo, out var behavior))
        {
            tags.AddRange(behavior.SourceEquipmentTags
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(tag => !string.IsNullOrWhiteSpace(tag)));
        }

        return tags
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static Task AssertAgileEquipmentMissingTargetRejectedAsync(
        int mana,
        string sourceObjectId,
        string cardNo,
        string targetObjectId) =>
        AssertEquipmentPlayRejectedAsync(
            mana,
            sourceObjectId,
            cardNo,
            targetObjectId,
            targetObjectIds: []);

    private static async Task AssertEquipmentWithTargetRejectedAsync(
        int mana,
        string sourceObjectId,
        string cardNo,
        string targetObjectId) =>
        await AssertEquipmentPlayRejectedAsync(
            mana,
            sourceObjectId,
            cardNo,
            targetObjectId,
            [targetObjectId]);

    private static async Task AssertEquipmentPlayRejectedAsync(
        int mana,
        string sourceObjectId,
        string cardNo,
        string targetObjectId,
        IReadOnlyList<string> targetObjectIds)
    {
        var state = PunishmentState(mana) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [sourceObjectId],
                    Base = [targetObjectId]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [targetObjectId] = new(
                    targetObjectId,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-{sourceObjectId.ToLowerInvariant()}-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                sourceObjectId,
                cardNo,
                targetObjectIds),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(mana, 0), result.State.RunePools["P1"]);
        Assert.Equal([sourceObjectId], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    private static async Task AssertSourceUnitWithTargetRejectedAsync(
        int mana,
        string sourceObjectId,
        string cardNo,
        string targetObjectId,
        string mode = "")
    {
        var state = PunishmentState(mana) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [sourceObjectId],
                    Base = [targetObjectId]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [targetObjectId] = new(
                    targetObjectId,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-{sourceObjectId.ToLowerInvariant()}-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                sourceObjectId,
                cardNo,
                [targetObjectId],
                mode),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(mana, 0), result.State.RunePools["P1"]);
        Assert.Equal([sourceObjectId], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    private static async Task AssertSourceUnitNonUnitTargetRejectedAsync(
        int mana,
        string sourceObjectId,
        string cardNo,
        string targetObjectId)
    {
        var state = PunishmentState(mana) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [sourceObjectId],
                    Base = [targetObjectId]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [targetObjectId] = new(
                    targetObjectId,
                    tags: [CardObjectTags.EquipmentCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-{sourceObjectId.ToLowerInvariant()}-non-unit-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                sourceObjectId,
                cardNo,
                [targetObjectId]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(mana, 0), result.State.RunePools["P1"]);
        Assert.Equal([sourceObjectId], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    private static MatchState CenterStageState(int mana)
    {
        return new MatchState(
            "p2-center-stage-room",
            0,
            7,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            ["P1", "P2"],
            "P1",
            MatchPhases.Main,
            TimingStates.NeutralOpen,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(mana, 0),
                ["P2"] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-DRAW-001", "P1-DRAW-002"],
                    Hand = ["P1-SPELL-CENTER-STAGE"]
                },
                ["P2"] = PlayerZones.Empty
            },
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal));
    }

    private static ConformanceFixture BuildPromptComparisonFixture(
        IReadOnlyDictionary<string, IReadOnlyList<string>> promptActions,
        IReadOnlyDictionary<string, ConformanceExpectedPrompt>? prompts = null)
    {
        return new ConformanceFixture(
            2,
            "prompt-comparison",
            "Prompt comparison harness semantics check.",
            "manual-test",
            "prompt-comparison-room",
            ["P1", "P2"],
            [],
            new ConformanceExpected(
                0,
                [],
                promptActions,
                Prompts: prompts));
    }

    private static ConformanceRunResult BuildPromptComparisonResult(
        IReadOnlyList<string> p1Actions,
        IReadOnlyList<string>? p2Actions = null)
    {
        var state = new MatchState(
            "prompt-comparison-room",
            0,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            });
        var prompts = new Dictionary<string, ActionPromptDto>(StringComparer.Ordinal)
        {
            ["P1"] = new("P1", true, "test", p1Actions),
            ["P2"] = new("P2", false, "test", p2Actions ?? ["WAIT"])
        };

        return new ConformanceRunResult(
            0,
            [],
            [],
            prompts,
            state);
    }

    private static int EventIndex(IReadOnlyList<GameEvent> events, Predicate<GameEvent> predicate)
    {
        for (var index = 0; index < events.Count; index++)
        {
            if (predicate(events[index]))
            {
                return index;
            }
        }

        Assert.Fail("Expected event was not emitted.");
        return -1;
    }

    private sealed class CapturingRuleEngine : IRuleEngine
    {
        public MatchState? CapturedState { get; private set; }

        public ValueTask<ResolutionResult> ResolveAsync(
            MatchState state,
            PlayerIntent intent,
            GameCommand command,
            CancellationToken cancellationToken)
        {
            CapturedState = state;
            var nextState = state with
            {
                Tick = state.Tick + 1
            };
            return ValueTask.FromResult(new ResolutionResult(
                true,
                null,
                nextState,
                [],
                ResolutionResult.BuildSnapshots(nextState),
                ResolutionResult.BuildPrompts(nextState)));
        }
    }

    private static IReadOnlyDictionary<string, CardObjectState> ReplaceCardObject(
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string objectId,
        CardObjectState replacement)
    {
        var next = cardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        next[objectId] = replacement;
        return next;
    }

    private static IReadOnlyDictionary<string, ObjectLocationState> ReplaceObjectLocation(
        IReadOnlyDictionary<string, ObjectLocationState> locations,
        string objectId,
        ObjectLocationState replacement)
    {
        var next = locations.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        next[objectId] = replacement;
        return next;
    }

    private static void AssertBattlefieldStaticAuraSnapshot(
        SnapshotDto snapshot,
        string sourceObjectId,
        params string[] participantObjectIds)
    {
        var continuousEffects = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(
            snapshot.Timing["continuousEffects"]);
        var staticAuraEffects = continuousEffects
            .Where(effect => string.Equals(effect["layer"] as string, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect["sourceObjectId"] as string, sourceObjectId, StringComparison.Ordinal))
            .OrderBy(effect => effect["targetObjectId"] as string, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(participantObjectIds.Length, staticAuraEffects.Length);
        Assert.Equal(participantObjectIds, staticAuraEffects.Select(effect => effect["targetObjectId"] as string).ToArray());
        Assert.All(
            staticAuraEffects,
            effect =>
            {
                Assert.Equal("BATTLEFIELD", Assert.IsType<string>(effect["scope"]));
                Assert.Equal(1, Assert.IsType<int>(effect["powerDelta"]));
                Assert.Equal("BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE", Assert.IsType<string>(effect["effectKind"]));
                Assert.Equal("SOURCE_BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE_AND_PARTICIPANT_UNIT_AT_BATTLEFIELD", Assert.IsType<string>(effect["condition"]));
                Assert.Equal("DERIVED_FROM_CURRENT_BATTLEFIELD_OBJECT_LOCATIONS", Assert.IsType<string>(effect["lifecycle"]));
                Assert.Equal(
                    participantObjectIds,
                    Assert.IsAssignableFrom<IReadOnlyList<string>>(effect["participantObjectIds"]));
            });
    }
}
