using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class ArmamentSpellDamageTests
{
    private const string Card = "SFD·191/221";
    private const string Ray = "OGN·009/298";

    private static MatchState Position()
    {
        var state = ArmamentMightTests.Position(Card);
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", Card, "P1"),
                ["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", "OGN·088/298", "P2") },
            RunePools = state.RunePools.ToDictionary(pair => pair.Key, _ => new RunePool(40, 20)) };
        state = CurrentControllerDamageBonusTests.Place(state, "GEAR", "P1", "HAND");
        state = LocalDestructionRecallTests.At(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        return AttackDamageDistributionTests.Spell(state, "RAY", Ray, "P1");
    }

    private static async Task Equip(Driver game)
    {
        await game.Act("P1", new PlayCardCommand("GEAR", Card, []));
        if (game.State.StackItems.Count > 0) await game.Top();
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base);
        Assert.Equal(36, game.State.RunePools["P1"].Mana);
        Assert.Equal(18, game.State.RunePools["P1"].TotalPower);
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_ANY_POWER"]));
        await game.Top();
        Assert.Equal("HOST", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(17, game.State.RunePools["P1"].TotalPower);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task OfficialPlayAndAssemblyGrantThreeDamageOnlyWhileAttached(bool attached)
    {
        var game = new Driver(Position());
        if (attached) await Equip(game);
        else {
            await game.Act("P1", new PlayCardCommand("GEAR", Card, []));
            if (game.State.StackItems.Count > 0) await game.Top();
        }
        await game.Act("P1", new PlayCardCommand("RAY", Ray, ["ENEMY"]));
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        await game.Top();
        Assert.Equal(attached ? 6 : 3, game.State.CardObjects["ENEMY"].Damage);
        Assert.Equal(attached ? 7 : 4, ArmamentMightTests.Might(game.State));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ResponseDetachmentOrHostReturnRemovesTheLiveDamageBonus(bool returnHost)
    {
        var state = Position();
        state = AttackDamageDistributionTests.Spell(state, "RESPONSE", returnHost ? "OGN·104/298" : "SFD·011/221", "P1");
        var game = new Driver(state); await Equip(game);
        await game.Act("P1", new PlayCardCommand("RAY", Ray, ["ENEMY"]));
        await game.Act("P1", new PlayCardCommand("RESPONSE", returnHost ? "OGN·104/298" : "SFD·011/221",
            returnHost ? ["HOST"] : ["HOST", "GEAR"]));
        await game.Top();
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        await game.Top();
        Assert.Equal(3, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    [Fact]
    public async Task OtherIndependentSpellDamageSourcesAddToTheArmamentBonus()
    {
        var game = new Driver(CurrentControllerDamageBonusTests.Annie(Position(), "P1"));
        await Equip(game);
        await game.Act("P1", new PlayCardCommand("RAY", Ray, ["ENEMY"]));
        await game.Top();
        Assert.Equal(7, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    [Theory]
    [InlineData("P1", 3)] [InlineData("P2", 6)]
    public async Task GrantedTextUsesTheHostsControllerInsteadOfTheEquipmentsOwner(string actor, int expected)
    {
        var state = Position();
        state = LocalDestructionRecallTests.At(state, "HOST", "P2", "BATTLEFIELD", "OTHER");
        state = CurrentControllerDamageBonusTests.Place(state, "GEAR", "P1", "BATTLEFIELD", "OTHER");
        state = state with {
            ActivePlayerId = actor, TurnPlayerId = actor, PriorityPlayerId = actor,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["GEAR"] = state.CardObjects["GEAR"] with { AttachedToObjectId = "HOST" },
                ["RAY"] = state.CardObjects["RAY"] with { OwnerId = actor, ControllerId = actor } } };
        state = CurrentControllerDamageBonusTests.Place(state, "RAY", actor, "HAND");
        var game = new Driver(state);
        await game.Act(actor, new PlayCardCommand("RAY", Ray, ["ENEMY"]));
        await game.Top();
        Assert.Equal(expected, game.State.CardObjects["ENEMY"].Damage);
        Assert.Equal("P1", game.State.CardObjects["GEAR"].ControllerId);
        Assert.Equal("P2", game.State.CardObjects["HOST"].ControllerId);
        await game.Replay();
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public async Task OfficialCountedDamageExampleDoesNotCreateADamageActionWhenTheCountIsZero(int count)
    {
        const string attacker = "P1-TEEMO-DEFEND-ATTACKER";
        const string defender = "P2-TEEMO-FACE-UP-DEFENDER";
        const string field = "P1-TEEMO-DEFEND-BATTLEFIELD";
        var state = CurrentControllerDamageBonusTests.CountedPosition("OGN·121/298", count, false) with { Tick = 0 };
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["CAP"] = OfficialGarenConquestBattlefieldCountTests.Printed("CAP", Card, "P2") with { AttachedToObjectId = defender } } };
        state = LocalDestructionRecallTests.At(state, "CAP", "P2", "BATTLEFIELD", field);
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand(attacker, "BASE", "BATTLEFIELD:" + field));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [attacker]));
        await game.Top();
        Assert.Equal(count == 0 ? 0 : count + 3, game.State.CardObjects[attacker].Damage);
        if (count == 0) Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "DAMAGE_APPLIED");
        else Assert.Equal(4, Assert.Single(game.Events(), entry => entry.Event.Kind == "DAMAGE_APPLIED").Event.Payload["damage"]);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DistributedAttackAddsThreeToThePoolOnceAndReevaluatesAttachmentAtResolution(bool detach)
    {
        var state = AttackDamageDistributionTests.Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", Card, "P1") with { AttachedToObjectId = "HOST" } } };
        state = AttackDamageDistributionTests.Spell(state, "DETACH", "SFD·011/221", "P1");
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F"));
        for (var index = 0; index < 4 && game.State.FocusPlayerId is not null; index++)
            await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        if (game.PromptFor("P1").EnabledActions().Contains(CommandTypes.DeclareBattle))
            await game.Act("P1", new DeclareBattleCommand("F", ["HOST"], ["ENEMY1", "ENEMY2", "ENEMY3"], ["COMBAT_ASSIGNMENT"]));
        var targets = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(targets.ChoiceId, targets.ChoiceWindow, ["ENEMY1", "ENEMY2", "ENEMY3"]));
        var captured = Assert.Single(game.State.StackItems);
        Assert.Equal(8, captured.DamageAmount);
        var bonus = Assert.Single(captured.DamageDistribution!.InitialBonuses);
        Assert.Equal(3, bonus.Amount); Assert.Equal("GEAR", bonus.Source.ObjectId);
        foreach (var mutation in new[] { bonus with { Amount = 4 }, bonus with { CardNo = "SFD·022/221" },
            bonus with { Source = bonus.Source with { Generation = bonus.Source.Generation + 1 } } })
        {
            var amount = 5 + mutation.Amount;
            var corrupt = game.State with { StackItems = [captured with { DamageAmount = amount,
                DamageDistribution = captured.DamageDistribution with { InitialDamage = amount, InitialBonuses = [mutation] } }] };
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(corrupt));
        }
        if (detach)
        {
            await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"]));
            await game.Top(); Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        }
        await game.Top();
        var pool = detach ? 5 : 8;
        Assert.Equal(pool, game.State.StackItems.Single().DamageDistribution!.ResolutionDamage);
        await game.Act("P1", AttackDamageDistributionTests.Allocation(game.State,
            ("ENEMY1", pool - 2), ("ENEMY2", 1), ("ENEMY3", 1)));
        Assert.Equal(pool, game.Events().Where(entry => entry.Event.Kind == "DAMAGE_APPLIED")
            .Sum(entry => Convert.ToInt32(entry.Event.Payload["damage"])));
        await game.Replay();
    }
}
