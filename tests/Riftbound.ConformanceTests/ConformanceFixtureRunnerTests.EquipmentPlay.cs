using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{

    [Fact]
    public async Task CoreRuleEnginePlaysPoroSnaxEquipmentDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-poro-snax-equipment-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-PORO-SNAX"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-DRAW-PORO-SNAX-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-PORO-SNAX"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsPoroSnaxWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-PORO-SNAX"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-PORO-SNAX-TARGET-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-PORO-SNAX-TARGET-001"] = new(
                    "P2-PORO-SNAX-TARGET-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-poro-snax-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-PORO-SNAX",
                "SFD·046/221",
                ["P2-PORO-SNAX-TARGET-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-PORO-SNAX"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4PoroSnaxTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-poro-snax-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-DRAW-PORO-SNAX-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-PORO-SNAX"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P2-PORO-SNAX-TARGET-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P2-PORO-SNAX-TARGET-001"].Damage);
        Assert.Equal(2, result.FinalState.CardObjects["P2-PORO-SNAX-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P2-PORO-SNAX-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysShurelyasRequiemEquipmentReadyAll()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-shurelyas-requiem-equipment-ready-all.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-SHURELYA-BASE-UNIT-001", "P1-EQUIPMENT-SHURELYAS-REQUIEM"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.False(result.FinalState.CardObjects["P1-SHURELYA-BASE-UNIT-001"].IsExhausted);
        Assert.False(result.FinalState.CardObjects["P1-SHURELYA-BATTLEFIELD-UNIT-001"].IsExhausted);
        Assert.True(result.FinalState.CardObjects["P2-SHURELYA-BASE-UNIT-001"].IsExhausted);
        Assert.Equal([CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon], result.FinalState.CardObjects["P1-EQUIPMENT-SHURELYAS-REQUIEM"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsShurelyasRequiemWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-SHURELYAS-REQUIEM"],
                    Base = ["P1-SHURELYA-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SHURELYA-BASE-UNIT-001"] = new(
                    "P1-SHURELYA-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-shurelyas-requiem-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-SHURELYAS-REQUIEM",
                "SFD·192/221",
                ["P1-SHURELYA-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(4, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-SHURELYAS-REQUIEM"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ShurelyasRequiemTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-shurelyas-requiem-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-SHURELYAS-REQUIEM"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SHURELYA-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SHURELYA-BASE-UNIT-001"].Damage);
        Assert.Equal(2, result.FinalState.CardObjects["P1-SHURELYA-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-SHURELYA-BASE-UNIT-001"].Tags);
        Assert.True(result.FinalState.CardObjects["P1-SHURELYA-BASE-UNIT-001"].IsExhausted);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysFutureForgeEquipmentCreateMinion()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-future-forge-equipment-create-minion.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-EQUIPMENT-FUTURE-FORGE", "P1-EQUIPMENT-FUTURE-FORGE-TOKEN-001"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-FUTURE-FORGE"].Tags);
        Assert.Equal(1, result.FinalState.CardObjects["P1-EQUIPMENT-FUTURE-FORGE-TOKEN-001"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.MinionTokenFamily, "随从"],
            result.FinalState.CardObjects["P1-EQUIPMENT-FUTURE-FORGE-TOKEN-001"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsFutureForgeWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-FUTURE-FORGE"],
                    Base = ["P1-FUTURE-FORGE-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-FUTURE-FORGE-BASE-UNIT-001"] = new(
                    "P1-FUTURE-FORGE-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-future-forge-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-FUTURE-FORGE",
                "OGN·212/298",
                ["P1-FUTURE-FORGE-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-FUTURE-FORGE"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4FutureForgeTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-future-forge-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-FUTURE-FORGE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-FUTURE-FORGE-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-FUTURE-FORGE-BASE-UNIT-001"].Damage);
        Assert.Equal(2, result.FinalState.CardObjects["P1-FUTURE-FORGE-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-FUTURE-FORGE-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysScrapHeapEquipmentDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-scrap-heap-equipment-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-SCRAP-HEAP"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-DRAW-SCRAP-HEAP-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-SCRAP-HEAP"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsScrapHeapWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-SCRAP-HEAP"],
                    Base = ["P1-SCRAP-HEAP-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SCRAP-HEAP-BASE-UNIT-001"] = new(
                    "P1-SCRAP-HEAP-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-scrap-heap-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-SCRAP-HEAP",
                "OGN·182/298",
                ["P1-SCRAP-HEAP-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-SCRAP-HEAP"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ScrapHeapTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-scrap-heap-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-DRAW-SCRAP-HEAP-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-SCRAP-HEAP"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SCRAP-HEAP-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SCRAP-HEAP-BASE-UNIT-001"].Damage);
        Assert.Equal(2, result.FinalState.CardObjects["P1-SCRAP-HEAP-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-SCRAP-HEAP-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSpriteLanternEquipmentCreateSprite()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-sprite-lantern-equipment-create-sprite.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-EQUIPMENT-SPRITE-LANTERN", "P1-EQUIPMENT-SPRITE-LANTERN-TOKEN-001"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            [CardObjectTags.EquipmentCard, CardObjectTags.Ephemeral],
            result.FinalState.CardObjects["P1-EQUIPMENT-SPRITE-LANTERN"].Tags);
        Assert.Equal(3, result.FinalState.CardObjects["P1-EQUIPMENT-SPRITE-LANTERN-TOKEN-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard, "仙灵", CardObjectTags.Ephemeral], result.FinalState.CardObjects["P1-EQUIPMENT-SPRITE-LANTERN-TOKEN-001"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSpriteLanternWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-SPRITE-LANTERN"],
                    Base = ["P1-SPRITE-LANTERN-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPRITE-LANTERN-BASE-UNIT-001"] = new(
                    "P1-SPRITE-LANTERN-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-sprite-lantern-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-SPRITE-LANTERN",
                "UNL-078/219",
                ["P1-SPRITE-LANTERN-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-SPRITE-LANTERN"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4SpriteLanternTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sprite-lantern-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-SPRITE-LANTERN"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SPRITE-LANTERN-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SPRITE-LANTERN-BASE-UNIT-001"].Damage);
        Assert.Equal(2, result.FinalState.CardObjects["P1-SPRITE-LANTERN-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-SPRITE-LANTERN-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSumpworksMapEquipmentEphemeral()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-sumpworks-map-equipment-ephemeral.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-SUMPWORKS-MAP"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            [CardObjectTags.EquipmentCard, CardObjectTags.Ephemeral],
            result.FinalState.CardObjects["P1-EQUIPMENT-SUMPWORKS-MAP"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSumpworksMapWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-SUMPWORKS-MAP"],
                    Base = ["P1-SUMPWORKS-MAP-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SUMPWORKS-MAP-BASE-UNIT-001"] = new(
                    "P1-SUMPWORKS-MAP-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-sumpworks-map-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-SUMPWORKS-MAP",
                "UNL-085/219",
                ["P1-SUMPWORKS-MAP-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-SUMPWORKS-MAP"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4SumpworksMapTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sumpworks-map-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-SUMPWORKS-MAP"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SUMPWORKS-MAP-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SUMPWORKS-MAP-BASE-UNIT-001"].Damage);
        Assert.Equal(2, result.FinalState.CardObjects["P1-SUMPWORKS-MAP-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-SUMPWORKS-MAP-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysScryingBlossomEquipmentExhausted()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-scrying-blossom-equipment-exhausted.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-SCRYING-BLOSSOM"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-SCRYING-BLOSSOM"].Tags);
        Assert.True(result.FinalState.CardObjects["P1-EQUIPMENT-SCRYING-BLOSSOM"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsScryingBlossomWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-SCRYING-BLOSSOM"],
                    Base = ["P1-SCRYING-BLOSSOM-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SCRYING-BLOSSOM-BASE-UNIT-001"] = new(
                    "P1-SCRYING-BLOSSOM-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-scrying-blossom-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-SCRYING-BLOSSOM",
                "UNL-136/219",
                ["P1-SCRYING-BLOSSOM-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-SCRYING-BLOSSOM"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ScryingBlossomTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-scrying-blossom-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-SCRYING-BLOSSOM"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SCRYING-BLOSSOM-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SCRYING-BLOSSOM-BASE-UNIT-001"].Damage);
        Assert.Equal(2, result.FinalState.CardObjects["P1-SCRYING-BLOSSOM-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-SCRYING-BLOSSOM-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysMagicBeansEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-magic-beans-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-MAGIC-BEANS"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-MAGIC-BEANS"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsMagicBeansWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-MAGIC-BEANS"],
                    Base = ["P1-MAGIC-BEANS-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-MAGIC-BEANS-BASE-UNIT-001"] = new(
                    "P1-MAGIC-BEANS-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-magic-beans-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-MAGIC-BEANS",
                "UNL-011/219",
                ["P1-MAGIC-BEANS-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-MAGIC-BEANS"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4MagicBeansTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-magic-beans-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-MAGIC-BEANS"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-MAGIC-BEANS-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-MAGIC-BEANS-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-MAGIC-BEANS-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-MAGIC-BEANS-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSteelBallistaEquipmentExhausted()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-steel-ballista-equipment-exhausted.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-STEEL-BALLISTA"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-STEEL-BALLISTA"].Tags);
        Assert.True(result.FinalState.CardObjects["P1-EQUIPMENT-STEEL-BALLISTA"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSteelBallistaWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-STEEL-BALLISTA"],
                    Base = ["P1-STEEL-BALLISTA-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-STEEL-BALLISTA-BASE-UNIT-001"] = new(
                    "P1-STEEL-BALLISTA-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-steel-ballista-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-STEEL-BALLISTA",
                "OGN·017/298",
                ["P1-STEEL-BALLISTA-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-STEEL-BALLISTA"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4SteelBallistaTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-steel-ballista-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-STEEL-BALLISTA"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-STEEL-BALLISTA-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-STEEL-BALLISTA-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-STEEL-BALLISTA-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-STEEL-BALLISTA-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHeartOfIceEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-heart-of-ice-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-HEART-OF-ICE"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-HEART-OF-ICE"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsHeartOfIceWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-HEART-OF-ICE"],
                    Base = ["P1-HEART-OF-ICE-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HEART-OF-ICE-BASE-UNIT-001"] = new(
                    "P1-HEART-OF-ICE-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-heart-of-ice-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-HEART-OF-ICE",
                "SFD·052/221",
                ["P1-HEART-OF-ICE-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-HEART-OF-ICE"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4HeartOfIceTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-heart-of-ice-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-HEART-OF-ICE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-HEART-OF-ICE-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-HEART-OF-ICE-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-HEART-OF-ICE-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-HEART-OF-ICE-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysRemorseOrbEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-remorse-orb-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-REMORSE-ORB"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-REMORSE-ORB"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsRemorseOrbWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-REMORSE-ORB"],
                    Base = ["P1-REMORSE-ORB-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-REMORSE-ORB-BASE-UNIT-001"] = new(
                    "P1-REMORSE-ORB-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-remorse-orb-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-REMORSE-ORB",
                "OGN·090/298",
                ["P1-REMORSE-ORB-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-REMORSE-ORB"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4RemorseOrbTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-remorse-orb-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-REMORSE-ORB"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-REMORSE-ORB-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-REMORSE-ORB-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-REMORSE-ORB-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-REMORSE-ORB-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSoulSwordEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-soul-sword-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-SOUL-SWORD"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(ExpectedEquipmentTagsForCard("UNL-039/219"), result.FinalState.CardObjects["P1-EQUIPMENT-SOUL-SWORD"].Tags);
        Assert.False(result.FinalState.CardObjects["P1-EQUIPMENT-SOUL-SWORD"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSoulSwordWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-SOUL-SWORD"],
                    Base = ["P1-SOUL-SWORD-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SOUL-SWORD-BASE-UNIT-001"] = new(
                    "P1-SOUL-SWORD-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-soul-sword-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-SOUL-SWORD",
                "UNL-039/219",
                ["P1-SOUL-SWORD-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-SOUL-SWORD"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4SoulSwordTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-soul-sword-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-SOUL-SWORD"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SOUL-SWORD-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SOUL-SWORD-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SOUL-SWORD-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-SOUL-SWORD-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysJaggedDirkEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-jagged-dirk-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-JAGGED-DIRK"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(ExpectedEquipmentTagsForCard("SFD·009/221"), result.FinalState.CardObjects["P1-EQUIPMENT-JAGGED-DIRK"].Tags);
        Assert.False(result.FinalState.CardObjects["P1-EQUIPMENT-JAGGED-DIRK"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsJaggedDirkWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-JAGGED-DIRK"],
                    Base = ["P1-JAGGED-DIRK-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-JAGGED-DIRK-BASE-UNIT-001"] = new(
                    "P1-JAGGED-DIRK-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-jagged-dirk-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-JAGGED-DIRK",
                "SFD·009/221",
                ["P1-JAGGED-DIRK-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-JAGGED-DIRK"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4JaggedDirkTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-jagged-dirk-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-JAGGED-DIRK"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-JAGGED-DIRK-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-JAGGED-DIRK-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-JAGGED-DIRK-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-JAGGED-DIRK-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDoransShieldEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-dorans-shield-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-DORANS-SHIELD"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(ExpectedEquipmentTagsForCard("SFD·033/221"), result.FinalState.CardObjects["P1-EQUIPMENT-DORANS-SHIELD"].Tags);
        Assert.False(result.FinalState.CardObjects["P1-EQUIPMENT-DORANS-SHIELD"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsDoransShieldWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-DORANS-SHIELD"],
                    Base = ["P1-DORANS-SHIELD-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-DORANS-SHIELD-BASE-UNIT-001"] = new(
                    "P1-DORANS-SHIELD-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-dorans-shield-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-DORANS-SHIELD",
                "SFD·033/221",
                ["P1-DORANS-SHIELD-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-DORANS-SHIELD"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DoransShieldTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-dorans-shield-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-DORANS-SHIELD"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-DORANS-SHIELD-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-DORANS-SHIELD-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-DORANS-SHIELD-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-DORANS-SHIELD-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHextechInfusedBulwarkEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-hextech-infused-bulwark-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-HEXTECH-INFUSED-BULWARK"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(ExpectedEquipmentTagsForCard("SFD·073/221"), result.FinalState.CardObjects["P1-EQUIPMENT-HEXTECH-INFUSED-BULWARK"].Tags);
        Assert.False(result.FinalState.CardObjects["P1-EQUIPMENT-HEXTECH-INFUSED-BULWARK"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsHextechInfusedBulwarkWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-HEXTECH-INFUSED-BULWARK"],
                    Base = ["P1-HEXTECH-INFUSED-BULWARK-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HEXTECH-INFUSED-BULWARK-BASE-UNIT-001"] = new(
                    "P1-HEXTECH-INFUSED-BULWARK-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-hextech-infused-bulwark-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-HEXTECH-INFUSED-BULWARK",
                "SFD·073/221",
                ["P1-HEXTECH-INFUSED-BULWARK-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-HEXTECH-INFUSED-BULWARK"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4HextechInfusedBulwarkTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-hextech-infused-bulwark-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-HEXTECH-INFUSED-BULWARK"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-HEXTECH-INFUSED-BULWARK-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-HEXTECH-INFUSED-BULWARK-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-HEXTECH-INFUSED-BULWARK-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-HEXTECH-INFUSED-BULWARK-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDoransBladeEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-dorans-blade-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-DORANS-BLADE"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(ExpectedEquipmentTagsForCard("SFD·095/221"), result.FinalState.CardObjects["P1-EQUIPMENT-DORANS-BLADE"].Tags);
        Assert.False(result.FinalState.CardObjects["P1-EQUIPMENT-DORANS-BLADE"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsDoransBladeWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-DORANS-BLADE"],
                    Base = ["P1-DORANS-BLADE-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-DORANS-BLADE-BASE-UNIT-001"] = new(
                    "P1-DORANS-BLADE-BASE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-dorans-blade-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-DORANS-BLADE",
                "SFD·095/221",
                ["P1-DORANS-BLADE-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-DORANS-BLADE"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DoransBladeTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-dorans-blade-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-DORANS-BLADE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-DORANS-BLADE-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-DORANS-BLADE-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-DORANS-BLADE-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-DORANS-BLADE-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysDoransRingEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-dorans-ring-equipment.fixture.json",
            "P1-EQUIPMENT-DORANS-RING");

    [Fact]
    public Task CoreRuleEngineRejectsDoransRingWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            1,
            "P1-EQUIPMENT-DORANS-RING",
            "SFD·124/221",
            "P1-DORANS-RING-BASE-UNIT-001");

    [Fact]
    public async Task P4DoransRingTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-dorans-ring-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-DORANS-RING"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-DORANS-RING-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-DORANS-RING-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-DORANS-RING-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-DORANS-RING-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysVanguardsEyeEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-vanguards-eye-equipment.fixture.json",
            "P1-EQUIPMENT-VANGUARDS-EYE");

    [Fact]
    public Task CoreRuleEngineRejectsVanguardsEyeWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            1,
            "P1-EQUIPMENT-VANGUARDS-EYE",
            "SFD·153/221",
            "P1-VANGUARDS-EYE-BASE-UNIT-001");

    [Fact]
    public async Task P4VanguardsEyeTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-vanguards-eye-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-VANGUARDS-EYE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-VANGUARDS-EYE-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-VANGUARDS-EYE-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-VANGUARDS-EYE-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-VANGUARDS-EYE-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysRecurveBowEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-recurve-bow-equipment.fixture.json",
            "P1-EQUIPMENT-RECURVE-BOW");

    [Fact]
    public Task CoreRuleEngineRejectsRecurveBowWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-RECURVE-BOW",
            "SFD·016/221",
            "P1-RECURVE-BOW-BASE-UNIT-001");

    [Fact]
    public async Task P4RecurveBowTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-recurve-bow-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-RECURVE-BOW"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-RECURVE-BOW-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-RECURVE-BOW-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-RECURVE-BOW-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-RECURVE-BOW-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysLongSwordAgileEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-long-sword-agile-equipment.fixture.json",
            "P1-EQUIPMENT-LONG-SWORD",
            expectedTags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
            expectedAttachedToObjectId: "P1-LONG-SWORD-ATTACH-TARGET");

    [Fact]
    public Task CoreRuleEngineRejectsLongSwordWhenRequiredAgileTargetIsMissing() =>
        AssertAgileEquipmentMissingTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-LONG-SWORD",
            "SFD·022/221",
            "P1-LONG-SWORD-BASE-UNIT-001");

    [Fact]
    public async Task P4LongSwordTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-long-sword-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-LONG-SWORD-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-LONG-SWORD"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-LONG-SWORD-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-LONG-SWORD-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-LONG-SWORD-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-LONG-SWORD-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysSteraksGageAgileEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-steraks-gage-agile-equipment.fixture.json",
            "P1-EQUIPMENT-STERAKS-GAGE",
            expectedTags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
            expectedAttachedToObjectId: "P1-STERAKS-GAGE-ATTACH-TARGET");

    [Fact]
    public Task CoreRuleEngineRejectsSteraksGageWhenRequiredAgileTargetIsMissing() =>
        AssertAgileEquipmentMissingTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-STERAKS-GAGE",
            "SFD·056/221",
            "P1-STERAKS-GAGE-BASE-UNIT-001");

    [Fact]
    public async Task P4SteraksGageTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-steraks-gage-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-STERAKS-GAGE-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-STERAKS-GAGE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-STERAKS-GAGE-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-STERAKS-GAGE-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-STERAKS-GAGE-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-STERAKS-GAGE-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysClothArmorAgileEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-cloth-armor-agile-equipment.fixture.json",
            "P1-EQUIPMENT-CLOTH-ARMOR",
            expectedTags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
            expectedAttachedToObjectId: "P1-CLOTH-ARMOR-ATTACH-TARGET");

    [Fact]
    public Task CoreRuleEngineRejectsClothArmorWhenRequiredAgileTargetIsMissing() =>
        AssertAgileEquipmentMissingTargetRejectedAsync(
            1,
            "P1-EQUIPMENT-CLOTH-ARMOR",
            "SFD·064/221",
            "P1-CLOTH-ARMOR-BASE-UNIT-001");

    [Fact]
    public async Task P4ClothArmorTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-cloth-armor-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-CLOTH-ARMOR-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-CLOTH-ARMOR"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-CLOTH-ARMOR-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-CLOTH-ARMOR-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-CLOTH-ARMOR-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-CLOTH-ARMOR-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysSpinningAxeAgileEphemeralEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-spinning-axe-agile-ephemeral-equipment.fixture.json",
            "P1-EQUIPMENT-SPINNING-AXE",
            expectedTags: [CardObjectTags.EquipmentCard, "武装", "灵便", CardObjectTags.Ephemeral],
            expectedAttachedToObjectId: "P1-SPINNING-AXE-ATTACH-TARGET");

    [Fact]
    public Task CoreRuleEngineRejectsSpinningAxeWhenRequiredAgileTargetIsMissing() =>
        AssertAgileEquipmentMissingTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-SPINNING-AXE",
            "SFD·186/221",
            "P1-SPINNING-AXE-BASE-UNIT-001");

    [Fact]
    public async Task P4SpinningAxeTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-spinning-axe-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPINNING-AXE-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-SPINNING-AXE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SPINNING-AXE-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SPINNING-AXE-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SPINNING-AXE-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-SPINNING-AXE-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysShepherdsHeirloomWeaponEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-shepherds-heirloom-weapon-equipment.fixture.json",
            "P1-EQUIPMENT-SHEPHERDS-HEIRLOOM",
            expectedTags: [CardObjectTags.EquipmentCard, "武装"]);

    [Fact]
    public Task CoreRuleEngineRejectsShepherdsHeirloomWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-SHEPHERDS-HEIRLOOM",
            "UNL-158/219",
            "P1-SHEPHERDS-HEIRLOOM-BASE-UNIT-001");

    [Fact]
    public async Task P4ShepherdsHeirloomTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-shepherds-heirloom-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(0, result.FinalState.PlayerExperience["P1"]);
        Assert.Equal(0, result.FinalState.PlayerExperience["P2"]);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SHEPHERDS-HEIRLOOM-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-SHEPHERDS-HEIRLOOM"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SHEPHERDS-HEIRLOOM-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SHEPHERDS-HEIRLOOM-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SHEPHERDS-HEIRLOOM-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-SHEPHERDS-HEIRLOOM-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysBrutalizerEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-brutalizer-equipment.fixture.json",
            "P1-EQUIPMENT-BRUTALIZER");

    [Fact]
    public Task CoreRuleEngineRejectsBrutalizerWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-BRUTALIZER",
            "SFD·042/221",
            "P1-BRUTALIZER-BASE-UNIT-001");

    [Fact]
    public async Task P4BrutalizerTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-brutalizer-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BRUTALIZER-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-BRUTALIZER"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BRUTALIZER-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BRUTALIZER-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BRUTALIZER-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BRUTALIZER-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysGuardianAngelEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-guardian-angel-equipment.fixture.json",
            "P1-EQUIPMENT-GUARDIAN-ANGEL");

    [Fact]
    public Task CoreRuleEngineRejectsGuardianAngelWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-GUARDIAN-ANGEL",
            "SFD·051/221",
            "P1-GUARDIAN-ANGEL-BASE-UNIT-001");

    [Fact]
    public async Task P4GuardianAngelTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-guardian-angel-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-GUARDIAN-ANGEL-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-GUARDIAN-ANGEL"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GUARDIAN-ANGEL-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-GUARDIAN-ANGEL-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-GUARDIAN-ANGEL-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-GUARDIAN-ANGEL-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysHexdrinkerEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-hexdrinker-equipment.fixture.json",
            "P1-EQUIPMENT-HEXDRINKER");

    [Fact]
    public Task CoreRuleEngineRejectsHexdrinkerWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-HEXDRINKER",
            "SFD·102/221",
            "P1-HEXDRINKER-BASE-UNIT-001");

    [Fact]
    public async Task P4HexdrinkerTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-hexdrinker-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HEXDRINKER-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-HEXDRINKER"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-HEXDRINKER-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-HEXDRINKER-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-HEXDRINKER-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-HEXDRINKER-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysWarmogsArmorEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-warmogs-armor-equipment.fixture.json",
            "P1-EQUIPMENT-WARMOGS-ARMOR");

    [Fact]
    public Task CoreRuleEngineRejectsWarmogsArmorWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            1,
            "P1-EQUIPMENT-WARMOGS-ARMOR",
            "SFD·108/221",
            "P1-WARMOGS-ARMOR-BASE-UNIT-001");

    [Fact]
    public async Task P4WarmogsArmorTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-warmogs-armor-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-WARMOGS-ARMOR-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-WARMOGS-ARMOR"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-WARMOGS-ARMOR-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-WARMOGS-ARMOR-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-WARMOGS-ARMOR-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-WARMOGS-ARMOR-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysTrinityForceEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-trinity-force-equipment.fixture.json",
            "P1-EQUIPMENT-TRINITY-FORCE");

    [Fact]
    public Task CoreRuleEngineRejectsTrinityForceWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            4,
            "P1-EQUIPMENT-TRINITY-FORCE",
            "SFD·115/221",
            "P1-TRINITY-FORCE-BASE-UNIT-001");

    [Fact]
    public async Task P4TrinityForceTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-trinity-force-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-TRINITY-FORCE-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-TRINITY-FORCE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-TRINITY-FORCE-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-TRINITY-FORCE-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-TRINITY-FORCE-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-TRINITY-FORCE-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysBootsOfSwiftnessEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-boots-of-swiftness-equipment.fixture.json",
            "P1-EQUIPMENT-BOOTS-OF-SWIFTNESS");

    [Fact]
    public Task CoreRuleEngineRejectsBootsOfSwiftnessWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-BOOTS-OF-SWIFTNESS",
            "SFD·133/221",
            "P1-BOOTS-OF-SWIFTNESS-BASE-UNIT-001");

    [Fact]
    public async Task P4BootsOfSwiftnessTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-boots-of-swiftness-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BOOTS-OF-SWIFTNESS-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-BOOTS-OF-SWIFTNESS"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BOOTS-OF-SWIFTNESS-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BOOTS-OF-SWIFTNESS-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BOOTS-OF-SWIFTNESS-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BOOTS-OF-SWIFTNESS-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysCullEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-cull-equipment.fixture.json",
            "P1-EQUIPMENT-CULL");

    [Fact]
    public Task CoreRuleEngineRejectsCullWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            1,
            "P1-EQUIPMENT-CULL",
            "SFD·134/221",
            "P1-CULL-BASE-UNIT-001");

    [Fact]
    public async Task P4CullTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-cull-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-CULL-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-CULL"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-CULL-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-CULL-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-CULL-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-CULL-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysSacredShearsEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-sacred-shears-equipment.fixture.json",
            "P1-EQUIPMENT-SACRED-SHEARS");

    [Fact]
    public Task CoreRuleEngineRejectsSacredShearsWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-SACRED-SHEARS",
            "SFD·172/221",
            "P1-SACRED-SHEARS-BASE-UNIT-001");

    [Fact]
    public async Task P4SacredShearsTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sacred-shears-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SACRED-SHEARS-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-SACRED-SHEARS"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SACRED-SHEARS-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SACRED-SHEARS-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SACRED-SHEARS-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-SACRED-SHEARS-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysBfSwordEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-bf-sword-equipment.fixture.json",
            "P1-EQUIPMENT-BF-SWORD");

    [Fact]
    public Task CoreRuleEngineRejectsBfSwordWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            4,
            "P1-EQUIPMENT-BF-SWORD",
            "SFD·161/221",
            "P1-BF-SWORD-BASE-UNIT-001");

    [Fact]
    public async Task P4BfSwordTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-bf-sword-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BF-SWORD-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-BF-SWORD"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BF-SWORD-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BF-SWORD-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BF-SWORD-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BF-SWORD-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysWanderersGuidebookEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-wanderers-guidebook-equipment.fixture.json",
            "P1-EQUIPMENT-WANDERERS-GUIDEBOOK", expectedTags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon]);

    [Fact]
    public Task CoreRuleEngineRejectsWanderersGuidebookWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-WANDERERS-GUIDEBOOK",
            "SFD·086/221",
            "P1-WANDERERS-GUIDEBOOK-BASE-UNIT-001");

    [Fact]
    public async Task P4WanderersGuidebookTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-wanderers-guidebook-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-WANDERERS-GUIDEBOOK-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-WANDERERS-GUIDEBOOK"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-WANDERERS-GUIDEBOOK-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-WANDERERS-GUIDEBOOK-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-WANDERERS-GUIDEBOOK-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-WANDERERS-GUIDEBOOK-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysArionsFallEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-arions-fall-equipment.fixture.json",
            "P1-EQUIPMENT-ARIONS-FALL");

    [Fact]
    public Task CoreRuleEngineRejectsArionsFallWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-ARIONS-FALL",
            "SFD·030/221",
            "P1-ARIONS-FALL-BASE-UNIT-001");

    [Fact]
    public async Task P4ArionsFallTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-arions-fall-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-ARIONS-FALL-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-ARIONS-FALL"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-ARIONS-FALL-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-ARIONS-FALL-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-ARIONS-FALL-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-ARIONS-FALL-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysHuntersMacheteEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-hunters-machete-equipment.fixture.json",
            "P1-EQUIPMENT-HUNTERS-MACHETE");

    [Fact]
    public Task CoreRuleEngineRejectsHuntersMacheteWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-HUNTERS-MACHETE",
            "UNL-096/219",
            "P1-HUNTERS-MACHETE-BASE-UNIT-001");

    [Fact]
    public async Task P4HuntersMacheteTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-hunters-machete-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HUNTERS-MACHETE-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-HUNTERS-MACHETE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-HUNTERS-MACHETE-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-HUNTERS-MACHETE-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-HUNTERS-MACHETE-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-HUNTERS-MACHETE-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysWitheredBattleaxeEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-withered-battleaxe-equipment.fixture.json",
            "P1-EQUIPMENT-WITHERED-BATTLEAXE");

    [Fact]
    public Task CoreRuleEngineRejectsWitheredBattleaxeWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            4,
            "P1-EQUIPMENT-WITHERED-BATTLEAXE",
            "UNL-019/219",
            "P1-WITHERED-BATTLEAXE-BASE-UNIT-001");

    [Fact]
    public async Task P4WitheredBattleaxeTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-withered-battleaxe-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-WITHERED-BATTLEAXE-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-WITHERED-BATTLEAXE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-WITHERED-BATTLEAXE-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-WITHERED-BATTLEAXE-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-WITHERED-BATTLEAXE-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-WITHERED-BATTLEAXE-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysBoneClubEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-bone-club-equipment.fixture.json",
            "P1-EQUIPMENT-BONE-CLUB");

    [Fact]
    public Task CoreRuleEngineRejectsBoneClubWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-BONE-CLUB",
            "SFD·118/221",
            "P1-BONE-CLUB-BASE-UNIT-001");

    [Fact]
    public async Task P4BoneClubTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-bone-club-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BONE-CLUB-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-BONE-CLUB"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BONE-CLUB-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BONE-CLUB-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BONE-CLUB-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BONE-CLUB-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysAncientSteleEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-ancient-stele-equipment.fixture.json",
            "P1-EQUIPMENT-ANCIENT-STELE");

    [Fact]
    public Task CoreRuleEngineRejectsAncientSteleWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-ANCIENT-STELE",
            "SFD·117/221",
            "P1-ANCIENT-STELE-BASE-UNIT-001");

    [Fact]
    public async Task P4AncientSteleTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-ancient-stele-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-ANCIENT-STELE-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-ANCIENT-STELE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-ANCIENT-STELE-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-ANCIENT-STELE-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-ANCIENT-STELE-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-ANCIENT-STELE-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysHextechAnomalyEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-hextech-anomaly-equipment.fixture.json",
            "P1-EQUIPMENT-HEXTECH-ANOMALY");

    [Fact]
    public Task CoreRuleEngineRejectsHextechAnomalyWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-HEXTECH-ANOMALY",
            "SFD·083/221",
            "P1-HEXTECH-ANOMALY-BASE-UNIT-001");

    [Fact]
    public async Task P4HextechAnomalyTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-hextech-anomaly-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HEXTECH-ANOMALY-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-HEXTECH-ANOMALY"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-HEXTECH-ANOMALY-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-HEXTECH-ANOMALY-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-HEXTECH-ANOMALY-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-HEXTECH-ANOMALY-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysEnergyChannelEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-energy-channel-equipment.fixture.json",
            "P1-EQUIPMENT-ENERGY-CHANNEL");

    [Fact]
    public Task CoreRuleEngineRejectsEnergyChannelWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-ENERGY-CHANNEL",
            "OGN·098/298",
            "P1-ENERGY-CHANNEL-BASE-UNIT-001");

    [Fact]
    public async Task P4EnergyChannelTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-energy-channel-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-ENERGY-CHANNEL-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-ENERGY-CHANNEL"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-ENERGY-CHANNEL-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-ENERGY-CHANNEL-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-ENERGY-CHANNEL-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-ENERGY-CHANNEL-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysTimeGateEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-time-gate-equipment.fixture.json",
            "P1-EQUIPMENT-TIME-GATE");

    [Fact]
    public Task CoreRuleEngineRejectsTimeGateWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-TIME-GATE",
            "SFD·078/221",
            "P1-TIME-GATE-BASE-UNIT-001");

    [Fact]
    public async Task P4TimeGateTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-time-gate-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-TIME-GATE-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-TIME-GATE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-TIME-GATE-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-TIME-GATE-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-TIME-GATE-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-TIME-GATE-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysRavenTomeEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-raven-tome-equipment.fixture.json",
            "P1-EQUIPMENT-RAVEN-TOME");

    [Fact]
    public Task CoreRuleEngineRejectsRavenTomeWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-RAVEN-TOME",
            "OGN·032/298",
            "P1-RAVEN-TOME-BASE-UNIT-001");

    [Fact]
    public async Task P4RavenTomeTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-raven-tome-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-RAVEN-TOME-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-RAVEN-TOME"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-RAVEN-TOME-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-RAVEN-TOME-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-RAVEN-TOME-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-RAVEN-TOME-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysSunDiscEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-sun-disc-equipment.fixture.json",
            "P1-EQUIPMENT-SUN-DISC");

    [Fact]
    public Task CoreRuleEngineRejectsSunDiscWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-SUN-DISC",
            "OGN·021/298",
            "P1-SUN-DISC-BASE-UNIT-001");

    [Fact]
    public async Task P4SunDiscTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sun-disc-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SUN-DISC-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-SUN-DISC"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SUN-DISC-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SUN-DISC-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SUN-DISC-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-SUN-DISC-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysForesightMaskEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-foresight-mask-equipment.fixture.json",
            "P1-EQUIPMENT-FORESIGHT-MASK");

    [Fact]
    public Task CoreRuleEngineRejectsForesightMaskWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-FORESIGHT-MASK",
            "OGN·060/298",
            "P1-FORESIGHT-MASK-BASE-UNIT-001");

    [Fact]
    public async Task P4ForesightMaskTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-foresight-mask-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-FORESIGHT-MASK-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-FORESIGHT-MASK"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-FORESIGHT-MASK-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-FORESIGHT-MASK-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-FORESIGHT-MASK-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-FORESIGHT-MASK-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysSolariAltarEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-solari-altar-equipment.fixture.json",
            "P1-EQUIPMENT-SOLARI-ALTAR");

    [Fact]
    public Task CoreRuleEngineRejectsSolariAltarWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-SOLARI-ALTAR",
            "OGN·072/298",
            "P1-SOLARI-ALTAR-BASE-UNIT-001");

    [Fact]
    public async Task P4SolariAltarTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-solari-altar-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SOLARI-ALTAR-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-SOLARI-ALTAR"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SOLARI-ALTAR-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SOLARI-ALTAR-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SOLARI-ALTAR-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-SOLARI-ALTAR-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysChemtechBarrelEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-chemtech-barrel-equipment.fixture.json",
            "P1-EQUIPMENT-CHEMTECH-BARREL");

    [Fact]
    public Task CoreRuleEngineRejectsChemtechBarrelWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            1,
            "P1-EQUIPMENT-CHEMTECH-BARREL",
            "SFD·063/221",
            "P1-CHEMTECH-BARREL-BASE-UNIT-001");

    [Fact]
    public async Task P4ChemtechBarrelTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-chemtech-barrel-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-CHEMTECH-BARREL-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-CHEMTECH-BARREL"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-CHEMTECH-BARREL-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-CHEMTECH-BARREL-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-CHEMTECH-BARREL-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-CHEMTECH-BARREL-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysSoulWheelEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-soul-wheel-equipment.fixture.json",
            "P1-EQUIPMENT-SOUL-WHEEL");

    [Fact]
    public Task CoreRuleEngineRejectsSoulWheelWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-SOUL-WHEEL",
            "SFD·144/221",
            "P1-SOUL-WHEEL-BASE-UNIT-001");

    [Fact]
    public async Task P4SoulWheelTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-soul-wheel-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SOUL-WHEEL-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-SOUL-WHEEL"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SOUL-WHEEL-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SOUL-WHEEL-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SOUL-WHEEL-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-SOUL-WHEEL-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysMushroomBagEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-mushroom-bag-equipment.fixture.json",
            "P1-EQUIPMENT-MUSHROOM-BAG");

    [Fact]
    public Task CoreRuleEngineRejectsMushroomBagWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-MUSHROOM-BAG",
            "OGN·101/298",
            "P1-MUSHROOM-BAG-BASE-UNIT-001");

    [Fact]
    public async Task P4MushroomBagTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-mushroom-bag-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MUSHROOM-BAG-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-MUSHROOM-BAG"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-MUSHROOM-BAG-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-MUSHROOM-BAG-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-MUSHROOM-BAG-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-MUSHROOM-BAG-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysArenaBarEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-arena-bar-equipment.fixture.json",
            "P1-EQUIPMENT-ARENA-BAR");

    [Fact]
    public Task CoreRuleEngineRejectsArenaBarWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-ARENA-BAR",
            "OGN·124/298",
            "P1-ARENA-BAR-BASE-UNIT-001");

    [Fact]
    public async Task P4ArenaBarTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-arena-bar-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-ARENA-BAR-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-ARENA-BAR"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-ARENA-BAR-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-ARENA-BAR-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-ARENA-BAR-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-ARENA-BAR-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysPirateHideoutEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-pirate-hideout-equipment.fixture.json",
            "P1-EQUIPMENT-PIRATE-HIDEOUT");

    [Fact]
    public Task CoreRuleEngineRejectsPirateHideoutWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-PIRATE-HIDEOUT",
            "OGN·143/298",
            "P1-PIRATE-HIDEOUT-BASE-UNIT-001");

    [Fact]
    public async Task P4PirateHideoutTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-pirate-hideout-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-PIRATE-HIDEOUT-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-PIRATE-HIDEOUT"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-PIRATE-HIDEOUT-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-PIRATE-HIDEOUT-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-PIRATE-HIDEOUT-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-PIRATE-HIDEOUT-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysForgottenSignpostEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-forgotten-signpost-equipment.fixture.json",
            "P1-EQUIPMENT-FORGOTTEN-SIGNPOST");

    [Fact]
    public Task CoreRuleEngineRejectsForgottenSignpostWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-FORGOTTEN-SIGNPOST",
            "UNL-045/219",
            "P1-FORGOTTEN-SIGNPOST-BASE-UNIT-001");

    [Fact]
    public async Task P4ForgottenSignpostTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-forgotten-signpost-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-FORGOTTEN-SIGNPOST-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-FORGOTTEN-SIGNPOST"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-FORGOTTEN-SIGNPOST-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-FORGOTTEN-SIGNPOST-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-FORGOTTEN-SIGNPOST-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-FORGOTTEN-SIGNPOST-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysFrozenGemEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-frozen-gem-equipment.fixture.json",
            "P1-EQUIPMENT-FROZEN-GEM");

    [Fact]
    public Task CoreRuleEngineRejectsFrozenGemWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-FROZEN-GEM",
            "UNL-074/219",
            "P1-FROZEN-GEM-BASE-UNIT-001");

    [Fact]
    public async Task P4FrozenGemTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-frozen-gem-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-FROZEN-GEM-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-FROZEN-GEM"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-FROZEN-GEM-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-FROZEN-GEM-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-FROZEN-GEM-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-FROZEN-GEM-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysCrumblingPalaceEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-crumbling-palace-equipment.fixture.json",
            "P1-EQUIPMENT-CRUMBLING-PALACE");

    [Fact]
    public Task CoreRuleEngineRejectsCrumblingPalaceWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            4,
            "P1-EQUIPMENT-CRUMBLING-PALACE",
            "UNL-088/219",
            "P1-CRUMBLING-PALACE-BASE-UNIT-001");

    [Fact]
    public async Task P4CrumblingPalaceTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-crumbling-palace-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-CRUMBLING-PALACE-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-CRUMBLING-PALACE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-CRUMBLING-PALACE-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-CRUMBLING-PALACE-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-CRUMBLING-PALACE-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-CRUMBLING-PALACE-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysScarletRoseEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-scarlet-rose-equipment.fixture.json",
            "P1-EQUIPMENT-SCARLET-ROSE");

    [Fact]
    public Task CoreRuleEngineRejectsScarletRoseWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            1,
            "P1-EQUIPMENT-SCARLET-ROSE",
            "UNL-109/219",
            "P1-SCARLET-ROSE-BASE-UNIT-001");

    [Fact]
    public async Task P4ScarletRoseTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-scarlet-rose-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SCARLET-ROSE-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-SCARLET-ROSE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SCARLET-ROSE-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SCARLET-ROSE-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SCARLET-ROSE-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-SCARLET-ROSE-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysReversalShardEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-reversal-shard-equipment.fixture.json",
            "P1-EQUIPMENT-REVERSAL-SHARD");

    [Fact]
    public Task CoreRuleEngineRejectsReversalShardWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            6,
            "P1-EQUIPMENT-REVERSAL-SHARD",
            "UNL-174/219",
            "P1-REVERSAL-SHARD-BASE-UNIT-001");

    [Fact]
    public async Task P4ReversalShardTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-reversal-shard-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(6, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-REVERSAL-SHARD-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-REVERSAL-SHARD"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-REVERSAL-SHARD-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-REVERSAL-SHARD-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-REVERSAL-SHARD-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-REVERSAL-SHARD-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysAssemblyRackEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-assembly-rack-equipment.fixture.json",
            "P1-EQUIPMENT-ASSEMBLY-RACK");

    [Fact]
    public Task CoreRuleEngineRejectsAssemblyRackWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            4,
            "P1-EQUIPMENT-ASSEMBLY-RACK",
            "SFD·019/221",
            "P1-ASSEMBLY-RACK-BASE-UNIT-001");

    [Fact]
    public async Task P4AssemblyRackTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-assembly-rack-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-ASSEMBLY-RACK-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-ASSEMBLY-RACK"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-ASSEMBLY-RACK-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-ASSEMBLY-RACK-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-ASSEMBLY-RACK-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-ASSEMBLY-RACK-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysSfurSongEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-sfur-song-equipment.fixture.json",
            "P1-EQUIPMENT-SFUR-SONG");

    [Fact]
    public Task CoreRuleEngineRejectsSfurSongWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-SFUR-SONG",
            "SFD·059/221",
            "P1-SFUR-SONG-BASE-UNIT-001");

    [Fact]
    public async Task P4SfurSongTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sfur-song-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SFUR-SONG-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-SFUR-SONG"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SFUR-SONG-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SFUR-SONG-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SFUR-SONG-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-SFUR-SONG-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysZDriveEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-z-drive-equipment.fixture.json",
            "P1-EQUIPMENT-Z-DRIVE");

    [Fact]
    public Task CoreRuleEngineRejectsZDriveWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-Z-DRIVE",
            "SFD·090/221",
            "P1-Z-DRIVE-BASE-UNIT-001");

    [Fact]
    public async Task P4ZDriveTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-z-drive-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-Z-DRIVE-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-Z-DRIVE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-Z-DRIVE-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-Z-DRIVE-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-Z-DRIVE-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-Z-DRIVE-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysVanguardArmoryEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-vanguard-armory-equipment.fixture.json",
            "P1-EQUIPMENT-VANGUARD-ARMORY");

    [Fact]
    public Task CoreRuleEngineRejectsVanguardArmoryWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            7,
            "P1-EQUIPMENT-VANGUARD-ARMORY",
            "SFD·168/221",
            "P1-VANGUARD-ARMORY-BASE-UNIT-001");

    [Fact]
    public async Task P4VanguardArmoryTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-vanguard-armory-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(7, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-VANGUARD-ARMORY-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-VANGUARD-ARMORY"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-VANGUARD-ARMORY-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-VANGUARD-ARMORY-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-VANGUARD-ARMORY-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-VANGUARD-ARMORY-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysRemembranceAltarEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-remembrance-altar-equipment.fixture.json",
            "P1-EQUIPMENT-REMEMBRANCE-ALTAR");

    [Fact]
    public Task CoreRuleEngineRejectsRemembranceAltarWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-REMEMBRANCE-ALTAR",
            "SFD·169/221",
            "P1-REMEMBRANCE-ALTAR-BASE-UNIT-001");

    [Fact]
    public async Task P4RemembranceAltarTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-remembrance-altar-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-REMEMBRANCE-ALTAR-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-REMEMBRANCE-ALTAR"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-REMEMBRANCE-ALTAR-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-REMEMBRANCE-ALTAR-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-REMEMBRANCE-ALTAR-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-REMEMBRANCE-ALTAR-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysRageSigilEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-rage-sigil-equipment.fixture.json",
            "P1-EQUIPMENT-RAGE-SIGIL");

    [Fact]
    public Task CoreRuleEngineRejectsRageSigilWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            0,
            "P1-EQUIPMENT-RAGE-SIGIL",
            "SFD·222/221",
            "P1-RAGE-SIGIL-BASE-UNIT-001");

    [Fact]
    public async Task P4RageSigilTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-rage-sigil-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-RAGE-SIGIL-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-RAGE-SIGIL"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-RAGE-SIGIL-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-RAGE-SIGIL-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-RAGE-SIGIL-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-RAGE-SIGIL-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysFocusSigilEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-focus-sigil-equipment.fixture.json",
            "P1-EQUIPMENT-FOCUS-SIGIL");

    [Fact]
    public Task CoreRuleEngineRejectsFocusSigilWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            0,
            "P1-EQUIPMENT-FOCUS-SIGIL",
            "SFD·226/221",
            "P1-FOCUS-SIGIL-BASE-UNIT-001");

    [Fact]
    public async Task P4FocusSigilTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-focus-sigil-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-FOCUS-SIGIL-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-FOCUS-SIGIL"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-FOCUS-SIGIL-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-FOCUS-SIGIL-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-FOCUS-SIGIL-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-FOCUS-SIGIL-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysInsightSigilEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-insight-sigil-equipment.fixture.json",
            "P1-EQUIPMENT-INSIGHT-SIGIL");

    [Fact]
    public Task CoreRuleEngineRejectsInsightSigilWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            0,
            "P1-EQUIPMENT-INSIGHT-SIGIL",
            "SFD·229/221",
            "P1-INSIGHT-SIGIL-BASE-UNIT-001");

    [Fact]
    public async Task P4InsightSigilTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-insight-sigil-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-INSIGHT-SIGIL-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-INSIGHT-SIGIL"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-INSIGHT-SIGIL-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-INSIGHT-SIGIL-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-INSIGHT-SIGIL-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-INSIGHT-SIGIL-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysPowerSigilEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-power-sigil-equipment.fixture.json",
            "P1-EQUIPMENT-POWER-SIGIL");

    [Fact]
    public Task CoreRuleEngineRejectsPowerSigilWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            0,
            "P1-EQUIPMENT-POWER-SIGIL",
            "SFD·231/221",
            "P1-POWER-SIGIL-BASE-UNIT-001");

    [Fact]
    public async Task P4PowerSigilTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-power-sigil-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-POWER-SIGIL-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-POWER-SIGIL"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-POWER-SIGIL-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-POWER-SIGIL-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-POWER-SIGIL-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-POWER-SIGIL-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysDiscordSigilEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-discord-sigil-equipment.fixture.json",
            "P1-EQUIPMENT-DISCORD-SIGIL");

    [Fact]
    public Task CoreRuleEngineRejectsDiscordSigilWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            0,
            "P1-EQUIPMENT-DISCORD-SIGIL",
            "SFD·234/221",
            "P1-DISCORD-SIGIL-BASE-UNIT-001");

    [Fact]
    public async Task P4DiscordSigilTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-discord-sigil-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-DISCORD-SIGIL-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-DISCORD-SIGIL"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-DISCORD-SIGIL-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-DISCORD-SIGIL-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-DISCORD-SIGIL-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-DISCORD-SIGIL-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysUnitySigilEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-unity-sigil-equipment.fixture.json",
            "P1-EQUIPMENT-UNITY-SIGIL");

    [Fact]
    public Task CoreRuleEngineRejectsUnitySigilWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            0,
            "P1-EQUIPMENT-UNITY-SIGIL",
            "SFD·238/221",
            "P1-UNITY-SIGIL-BASE-UNIT-001");

    [Fact]
    public async Task P4UnitySigilTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-unity-sigil-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-UNITY-SIGIL-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-UNITY-SIGIL"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-UNITY-SIGIL-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-UNITY-SIGIL-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-UNITY-SIGIL-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-UNITY-SIGIL-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysOgnRageSigilEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-ogn-rage-sigil-equipment.fixture.json",
            "P1-EQUIPMENT-OGN-RAGE-SIGIL");

    [Fact]
    public Task CoreRuleEngineRejectsOgnRageSigilWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            0,
            "P1-EQUIPMENT-OGN-RAGE-SIGIL",
            "OGN·040/298",
            "P1-OGN-RAGE-SIGIL-BASE-UNIT-001");

    [Fact]
    public async Task P4OgnRageSigilTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-ogn-rage-sigil-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-OGN-RAGE-SIGIL-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-OGN-RAGE-SIGIL"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-OGN-RAGE-SIGIL-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-OGN-RAGE-SIGIL-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-OGN-RAGE-SIGIL-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-OGN-RAGE-SIGIL-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysOgnFocusSigilEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-ogn-focus-sigil-equipment.fixture.json",
            "P1-EQUIPMENT-OGN-FOCUS-SIGIL");

    [Fact]
    public Task CoreRuleEngineRejectsOgnFocusSigilWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            0,
            "P1-EQUIPMENT-OGN-FOCUS-SIGIL",
            "OGN·081/298",
            "P1-OGN-FOCUS-SIGIL-BASE-UNIT-001");

    [Fact]
    public async Task P4OgnFocusSigilTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-ogn-focus-sigil-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-OGN-FOCUS-SIGIL-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-OGN-FOCUS-SIGIL"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-OGN-FOCUS-SIGIL-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-OGN-FOCUS-SIGIL-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-OGN-FOCUS-SIGIL-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-OGN-FOCUS-SIGIL-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysOgnInsightSigilEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-ogn-insight-sigil-equipment.fixture.json",
            "P1-EQUIPMENT-OGN-INSIGHT-SIGIL");

    [Fact]
    public Task CoreRuleEngineRejectsOgnInsightSigilWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            0,
            "P1-EQUIPMENT-OGN-INSIGHT-SIGIL",
            "OGN·120/298",
            "P1-OGN-INSIGHT-SIGIL-BASE-UNIT-001");

    [Fact]
    public async Task P4OgnInsightSigilTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-ogn-insight-sigil-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-OGN-INSIGHT-SIGIL-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-OGN-INSIGHT-SIGIL"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-OGN-INSIGHT-SIGIL-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-OGN-INSIGHT-SIGIL-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-OGN-INSIGHT-SIGIL-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-OGN-INSIGHT-SIGIL-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysOgnPowerSigilEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-ogn-power-sigil-equipment.fixture.json",
            "P1-EQUIPMENT-OGN-POWER-SIGIL");

    [Fact]
    public Task CoreRuleEngineRejectsOgnPowerSigilWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            0,
            "P1-EQUIPMENT-OGN-POWER-SIGIL",
            "OGN·163/298",
            "P1-OGN-POWER-SIGIL-BASE-UNIT-001");

    [Fact]
    public async Task P4OgnPowerSigilTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-ogn-power-sigil-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-OGN-POWER-SIGIL-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-OGN-POWER-SIGIL"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-OGN-POWER-SIGIL-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-OGN-POWER-SIGIL-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-OGN-POWER-SIGIL-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-OGN-POWER-SIGIL-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysOgnDiscordSigilEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-ogn-discord-sigil-equipment.fixture.json",
            "P1-EQUIPMENT-OGN-DISCORD-SIGIL");

    [Fact]
    public Task CoreRuleEngineRejectsOgnDiscordSigilWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            0,
            "P1-EQUIPMENT-OGN-DISCORD-SIGIL",
            "OGN·204/298",
            "P1-OGN-DISCORD-SIGIL-BASE-UNIT-001");

    [Fact]
    public async Task P4OgnDiscordSigilTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-ogn-discord-sigil-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-OGN-DISCORD-SIGIL-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-OGN-DISCORD-SIGIL"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-OGN-DISCORD-SIGIL-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-OGN-DISCORD-SIGIL-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-OGN-DISCORD-SIGIL-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-OGN-DISCORD-SIGIL-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysOgnUnitySigilEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-ogn-unity-sigil-equipment.fixture.json",
            "P1-EQUIPMENT-OGN-UNITY-SIGIL");

    [Fact]
    public Task CoreRuleEngineRejectsOgnUnitySigilWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            0,
            "P1-EQUIPMENT-OGN-UNITY-SIGIL",
            "OGN·245/298",
            "P1-OGN-UNITY-SIGIL-BASE-UNIT-001");

    [Fact]
    public async Task P4OgnUnitySigilTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-ogn-unity-sigil-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-OGN-UNITY-SIGIL-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-OGN-UNITY-SIGIL"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-OGN-UNITY-SIGIL-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-OGN-UNITY-SIGIL-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-OGN-UNITY-SIGIL-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-OGN-UNITY-SIGIL-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysWondrousPackEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-wondrous-pack-equipment.fixture.json",
            "P1-EQUIPMENT-WONDROUS-PACK");

    [Fact]
    public Task CoreRuleEngineRejectsWondrousPackWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-WONDROUS-PACK",
            "OGN·181/298",
            "P1-WONDROUS-PACK-BASE-UNIT-001");

    [Fact]
    public async Task P4WondrousPackTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-wondrous-pack-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-WONDROUS-PACK-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-WONDROUS-PACK"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-WONDROUS-PACK-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-WONDROUS-PACK-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-WONDROUS-PACK-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-WONDROUS-PACK-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysSirenEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-siren-equipment.fixture.json",
            "P1-EQUIPMENT-SIREN");

    [Fact]
    public Task CoreRuleEngineRejectsSirenWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-SIREN",
            "OGN·184/298",
            "P1-SIREN-BASE-UNIT-001");

    [Fact]
    public async Task P4SirenTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-siren-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SIREN-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-SIREN"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SIREN-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SIREN-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SIREN-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-SIREN-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysOwnerlessTreasureEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-ownerless-treasure-equipment.fixture.json",
            "P1-EQUIPMENT-OWNERLESS-TREASURE");

    [Fact]
    public Task CoreRuleEngineRejectsOwnerlessTreasureWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-OWNERLESS-TREASURE",
            "OGN·186/298",
            "P1-OWNERLESS-TREASURE-BASE-UNIT-001");

    [Fact]
    public async Task P4OwnerlessTreasureTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-ownerless-treasure-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-OWNERLESS-TREASURE-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-OWNERLESS-TREASURE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-OWNERLESS-TREASURE-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-OWNERLESS-TREASURE-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-OWNERLESS-TREASURE-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-OWNERLESS-TREASURE-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysScavengingWhizEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-scavenging-whiz-equipment.fixture.json",
            "P1-EQUIPMENT-SCAVENGING-WHIZ");

    [Fact]
    public Task CoreRuleEngineRejectsScavengingWhizWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-SCAVENGING-WHIZ",
            "OGN·099/298",
            "P1-SCAVENGING-WHIZ-BASE-UNIT-001");

    [Fact]
    public async Task P4ScavengingWhizTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-scavenging-whiz-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SCAVENGING-WHIZ-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-SCAVENGING-WHIZ"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SCAVENGING-WHIZ-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SCAVENGING-WHIZ-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SCAVENGING-WHIZ-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-SCAVENGING-WHIZ-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysMistfallBladeyardEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-mistfall-bladeyard-equipment.fixture.json",
            "P1-EQUIPMENT-MISTFALL-BLADEYARD");

    [Fact]
    public Task CoreRuleEngineRejectsMistfallBladeyardWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-MISTFALL-BLADEYARD",
            "OGN·152/298",
            "P1-MISTFALL-BLADEYARD-BASE-UNIT-001");

    [Fact]
    public async Task P4MistfallBladeyardTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-mistfall-bladeyard-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MISTFALL-BLADEYARD-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-MISTFALL-BLADEYARD"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-MISTFALL-BLADEYARD-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-MISTFALL-BLADEYARD-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-MISTFALL-BLADEYARD-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-MISTFALL-BLADEYARD-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysShimmeringAuroraEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-shimmering-aurora-equipment.fixture.json",
            "P1-EQUIPMENT-SHIMMERING-AURORA");

    [Fact]
    public Task CoreRuleEngineRejectsShimmeringAuroraWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            9,
            "P1-EQUIPMENT-SHIMMERING-AURORA",
            "OGN·160/298",
            "P1-SHIMMERING-AURORA-BASE-UNIT-001");

    [Fact]
    public async Task P4ShimmeringAuroraTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-shimmering-aurora-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(9, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SHIMMERING-AURORA-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-SHIMMERING-AURORA"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SHIMMERING-AURORA-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SHIMMERING-AURORA-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SHIMMERING-AURORA-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-SHIMMERING-AURORA-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysSolariEmblemEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-solari-emblem-equipment.fixture.json",
            "P1-EQUIPMENT-SOLARI-EMBLEM");

    [Fact]
    public Task CoreRuleEngineRejectsSolariEmblemWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            1,
            "P1-EQUIPMENT-SOLARI-EMBLEM",
            "OGN·227/298",
            "P1-SOLARI-EMBLEM-BASE-UNIT-001");

    [Fact]
    public async Task P4SolariEmblemTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-solari-emblem-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SOLARI-EMBLEM-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-SOLARI-EMBLEM"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SOLARI-EMBLEM-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SOLARI-EMBLEM-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SOLARI-EMBLEM-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-SOLARI-EMBLEM-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysVanguardHelmEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-vanguard-helm-equipment.fixture.json",
            "P1-EQUIPMENT-VANGUARD-HELM");

    [Fact]
    public Task CoreRuleEngineRejectsVanguardHelmWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-VANGUARD-HELM",
            "OGN·228/298",
            "P1-VANGUARD-HELM-BASE-UNIT-001");

    [Fact]
    public async Task P4VanguardHelmTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-vanguard-helm-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-VANGUARD-HELM-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-VANGUARD-HELM"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-VANGUARD-HELM-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-VANGUARD-HELM-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-VANGUARD-HELM-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-VANGUARD-HELM-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysHoneyfruitEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-honeyfruit-equipment.fixture.json",
            "P1-EQUIPMENT-HONEYFRUIT",
            expectedIsExhausted: true);

    [Fact]
    public Task CoreRuleEngineRejectsHoneyfruitWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-HONEYFRUIT",
            "UNL-049/219",
            "P1-HONEYFRUIT-BASE-UNIT-001");

    [Fact]
    public async Task P4HoneyfruitTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-honeyfruit-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HONEYFRUIT-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-HONEYFRUIT"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-HONEYFRUIT-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-HONEYFRUIT-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-HONEYFRUIT-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-HONEYFRUIT-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysLastRitesEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-last-rites-equipment.fixture.json",
            "P1-EQUIPMENT-LAST-RITES");

    [Fact]
    public Task CoreRuleEngineRejectsLastRitesWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-LAST-RITES",
            "SFD·150/221",
            "P1-LAST-RITES-BASE-UNIT-001");

    [Fact]
    public async Task P4LastRitesTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-last-rites-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-LAST-RITES-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-LAST-RITES"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-LAST-RITES-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-LAST-RITES-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-LAST-RITES-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-LAST-RITES-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysBladeOfRuinedKingEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-blade-of-ruined-king-equipment.fixture.json",
            "P1-EQUIPMENT-BLADE-OF-RUINED-KING");

    [Fact]
    public Task CoreRuleEngineRejectsBladeOfRuinedKingWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-BLADE-OF-RUINED-KING",
            "SFD·178/221",
            "P1-BLADE-OF-RUINED-KING-BASE-UNIT-001");

    [Fact]
    public async Task P4BladeOfRuinedKingTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-blade-of-ruined-king-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BLADE-OF-RUINED-KING-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-BLADE-OF-RUINED-KING"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BLADE-OF-RUINED-KING-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BLADE-OF-RUINED-KING-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BLADE-OF-RUINED-KING-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BLADE-OF-RUINED-KING-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysMysteriousWeaponEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-mysterious-weapon-equipment.fixture.json",
            "P1-EQUIPMENT-MYSTERIOUS-WEAPON");

    [Fact]
    public Task CoreRuleEngineRejectsMysteriousWeaponWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-MYSTERIOUS-WEAPON",
            "OGN·023/298",
            "P1-MYSTERIOUS-WEAPON-BASE-UNIT-001");

    [Fact]
    public async Task P4MysteriousWeaponTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-mysterious-weapon-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MYSTERIOUS-WEAPON-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-MYSTERIOUS-WEAPON"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-MYSTERIOUS-WEAPON-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-MYSTERIOUS-WEAPON-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-MYSTERIOUS-WEAPON-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-MYSTERIOUS-WEAPON-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysSeaMonsterHookEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-sea-monster-hook-equipment.fixture.json",
            "P1-EQUIPMENT-SEA-MONSTER-HOOK");

    [Fact]
    public Task CoreRuleEngineRejectsSeaMonsterHookWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-SEA-MONSTER-HOOK",
            "OGN·242/298",
            "P1-SEA-MONSTER-HOOK-BASE-UNIT-001");

    [Fact]
    public async Task P4SeaMonsterHookTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sea-monster-hook-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SEA-MONSTER-HOOK-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-SEA-MONSTER-HOOK"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SEA-MONSTER-HOOK-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SEA-MONSTER-HOOK-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-SEA-MONSTER-HOOK-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-SEA-MONSTER-HOOK-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysPetriciteMonumentEquipmentEphemeral() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-petricite-monument-equipment-ephemeral.fixture.json",
            "P1-EQUIPMENT-PETRICITE-MONUMENT",
            expectedTags: [CardObjectTags.EquipmentCard, CardObjectTags.Ephemeral]);

    [Fact]
    public Task CoreRuleEngineRejectsPetriciteMonumentWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-PETRICITE-MONUMENT",
            "SFD·104/221",
            "P1-PETRICITE-MONUMENT-BASE-UNIT-001");

    [Fact]
    public async Task P4PetriciteMonumentTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-petricite-monument-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-PETRICITE-MONUMENT-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-PETRICITE-MONUMENT"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-PETRICITE-MONUMENT-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-PETRICITE-MONUMENT-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-PETRICITE-MONUMENT-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-PETRICITE-MONUMENT-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysZhonyasHourglassEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-zhonyas-hourglass-equipment.fixture.json",
            "P1-EQUIPMENT-ZHONYAS-HOURGLASS");

    [Fact]
    public Task CoreRuleEngineRejectsZhonyasHourglassWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            2,
            "P1-EQUIPMENT-ZHONYAS-HOURGLASS",
            "OGN·077/298",
            "P1-ZHONYAS-HOURGLASS-BASE-UNIT-001");

    [Fact]
    public async Task P4ZhonyasHourglassTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-zhonyas-hourglass-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-ZHONYAS-HOURGLASS-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-ZHONYAS-HOURGLASS"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-ZHONYAS-HOURGLASS-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-ZHONYAS-HOURGLASS-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-ZHONYAS-HOURGLASS-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-ZHONYAS-HOURGLASS-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysEdgeOfNightEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-edge-of-night-equipment.fixture.json",
            "P1-EQUIPMENT-EDGE-OF-NIGHT");

    [Fact]
    public Task CoreRuleEngineRejectsEdgeOfNightWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-EDGE-OF-NIGHT",
            "SFD·139/221",
            "P1-EDGE-OF-NIGHT-BASE-UNIT-001");

    [Fact]
    public async Task P4EdgeOfNightTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-edge-of-night-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-EDGE-OF-NIGHT-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-EDGE-OF-NIGHT"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-EDGE-OF-NIGHT-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-EDGE-OF-NIGHT-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-EDGE-OF-NIGHT-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-EDGE-OF-NIGHT-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysHearthfireCloakEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-hearthfire-cloak-equipment.fixture.json",
            "P1-EQUIPMENT-HEARTHFIRE-CLOAK", expectedTags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon]);

    [Fact]
    public Task CoreRuleEngineRejectsHearthfireCloakWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            4,
            "P1-EQUIPMENT-HEARTHFIRE-CLOAK",
            "SFD·190/221",
            "P1-HEARTHFIRE-CLOAK-BASE-UNIT-001");

    [Fact]
    public async Task P4HearthfireCloakTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-hearthfire-cloak-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HEARTHFIRE-CLOAK-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-HEARTHFIRE-CLOAK"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-HEARTHFIRE-CLOAK-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-HEARTHFIRE-CLOAK-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-HEARTHFIRE-CLOAK-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-HEARTHFIRE-CLOAK-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysRabadonsDeathcapEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-rabadons-deathcap-equipment.fixture.json",
            "P1-EQUIPMENT-RABADONS-DEATHCAP", expectedTags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon]);

    [Fact]
    public Task CoreRuleEngineRejectsRabadonsDeathcapWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            4,
            "P1-EQUIPMENT-RABADONS-DEATHCAP",
            "SFD·191/221",
            "P1-RABADONS-DEATHCAP-BASE-UNIT-001");

    [Fact]
    public async Task P4RabadonsDeathcapTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-rabadons-deathcap-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-RABADONS-DEATHCAP-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-RABADONS-DEATHCAP"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-RABADONS-DEATHCAP-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-RABADONS-DEATHCAP-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-RABADONS-DEATHCAP-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-RABADONS-DEATHCAP-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysBlastConeEquipmentNoMove() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-blast-cone-equipment-no-move.fixture.json",
            "P1-EQUIPMENT-BLAST-CONE");

    [Fact]
    public Task CoreRuleEngineRejectsBlastConeWhenTargetsAreProvidedInNoMovePath() =>
        AssertEquipmentWithTargetRejectedAsync(
            4,
            "P1-EQUIPMENT-BLAST-CONE",
            "UNL-133/219",
            "P1-BLAST-CONE-BASE-UNIT-001");

    [Fact]
    public async Task P4BlastConeTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-blast-cone-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BLAST-CONE-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-BLAST-CONE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BLAST-CONE-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BLAST-CONE-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BLAST-CONE-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BLAST-CONE-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysDeathListEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-death-list-equipment.fixture.json",
            "P1-EQUIPMENT-DEATH-LIST");

    [Fact]
    public Task CoreRuleEngineRejectsDeathListWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            1,
            "P1-EQUIPMENT-DEATH-LIST",
            "UNL-138/219",
            "P1-DEATH-LIST-BASE-UNIT-001");

    [Fact]
    public async Task P4DeathListTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-death-list-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-DEATH-LIST-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-DEATH-LIST"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-DEATH-LIST-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-DEATH-LIST-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-DEATH-LIST-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-DEATH-LIST-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCursedSarcophagusEquipmentBanishesGraveyardUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-cursed-sarcophagus-equipment-banish-graveyard-units.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-CURSED-SARCOPHAGUS"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-GRAVE-SPELL-001"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P1-GRAVE-UNIT-001", "P1-GRAVE-UNIT-002"], result.FinalState.PlayerZones["P1"].Banished);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-CURSED-SARCOPHAGUS"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineCursedSarcophagusBanishesOnlyControlledGraveyardUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-cursed-sarcophagus-equipment-banish-graveyard-units.fixture.json"),
            CancellationToken.None);
        var initial = fixture.InitialState!;
        var players = initial.Players!.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        players["P1"] = players["P1"] with
        {
            Graveyard = ["P1-GRAVE-DIRTY-P2-UNIT", "P1-GRAVE-UNIT-001", "P1-GRAVE-SPELL-001", "P1-GRAVE-UNIT-002"]
        };
        var cardObjects = initial.CardObjects!.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-GRAVE-DIRTY-P2-UNIT"] = new ConformanceCardObjectState
        {
            Tags = [CardObjectTags.UnitCard],
            OwnerId = "P2",
            ControllerId = "P2"
        };
        var dirtyFixture = fixture with
        {
            InitialState = initial with
            {
                Players = players,
                CardObjects = cardObjects
            }
        };

        var result = await ConformanceFixtureRunner.RunAsync(
            dirtyFixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(["P1-GRAVE-DIRTY-P2-UNIT", "P1-GRAVE-SPELL-001"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P1-GRAVE-UNIT-001", "P1-GRAVE-UNIT-002"], result.FinalState.PlayerZones["P1"].Banished);
        var banishedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_BANISHED", StringComparison.Ordinal));
        Assert.Equal(["P1-GRAVE-UNIT-001", "P1-GRAVE-UNIT-002"], Assert.IsAssignableFrom<IReadOnlyList<string>>(banishedEvent.Payload["cardIds"]));
        Assert.Equal(2, banishedEvent.Payload["count"]);
    }

    [Fact]
    public Task CoreRuleEngineRejectsCursedSarcophagusWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            4,
            "P1-EQUIPMENT-CURSED-SARCOPHAGUS",
            "UNL-148/219",
            "P1-CURSED-SARCOPHAGUS-BASE-UNIT-001");

    [Fact]
    public async Task P4CursedSarcophagusTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-cursed-sarcophagus-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-CURSED-SARCOPHAGUS-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-CURSED-SARCOPHAGUS"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-CURSED-SARCOPHAGUS-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(
            [
                "P1-CURSED-SARCOPHAGUS-GRAVE-UNIT-001",
                "P1-CURSED-SARCOPHAGUS-GRAVE-SPELL-001",
                "P1-CURSED-SARCOPHAGUS-GRAVE-UNIT-002"
            ],
            result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Banished);
        Assert.Equal(0, result.FinalState.CardObjects["P1-CURSED-SARCOPHAGUS-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-CURSED-SARCOPHAGUS-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-CURSED-SARCOPHAGUS-BASE-UNIT-001"].Tags);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-CURSED-SARCOPHAGUS-GRAVE-UNIT-001"].Tags);
        Assert.Equal([CardObjectTags.SpellCard], result.FinalState.CardObjects["P1-CURSED-SARCOPHAGUS-GRAVE-SPELL-001"].Tags);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-CURSED-SARCOPHAGUS-GRAVE-UNIT-002"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysScryingShellEquipmentPredictRecycleTopCard()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-scrying-shell-equipment-predict-recycle.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-EQUIPMENT-SCRYING-SHELL"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-SCRYING-SHELL-KEEP-001", "P1-SCRYING-SHELL-RECYCLE-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-SCRYING-SHELL"].Tags);
    }

    [Fact]
    public Task CoreRuleEnginePlaysScryingShellEquipmentPredictNoRecycle() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-scrying-shell-equipment-predict-no-recycle.fixture.json",
            "P1-EQUIPMENT-SCRYING-SHELL");

    [Fact]
    public async Task CoreRuleEngineRejectsScryingShellWhenPredictTargetIsOutsideTopCard()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-SCRYING-SHELL"],
                    MainDeck = ["P1-SCRYING-SHELL-TOP-001", "P1-SCRYING-SHELL-SECOND-001"]
                }
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-scrying-shell-second-card", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-SCRYING-SHELL",
                "UNL-161/219",
                ["P1-SCRYING-SHELL-SECOND-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-SCRYING-SHELL"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SCRYING-SHELL-TOP-001", "P1-SCRYING-SHELL-SECOND-001"], result.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ScryingShellPredictOutsideTopCardRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-scrying-shell-predict-outside-top-card-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-SCRYING-SHELL"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-SCRYING-SHELL-TOP-001", "P1-SCRYING-SHELL-SECOND-001"],
            result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.FinalState.StackItems);
    }
}
