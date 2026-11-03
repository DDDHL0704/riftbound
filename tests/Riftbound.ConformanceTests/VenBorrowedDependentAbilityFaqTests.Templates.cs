using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    [Theory]
    [InlineData("UNL-026/219", "UNIT", "点伤害", 3, 1, 0)]
    [InlineData("OGN·017/298", "EQUIPMENT", "点伤害", 2, 0, 0)]
    [InlineData("UNL-109/219", "UNIT", "单位活跃", 0, 0, 3)]
    [InlineData("OGN·124/298", "EQUIPMENT", "增益", 0, 0, 0)]
    [InlineData("VEN·147", "LEGEND", "壁垒", 0, 0, 0)]
    [InlineData("UNL-001/219", "UNIT", "战力", 0, 0, 0)]
    public async Task BorrowedTemplateUsesBorrowerForPaymentAndResolution(string donor, string category, string label,
        int damage, int red, int experience)
    {
        var game = await WithTemplate(donor, category);
        var id = TemplateId(game.State, label);
        var before = game.State;
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, ["ALLY"]));
        Assert.Equal(before.RunePools["P1"].PowerByTrait.GetValueOrDefault("red") - red,
            game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault("red"));
        Assert.Equal(before.PlayerExperience["P1"] - experience, game.State.PlayerExperience["P1"]);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.True(game.State.CardObjects["DONOR"].IsExhausted);
        await game.Top();
        Assert.Equal(damage, game.State.CardObjects["ALLY"].Damage);
        if (label == "单位活跃") Assert.False(game.State.CardObjects["ALLY"].IsExhausted);
        if (label == "增益") Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        if (label == "壁垒") Assert.Contains("COMBAT_KEYWORD:壁垒:1", game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        if (label == "战力") Assert.Equal(before.CardObjects["ALLY"].Power + 3, game.State.CardObjects["ALLY"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData("UNL-026/219", "UNIT", "点伤害", "wrong-color")]
    [InlineData("UNL-026/219", "UNIT", "点伤害", "source-base")]
    [InlineData("OGN·017/298", "EQUIPMENT", "点伤害", "target-base")]
    [InlineData("UNL-109/219", "UNIT", "单位活跃", "experience")]
    [InlineData("OGN·124/298", "EQUIPMENT", "增益", "active-target")]
    [InlineData("VEN·147", "LEGEND", "壁垒", "enemy-target")]
    [InlineData("UNL-001/219", "UNIT", "战力", "equipment-target")]
    public async Task BorrowedTemplateRejectsItsPrintedBoundaryWithoutSpending(string donor, string category, string label, string boundary)
    {
        var game = await WithTemplate(donor, category);
        var id = TemplateId(game.State, label);
        var state = game.State;
        if (boundary == "wrong-color") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(20, 0, new Dictionary<string, int> { ["blue"] = 20 }) } };
        if (boundary == "experience") state = state with { PlayerExperience = new Dictionary<string, int>(state.PlayerExperience) { ["P1"] = 2 } };
        if (boundary is "source-base" or "target-base") state = LocalDestructionRecallTests.At(state,
            boundary == "source-base" ? "HOST" : "ALLY", "P1", "BASE");
        if (boundary == "active-target") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with { IsExhausted = false } } };
        var target = boundary == "enemy-target" ? "ENEMY" : boundary == "equipment-target" ? "GEAR" : "ALLY";
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("borrowed-template-invalid", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("HOST", id, [target]), default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }

    private static string TemplateId(MatchState state, string label)
        => (ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? [])
            .Where(c => c.Enabled && c.Action == CommandTypes.ActivateAbility
                && c.Metadata?.GetValueOrDefault("sourceRequirements") is IEnumerable<IReadOnlyDictionary<string, object?>>)
            .SelectMany(c => (IEnumerable<IReadOnlyDictionary<string, object?>>)c.Metadata!["sourceRequirements"]!)
            .Where(r => Equals(r.GetValueOrDefault("sourceObjectId"), "HOST")
                && r.GetValueOrDefault("abilityLabel") is string text && text.Contains(label, StringComparison.Ordinal))
            .Select(r => (string)r["abilityId"]!).First();

    [Fact]
    public async Task BorrowedSwiftSkillUsesTheRealDuelFocusAndRejectsClosedPriority()
    {
        var initial = AttackDamageDistributionTests.Position("OGN·111/298");
        initial = AttackDamageDistributionTests.Spell(initial, "DUELREADY", "OGN·146/298", "P1");
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["DONOR"] = new("DONOR", cardNo: "VEN·147", ownerId: "P1", controllerId: "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = ["DONOR"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["DONOR"] = new("P1", "LEGEND") } };
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        await game.Act("P1", new PlayCardCommand("DUELREADY", "OGN·146/298", ["HOST"]));
        await game.Top();
        if (game.State.FocusPlayerId == "P2") await game.Act("P2", new PassFocusCommand());
        Assert.Equal(TimingStates.SpellDuelOpen, game.State.TimingState);
        var id = TemplateId(game.State, "壁垒");
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, ["HOST"]));
        Assert.Equal(TimingStates.SpellDuelClosed, game.State.TimingState);
        var closed = game.State;
        var rejected = await new CoreRuleEngine().ResolveAsync(closed, new("borrowed-swift-closed", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("HOST", id, ["HOST"]), default);
        Assert.False(rejected.Accepted);
        Assert.Equal(MatchStateHasher.Hash(closed), MatchStateHasher.Hash(rejected.State));
        await game.Top();
        Assert.Contains("COMBAT_KEYWORD:壁垒:1", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        Assert.Equal(TimingStates.SpellDuelOpen, game.State.TimingState);
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedDamageRunsTheSharedLethalDamageCleanup()
    {
        var game = await WithTemplate("OGN·017/298", "EQUIPMENT", allyPower: 1);
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(game.State, "点伤害"), ["ALLY"]));
        await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("GRAVEYARD", game.State.ObjectLocations["ALLY"].Zone);
        await game.Replay();
    }

    private static async Task<Driver> WithTemplate(string donor, string category, int allyPower = 7,
        bool empower = false, int experience = 5, RunePool? pool = null, Func<MatchState, MatchState>? adjust = null)
    {
        var game = await Ready("OGN·111/298", empower, adjust: initial => {
            initial = AttackDamageDistributionTests.Spell(initial, "READY2", "OGN·146/298", "P1");
            initial = OfficialHoldSequenceTests.AddUnit(initial, "ALLY", "OGN·001/298", "F", "P1");
            initial = OfficialHoldSequenceTests.AddUnit(initial, "ENEMY", "OGN·001/298", "P2-BASE", "P2");
            initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BASE");
            var tags = category == "UNIT" ? new[] { CardObjectTags.UnitCard }
                : category == "EQUIPMENT" ? new[] { CardObjectTags.EquipmentCard } : Array.Empty<string>();
            initial = initial with {
                RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
                    ["P1"] = pool ?? new(20, 20, new Dictionary<string, int> { ["red"] = 20 }) },
                PlayerExperience = new Dictionary<string, int>(initial.PlayerExperience) { ["P1"] = experience },
                CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                    ["ALLY"] = initial.CardObjects["ALLY"] with { Power = allyPower, IsExhausted = true },
                    ["DONOR"] = new("DONOR", cardNo: donor, power: category == "UNIT" ? 5 : 0,
                        ownerId: "P1", controllerId: "P1", isExhausted: true, tags: tags) },
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                    ["P1"] = category == "LEGEND" ? initial.PlayerZones["P1"] with { LegendZone = ["DONOR"] }
                        : initial.PlayerZones["P1"] with { Base = [..initial.PlayerZones["P1"].Base, "DONOR"] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                    ["DONOR"] = new("P1", category == "LEGEND" ? "LEGEND" : "BASE") } };
            // Keep a single legend in this fixture; the default Jayce is replaced.
            if (category == "LEGEND") initial = initial with { CardObjects = initial.CardObjects.Where(p => p.Key != "JAYCE").ToDictionary(),
                ObjectLocations = initial.ObjectLocations.Where(p => p.Key != "JAYCE").ToDictionary() };
            return adjust?.Invoke(initial) ?? initial;
        });
        if (donor == "UNL-026/219") {
            await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
            await game.Act("P1", new PlayCardCommand("READY2", "OGN·146/298", ["HOST"]));
            await game.Top();
        }
        return game;
    }

    [Fact]
    public async Task BorrowedReadyKeepsTheRemainingTargetWhenTheOtherTokenCeases()
    {
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("UNL·T05", out var gold));
        var game = await Ready("OGN·111/298", true, adjust: state => state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["GEAR2"] = gold.CreateObject("GEAR2", "P2", "P2") with { IsExhausted = false } },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = state.PlayerZones["P1"].Base.Where(id => id != "GEAR2").ToArray() },
                ["P2"] = state.PlayerZones["P2"] with { Base = [..state.PlayerZones["P2"].Base, "GEAR2"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["GEAR2"] = new("P2", "BASE") } });
        await game.Act("P1", new ActivateAbilityCommand("HOST", BorrowedId(game.State, 2), ["GEAR", "GEAR2"]));
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new ActivateAbilityCommand("GEAR2", P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId, []));
        Assert.False(game.State.CardObjects.ContainsKey("GEAR2"));
        var item = Assert.Single(game.State.StackItems);
        var forged = item with { BorrowedExhaust = item.BorrowedExhaust! with { TargetTokenFactories = null } };
        Assert.Contains("invalid captured borrowed skill", OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [forged] }));
        await game.Top();
        Assert.False(game.State.CardObjects["GEAR"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task CopiedInheritanceTextCreatesAnIndependentCapturedSkill()
    {
        var game = await Ready("OGN·111/298", true);
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"]));
        await game.Top();
        Assert.NotNull(game.State.CardObjects["GEAR"].AttachedRuleText);
        var rows = (ResolutionResult.BuildPrompts(game.State)["P1"].Candidates ?? [])
            .Where(c => c.Enabled && c.Action == CommandTypes.ActivateAbility
                && c.Metadata?.GetValueOrDefault("sourceRequirements") is IEnumerable<IReadOnlyDictionary<string, object?>>)
            .SelectMany(c => (IEnumerable<IReadOnlyDictionary<string, object?>>)c.Metadata!["sourceRequirements"]!)
            .Where(r => Equals(r.GetValueOrDefault("sourceObjectId"), "HOST") && Equals(r.GetValueOrDefault("minTargetCount"), 2)).ToArray();
        Assert.Equal(2, rows.Length);
        await game.Act("P1", new ActivateAbilityCommand("HOST", (string)rows[^1]["abilityId"]!, ["GEAR", "GEAR2"]));
        var item = Assert.Single(game.State.StackItems);
        Assert.NotNull(item.BorrowedExhaust!.Borrower.Origin);
        var origin = item.BorrowedExhaust.Borrower.Origin!;
        var forged = item with { BorrowedExhaust = item.BorrowedExhaust with {
            Borrower = item.BorrowedExhaust.Borrower with { Origin = origin with { Revision = origin.Revision + 1 } } } };
        Assert.Contains("invalid captured borrowed skill", OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [forged] }));
        await game.Top();
        Assert.False(game.State.CardObjects["GEAR"].IsExhausted);
        Assert.False(game.State.CardObjects["GEAR2"].IsExhausted);
        await game.Replay();
    }
}
