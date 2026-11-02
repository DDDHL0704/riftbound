using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadHextechMinefieldTests
{
    private const string Card = "RAD·142/167";
    private static MatchState Board()
    {
        var s = VenUnitEntryBasicTests.Position(Card);
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.BombTokenCardNo, out var factory));
        return s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["B1"] = factory.CreateObject("B1", "P1", "P1"), ["B2"] = factory.CreateObject("B2", "P1", "P1"),
                ["B3"] = factory.CreateObject("B3", "P2", "P2") },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                ["P1"] = s.PlayerZones["P1"] with { Battlefields = ["BF", "B1", "B2"] },
                ["P2"] = s.PlayerZones["P2"] with { Battlefields = ["OTHER", "B3"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) {
                ["B1"] = new("P1", "BATTLEFIELD", "BF"), ["B2"] = new("P1", "BATTLEFIELD", "BF"),
                ["B3"] = new("P2", "BATTLEFIELD", "OTHER") } };
    }

    [Fact]
    public async Task OfficialSpellRetainsItsPhysicalIdentityAndQuickTiming()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Card);
        Assert.Equal(39650L, card.Id); Assert.Equal("海克斯爆破雷区", card.CardName);
        Assert.Equal(2, card.Energy); Assert.Equal(1, card.ReturnEnergy);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Card, out var b));
        Assert.True(b.CanPlayDuringSpellDuel); Assert.False(b.CanPlayDuringPriority);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task OnlyTheBombsActuallyDestroyedAreRebuiltTogetherAfterAnEndStepChoice(int count)
    {
        var game = new Driver(Board()); var selected = new[] { "B1", "B2" }.Take(count).ToArray();
        await game.Act("P1", new PlayCardCommand("UNIT", Card, selected)); await game.Top();
        Assert.Equal(2 - count, game.State.CardObjects.Values.Count(c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo && c.ControllerId == "P1"));
        Assert.True(game.State.CardObjects.ContainsKey("B3"));
        Assert.Equal(count > 0, game.State.UntilEndOfTurnEffects.Any(e => e.StartsWith("BOMB_REBUILD:")));
        if (game.State.TriggerQueue.Count > 0) {
            Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(game.State));
            var trigger = game.State.TriggerQueue[0]; var context = trigger.FieldContext!;
            var origin = context.BombDestruction!;
            foreach (var forged in new[] {
                trigger with { FieldContext = context with { BombDestruction = null } },
                trigger with { FieldContext = context with { BombDestruction = origin with { CapturedTick = game.State.Tick + 1 } } },
                trigger with { FieldContext = context with { BombDestruction = origin with { Bomb = origin.Bomb with { ObjectId = "B2" } } } },
                trigger with { FieldContext = context with { SourceTokenFactoryCardNo = "VEN·T02" } },
                trigger with { SourceObjectId = "missing" } }) {
                var altered = game.State with { TriggerQueue = [forged, ..game.State.TriggerQueue.Skip(1)] };
                Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(altered));
            }
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        }
        Assert.Empty(game.State.StackItems);
        await game.Act("P1", new EndTurnCommand());
        if (count > 0) {
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            Assert.Equal("BOMB_REBUILD_BATTLEFIELD", choice.ChoiceWindow);
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["OTHER"]));
            await game.Top();
            Assert.Equal(count, game.State.CardObjects.Values.Count(c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo
                && c.ControllerId == "P1" && game.State.ObjectLocations[c.ObjectId].BattlefieldObjectId == "OTHER"));
        }
        await game.Replay();
    }

    [Theory]
    [InlineData("GEAR")] [InlineData("B3")] [InlineData("ALLY")] [InlineData("missing")]
    public async Task OnlyFriendlyBombTokenFactoriesCanBeChosen(string target)
    {
        var s = Board(); var command = new PlayCardCommand("UNIT", Card, [target]);
        var hash = MatchStateHasher.Hash(s); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(s));
        Assert.False(new CoreRuleEngine().PreviewPlayCard(s, "P1", PlayCostPreviewTests.Request(s, command)).CanPay);
        var rejected = await new CoreRuleEngine().ResolveAsync(s, new("invalid-minefield", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(rejected.State)));
    }

    [Fact]
    public async Task CounteredMinefieldDoesNotDestroyOrScheduleBombs()
    {
        var game = new Driver(OfficialCopyIdentityTests.AddSpell(Board(), "COUNTER", "OGN·064/298"));
        await game.Act("P1", new PlayCardCommand("UNIT", Card, ["B1", "B2"]));
        var spell = Assert.Single(game.State.StackItems);
        await game.Act("P1", new PlayCardCommand("COUNTER", "OGN·064/298", [spell.StackItemId])); await game.Top();
        Assert.True(game.State.CardObjects.ContainsKey("B1")); Assert.True(game.State.CardObjects.ContainsKey("B2"));
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, e => e.StartsWith("BOMB_REBUILD:"));
        await game.Replay();
    }

    [Fact]
    public async Task OnlyBombsStillLegalAtResolutionCountAfterAReactionGrantedByKaisa()
    {
        var s = ArmamentMightTests.Position(unit: "RAD·063/167", power: 5);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["F"] = s.CardObjects["F"] with { ControllerId = "P2" } } };
        s = AttackDamageDistributionTests.Spell(s, "MINEFIELD", Card, "P1");
        s = AttackDamageDistributionTests.Spell(s, "DESTROY", "OGN·224/298", "P1");
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.BombTokenCardNo, out var factory));
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["B1"] = factory.CreateObject("B1", "P1", "P1"), ["B2"] = factory.CreateObject("B2", "P1", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with {
                Battlefields = [..s.PlayerZones["P1"].Battlefields, "B1", "B2"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) {
                ["B1"] = new("P1", "BATTLEFIELD", "F"), ["B2"] = new("P1", "BATTLEFIELD", "F") } };
        var game = new Driver(s);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        await game.Act("P1", new PlayCardCommand("MINEFIELD", Card, ["B1", "B2"]));
        await game.Act("P1", new PlayCardCommand("DESTROY", "OGN·224/298", ["B1"])); await game.Top(); await game.Top();
        var marker = Assert.Single(game.State.UntilEndOfTurnEffects, e => e.StartsWith("BOMB_REBUILD:"));
        Assert.Equal(1, CoreRuleEngine.ReadBombRebuild(marker)!.BombCount);
        await game.Replay();
    }
}
