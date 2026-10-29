using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesLongSwordToFriendlyUnit()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-LONG-SWORD", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-LONG-SWORD"] = new(
                    "P1-EQUIPMENT-LONG-SWORD",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-premodel", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-LONG-SWORD",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_RED"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        Assert.Equal(["COST_PAID", "ABILITY_ACTIVATED", "STACK_ITEM_ADDED", "PRIORITY_PASSED", "PRIORITY_PASSED", "STACK_ITEM_RESOLVED", "EQUIPMENT_ATTACHED"], result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(3, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-LONG-SWORD", "P1-UNIT-ASSEMBLE-TARGET"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        Assert.Empty(result.State.StackItems);

        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(0, costPaidEvent.Payload["mana"]);
        Assert.Equal(1, costPaidEvent.Payload["power"]);
        Assert.Equal(["ASSEMBLE_RED"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));

        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("P1-EQUIPMENT-LONG-SWORD", attachedEvent.Payload["equipmentObjectId"]);
        Assert.Equal("P1-UNIT-ASSEMBLE-TARGET", attachedEvent.Payload["unitObjectId"]);
        Assert.Equal("P1-UNIT-ASSEMBLE-TARGET", attachedEvent.Payload["attachedToObjectId"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesJaggedDirkWithRedCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-JAGGED-DIRK", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-JAGGED-DIRK"] = new(
                    "P1-EQUIPMENT-JAGGED-DIRK",
                    cardNo: "SFD·009/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-jagged-dirk", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-JAGGED-DIRK",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_RED"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-JAGGED-DIRK"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_RED"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·009/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesRecurveBowWithRedCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-RECURVE-BOW", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-RECURVE-BOW"] = new(
                    "P1-EQUIPMENT-RECURVE-BOW",
                    cardNo: "SFD·016/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-recurve-bow", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-RECURVE-BOW",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_RED"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-RECURVE-BOW"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_RED"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·016/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesArionsFallWithRedCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    1,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-ARIONS-FALL", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-ARIONS-FALL"] = new(
                    "P1-EQUIPMENT-ARIONS-FALL",
                    cardNo: "SFD·030/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-arions-fall", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-ARIONS-FALL",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_RED"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-ARIONS-FALL"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_RED"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·030/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesWitheredBattleaxeWithRedCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    1,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-WITHERED-BATTLEAXE", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-WITHERED-BATTLEAXE"] = new(
                    "P1-EQUIPMENT-WITHERED-BATTLEAXE",
                    cardNo: "UNL-019/219",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-withered-battleaxe", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-WITHERED-BATTLEAXE",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_RED"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-WITHERED-BATTLEAXE"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_RED"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("UNL-019/219", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesBrutalizerWithGreenCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Green] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-BRUTALIZER", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-BRUTALIZER"] = new(
                    "P1-EQUIPMENT-BRUTALIZER",
                    cardNo: "SFD·042/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-brutalizer", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-BRUTALIZER",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_GREEN"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-BRUTALIZER"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_GREEN"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·042/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesGuardianAngelWithGreenCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Green] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-GUARDIAN-ANGEL", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-GUARDIAN-ANGEL"] = new(
                    "P1-EQUIPMENT-GUARDIAN-ANGEL",
                    cardNo: "SFD·051/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-guardian-angel", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-GUARDIAN-ANGEL",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_GREEN"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-GUARDIAN-ANGEL"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_GREEN"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·051/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesSoulSwordWithGreenCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Green] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-SOUL-SWORD", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-SOUL-SWORD"] = new(
                    "P1-EQUIPMENT-SOUL-SWORD",
                    cardNo: "UNL-039/219",
                    tags: [CardObjectTags.EquipmentCard]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-soul-sword", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-SOUL-SWORD",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_GREEN"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-SOUL-SWORD"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_GREEN"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("UNL-039/219", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesClothArmorToFriendlyUnit()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Blue] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-CLOTH-ARMOR", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-CLOTH-ARMOR"] = new(
                    "P1-EQUIPMENT-CLOTH-ARMOR",
                    cardNo: "SFD·064/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-cloth-armor", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-CLOTH-ARMOR",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_BLUE"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Null(result.ErrorCode);
        Assert.Equal(["COST_PAID", "ABILITY_ACTIVATED", "STACK_ITEM_ADDED", "PRIORITY_PASSED", "PRIORITY_PASSED", "STACK_ITEM_RESOLVED", "EQUIPMENT_ATTACHED"], result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(3, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-CLOTH-ARMOR", "P1-UNIT-ASSEMBLE-TARGET"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-CLOTH-ARMOR"].AttachedToObjectId);
        Assert.Empty(result.State.StackItems);

        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(0, costPaidEvent.Payload["mana"]);
        Assert.Equal(1, costPaidEvent.Payload["power"]);
        Assert.Equal(["ASSEMBLE_BLUE"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));

        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("P1-EQUIPMENT-CLOTH-ARMOR", attachedEvent.Payload["equipmentObjectId"]);
        Assert.Equal("P1-UNIT-ASSEMBLE-TARGET", attachedEvent.Payload["unitObjectId"]);
        Assert.Equal("P1-UNIT-ASSEMBLE-TARGET", attachedEvent.Payload["attachedToObjectId"]);
        Assert.Equal("SFD·064/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesHextechInfusedBulwarkWithBlueCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Blue] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-HEXTECH-INFUSED-BULWARK", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-HEXTECH-INFUSED-BULWARK"] = new(
                    "P1-EQUIPMENT-HEXTECH-INFUSED-BULWARK",
                    cardNo: "SFD·073/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-hextech-infused-bulwark", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-HEXTECH-INFUSED-BULWARK",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_BLUE"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-HEXTECH-INFUSED-BULWARK"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_BLUE"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·073/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesWanderersGuidebookWithBlueCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Blue] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-WANDERERS-GUIDEBOOK", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-WANDERERS-GUIDEBOOK"] = new(
                    "P1-EQUIPMENT-WANDERERS-GUIDEBOOK",
                    cardNo: "SFD·086/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-wanderers-guidebook", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-WANDERERS-GUIDEBOOK",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_BLUE"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-WANDERERS-GUIDEBOOK"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_BLUE"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·086/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesZDriveWithBlueCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    1,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Blue] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-Z-DRIVE", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-Z-DRIVE"] = new(
                    "P1-EQUIPMENT-Z-DRIVE",
                    cardNo: "SFD·090/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-z-drive", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-Z-DRIVE",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_BLUE"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-Z-DRIVE"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_BLUE"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·090/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesSteraksGageToFriendlyUnit()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Green] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-STERAKS-GAGE", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-STERAKS-GAGE"] = new(
                    "P1-EQUIPMENT-STERAKS-GAGE",
                    cardNo: "SFD·056/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-steraks-gage", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-STERAKS-GAGE",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_GREEN"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-STERAKS-GAGE"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_GREEN"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·056/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesSvarshangSongWithGreenCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    1,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Green] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-SVARSHANG-SONG", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-SVARSHANG-SONG"] = new(
                    "P1-EQUIPMENT-SVARSHANG-SONG",
                    cardNo: "SFD·059/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-svarshang-song", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-SVARSHANG-SONG",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_GREEN"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-SVARSHANG-SONG"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_GREEN"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·059/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesDoransShieldWithoutAgileTag()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Green] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-DORANS-SHIELD", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-DORANS-SHIELD"] = new(
                    "P1-EQUIPMENT-DORANS-SHIELD",
                    cardNo: "SFD·033/221",
                    tags: [CardObjectTags.EquipmentCard]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-dorans-shield", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-DORANS-SHIELD",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_GREEN"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-DORANS-SHIELD"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_GREEN"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·033/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesDoransRingWithPurpleCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Purple] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-DORANS-RING", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-DORANS-RING"] = new(
                    "P1-EQUIPMENT-DORANS-RING",
                    cardNo: "SFD·124/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-dorans-ring", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-DORANS-RING",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_PURPLE"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-DORANS-RING"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_PURPLE"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·124/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesDoransBladeWithOrangeCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Orange] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-DORANS-BLADE", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-DORANS-BLADE"] = new(
                    "P1-EQUIPMENT-DORANS-BLADE",
                    cardNo: "SFD·095/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-dorans-blade", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-DORANS-BLADE",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_ORANGE"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-DORANS-BLADE"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_ORANGE"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·095/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesHexdrinkerWithOrangeCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Orange] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-HEXDRINKER", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-HEXDRINKER"] = new(
                    "P1-EQUIPMENT-HEXDRINKER",
                    cardNo: "SFD·102/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-hexdrinker", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-HEXDRINKER",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_ORANGE"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-HEXDRINKER"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_ORANGE"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·102/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesWarmogsArmorWithOrangeCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Orange] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-WARMOGS-ARMOR", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-WARMOGS-ARMOR"] = new(
                    "P1-EQUIPMENT-WARMOGS-ARMOR",
                    cardNo: "SFD·108/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-warmogs-armor", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-WARMOGS-ARMOR",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_ORANGE"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-WARMOGS-ARMOR"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_ORANGE"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·108/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesTrinityForceWithOrangeCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Orange] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-TRINITY-FORCE", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-TRINITY-FORCE"] = new(
                    "P1-EQUIPMENT-TRINITY-FORCE",
                    cardNo: "SFD·115/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-trinity-force", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-TRINITY-FORCE",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_ORANGE"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-TRINITY-FORCE"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_ORANGE"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·115/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesHuntersMacheteWithOrangeCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Orange] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-HUNTERS-MACHETE", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-HUNTERS-MACHETE"] = new(
                    "P1-EQUIPMENT-HUNTERS-MACHETE",
                    cardNo: "UNL-096/219",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-hunters-machete", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-HUNTERS-MACHETE",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_ORANGE"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-HUNTERS-MACHETE"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_ORANGE"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("UNL-096/219", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesBoneClubWithOrangeCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    1,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Orange] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-BONE-CLUB", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-BONE-CLUB"] = new(
                    "P1-EQUIPMENT-BONE-CLUB",
                    cardNo: "SFD·118/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-bone-club", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-BONE-CLUB",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_ORANGE"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-BONE-CLUB"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_ORANGE"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·118/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesBoneClubPromoWithOrangeCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    1,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Orange] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-BONE-CLUB-PROMO", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-BONE-CLUB-PROMO"] = new(
                    "P1-EQUIPMENT-BONE-CLUB-PROMO",
                    cardNo: "SFD·118a/221·P",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-bone-club-promo", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-BONE-CLUB-PROMO",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_ORANGE"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-BONE-CLUB-PROMO"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_ORANGE"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·118a/221·P", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesBootsOfSwiftnessWithPurpleCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Purple] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-BOOTS-OF-SWIFTNESS", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-BOOTS-OF-SWIFTNESS"] = new(
                    "P1-EQUIPMENT-BOOTS-OF-SWIFTNESS",
                    cardNo: "SFD·133/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-boots-of-swiftness", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-BOOTS-OF-SWIFTNESS",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_PURPLE"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-BOOTS-OF-SWIFTNESS"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_PURPLE"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·133/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesCullWithPurpleCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Purple] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-CULL", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-CULL"] = new(
                    "P1-EQUIPMENT-CULL",
                    cardNo: "SFD·134/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-cull", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-CULL",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_PURPLE"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-CULL"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_PURPLE"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·134/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesEdgeOfNightWithPurpleCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Purple] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-EDGE-OF-NIGHT", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-EDGE-OF-NIGHT"] = new(
                    "P1-EQUIPMENT-EDGE-OF-NIGHT",
                    cardNo: "SFD·139/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-edge-of-night", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-EDGE-OF-NIGHT",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_PURPLE"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-EDGE-OF-NIGHT"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_PURPLE"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·139/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesVanguardsEyeWithYellowCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Yellow] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-VANGUARDS-EYE", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-VANGUARDS-EYE"] = new(
                    "P1-EQUIPMENT-VANGUARDS-EYE",
                    cardNo: "SFD·153/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-vanguards-eye", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-VANGUARDS-EYE",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_YELLOW"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-VANGUARDS-EYE"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_YELLOW"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·153/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesBfSwordWithYellowCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Yellow] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-BF-SWORD", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-BF-SWORD"] = new(
                    "P1-EQUIPMENT-BF-SWORD",
                    cardNo: "SFD·161/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-bf-sword", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-BF-SWORD",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_YELLOW"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-BF-SWORD"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_YELLOW"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·161/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesSacredShearsWithYellowCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Yellow] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-SACRED-SHEARS", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-SACRED-SHEARS"] = new(
                    "P1-EQUIPMENT-SACRED-SHEARS",
                    cardNo: "SFD·172/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-sacred-shears", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-SACRED-SHEARS",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_YELLOW"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-SACRED-SHEARS"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_YELLOW"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·172/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesSpinningAxeWithAnyPowerCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(0, 1),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-SPINNING-AXE", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-SPINNING-AXE"] = new(
                    "P1-EQUIPMENT-SPINNING-AXE",
                    cardNo: "SFD·186/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便", "瞬息"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-spinning-axe", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-SPINNING-AXE",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_ANY_POWER"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-SPINNING-AXE"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_ANY_POWER"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·186/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesHearthfireCloakWithAnyPowerCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Green] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-HEARTHFIRE-CLOAK", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-HEARTHFIRE-CLOAK"] = new(
                    "P1-EQUIPMENT-HEARTHFIRE-CLOAK",
                    cardNo: "SFD·190/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-hearthfire-cloak", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-HEARTHFIRE-CLOAK",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_ANY_POWER"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-HEARTHFIRE-CLOAK"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_ANY_POWER"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·190/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesRabadonsDeathcapWithAnyPowerCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Blue] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-RABADONS-DEATHCAP", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-RABADONS-DEATHCAP"] = new(
                    "P1-EQUIPMENT-RABADONS-DEATHCAP",
                    cardNo: "SFD·191/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-rabadons-deathcap", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-RABADONS-DEATHCAP",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_ANY_POWER"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-RABADONS-DEATHCAP"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_ANY_POWER"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·191/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesShurelyasRequiemWithAnyPowerCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(0, 1),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-SHURELYAS-REQUIEM", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-SHURELYAS-REQUIEM"] = new(
                    "P1-EQUIPMENT-SHURELYAS-REQUIEM",
                    cardNo: "SFD·192/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-shurelyas-requiem", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-SHURELYAS-REQUIEM",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_ANY_POWER"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-SHURELYAS-REQUIEM"].AttachedToObjectId);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_ANY_POWER"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("SFD·192/221", attachedEvent.Payload["equipmentCardNo"]);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAcceptsRainbowPowerForRedAssembleCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(0, 1),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-LONG-SWORD", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-LONG-SWORD"] = new(
                    "P1-EQUIPMENT-LONG-SWORD",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-generic-power", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-LONG-SWORD",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_RED"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);
        Assert.Equal("P1-UNIT-ASSEMBLE-TARGET", result.State.CardObjects["P1-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandRejectsRedPowerForBlueClothArmorCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-CLOTH-ARMOR", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-CLOTH-ARMOR"] = new(
                    "P1-EQUIPMENT-CLOTH-ARMOR",
                    cardNo: "SFD·064/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-cloth-armor-red-power", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-CLOTH-ARMOR",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_BLUE"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(
            new RunePool(
                0,
                0,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Red] = 1
                }),
            result.State.RunePools["P1"]);
        Assert.Null(result.State.CardObjects["P1-EQUIPMENT-CLOTH-ARMOR"].AttachedToObjectId);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandRejectsBluePowerForGreenSteraksGageCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Blue] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-STERAKS-GAGE", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-STERAKS-GAGE"] = new(
                    "P1-EQUIPMENT-STERAKS-GAGE",
                    cardNo: "SFD·056/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-steraks-gage-blue-power", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-STERAKS-GAGE",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_GREEN"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Null(result.State.CardObjects["P1-EQUIPMENT-STERAKS-GAGE"].AttachedToObjectId);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandRejectsTargetWithoutCardNo()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-LONG-SWORD", "P1-UNIT-UNKNOWN-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-LONG-SWORD"] = new(
                    "P1-EQUIPMENT-LONG-SWORD",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"]),
                ["P1-UNIT-UNKNOWN-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-UNKNOWN-ASSEMBLE-TARGET",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-unknown-cardno-target", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-LONG-SWORD",
                "P1-UNIT-UNKNOWN-ASSEMBLE-TARGET",
                ["ASSEMBLE_RED"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.Equal(0, result.State.RunePools["P1"].Power);
        Assert.Equal(1, result.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Null(result.State.CardObjects["P1-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        Assert.Equal(["P1-EQUIPMENT-LONG-SWORD", "P1-UNIT-UNKNOWN-ASSEMBLE-TARGET"], result.State.PlayerZones["P1"].Base);
    }

    [Fact]
    public async Task P7TypedPowerPaymentAssemblesLongSwordWithTraitPool()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-LONG-SWORD", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-LONG-SWORD"] = new(
                    "P1-EQUIPMENT-LONG-SWORD",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-assemble-equipment-trait-power", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-LONG-SWORD",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_RED"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.RunePools["P1"].PowerByTrait);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        Assert.Equal(["COST_PAID", "ABILITY_ACTIVATED", "STACK_ITEM_ADDED", "PRIORITY_PASSED", "PRIORITY_PASSED", "STACK_ITEM_RESOLVED", "EQUIPMENT_ATTACHED"], result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
    }

    [Fact]
    public async Task AssembleEquipmentDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        const string equipmentObjectId = "P1-EQUIPMENT-LONG-SWORD";
        const string unitObjectId = "P1-UNIT-ASSEMBLE-TARGET";
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [equipmentObjectId, unitObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [equipmentObjectId] = new(
                    equipmentObjectId,
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                    ownerId: "P1",
                    controllerId: "P1"),
                [unitObjectId] = new(
                    unitObjectId,
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new AssembleEquipmentCommand(
            equipmentObjectId,
            unitObjectId,
            ["ASSEMBLE_RED"]);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.AssembleEquipment, prompt.EnabledActions());
        Assert.Contains(prompt.Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.AssembleEquipment, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, equipmentObjectId, StringComparison.Ordinal))
            && (candidate.Targets ?? []).Any(target => string.Equals(target.Id, unitObjectId, StringComparison.Ordinal))
            && (candidate.OptionalCosts ?? []).Any(optionalCost => string.Equals(optionalCost.Id, "ASSEMBLE_RED", StringComparison.Ordinal)));
        var rawCommand = PromptScopedRawCommand(CommandTypes.AssembleEquipment, prompt);
        const string clientIntentId = "intent-assemble-equipment-long-sword-raw-idempotency";

        var accepted = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            NativeCommandWire.Bind(command, ref rawCommand),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["COST_PAID", "ABILITY_ACTIVATED", "STACK_ITEM_ADDED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(1, accepted.State.Tick);
        Assert.Equal(new RunePool(0, 0), accepted.State.RunePools["P1"]);
        Assert.Empty(accepted.State.RunePools["P1"].PowerByTrait);
        Assert.Equal([equipmentObjectId, unitObjectId], accepted.State.PlayerZones["P1"].Base);
        Assert.Null(accepted.State.CardObjects[equipmentObjectId].AttachedToObjectId);
        Assert.Single(accepted.State.StackItems);
        Assert.DoesNotContain(accepted.Prompts["P1"].Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.AssembleEquipment, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, equipmentObjectId, StringComparison.Ordinal)));
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedEventsHash = MatchStateHasher.HashValue(accepted.Events);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var currentAssemblePromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(accepted.State));
        var currentAssembleSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(accepted.State));
        var journalEntry = Assert.Single(journal.Entries);
        Assert.Equal(clientIntentId, journalEntry.ClientIntentId);
        Assert.Equal("P1", journalEntry.PlayerId);
        Assert.Equal(CommandTypes.AssembleEquipment, journalEntry.CommandType);
        Assert.True(journalEntry.Accepted);
        Assert.True(journalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.AssembleEquipment, journalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, journalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, journalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(journalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var replay = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            NativeCommandWire.Bind(command, ref rawCommand),
            CancellationToken.None);

        Assert.True(replay.Accepted, replay.ErrorMessage);
        Assert.Null(replay.ErrorCode);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(acceptedEventsHash, MatchStateHasher.HashValue(replay.Events));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Equal(["COST_PAID", "ABILITY_ACTIVATED", "STACK_ITEM_ADDED"], replay.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(new RunePool(0, 0), replay.State.RunePools["P1"]);
        Assert.Empty(replay.State.RunePools["P1"].PowerByTrait);
        Assert.Equal([equipmentObjectId, unitObjectId], replay.State.PlayerZones["P1"].Base);
        Assert.Null(replay.State.CardObjects[equipmentObjectId].AttachedToObjectId);
        Assert.DoesNotContain(replay.Prompts["P1"].Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.AssembleEquipment, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, equipmentObjectId, StringComparison.Ordinal)));
        Assert.Single(journal.Entries);

        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.AssembleEquipment,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });
        var conflict = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            NativeCommandWire.Bind(command, ref changedRawCommand),
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(accepted.State.Tick, conflict.State.Tick);
        Assert.Equal(currentAssemblePromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(currentAssembleSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Equal(new RunePool(0, 0), conflict.State.RunePools["P1"]);
        Assert.Empty(conflict.State.RunePools["P1"].PowerByTrait);
        Assert.Equal([equipmentObjectId, unitObjectId], conflict.State.PlayerZones["P1"].Base);
        Assert.Null(conflict.State.CardObjects[equipmentObjectId].AttachedToObjectId);
        Assert.DoesNotContain(conflict.Prompts["P1"].Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.AssembleEquipment, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, equipmentObjectId, StringComparison.Ordinal)));
        Assert.Single(journal.Entries);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
        var completed = await EquipTestDriver.FinishAsync(accepted, session);
        Assert.Equal(unitObjectId, completed.State.CardObjects[equipmentObjectId].AttachedToObjectId);
        Assert.Equal(accepted.State.RunePools["P1"], completed.State.RunePools["P1"]);
        Assert.Empty(completed.State.StackItems);
    }

    [Fact]
    public async Task P7TypedPowerPaymentAssemblesLongSwordWithRecycleRunePaymentResource()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base =
                    [
                        "P1-EQUIPMENT-LONG-SWORD",
                        "P1-UNIT-ASSEMBLE-TARGET",
                        "P1-RUNE-RED-ASSEMBLE-PAYMENT"
                    ],
                    RuneDeck = ["P1-RUNE-BOTTOM-ASSEMBLE-PAYMENT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-LONG-SWORD"] = new(
                    "P1-EQUIPMENT-LONG-SWORD",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard]),
                ["P1-RUNE-RED-ASSEMBLE-PAYMENT"] = new(
                    "P1-RUNE-RED-ASSEMBLE-PAYMENT",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-assemble-equipment-recycle-payment", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-LONG-SWORD",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_RED", "RECYCLE_RUNE:P1-RUNE-RED-ASSEMBLE-PAYMENT"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        Assert.Equal(
            ["RUNE_RECYCLED", "POWER_GAINED", "COST_PAID", "ABILITY_ACTIVATED", "STACK_ITEM_ADDED", "PRIORITY_PASSED", "PRIORITY_PASSED", "STACK_ITEM_RESOLVED", "EQUIPMENT_ATTACHED"],
            result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            ["P1-EQUIPMENT-LONG-SWORD", "P1-UNIT-ASSEMBLE-TARGET"],
            result.State.PlayerZones["P1"].Base);
        Assert.Equal(
            ["P1-RUNE-BOTTOM-ASSEMBLE-PAYMENT", "P1-RUNE-RED-ASSEMBLE-PAYMENT"],
            result.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        Assert.Equal("RUNE_DECK", result.State.ObjectLocations["P1-RUNE-RED-ASSEMBLE-PAYMENT"].Zone);

        var recycleEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal));
        Assert.Equal("ASSEMBLE_EQUIPMENT", recycleEvent.Payload["paymentWindow"]);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(
            ["ASSEMBLE_RED"],
            Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
        Assert.Equal(
            ["RECYCLE_RUNE:P1-RUNE-RED-ASSEMBLE-PAYMENT"],
            Assert.IsType<string[]>(costPaidEvent.Payload["paymentResourceActions"]));
    }

    [Fact]
    public async Task P5EquipmentStateAssembleLongSwordPreservesOwnerControllerAndAttachment()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-LONG-SWORD", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-LONG-SWORD"] = new(
                    "P1-EQUIPMENT-LONG-SWORD",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p5-equipment-state-assemble", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-LONG-SWORD",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_RED"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted);
        Assert.Equal(3, result.State.Tick);
        Assert.Equal(["P1-EQUIPMENT-LONG-SWORD", "P1-UNIT-ASSEMBLE-TARGET"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P2"].Base);

        var equipment = result.State.CardObjects["P1-EQUIPMENT-LONG-SWORD"];
        Assert.Equal("P1", equipment.OwnerId);
        Assert.Equal("P1", equipment.ControllerId);
        Assert.Equal("P1-UNIT-ASSEMBLE-TARGET", equipment.AttachedToObjectId);

        var target = result.State.CardObjects["P1-UNIT-ASSEMBLE-TARGET"];
        Assert.Equal("P1", target.OwnerId);
        Assert.Equal("P1", target.ControllerId);
        Assert.Null(target.AttachedToObjectId);

        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("P1", attachedEvent.Payload["ownerId"]);
        Assert.Equal("P1", attachedEvent.Payload["controllerId"]);

        var p2Snapshot = result.Snapshots["P2"];
        var p1ViewForP2 = Assert.IsType<Dictionary<string, object?>>(p2Snapshot.Players["P1"]);
        var p1ObjectsForP2 = Assert.IsType<Dictionary<string, object?>>(p1ViewForP2["objects"]);
        var equipmentView = Assert.IsType<Dictionary<string, object?>>(p1ObjectsForP2["P1-EQUIPMENT-LONG-SWORD"]);
        Assert.Equal("P1", equipmentView["ownerId"]);
        Assert.Equal("P1", equipmentView["controllerId"]);
        Assert.Equal("P1-UNIT-ASSEMBLE-TARGET", equipmentView["attachedToObjectId"]);
    }

    [Fact]
    public async Task P5EquipmentStateAssembleLongSwordRejectsControllerMismatchWithoutSideEffects()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(0, 1),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-LONG-SWORD", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-LONG-SWORD"] = new(
                    "P1-EQUIPMENT-LONG-SWORD",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                    ownerId: "P1",
                    controllerId: "P2"),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p5-equipment-state-controller-mismatch", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-LONG-SWORD",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_RED"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 1), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-LONG-SWORD", "P1-UNIT-ASSEMBLE-TARGET"], result.State.PlayerZones["P1"].Base);

        var equipment = result.State.CardObjects["P1-EQUIPMENT-LONG-SWORD"];
        Assert.Equal("P1", equipment.OwnerId);
        Assert.Equal("P2", equipment.ControllerId);
        Assert.Null(equipment.AttachedToObjectId);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P5EquipmentStateAssembleLongSwordAllowsControlledOpponentOwnedTarget()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-LONG-SWORD", "P1-CONTROLLED-OPPONENT-OWNED-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-LONG-SWORD"] = new(
                    "P1-EQUIPMENT-LONG-SWORD",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-CONTROLLED-OPPONENT-OWNED-TARGET"] = new(
                    "P1-CONTROLLED-OPPONENT-OWNED-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P1")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p5-equipment-state-controlled-opponent-owned-target", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-LONG-SWORD",
                "P1-CONTROLLED-OPPONENT-OWNED-TARGET",
                ["ASSEMBLE_RED"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted);
        Assert.Equal(3, result.State.Tick);
        Assert.Equal(
            "P1-CONTROLLED-OPPONENT-OWNED-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        Assert.Equal("P2", result.State.CardObjects["P1-CONTROLLED-OPPONENT-OWNED-TARGET"].OwnerId);
        Assert.Equal("P1", result.State.CardObjects["P1-CONTROLLED-OPPONENT-OWNED-TARGET"].ControllerId);
        Assert.Equal(["COST_PAID", "ABILITY_ACTIVATED", "STACK_ITEM_ADDED", "PRIORITY_PASSED", "PRIORITY_PASSED", "STACK_ITEM_RESOLVED", "EQUIPMENT_ATTACHED"], result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandInPriorityWindowIsRejectedUntilEquipmentSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P1",
            PassedPriorityPlayerIds = [],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-LONG-SWORD", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-LONG-SWORD"] = new(
                    "P1-EQUIPMENT-LONG-SWORD",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            },
            StackItems =
            [
                new StackItemState(
                    "STACK-0-P2-SPELL-PROBE",
                    "P2",
                    "P2-SPELL-PROBE",
                    "PENDING_TEST_SPELL",
                    "TEST-000",
                    [])
            ]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-priority-window", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-LONG-SWORD",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_RED"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前装备装配路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Equal("P1", result.State.PriorityPlayerId);
        Assert.Equal(["P1-EQUIPMENT-LONG-SWORD", "P1-UNIT-ASSEMBLE-TARGET"], result.State.PlayerZones["P1"].Base);
        Assert.Null(result.State.CardObjects["P1-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("STACK-0-P2-SPELL-PROBE", stackItem.StackItemId);
        Assert.Equal("P2", stackItem.ControllerId);
        Assert.Equal("PENDING_TEST_SPELL", stackItem.EffectKind);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandWithAlreadyAttachedSourceIsRejectedUntilEquipmentSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base =
                    [
                        "P1-EQUIPMENT-LONG-SWORD",
                        "P1-UNIT-ASSEMBLE-OLD-TARGET",
                        "P1-UNIT-ASSEMBLE-TARGET"
                    ]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-LONG-SWORD"] = new(
                    "P1-EQUIPMENT-LONG-SWORD",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                    attachedToObjectId: "P1-UNIT-ASSEMBLE-OLD-TARGET"),
                ["P1-UNIT-ASSEMBLE-OLD-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-OLD-TARGET",
                    power: 2,
                    tags: [CardObjectTags.UnitCard]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-already-attached", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-LONG-SWORD",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_RED"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前装备装配路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(
            ["P1-EQUIPMENT-LONG-SWORD", "P1-UNIT-ASSEMBLE-OLD-TARGET", "P1-UNIT-ASSEMBLE-TARGET"],
            result.State.PlayerZones["P1"].Base);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-OLD-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        Assert.Equal(2, result.State.CardObjects["P1-UNIT-ASSEMBLE-OLD-TARGET"].Power);
        Assert.Equal(3, result.State.CardObjects["P1-UNIT-ASSEMBLE-TARGET"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandWithMissingTargetIsRejectedUntilEquipmentSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-LONG-SWORD"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-LONG-SWORD"] = new(
                    "P1-EQUIPMENT-LONG-SWORD",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-missing-target", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-LONG-SWORD",
                "",
                ["ASSEMBLE_RED"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前装备装配路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-LONG-SWORD"], result.State.PlayerZones["P1"].Base);
        Assert.Null(result.State.CardObjects["P1-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandWithUnknownTargetIsRejectedUntilEquipmentSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-LONG-SWORD"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-LONG-SWORD"] = new(
                    "P1-EQUIPMENT-LONG-SWORD",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-unknown-target", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-LONG-SWORD",
                "P1-UNIT-ASSEMBLE-MISSING",
                ["ASSEMBLE_RED"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前装备装配路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-LONG-SWORD"], result.State.PlayerZones["P1"].Base);
        Assert.False(result.State.CardObjects.ContainsKey("P1-UNIT-ASSEMBLE-MISSING"));
        Assert.Null(result.State.CardObjects["P1-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandWithMissingSourceIsRejectedUntilEquipmentSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-missing-source", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_RED"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前装备装配路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-ASSEMBLE-TARGET"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(3, result.State.CardObjects["P1-UNIT-ASSEMBLE-TARGET"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandWithUnknownSourceIsRejectedUntilEquipmentSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-unknown-source", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-LONG-SWORD-MISSING",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_RED"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前装备装配路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-ASSEMBLE-TARGET"], result.State.PlayerZones["P1"].Base);
        Assert.False(result.State.CardObjects.ContainsKey("P1-EQUIPMENT-LONG-SWORD-MISSING"));
        Assert.Equal(3, result.State.CardObjects["P1-UNIT-ASSEMBLE-TARGET"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesShepherdsHeirloomWithExperienceCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 1,
                ["P2"] = 0
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-SHEPHERDS-HEIRLOOM", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-SHEPHERDS-HEIRLOOM"] = new(
                    "P1-EQUIPMENT-SHEPHERDS-HEIRLOOM",
                    cardNo: "UNL-158/219",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-shepherds-heirloom-experience", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-SHEPHERDS-HEIRLOOM",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["SPEND_EXPERIENCE:1"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        Assert.Equal(7, result.Events.Count);
        Assert.Equal(["COST_PAID", "ABILITY_ACTIVATED", "STACK_ITEM_ADDED", "PRIORITY_PASSED", "PRIORITY_PASSED", "STACK_ITEM_RESOLVED", "EQUIPMENT_ATTACHED"], result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(3, result.State.Tick);
        Assert.Equal(0, result.State.PlayerExperience["P1"]);
        Assert.Equal(["P1-EQUIPMENT-SHEPHERDS-HEIRLOOM", "P1-UNIT-ASSEMBLE-TARGET"], result.State.PlayerZones["P1"].Base);
        Assert.Equal("P1-UNIT-ASSEMBLE-TARGET", result.State.CardObjects["P1-EQUIPMENT-SHEPHERDS-HEIRLOOM"].AttachedToObjectId);
        Assert.Null(result.State.CardObjects["P1-UNIT-ASSEMBLE-TARGET"].AttachedToObjectId);
        Assert.Empty(result.State.StackItems);

        var costPaidPayload = result.Events[0].Payload;
        Assert.Equal("P1", costPaidPayload["playerId"]);
        Assert.Equal(0, costPaidPayload["mana"]);
        Assert.Equal(0, costPaidPayload["power"]);
        Assert.Equal(1, costPaidPayload["experience"]);
        Assert.Equal(["SPEND_EXPERIENCE:1"], Assert.IsAssignableFrom<IEnumerable<string>>(costPaidPayload["optionalCosts"]));
        var attachPayload = Assert.Single(result.Events, e => e.Kind == "EQUIPMENT_ATTACHED").Payload;
        Assert.Equal("UNL-158/219", attachPayload["equipmentCardNo"]);
        Assert.Equal("P1-UNIT-ASSEMBLE-TARGET", attachPayload["attachedToObjectId"]);

        var insufficientResult = await new CoreRuleEngine().ResolveAsync(
            state with
            {
                PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["P1"] = 0,
                    ["P2"] = 0
                }
            },
            new PlayerIntent("intent-p4-assemble-equipment-shepherds-heirloom-no-experience", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-SHEPHERDS-HEIRLOOM",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["SPEND_EXPERIENCE:1"]),
            CancellationToken.None);
        insufficientResult = await EquipTestDriver.FinishAsync(insufficientResult);

        Assert.False(insufficientResult.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, insufficientResult.ErrorCode);
        Assert.Empty(insufficientResult.Events);
        Assert.Equal(0, insufficientResult.State.Tick);
        Assert.Equal(0, insufficientResult.State.PlayerExperience["P1"]);
        Assert.Null(insufficientResult.State.CardObjects["P1-EQUIPMENT-SHEPHERDS-HEIRLOOM"].AttachedToObjectId);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesBladeOfTheRuinedKingWithDestroyFriendlyUnitCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Yellow] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base =
                    [
                        "P1-EQUIPMENT-BLADE-RUINED-KING",
                        "P1-UNIT-ASSEMBLE-TARGET",
                        "P1-UNIT-ASSEMBLE-COST"
                    ]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-BLADE-RUINED-KING"] = new(
                    "P1-EQUIPMENT-BLADE-RUINED-KING",
                    cardNo: "SFD·178/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-ASSEMBLE-COST"] = new(
                    "P1-UNIT-ASSEMBLE-COST",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-blade-destroy-friendly-unit", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-BLADE-RUINED-KING",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_YELLOW", "DESTROY_FRIENDLY_UNIT:P1-UNIT-ASSEMBLE-COST"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        Assert.Equal(["COST_PAID", "UNIT_DESTROYED", "ABILITY_ACTIVATED", "STACK_ITEM_ADDED", "PRIORITY_PASSED", "PRIORITY_PASSED", "STACK_ITEM_RESOLVED", "EQUIPMENT_ATTACHED"], result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(3, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            ["P1-EQUIPMENT-BLADE-RUINED-KING", "P1-UNIT-ASSEMBLE-TARGET"],
            result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-UNIT-ASSEMBLE-COST"], result.State.PlayerZones["P1"].Graveyard);
        CardZoneTestAssertions.RetainedOutsidePlay(result.State, "P1-UNIT-ASSEMBLE-COST", "GRAVEYARD");
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-BLADE-RUINED-KING"].AttachedToObjectId);
        Assert.Equal(["P1"], result.State.DestroyedUnitOwnerIdsThisTurn);
        Assert.Empty(result.State.StackItems);

        var costPaidPayload = result.Events[0].Payload;
        Assert.Equal(1, costPaidPayload["power"]);
        Assert.Equal(0, costPaidPayload["experience"]);
        Assert.Equal(
            ["ASSEMBLE_YELLOW", "DESTROY_FRIENDLY_UNIT:P1-UNIT-ASSEMBLE-COST"],
            Assert.IsAssignableFrom<IEnumerable<string>>(costPaidPayload["optionalCosts"]).ToArray());
        Assert.Equal(
            ["P1-UNIT-ASSEMBLE-COST"],
            Assert.IsAssignableFrom<IEnumerable<string>>(costPaidPayload["destroyedAdditionalCostTargetObjectIds"]).ToArray());

        var destroyedPayload = result.Events[1].Payload;
        Assert.Equal("P1-UNIT-ASSEMBLE-COST", destroyedPayload["targetObjectId"]);
        Assert.Equal("GRAVEYARD", destroyedPayload["destinationZone"]);
        Assert.Equal("ADDITIONAL_COST", destroyedPayload["reason"]);

        var sameTargetResult = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-blade-destroy-host", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-BLADE-RUINED-KING",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_YELLOW", "DESTROY_FRIENDLY_UNIT:P1-UNIT-ASSEMBLE-TARGET"]),
            CancellationToken.None);
        sameTargetResult = await EquipTestDriver.FinishAsync(sameTargetResult);
        Assert.False(sameTargetResult.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, sameTargetResult.ErrorCode);
        Assert.Empty(sameTargetResult.Events);
        Assert.Equal(0, sameTargetResult.State.Tick);
        Assert.Equal(
            ["P1-EQUIPMENT-BLADE-RUINED-KING", "P1-UNIT-ASSEMBLE-TARGET", "P1-UNIT-ASSEMBLE-COST"],
            sameTargetResult.State.PlayerZones["P1"].Base);
        Assert.Null(sameTargetResult.State.CardObjects["P1-EQUIPMENT-BLADE-RUINED-KING"].AttachedToObjectId);

        var missingDestroyCostResult = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-blade-missing-destroy", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-BLADE-RUINED-KING",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_YELLOW"]),
            CancellationToken.None);
        missingDestroyCostResult = await EquipTestDriver.FinishAsync(missingDestroyCostResult);
        Assert.False(missingDestroyCostResult.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, missingDestroyCostResult.ErrorCode);
        Assert.Empty(missingDestroyCostResult.Events);
        Assert.Equal(0, missingDestroyCostResult.State.Tick);
        Assert.Equal(1, missingDestroyCostResult.State.RunePools["P1"].PowerByTrait[RuneTrait.Yellow]);
        Assert.True(missingDestroyCostResult.State.CardObjects.ContainsKey("P1-UNIT-ASSEMBLE-COST"));
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesLastRitesWithGraveyardRecycleCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Purple] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-LAST-RITES", "P1-UNIT-ASSEMBLE-TARGET"],
                    Graveyard = ["P1-LAST-RITES-RECYCLE-001", "P1-LAST-RITES-RECYCLE-002"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-LAST-RITES"] = new(
                    "P1-EQUIPMENT-LAST-RITES",
                    cardNo: "SFD·150/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LAST-RITES-RECYCLE-001"] = new(
                    "P1-LAST-RITES-RECYCLE-001",
                    cardNo: "SFD·013/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LAST-RITES-RECYCLE-002"] = new(
                    "P1-LAST-RITES-RECYCLE-002",
                    cardNo: "SFD·067/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-last-rites-recycle-graveyard", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-LAST-RITES",
                "P1-UNIT-ASSEMBLE-TARGET",
                [
                    "ASSEMBLE_PURPLE",
                    "RECYCLE_GRAVEYARD_CARD:P1-LAST-RITES-RECYCLE-001",
                    "RECYCLE_GRAVEYARD_CARD:P1-LAST-RITES-RECYCLE-002"
                ]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        Assert.Equal([ "COST_PAID", "CARDS_RECYCLED", "ABILITY_ACTIVATED", "STACK_ITEM_ADDED", "PRIORITY_PASSED", "PRIORITY_PASSED", "STACK_ITEM_RESOLVED", "EQUIPMENT_ATTACHED" ], result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(3, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-LAST-RITES", "P1-UNIT-ASSEMBLE-TARGET"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(
            ["P1-LAST-RITES-RECYCLE-001", "P1-LAST-RITES-RECYCLE-002"],
            result.State.PlayerZones["P1"].MainDeck.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal("P1-UNIT-ASSEMBLE-TARGET", result.State.CardObjects["P1-EQUIPMENT-LAST-RITES"].AttachedToObjectId);
        Assert.Empty(result.State.StackItems);

        var costPaidPayload = result.Events[0].Payload;
        Assert.Equal(1, costPaidPayload["power"]);
        Assert.Equal(
            [
                "RECYCLE_GRAVEYARD_CARD:P1-LAST-RITES-RECYCLE-001",
                "RECYCLE_GRAVEYARD_CARD:P1-LAST-RITES-RECYCLE-002"
            ],
            Assert.IsAssignableFrom<IEnumerable<string>>(costPaidPayload["recycledAdditionalCostTargetObjectIds"])
                .Select(objectId => $"RECYCLE_GRAVEYARD_CARD:{objectId}")
                .Order(StringComparer.Ordinal)
                .ToArray());

        var recyclePayload = result.Events[1].Payload;
        Assert.Equal("ADDITIONAL_COST", recyclePayload["reason"]);
        Assert.Equal(2, recyclePayload["count"]);
        Assert.Equal(
            ["P1-LAST-RITES-RECYCLE-001", "P1-LAST-RITES-RECYCLE-002"],
            Assert.IsAssignableFrom<IEnumerable<string>>(recyclePayload["cardIds"])
                .Order(StringComparer.Ordinal)
                .ToArray());

        Assert.Equal("MAIN_DECK", result.State.ObjectLocations["P1-LAST-RITES-RECYCLE-001"].Zone);
        Assert.Equal("MAIN_DECK", result.State.ObjectLocations["P1-LAST-RITES-RECYCLE-002"].Zone);

        var missingRecycleCostResult = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-last-rites-missing-recycle", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-LAST-RITES",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_PURPLE", "RECYCLE_GRAVEYARD_CARD:P1-LAST-RITES-RECYCLE-001"]),
            CancellationToken.None);
        missingRecycleCostResult = await EquipTestDriver.FinishAsync(missingRecycleCostResult);

        Assert.False(missingRecycleCostResult.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, missingRecycleCostResult.ErrorCode);
        Assert.Empty(missingRecycleCostResult.Events);
        Assert.Equal(0, missingRecycleCostResult.State.Tick);
        Assert.Equal(1, missingRecycleCostResult.State.RunePools["P1"].PowerByTrait[RuneTrait.Purple]);
        Assert.Equal(
            ["P1-LAST-RITES-RECYCLE-001", "P1-LAST-RITES-RECYCLE-002"],
            missingRecycleCostResult.State.PlayerZones["P1"].Graveyard);
        Assert.Null(missingRecycleCostResult.State.CardObjects["P1-EQUIPMENT-LAST-RITES"].AttachedToObjectId);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAttachesHextechGauntletWithTargetPowerManaReduction()
    {
        var state = PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(1, 1),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-HEXTECH-GAUNTLET", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-HEXTECH-GAUNTLET"] = new(
                    "P1-EQUIPMENT-HEXTECH-GAUNTLET",
                    cardNo: "UNL-188/219",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-hextech-gauntlet", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-HEXTECH-GAUNTLET",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_3_ANY_POWER"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        Assert.Equal(["COST_PAID", "ABILITY_ACTIVATED", "STACK_ITEM_ADDED", "PRIORITY_PASSED", "PRIORITY_PASSED", "STACK_ITEM_RESOLVED", "EQUIPMENT_ATTACHED"], result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(3, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-HEXTECH-GAUNTLET"].AttachedToObjectId);

        var costPaidPayload = result.Events[0].Payload;
        Assert.Equal(1, costPaidPayload["mana"]);
        Assert.Equal(3, costPaidPayload["baseManaCost"]);
        Assert.Equal(2, costPaidPayload["targetPowerManaReduction"]);
        Assert.Equal(1, costPaidPayload["power"]);
        Assert.Equal(["ASSEMBLE_3_ANY_POWER"], Assert.IsType<string[]>(costPaidPayload["optionalCosts"]));

        var insufficientManaState = state with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(0, 1),
                ["P2"] = RunePool.Empty
            }
        };
        var insufficientManaResult = await new CoreRuleEngine().ResolveAsync(
            insufficientManaState,
            new PlayerIntent("intent-p4-assemble-equipment-hextech-gauntlet-insufficient-mana", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-HEXTECH-GAUNTLET",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_3_ANY_POWER"]),
            CancellationToken.None);
        insufficientManaResult = await EquipTestDriver.FinishAsync(insufficientManaResult);

        Assert.False(insufficientManaResult.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, insufficientManaResult.ErrorCode);
        Assert.Empty(insufficientManaResult.Events);
        Assert.Equal(0, insufficientManaResult.State.Tick);
        Assert.Equal(new RunePool(0, 1), insufficientManaResult.State.RunePools["P1"]);
        Assert.Null(insufficientManaResult.State.CardObjects["P1-EQUIPMENT-HEXTECH-GAUNTLET"].AttachedToObjectId);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandWithHandSourceIsRejectedUntilEquipmentSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-LONG-SWORD"],
                    Base = ["P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-LONG-SWORD"] = new(
                    "P1-HAND-LONG-SWORD",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-hand-source", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-HAND-LONG-SWORD",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_RED"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前装备装配路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-HAND-LONG-SWORD"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-UNIT-ASSEMBLE-TARGET"], result.State.PlayerZones["P1"].Base);
        Assert.Null(result.State.CardObjects["P1-HAND-LONG-SWORD"].AttachedToObjectId);
        Assert.Equal(3, result.State.CardObjects["P1-UNIT-ASSEMBLE-TARGET"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandWithOpponentSourceIsRejectedUntilEquipmentSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-EQUIPMENT-LONG-SWORD"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-EQUIPMENT-LONG-SWORD"] = new(
                    "P2-EQUIPMENT-LONG-SWORD",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-opponent-source", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P2-EQUIPMENT-LONG-SWORD",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_RED"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前装备装配路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-ASSEMBLE-TARGET"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-EQUIPMENT-LONG-SWORD"], result.State.PlayerZones["P2"].Base);
        Assert.Null(result.State.CardObjects["P2-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        Assert.Equal(3, result.State.CardObjects["P1-UNIT-ASSEMBLE-TARGET"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandWithNonEquipmentSourceIsRejectedUntilEquipmentSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-UNIT-ASSEMBLE-NON-EQUIPMENT-SOURCE", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-ASSEMBLE-NON-EQUIPMENT-SOURCE"] = new(
                    "P1-UNIT-ASSEMBLE-NON-EQUIPMENT-SOURCE",
                    power: 2,
                    tags: [CardObjectTags.UnitCard]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-non-equipment-source", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-UNIT-ASSEMBLE-NON-EQUIPMENT-SOURCE",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_RED"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前装备装配路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            ["P1-UNIT-ASSEMBLE-NON-EQUIPMENT-SOURCE", "P1-UNIT-ASSEMBLE-TARGET"],
            result.State.PlayerZones["P1"].Base);
        Assert.Equal(2, result.State.CardObjects["P1-UNIT-ASSEMBLE-NON-EQUIPMENT-SOURCE"].Power);
        Assert.Equal(3, result.State.CardObjects["P1-UNIT-ASSEMBLE-TARGET"].Power);
        Assert.Null(result.State.CardObjects["P1-UNIT-ASSEMBLE-NON-EQUIPMENT-SOURCE"].AttachedToObjectId);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandWithNonUnitTargetIsRejectedUntilEquipmentSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-LONG-SWORD", "P1-EQUIPMENT-ASSEMBLE-NON-UNIT-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-LONG-SWORD"] = new(
                    "P1-EQUIPMENT-LONG-SWORD",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"]),
                ["P1-EQUIPMENT-ASSEMBLE-NON-UNIT-TARGET"] = new(
                    "P1-EQUIPMENT-ASSEMBLE-NON-UNIT-TARGET",
                    tags: [CardObjectTags.EquipmentCard, "武装"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-non-unit-target", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-LONG-SWORD",
                "P1-EQUIPMENT-ASSEMBLE-NON-UNIT-TARGET",
                ["ASSEMBLE_RED"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前装备装配路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            ["P1-EQUIPMENT-LONG-SWORD", "P1-EQUIPMENT-ASSEMBLE-NON-UNIT-TARGET"],
            result.State.PlayerZones["P1"].Base);
        Assert.Null(result.State.CardObjects["P1-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        Assert.Null(result.State.CardObjects["P1-EQUIPMENT-ASSEMBLE-NON-UNIT-TARGET"].AttachedToObjectId);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandWithUnsupportedOptionalCostIsRejectedUntilEquipmentSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-LONG-SWORD", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-LONG-SWORD"] = new(
                    "P1-EQUIPMENT-LONG-SWORD",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-unsupported-optional-cost", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-LONG-SWORD",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ECHO"]),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前装备装配路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-LONG-SWORD", "P1-UNIT-ASSEMBLE-TARGET"], result.State.PlayerZones["P1"].Base);
        Assert.Null(result.State.CardObjects["P1-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        Assert.Equal(3, result.State.CardObjects["P1-UNIT-ASSEMBLE-TARGET"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandWithMissingOptionalCostIsRejectedUntilEquipmentSystemExists()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-LONG-SWORD", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-LONG-SWORD"] = new(
                    "P1-EQUIPMENT-LONG-SWORD",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"]),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-assemble-equipment-missing-optional-cost", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-LONG-SWORD",
                "P1-UNIT-ASSEMBLE-TARGET",
                []),
            CancellationToken.None);
        result = await EquipTestDriver.FinishAsync(result);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前装备装配路径尚未由服务端开放。", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-LONG-SWORD", "P1-UNIT-ASSEMBLE-TARGET"], result.State.PlayerZones["P1"].Base);
        Assert.Null(result.State.CardObjects["P1-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        Assert.Equal(3, result.State.CardObjects["P1-UNIT-ASSEMBLE-TARGET"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandLongSwordAttachFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-assemble-equipment-long-sword-attach.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-LONG-SWORD", "P1-UNIT-ASSEMBLE-TARGET"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.FinalState.CardObjects["P1-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P5EquipmentStateAssembleLongSwordOwnerControllerFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p5-equipment-state-assemble-long-sword-owner-controller.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalState.Tick);
        Assert.Equal("P1", result.FinalState.CardObjects["P1-EQUIPMENT-LONG-SWORD"].OwnerId);
        Assert.Equal("P1", result.FinalState.CardObjects["P1-EQUIPMENT-LONG-SWORD"].ControllerId);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.FinalState.CardObjects["P1-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        Assert.Equal("P1", result.FinalState.CardObjects["P1-UNIT-ASSEMBLE-TARGET"].OwnerId);
        Assert.Equal("P1", result.FinalState.CardObjects["P1-UNIT-ASSEMBLE-TARGET"].ControllerId);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandPriorityWindowRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-assemble-equipment-priority-window-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal("NEUTRAL_CLOSED", result.FinalState.TimingState);
        Assert.Equal("P1", result.FinalState.PriorityPlayerId);
        Assert.Equal(["P1-EQUIPMENT-LONG-SWORD", "P1-UNIT-ASSEMBLE-TARGET"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Null(result.FinalState.CardObjects["P1-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        var stackItem = Assert.Single(result.FinalState.StackItems);
        Assert.Equal("STACK-0-P2-SPELL-PROBE", stackItem.StackItemId);
        Assert.Equal("P2", stackItem.ControllerId);
        Assert.Equal("PENDING_TEST_SPELL", stackItem.EffectKind);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandAlreadyAttachedSourceRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-assemble-equipment-already-attached-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(
            ["P1-EQUIPMENT-LONG-SWORD", "P1-UNIT-ASSEMBLE-OLD-TARGET", "P1-UNIT-ASSEMBLE-TARGET"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-OLD-TARGET",
            result.FinalState.CardObjects["P1-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        Assert.Equal(2, result.FinalState.CardObjects["P1-UNIT-ASSEMBLE-OLD-TARGET"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-ASSEMBLE-TARGET"].Power);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandMissingTargetRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-assemble-equipment-missing-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-LONG-SWORD"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Null(result.FinalState.CardObjects["P1-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandUnknownTargetRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-assemble-equipment-unknown-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-LONG-SWORD"], result.FinalState.PlayerZones["P1"].Base);
        Assert.False(result.FinalState.CardObjects.ContainsKey("P1-UNIT-ASSEMBLE-MISSING"));
        Assert.Null(result.FinalState.CardObjects["P1-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandMissingSourceRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-assemble-equipment-missing-source-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-ASSEMBLE-TARGET"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-ASSEMBLE-TARGET"].Power);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandUnknownSourceRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-assemble-equipment-unknown-source-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-ASSEMBLE-TARGET"], result.FinalState.PlayerZones["P1"].Base);
        Assert.False(result.FinalState.CardObjects.ContainsKey("P1-EQUIPMENT-LONG-SWORD-MISSING"));
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-ASSEMBLE-TARGET"].Power);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandHandSourceRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-assemble-equipment-hand-source-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HAND-LONG-SWORD"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-UNIT-ASSEMBLE-TARGET"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Null(result.FinalState.CardObjects["P1-HAND-LONG-SWORD"].AttachedToObjectId);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-ASSEMBLE-TARGET"].Power);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandOpponentSourceRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-assemble-equipment-opponent-source-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-ASSEMBLE-TARGET"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P2-EQUIPMENT-LONG-SWORD"], result.FinalState.PlayerZones["P2"].Base);
        Assert.Null(result.FinalState.CardObjects["P2-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-ASSEMBLE-TARGET"].Power);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandNonEquipmentSourceRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-assemble-equipment-non-equipment-source-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(
            ["P1-UNIT-ASSEMBLE-NON-EQUIPMENT-SOURCE", "P1-UNIT-ASSEMBLE-TARGET"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(2, result.FinalState.CardObjects["P1-UNIT-ASSEMBLE-NON-EQUIPMENT-SOURCE"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-ASSEMBLE-TARGET"].Power);
        Assert.Null(result.FinalState.CardObjects["P1-UNIT-ASSEMBLE-NON-EQUIPMENT-SOURCE"].AttachedToObjectId);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandNonUnitTargetRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-assemble-equipment-non-unit-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(
            ["P1-EQUIPMENT-LONG-SWORD", "P1-EQUIPMENT-ASSEMBLE-NON-UNIT-TARGET"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Null(result.FinalState.CardObjects["P1-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        Assert.Null(result.FinalState.CardObjects["P1-EQUIPMENT-ASSEMBLE-NON-UNIT-TARGET"].AttachedToObjectId);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandUnsupportedOptionalCostRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-assemble-equipment-unsupported-optional-cost-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-LONG-SWORD", "P1-UNIT-ASSEMBLE-TARGET"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Null(result.FinalState.CardObjects["P1-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-ASSEMBLE-TARGET"].Power);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4AssembleEquipmentCommandMissingOptionalCostRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-assemble-equipment-missing-optional-cost-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-LONG-SWORD", "P1-UNIT-ASSEMBLE-TARGET"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Null(result.FinalState.CardObjects["P1-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-ASSEMBLE-TARGET"].Power);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandDealsCombatDamageForRepresentativePath()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-GAREN"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-MUTANT-KITTEN"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GAREN"] = new(
                    "P1-BATTLEFIELD-GAREN",
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "强攻2", "坚守2"]),
                ["P2-BATTLEFIELD-MUTANT-KITTEN"] = new(
                    "P2-BATTLEFIELD-MUTANT-KITTEN",
                    cardNo: "UNL-036/219",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "坚守2", "壁垒", "猫科"])
            }
        };

        state = P4PhysicalBattlefieldState(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-premodel", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-GAREN"],
                ["P2-BATTLEFIELD-MUTANT-KITTEN"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        Assert.Collection(
            result.Events,
            battleEvent =>
            {
                Assert.Equal("BATTLE_DECLARED", battleEvent.Kind);
                Assert.Equal("P1", battleEvent.Payload["playerId"]);
                Assert.Equal("BATTLEFIELD:P1-MAIN", battleEvent.Payload["battlefieldId"]);
                Assert.Equal(["P1-BATTLEFIELD-GAREN"], Assert.IsType<string[]>(battleEvent.Payload["attackerObjectIds"]));
                Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], Assert.IsType<string[]>(battleEvent.Payload["defenderObjectIds"]));
                Assert.Equal(["COMBAT_ASSIGNMENT"], Assert.IsType<string[]>(battleEvent.Payload["optionalCosts"]));
            },
            attackerDamageEvent =>
            {
                Assert.Equal("DAMAGE_APPLIED", attackerDamageEvent.Kind);
                Assert.Equal("P1-BATTLEFIELD-GAREN", attackerDamageEvent.Payload["sourceObjectId"]);
                Assert.Equal("P2-BATTLEFIELD-MUTANT-KITTEN", attackerDamageEvent.Payload["targetObjectId"]);
                Assert.Equal("ATTACKER", attackerDamageEvent.Payload["combatRole"]);
                Assert.Equal("强攻", attackerDamageEvent.Payload["keyword"]);
                Assert.Equal(5, attackerDamageEvent.Payload["basePower"]);
                Assert.Equal(2, attackerDamageEvent.Payload["keywordBonus"]);
                Assert.Equal(7, attackerDamageEvent.Payload["combatPower"]);
                Assert.Equal(7, attackerDamageEvent.Payload["damage"]);
            },
            defenderDamageEvent =>
            {
                Assert.Equal("DAMAGE_APPLIED", defenderDamageEvent.Kind);
                Assert.Equal("P2-BATTLEFIELD-MUTANT-KITTEN", defenderDamageEvent.Payload["sourceObjectId"]);
                Assert.Equal("P1-BATTLEFIELD-GAREN", defenderDamageEvent.Payload["targetObjectId"]);
                Assert.Equal("DEFENDER", defenderDamageEvent.Payload["combatRole"]);
                Assert.Equal("坚守", defenderDamageEvent.Payload["keyword"]);
                Assert.Equal(1, defenderDamageEvent.Payload["basePower"]);
                Assert.Equal(2, defenderDamageEvent.Payload["keywordBonus"]);
                Assert.Equal(3, defenderDamageEvent.Payload["combatPower"]);
                Assert.Equal(3, defenderDamageEvent.Payload["damage"]);
            },
            destroyedEvent =>
            {
                Assert.Equal("UNIT_DESTROYED", destroyedEvent.Kind);
                Assert.Equal("P1-BATTLEFIELD-GAREN", destroyedEvent.Payload["sourceObjectId"]);
                Assert.Equal("P2-BATTLEFIELD-MUTANT-KITTEN", destroyedEvent.Payload["targetObjectId"]);
                Assert.Equal("P2", destroyedEvent.Payload["ownerPlayerId"]);
                Assert.Equal("P1", destroyedEvent.Payload["destroyedByPlayerId"]);
                Assert.Equal("GRAVEYARD", destroyedEvent.Payload["destinationZone"]);
                Assert.Equal("LETHAL_DAMAGE", destroyedEvent.Payload["reason"]);
            },
            damageRemovedEvent =>
            {
                Assert.Equal("DAMAGE_REMOVED", damageRemovedEvent.Kind);
                Assert.Equal("BATTLEFIELD:P1-MAIN", damageRemovedEvent.Payload["battlefieldId"]);
                Assert.Equal(["P1-BATTLEFIELD-GAREN"], Assert.IsType<string[]>(damageRemovedEvent.Payload["objectIds"]));
                Assert.Equal(1, damageRemovedEvent.Payload["count"]);
                var previousDamageByObject = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(damageRemovedEvent.Payload["previousDamageByObject"]);
                Assert.Equal(3, previousDamageByObject["P1-BATTLEFIELD-GAREN"]);
                Assert.Equal(3, damageRemovedEvent.Payload["totalDamageRemoved"]);
                Assert.Equal("BATTLE_CLEANUP", damageRemovedEvent.Payload["reason"]);
            },
            cleanupEvent =>
            {
                Assert.Equal("BATTLE_CLEANUP_COMPLETED", cleanupEvent.Kind);
                var cleanup = Assert.IsType<BattleResultContinuation>(cleanupEvent.Payload["continuation"]);
                Assert.Equal("BATTLEFIELD:P1-MAIN", cleanup.BattlefieldId);
                Assert.Equal("CLEANUP_TRIGGERS", cleanup.Stage);
                Assert.Null(cleanup.WinnerPlayerId); Assert.False(cleanup.AttackerRecalledByBattleCleanup);
            },
            resultEvent =>
            {
                Assert.Equal("BATTLE_RESULT_DETERMINED", resultEvent.Kind);
                Assert.Equal("P1", Assert.IsType<BattleResultContinuation>(resultEvent.Payload["continuation"]).WinnerPlayerId);
            },
            controlEvent =>
            {
                Assert.Equal("BATTLEFIELD_CONTROL_RESOLVED", controlEvent.Kind);
                Assert.Equal("P2", controlEvent.Payload["previousControllerId"]);
                Assert.Equal("P1", controlEvent.Payload["controllerId"]);
            },
            conquestEvent =>
            {
                Assert.Equal("BATTLEFIELD_CONQUERED", conquestEvent.Kind);
                Assert.Equal("P1", conquestEvent.Payload["playerId"]);
                Assert.Equal("BATTLEFIELD:P1-MAIN", conquestEvent.Payload["battlefieldId"]);
            },
            scoreEvent =>
            {
                Assert.Equal("SCORE_GAINED", scoreEvent.Kind);
                Assert.Equal("P1", scoreEvent.Payload["playerId"]);
            },
            closedEvent =>
            {
                Assert.Equal("BATTLE_CLOSED", closedEvent.Kind);
            });
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-GAREN"], result.State.PlayerZones["P1"].Battlefields.Where(id => id != "BATTLEFIELD:P1-MAIN").ToArray());
        Assert.Empty(result.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-MUTANT-KITTEN"], result.State.PlayerZones["P2"].Graveyard);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-GAREN"].IsAttacking);
        CardZoneTestAssertions.RetainedOutsidePlay(result.State, "P2-BATTLEFIELD-MUTANT-KITTEN", "GRAVEYARD");
        Assert.Equal(0, result.State.CardObjects["P1-BATTLEFIELD-GAREN"].Damage);
        Assert.Equal(5, result.State.CardObjects["P1-BATTLEFIELD-GAREN"].Power);
        Assert.Equal(["P2"], result.State.DestroyedUnitOwnerIdsThisTurn);
        Assert.Equal(1, result.State.PlayerScores["P1"]);
        Assert.Equal("P1", result.State.CardObjects["BATTLEFIELD:P1-MAIN"].ControllerId);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeclareBattleCommandStunnedAttackerContributesZeroCombatPower()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-STUNNED-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-STUNNED-ATTACKER"] = new(
                    "P1-BATTLEFIELD-STUNNED-ATTACKER",
                    untilEndOfTurnEffects: ["STUNNED"],
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "强攻2"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-DEFENDER"] = new(
                    "P2-BATTLEFIELD-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
        state = P4PhysicalBattlefieldState(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-stunned-attacker", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-STUNNED-ATTACKER"],
                ["P2-BATTLEFIELD-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "ATTACKER", StringComparison.Ordinal));
        var defenderDamageEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal));
        Assert.Equal("P2-BATTLEFIELD-DEFENDER", defenderDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal("P1-BATTLEFIELD-STUNNED-ATTACKER", defenderDamageEvent.Payload["targetObjectId"]);
        Assert.Equal("DEFENDER", defenderDamageEvent.Payload["combatRole"]);
        Assert.Equal(2, defenderDamageEvent.Payload["basePower"]);
        Assert.Equal(0, defenderDamageEvent.Payload["keywordBonus"]);
        Assert.Equal(2, defenderDamageEvent.Payload["combatPower"]);
        Assert.Equal(2, defenderDamageEvent.Payload["damage"]);
        var noResultEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_NO_RESULT", StringComparison.Ordinal));
        Assert.Equal("BATTLE_CLEANUP_ATTACKER_RECALL", noResultEvent.Payload["reason"]);
        var damageRemovedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_REMOVED", StringComparison.Ordinal));
        Assert.Equal(["P1-BATTLEFIELD-STUNNED-ATTACKER"], Assert.IsType<string[]>(damageRemovedEvent.Payload["objectIds"]));
        var previousDamageByObject = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(damageRemovedEvent.Payload["previousDamageByObject"]);
        Assert.Equal(2, previousDamageByObject["P1-BATTLEFIELD-STUNNED-ATTACKER"]);
        Assert.Equal(2, damageRemovedEvent.Payload["totalDamageRemoved"]);
        Assert.Equal("BATTLE_CLEANUP", damageRemovedEvent.Payload["reason"]);
        var recalledEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_RECALLED_TO_BASE", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-STUNNED-ATTACKER", recalledEvent.Payload["targetObjectId"]);
        Assert.Equal("BATTLEFIELD:P1-MAIN", recalledEvent.Payload["battlefieldId"]);
        Assert.Equal("BATTLE_CLEANUP_ATTACKER_RECALL", recalledEvent.Payload["reason"]);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));

        Assert.Empty(result.State.PlayerZones["P1"].Battlefields.Where(id => id != "BATTLEFIELD:P1-MAIN").ToArray());
        Assert.Equal(["P1-BATTLEFIELD-STUNNED-ATTACKER"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-BATTLEFIELD-DEFENDER"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(0, result.State.CardObjects["P1-BATTLEFIELD-STUNNED-ATTACKER"].Damage);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-STUNNED-ATTACKER"].IsAttacking);
        Assert.Equal(0, result.State.CardObjects["P2-BATTLEFIELD-DEFENDER"].Damage);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.PlayerZones["P2"].Graveyard);
        Assert.Empty(result.State.StackItems);
        Assert.Contains("BATTLEFIELD:P1-MAIN", result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal("P2", result.State.CardObjects["BATTLEFIELD:P1-MAIN"].ControllerId);
        var battleResolution = Assert.Single(result.State.BattleResolutions);
        Assert.Equal("NO_RESULT", battleResolution.Kind);
        Assert.Equal("BATTLE_CLEANUP_ATTACKER_RECALL", battleResolution.Reason);
        Assert.Contains("DAMAGE_REMOVED", battleResolution.RelatedEventKinds);
        Assert.Contains("UNIT_RECALLED_TO_BASE", battleResolution.RelatedEventKinds);
        var snapshotBattleResolutions = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(
            result.Snapshots["P1"].Timing["battleResolutions"]);
        var snapshotBattleResolution = Assert.Single(snapshotBattleResolutions);
        var snapshotRelatedEventKinds = Assert.IsAssignableFrom<IReadOnlyList<string>>(snapshotBattleResolution["relatedEventKinds"]);
        Assert.Contains("DAMAGE_REMOVED", snapshotRelatedEventKinds);
        Assert.Contains("UNIT_RECALLED_TO_BASE", snapshotRelatedEventKinds);
    }

    [Theory]
    [InlineData(5, false)]
    [InlineData(7, true)]
    public async Task P4DeclareBattleCommandStunnedDefenderContributesZeroCombatPowerButCanBeDestroyed(int attackerPower, bool lethal)
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-STUNNED-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-ATTACKER"] = new(
                    "P1-BATTLEFIELD-ATTACKER",
                    cardNo: "OGS·007/024",
                    power: attackerPower,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-STUNNED-DEFENDER"] = new(
                    "P2-BATTLEFIELD-STUNNED-DEFENDER",
                    untilEndOfTurnEffects: ["STUNNED"],
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "坚守2"],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-declare-battle-stunned-defender", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-ATTACKER"],
                ["P2-BATTLEFIELD-STUNNED-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "DEFENDER", StringComparison.Ordinal));
        var attackerDamageEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-ATTACKER", attackerDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal("P2-BATTLEFIELD-STUNNED-DEFENDER", attackerDamageEvent.Payload["targetObjectId"]);
        Assert.Equal("ATTACKER", attackerDamageEvent.Payload["combatRole"]);
        Assert.Equal(attackerPower, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(0, attackerDamageEvent.Payload["keywordBonus"]);
        Assert.Equal(attackerPower, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(attackerPower, attackerDamageEvent.Payload["damage"]);
        // Stun suppresses damage contribution, not the defender's +2 current might.
        // Five marked damage is nonlethal; seven still destroys the stunned unit.
        if (!lethal)
        {
            Assert.DoesNotContain(result.Events, e => e.Kind == "UNIT_DESTROYED");
            Assert.Empty(result.State.PlayerZones["P2"].Graveyard);
            Assert.Contains("P2-BATTLEFIELD-STUNNED-DEFENDER", result.State.PlayerZones["P2"].Battlefields);
            Assert.Contains("P1-BATTLEFIELD-ATTACKER", result.State.PlayerZones["P1"].Base);
            Assert.Equal(0, result.State.CardObjects["P2-BATTLEFIELD-STUNNED-DEFENDER"].Damage);
            Assert.Empty(result.State.StackItems);
            return;
        }
        var destroyedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-ATTACKER", destroyedEvent.Payload["sourceObjectId"]);
        Assert.Equal("P2-BATTLEFIELD-STUNNED-DEFENDER", destroyedEvent.Payload["targetObjectId"]);
        Assert.Equal("P2", destroyedEvent.Payload["ownerPlayerId"]);
        Assert.Equal("P1", destroyedEvent.Payload["destroyedByPlayerId"]);
        Assert.Equal("LETHAL_DAMAGE", destroyedEvent.Payload["reason"]);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));

        Assert.Equal(["P1-BATTLEFIELD-ATTACKER"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-STUNNED-DEFENDER"], result.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(0, result.State.CardObjects["P1-BATTLEFIELD-ATTACKER"].Damage);
        CardZoneTestAssertions.RetainedOutsidePlay(result.State, "P2-BATTLEFIELD-STUNNED-DEFENDER", "GRAVEYARD");
        Assert.Equal(["P2"], result.State.DestroyedUnitOwnerIdsThisTurn);
        Assert.Empty(result.State.StackItems);
    }
}
