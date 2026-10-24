using System.Reflection;
using System.Security.Cryptography;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;

namespace Riftbound.ConformanceTests;

public sealed class GlobalBattleCleanupTests
{
    public GlobalBattleCleanupTests(ITestOutputHelper output)
    {
        var engine = typeof(CoreRuleEngine).Assembly;
        output.WriteLine($"Engine={engine.Location}; SHA256={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(engine.Location))).ToLowerInvariant()}; MVID={engine.ManifestModule.ModuleVersionId}; RIF={RuleInputIdentity.Current.IdentitySha256}");
    }

    // CN 468.1a.1 removes damage from every unit at battle cleanup. The
    // participant lists still control recall, but do not limit this healing.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BattleCleanupHealsBaseAndNonparticipatingUnitsEvenWithoutSurvivingCombatants(bool survivors)
    {
        var (zones, cards) = Position();
        var previous = cards.ToDictionary(entry => entry.Key, entry => entry.Value);
        var events = Cleanup(zones, cards, survivors);

        string[] healed = ["ATTACKER", "DEFENDER", "ESCAPED", "BASE-BYSTANDER", "OTHER-FIELD", "UNIT-GEAR"];
        foreach (var id in healed)
        {
            Assert.Equal(0, cards[id].Damage);
            Assert.Null(cards[id].DamageMarkedByPlayerIds);
            Assert.Equal(previous[id].Power, cards[id].Power);
            Assert.Equal(previous[id].ObjectGeneration, cards[id].ObjectGeneration);
        }
        foreach (var id in new[] { "HAND-UNIT", "GRAVE-UNIT", "STANDBY", "EQUIPMENT" })
            Assert.Equal(previous[id], cards[id]);

        var removed = Assert.Single(events, e => e.Kind == "DAMAGE_REMOVED");
        Assert.Equal("BATTLE_CLEANUP", removed.Payload["reason"]);
        Assert.Equal("F", removed.Payload["battlefieldId"]);
        Assert.Equal(healed.Length, removed.Payload["count"]);
        Assert.Equal(13, removed.Payload["totalDamageRemoved"]);
        var ids = Assert.IsAssignableFrom<IEnumerable<string>>(removed.Payload["objectIds"]);
        Assert.Equal(healed.Order(StringComparer.Ordinal), ids.Order(StringComparer.Ordinal));
        var priorDamage = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(removed.Payload["previousDamageByObject"]);
        Assert.Equal(healed.Order(StringComparer.Ordinal), priorDamage.Keys.Order(StringComparer.Ordinal));
        foreach (var id in healed) Assert.Equal(previous[id].Damage, priorDamage[id]);

        if (survivors)
        {
            Assert.Contains("ATTACKER", zones["P1"].Base);
            Assert.DoesNotContain("ATTACKER", zones["P1"].Battlefields);
        }
        else Assert.Contains("ATTACKER", zones["P1"].Battlefields);
    }

    [Fact]
    public void BattleCleanupWithoutDamagedUnitsDoesNotEmitDamageRemoval()
    {
        var (zones, cards) = Position();
        foreach (var id in new[] { "ATTACKER", "DEFENDER", "ESCAPED", "BASE-BYSTANDER", "OTHER-FIELD", "UNIT-GEAR" })
            cards[id] = cards[id] with { Damage = 0, DamageMarkedByPlayerIds = null };

        var events = Cleanup(zones, cards, survivors: true);

        Assert.DoesNotContain(events, e => e.Kind == "DAMAGE_REMOVED");
        Assert.Contains("ATTACKER", zones["P1"].Base);
        Assert.Equal(4, cards["HAND-UNIT"].Damage);
        Assert.Equal(5, cards["GRAVE-UNIT"].Damage);
    }

    private static (Dictionary<string, PlayerZones> Zones, Dictionary<string, CardObjectState> Cards) Position()
    {
        CardObjectState Unit(string id, string owner, int damage, params string[] extraTags)
            => new(id, power: 6, tags: [CardObjectTags.UnitCard, .. extraTags], ownerId: owner, controllerId: owner)
            {
                Damage = damage,
                DamageMarkedByPlayerIds = ["P2"],
                ObjectGeneration = 1
            };
        var cards = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["ATTACKER"] = Unit("ATTACKER", "P1", 1) with { IsAttacking = true },
            ["DEFENDER"] = Unit("DEFENDER", "P2", 2) with { IsDefending = true },
            ["ESCAPED"] = Unit("ESCAPED", "P1", 4),
            ["BASE-BYSTANDER"] = Unit("BASE-BYSTANDER", "P2", 2),
            ["OTHER-FIELD"] = Unit("OTHER-FIELD", "P1", 2),
            ["UNIT-GEAR"] = Unit("UNIT-GEAR", "P1", 2, CardObjectTags.EquipmentCard),
            ["HAND-UNIT"] = Unit("HAND-UNIT", "P1", 4),
            ["GRAVE-UNIT"] = Unit("GRAVE-UNIT", "P2", 5),
            ["STANDBY"] = Unit("STANDBY", "P2", 3) with { IsFaceDown = true },
            ["EQUIPMENT"] = new("EQUIPMENT", power: 0, tags: [CardObjectTags.EquipmentCard], ownerId: "P1", controllerId: "P1") { Damage = 6 }
        };
        var zones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
        {
            ["P1"] = PlayerZones.Empty with
            {
                Base = ["ESCAPED", "UNIT-GEAR", "EQUIPMENT"],
                Battlefields = ["ATTACKER", "OTHER-FIELD"],
                Hand = ["HAND-UNIT"]
            },
            ["P2"] = PlayerZones.Empty with
            {
                Base = ["BASE-BYSTANDER"],
                Battlefields = ["DEFENDER", "STANDBY"],
                Graveyard = ["GRAVE-UNIT"]
            }
        };
        return (zones, cards);
    }

    private static List<GameEvent> Cleanup(Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, bool survivors)
    {
        var events = new List<GameEvent>();
        var cleanup = typeof(CoreRuleEngine).GetMethod("ApplyBattleCleanup", BindingFlags.Static | BindingFlags.NonPublic)!;
        cleanup.Invoke(null, [zones, cards, "F", survivors ? new[] { "ATTACKER" } : Array.Empty<string>(),
            survivors ? new[] { "DEFENDER" } : Array.Empty<string>(), events]);
        return events;
    }
}
