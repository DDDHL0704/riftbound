using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadLocalLastBreathUnitTests
{
    private const string Sorrow = "RAD·126/167";

    [Theory]
    [InlineData("ALLY")] [InlineData("GEAR")] [InlineData("DECLINE")]
    public async Task BaseDeathCanDestroyAUnitOrEquipmentOnlyInItsCapturedBaseOrBeDeclined(string target)
    {
        var initial = AttackDamageDistributionTests.Spell(VenUnitEntryBasicTests.Position(Sorrow), "KILL", "OGN·229/298", "P1");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Sorrow, [], Destination: "BASE"));
        var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["UNIT"])); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        var item = Assert.Single(game.State.StackItems);
        Assert.Equal("P1", item.LastBreathBattlefieldDamage!.SourceBasePlayerId);
        Assert.Equal(generation, item.LastBreathBattlefieldDamage.Source.Generation);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(new[] { "ALLY", "GEAR" }, choice.LegalObjectIds);
        await Reject(game.State, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, target == "DECLINE" ? [] : [target]));
        if (target != "DECLINE")
        {
            Assert.Contains(target, game.State.PlayerZones["P1"].Base);
            await game.Top(); Assert.Contains(target, game.State.PlayerZones["P1"].Graveyard);
        }
        else Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task BattlefieldDeathCanTargetItsEnemyAndUsesTheOrdinarySpellshieldPayment(bool pay)
    {
        var state = VenUnitEntryBasicTests.Position(Sorrow);
        var enemy = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", "RAD·036/167", "P2");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = enemy with { Tags = [..enemy.Tags, CardObjectTags.Spellshield] },
            ["BF"] = state.CardObjects["BF"] with { ControllerId = "P2" } },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = [], Base = ["ALLY", "GEAR", "UNIT"] },
                ["P2"] = state.PlayerZones["P2"] with { Base = [], Battlefields = ["OTHER", "ENEMY"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["UNIT"] = new("P1", "BASE"),
                ["ENEMY"] = new("P2", "BATTLEFIELD", "BF") } };
        state = AttackDamageDistributionTests.Spell(state, "RAY", "OGN·009/298", "P1");
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF"));
        await game.Act("P1", new PlayCardCommand("RAY", "OGN·009/298", ["UNIT"])); await game.Top();
        var item = Assert.Single(game.State.StackItems);
        Assert.Equal("BF", item.LastBreathBattlefieldDamage!.BattlefieldObjectId);
        var choice = game.State.PendingCardChoice!;
        Assert.Equal(new[] { "ENEMY" }, choice.LegalObjectIds);
        await Reject(game.State, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GEAR"]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(1, payment.PowerCost); var pool = game.State.RunePools["P1"].TotalPower;
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
        Assert.Equal(pool - (pay ? 1 : 0), game.State.RunePools["P1"].TotalPower);
        if (pay) { Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Battlefields); await game.Top(); }
        Assert.Equal(pay, game.State.PlayerZones["P2"].Graveyard.Contains("ENEMY")); await game.Replay();
    }

    [Fact]
    public async Task ChosenNewIncarnationIsNotDestroyedAndTheCapturedLocationCannotBeRemoved()
    {
        var state = AttackDamageDistributionTests.Spell(VenUnitEntryBasicTests.Position(Sorrow), "KILL", "OGN·229/298", "P1");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", Sorrow, [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["UNIT"])); await game.Top();
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        var item = Assert.Single(game.State.StackItems);
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with { LastBreathBattlefieldDamage = null }] }));
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with {
            LastBreathBattlefieldDamage = item.LastBreathBattlefieldDamage! with { SourceBasePlayerId = null } }] }));
        var changed = game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
            ["ALLY"] = game.State.CardObjects["ALLY"] with { ObjectGeneration = game.State.CardObjects["ALLY"].ObjectGeneration + 1 } } };
        var done = await OfficialGraveyardRecastTests.Top(OfficialInsightAndSpellLockTests.Restore(changed));
        Assert.Contains("ALLY", done.State.PlayerZones["P1"].Base); OfficialInsightAndSpellLockTests.Restore(done.State);
    }

    [Fact]
    public async Task EachCopiedLastBreathChoosesIndependentlyAndDoesNotDestroyAnAlreadyRemovedTarget()
    {
        var state = VenUnitEntryBasicTests.Position(Sorrow);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", "SFD·059/221", "P1") } };
        state = AttackDamageDistributionTests.Spell(state, "KILL", "OGN·229/298", "P1");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", Sorrow, [], Destination: "BASE"));
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "UNIT", ["ASSEMBLE_GREEN"])); await game.Top();
        Assert.Equal(Sorrow, game.State.CardObjects["GEAR"].AttachedRuleText!.CardNo);
        await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["UNIT"])); await game.Top();
        Assert.Equal(2, game.State.TriggerQueue.Count);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        for (var i = 0; i < 2; i++)
        {
            var choice = game.State.PendingCardChoice!;
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        }
        Assert.Equal(2, game.State.StackItems.Count);
        await game.Top(); var generation = game.State.CardObjects["ALLY"].ObjectGeneration;
        await game.Top(); Assert.Equal(generation, game.State.CardObjects["ALLY"].ObjectGeneration);
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task ASourceReturnedToHandDoesNotLoseItsCapturedSkillOrExposeItsPrivateIdentity()
    {
        var state = AttackDamageDistributionTests.Spell(VenUnitEntryBasicTests.Position(Sorrow), "KILL", "OGN·229/298", "P1");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", Sorrow, [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["UNIT"])); await game.Top();
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        var changed = game.State with { PlayerZones = new Dictionary<string, PlayerZones>(game.State.PlayerZones) {
            ["P1"] = game.State.PlayerZones["P1"] with { Graveyard = game.State.PlayerZones["P1"].Graveyard.Where(id => id != "UNIT").ToArray(),
                Hand = [..game.State.PlayerZones["P1"].Hand, "UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(game.State.ObjectLocations) { ["UNIT"] = new("P1", "HAND") },
            CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
                ["UNIT"] = game.State.CardObjects["UNIT"] with { ObjectGeneration = game.State.CardObjects["UNIT"].ObjectGeneration + 1 } } };
        var done = await OfficialGraveyardRecastTests.Top(OfficialInsightAndSpellLockTests.Restore(changed));
        Assert.Contains("ALLY", done.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("UNIT", done.State.PlayerZones["P1"].Hand);
        Assert.All(done.Events.Where(e => e.Kind is "TRIGGER_RESOLVED" or "UNIT_DESTROYED"), e => Assert.Equal("HIDDEN", e.Payload["sourceObjectId"]));
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(done); OfficialInsightAndSpellLockTests.Restore(done.State);
    }

    [Fact]
    public async Task ADeathWithNoPermanentLeftAtItsLocationDoesNotBlockTheGame()
    {
        var state = VenUnitEntryBasicTests.Position(Sorrow);
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Base = [], Hand = ["UNIT", "ALLY", "GEAR"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["ALLY"] = new("P1", "HAND"), ["GEAR"] = new("P1", "HAND") } };
        state = AttackDamageDistributionTests.Spell(state, "KILL", "OGN·229/298", "P1");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", Sorrow, [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["UNIT"])); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        Assert.Null(game.State.PendingCardChoice); Assert.Null(game.State.PendingPayment); await game.Replay();
    }

    private static async Task Reject(MatchState state, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-here", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
