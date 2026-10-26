using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class EmpoweredBanishBoundaryTests
{
    [Fact]
    public async Task AnActuallyCopiedTokenCanCeaseToExistWithoutErasingItsEarnedBanish()
    {
        var initial = Put(Put(Put(ShadySpectaclesTests.Position(Mel), "FODDER", "VEN·049", "P1", "HAND"),
            "RETURN", "OGN·104/298", "P1", "HAND"), "TARGET", "OGN·096/298", "P2", "BATTLEFIELD", "OTHER");
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.SandSoldierTokenCardNo, out var factory));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["HOST"] = factory.CreateObject("HOST", "P1", "P1") } };
        await Run("earned-copy-token-ceased", initial, async game => {
            await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_YELLOW"])); await game.Top();
            var selection = game.State.PendingRuleChoice!;
            await game.Act("P1", new PayCostCommand(selection.Request.Id, "RULE_REPLACEMENT", ["COPY:COPY-SOURCE"]));
            var requirement = ResolutionResult.BuildPrompts(game.State)["P1"].Candidates!.Where(c => c.Action == CommandTypes.ActivateAbility)
                .SelectMany(c => Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(c.Metadata!["sourceRequirements"]))
                .Single(row => (string?)row["sourceObjectId"] == "HOST" && (string?)row["abilityId"] == "EMPOWER:0:0");
            await game.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", [], OptionalCosts: ["DISCARD_HAND_CARD:FODDER"])
                with { AbilityInstanceId = Assert.IsType<string>(requirement["abilityInstanceId"]) }); await game.Top();
            Assert.NotNull(game.State.PendingCardChoice); await Choose(game, "TARGET");
            var before = Assert.Single(game.State.StackItems).FieldContext!;
            Assert.Equal(P6TokenFactoryCatalog.SandSoldierTokenCardNo, before.SourceTokenFactoryCardNo); Assert.False(before.SourceCeasedToExist);
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
            Assert.False(game.State.CardObjects.ContainsKey("HOST")); Assert.DoesNotContain("HOST", game.State.PlayerZones["P1"].Hand);
            Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
            var earned = Assert.Single(game.State.StackItems).FieldContext!; Assert.True(earned.SourceCeasedToExist);
            Assert.Equal(before.EmpoweredTransition!.Source, earned.EmpoweredTransition!.Source); Assert.NotNull(earned.EmpoweredTransition.CeasedSource);
            await game.Top(); Assert.Contains("TARGET", game.State.PlayerZones["P2"].Banished);
            Assert.Single(game.Events(), e => e.Event.Kind == "TOKEN_CEASED_TO_EXIST");
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_DESTROYED");
        });
    }

    [Fact]
    public async Task OriginalCanonicalHistoryRejectsACoordinatedFalseEmpowerBirthWithoutCachingIt()
    {
        var game = await VenEmpowerCanonicalOpeningTests.Open("P1", "vex-spells", "UNL-R05",
            [Mel, Mel, Mel, "SFD·122/221", "SFD·122/221", "SFD·122/221", "OGN·175/298", "OGN·175/298", "OGN·175/298"]);
        try {
            for (var turn = 0; turn < 80; turn++) {
                if (game.State.TurnPlayerId == "P2" && game.Runes("P1").Length >= 5 && game.Runes("P2").Length >= 5
                    && game.Hand("P1", Mel).Any() && game.Hand("P1", "SFD·122/221").Any() && game.Hand("P2", "OGN·175/298").Any()) break;
                await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            }
            Assert.Equal("P2", game.State.TurnPlayerId); var target = game.Hand("P2", "OGN·175/298").First();
            await game.Tap("P2"); await game.Act("P2", new PlayCardCommand(target, "OGN·175/298", []));
            await game.Act("P2", new EndTurnCommand()); await game.Act("P1", new EndTurnCommand());
            var field = Assert.Single(game.State.PlayerZones["P2"].Battlefields, id => game.State.CardObjects[id].Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag));
            await game.Act("P2", new MoveUnitCommand(target, "BASE", "BATTLEFIELD:" + field, []));
            for (var action = 0; action < 12 && game.State.TimingState != TimingStates.NeutralOpen; action++) {
                if (game.State.PriorityPlayerId is { } priority) await game.Act(priority, new PassPriorityCommand());
                else if (game.State.FocusPlayerId is { } focus) await game.Act(focus, new PassFocusCommand());
                else Assert.Fail("Expected ordinary uncontested movement response.");
            }
            Assert.Equal(TimingStates.NeutralOpen, game.State.TimingState); Assert.Equal(3, game.Might("P2", target));
            await game.Act("P2", new EndTurnCommand()); await game.Tap("P1");
            var unit = game.Hand("P1", Mel).First(); var fodder = game.Hand("P1", "SFD·122/221").First();
            await game.Act("P1", new PlayCardCommand(unit, Mel, []));
            await game.Act("P1", new ActivateAbilityCommand(unit, "EMPOWER:0:0", [], OptionalCosts: ["DISCARD_HAND_CARD:" + fodder]));
            await game.Top(); var item = Assert.Single(game.State.StackItems); var context = item.FieldContext!;
            var birth = context.EmpoweredTransition!; Assert.True(birth.CapturedTick > 0);
            var forged = game.State with { StackItems = [item with { FieldContext = context with {
                EmpoweredTransition = birth with { CapturedTick = birth.CapturedTick - 1 } } }] };
            await game.RejectCoordinatedForgeryTwiceThenRecover("false-empower-birth", forged);
            var choice = game.State.PendingCardChoice!;
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target])); await game.ColdCheckpoint();
            await game.Top(); Assert.Contains(target, game.State.PlayerZones["P2"].Banished); await game.ColdCheckpoint();
        }
        finally { await game.Save("empowered-banish-birth-registry"); }
    }
}
