using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class Ven128EmpoweredLastBreathConsumerTests
{
    private const string No = "VEN·128";
    private const string Text = "{{强化1黄色}}（支付{{1}}和{{黄色}}：强化我。仅在未强化时可用。）\n{{已强化>}}{{>绝念>}} 打出两名1{{S}}的“随从”到你的基地。（当我在已强化状态下被摧毁时，发动此效果。）";
    private const string Destroy = "OGN·229/298";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task ExactCnPrintingSeparatesPlayCostFromTheNativeTypedEmpowerCost()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(37828, card.Id); Assert.Equal("诺克萨斯使节", card.CardName); Assert.Equal(Text, card.CardEffect);
        Assert.Equal(2, card.Energy); Assert.Null(card.ReturnEnergy); Assert.Equal(2, card.Power);
        Assert.Equal([RuneTrait.Yellow], card.CardColorList); Assert.Equal(["unit"], card.CardCategoryList);
        Assert.Equal("诺克萨斯", card.Region); Assert.Empty(card.Tag);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior));
        Assert.True(behavior.PlaysSourceToBaseAsUnit); Assert.Equal(2, behavior.ManaCost); Assert.Equal(2, behavior.SourceUnitPower);
        var cost = Assert.Single(Assert.Single(EmpowerParser.Parse(card.CardEffect)).Alternatives);
        Assert.Equal(1, cost.Mana); Assert.Equal(0, cost.Power); Assert.Equal(1, Assert.Single(cost.TypedPower).Value);
        Assert.Equal(RuneTrait.Yellow, Assert.Single(cost.TypedPower).Key); Assert.True(cost.SupportedResources); Assert.False(cost.Exhaust);
        var self = EmpowerParser.Self(card.CardEffect); Assert.Equal(0, self.Might); Assert.Empty(self.Keywords);
    }

    [Fact]
    public async Task DependentLastBreathRequiresTheSourcesEmpoweredStatusAndCreatesTwoOneMightMinions()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        var trigger = Assert.Single(TriggerParser.Parse(card.CardEffect), t => t.Kind == TriggerKinds.UnitLastBreathCreateMinions);
        Assert.Equal(TriggerTimings.UnitDestroyed, trigger.Timing); Assert.Equal(TriggerTargetScopes.SourceUnit, trigger.TargetScope);
        Assert.True(trigger.RequiresSourceEmpowered); Assert.Equal(2, trigger.CreatedTokenCount);
        Assert.Equal("随从", trigger.CreatedTokenName); Assert.Equal(1, trigger.CreatedTokenPower);
        Assert.Equal(TriggerTokenDestinations.OwnerBase, trigger.CreatedTokenDestination);
    }

    [Fact]
    public async Task TheExactNewRecordPreservesAllPriorFunctionalGroupsAndMembers()
    {
        var latestCatalog = await OfficialCardCatalog.LoadDefaultAsync();
        var catalog = latestCatalog with { Total = 1088, Cards = latestCatalog.Cards.Where(c => c.CardNo != "VEN·124").ToArray() }; var old = catalog.Cards.Where(c => c.CardNo != No).ToArray();
        Assert.Equal(1087, old.Length); Assert.Equal(1088, catalog.Cards.Count);
        var prior = FunctionalUnitBuilder.Build(old); var current = FunctionalUnitBuilder.Build(catalog.Cards);
        Assert.Equal(868, prior.Count); Assert.Equal(869, current.Count);
        var legacy = old.Where(c => c.CardNo != "VEN·078").ToArray();
        Assert.Equal(1086, legacy.Length); Assert.Equal(867, FunctionalUnitBuilder.Build(legacy).Count);
        foreach (var unit in prior)
            Assert.Equal(JsonSerializer.Serialize(unit), JsonSerializer.Serialize(Assert.Single(current, c => c.Id == unit.Id)));
        Assert.Equal(No, Assert.Single(Assert.Single(current, c => c.Cards.Any(card => card.CardNo == No)).Cards).CardNo);
        Assert.Equal(1089, latestCatalog.Cards.Count);
        var latestUnits = FunctionalUnitBuilder.Build(latestCatalog.Cards);
        Assert.Equal(870, latestUnits.Count);
        foreach (var unit in FunctionalUnitBuilder.Build(catalog.Cards))
            Assert.Equal(JsonSerializer.Serialize(unit), JsonSerializer.Serialize(Assert.Single(latestUnits, c => c.Id == unit.Id)));
        Assert.Equal("VEN·124", Assert.Single(Assert.Single(latestUnits, c => c.Cards.Any(card => card.CardNo == "VEN·124")).Cards).CardNo);
    }

    [Theory]
    [InlineData("P1", false)] [InlineData("P1", true)]
    [InlineData("P2", false)] [InlineData("P2", true)]
    public async Task RealPlayAndTypedEmpowerBeforeActualDestructionEarnTheConditionalSkillExactlyOnce(string owner, bool empower)
    {
        var initial = Position(owner); var game = new Driver(initial); var opponent = owner == "P1" ? "P2" : "P1";
        try
        {
            await Tap(game, owner); Assert.Equal(4, game.State.RunePools[owner].Mana); Assert.Equal(0, game.State.RunePools[owner].TotalPower);
            await game.Act(owner, new PlayCardCommand("UNIT", No, []));
            Assert.Equal(2, game.State.RunePools[owner].Mana); Assert.Equal(0, game.State.RunePools[owner].TotalPower);
            Assert.Contains("UNIT", game.State.PlayerZones[owner].Base); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            if (empower)
            {
                await game.Act(owner, new RecycleRuneCommand(owner + "-RUNE-1"));
                Assert.Equal(1, game.State.RunePools[owner].PowerByTrait[RuneTrait.Yellow]);
                await game.Act(owner, new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
                Assert.Equal(1, game.State.RunePools[owner].Mana); Assert.Equal(0, game.State.RunePools[owner].TotalPower);
                var activation = Assert.Single(game.State.StackItems).EmpowerActivation!;
                Assert.Equal(1, activation.PaidMana); Assert.Equal(generation, activation.Source.Generation);
                Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); await game.Top();
                Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(2, game.State.CardObjects["UNIT"].Power);
            }
            Assert.Empty(Minions(game.State)); await game.Act(owner, new EndTurnCommand());
            Assert.Equal(opponent, game.State.TurnPlayerId); Assert.Equal(empower, game.State.CardObjects["UNIT"].IsEmpowered);
            await Tap(game, opponent);
            await game.Act(opponent, new RecycleRuneCommand(opponent + "-RUNE-1"));
            await game.Act(opponent, new RecycleRuneCommand(opponent + "-RUNE-2"));
            Assert.Equal(4, game.State.RunePools[opponent].Mana); Assert.Equal(2, game.State.RunePools[opponent].PowerByTrait[RuneTrait.Yellow]);
            await game.Act(opponent, new PlayCardCommand("KILL", Destroy, ["UNIT"]));
            Assert.Equal(0, game.State.RunePools[opponent].Mana); Assert.Equal(0, game.State.RunePools[opponent].TotalPower);
            await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones[owner].Graveyard); Assert.DoesNotContain("UNIT", game.State.PlayerZones[owner].Base);
            Assert.Equal("GRAVEYARD", game.State.ObjectLocations["UNIT"].Zone); Assert.Equal(owner, game.State.CardObjects["UNIT"].OwnerId);
            Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Empty(Minions(game.State));
            var skills = game.State.StackItems.Where(s => s.EffectKind == TriggerKinds.UnitLastBreathCreateMinions).ToArray();
            Assert.Equal(empower ? 1 : 0, skills.Length);
            if (empower)
            {
                var skill = Assert.Single(skills); Assert.Equal("UNIT", skill.SourceObjectId); Assert.Equal(owner, skill.ControllerId);
                Assert.Empty(skill.TargetObjectIds); await game.Top();
                Assert.Equal(2, Minions(game.State).Length);
                foreach (var minion in Minions(game.State))
                {
                    Assert.Equal(owner, minion.OwnerId); Assert.Equal(owner, minion.ControllerId); Assert.Equal(1, minion.Power);
                    Assert.True(minion.IsExhausted); Assert.False(minion.IsEmpowered); Assert.Contains(CardObjectTags.UnitCard, minion.Tags);
                    Assert.False(string.IsNullOrWhiteSpace(minion.TokenFactoryCardNo)); Assert.Equal(minion.CardNo, minion.TokenFactoryCardNo);
                    Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(minion.TokenFactoryCardNo!, out var factory)); Assert.Equal("随从", factory.TokenFamilyName);
                    Assert.Contains(minion.ObjectId, game.State.PlayerZones[owner].Base); Assert.Equal("BASE", game.State.ObjectLocations[minion.ObjectId].Zone);
                }
            }
            await game.Act(opponent, new EndTurnCommand()); await game.Act(owner, new EndTurnCommand());
            Assert.Equal(empower ? 2 : 0, Minions(game.State).Length); Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
            await game.Replay();
        }
        finally { await Export("native-death-" + owner + "-" + empower, initial, game); }
    }

    private static MatchState Position(string owner)
    {
        var state = VenUnitEntryBasicTests.Position(No); var opponent = owner == "P1" ? "P2" : "P1";
        var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary(); var zones = state.PlayerZones.ToDictionary();
        cards["UNIT"] = OfficialGarenConquestBattlefieldCountTests.Printed("UNIT", No, owner); locations["UNIT"] = new(owner, "HAND");
        foreach (var player in new[] { "P1", "P2" })
        {
            var runes = Enumerable.Range(1, 4).Select(i => player + "-RUNE-" + i).ToArray();
            foreach (var id in runes) { cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R06", player); locations[id] = new(player, "BASE"); }
            zones[player] = zones[player] with { Hand = player == owner ? ["UNIT"] : ["KILL"], Base = [..zones[player].Base, ..runes] };
        }
        cards["KILL"] = OfficialGarenConquestBattlefieldCountTests.Printed("KILL", Destroy, opponent); locations["KILL"] = new(opponent, "HAND");
        return state with { RoomId = "VEN128-EXPLICIT-MIDGAME-" + owner, TurnPlayerId = owner, ActivePlayerId = owner,
            CardObjects = cards, ObjectLocations = locations, PlayerZones = zones,
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(0, 0), ["P2"] = new(0, 0) } };
    }
    private static async Task Tap(Driver game, string player)
    { foreach (var id in game.State.PlayerZones[player].Base.Where(id => game.State.CardObjects[id].CardNo == "UNL-R06" && !game.State.CardObjects[id].IsExhausted).ToArray()) await game.Act(player, new TapRuneCommand(id)); }
    private static CardObjectState[] Minions(MatchState state) => state.CardObjects.Values.Where(c => c.Tags.Contains(CardObjectTags.MinionTokenFamily)).ToArray();
    private static async Task Export(string label, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN128_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        var runtime = new[] { typeof(CoreRuleEngine).Assembly, typeof(OfficialCardCatalog).Assembly, typeof(GameCommand).Assembly, Assembly.GetExecutingAssembly() }
            .Select(a => new { name = a.GetName().Name, path = a.Location, mvid = a.ManifestModule.ModuleVersionId,
                infoVersion = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))).ToLowerInvariant() });
        await File.WriteAllTextAsync(Path.Combine(output, label + ".json"), JsonSerializer.Serialize(new {
            scope = "Explicit turn-five fixture. Native physical runes, printed play, typed Empower and opponent destruction; not canonical opening, L2, L3 or whole-card certification.",
            initial, final = game.State, commands = game.Commands(), events = game.Events(), runtime, ruleInputs = RuleInputIdentity.Current }, Json));
    }
}
