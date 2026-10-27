using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using Fixture = Riftbound.ConformanceTests.Ven124CreatedTokenCostInteractionTests;
using Flow = Riftbound.ConformanceTests.LastBreathDrawBoundaryTests;

namespace Riftbound.ConformanceTests;

public sealed class CeasedAreaAndCreationLastBreathConsumerTests
{
    [Theory]
    [InlineData(true, 0)] [InlineData(true, 2)] [InlineData(false, 0)] [InlineData(false, 2)]
    public async Task ActualCreatedCopiedTokenKeepsEveryBaseAreaOrEmpoweredCreationSkillAfterCeasing(bool area, int copies)
    {
        var no = area ? "OGN·190/298" : "VEN·128"; var initial = Fixture.Position(no, copies);
        if (!area) initial = Flow.Add(initial, "TOKEN-COST-RUNE-12", "UNL-R06", "P1", "BASE");
        var game = new Driver(initial);
        try
        {
            Assert.DoesNotContain(game.State.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.MinionTokenFamily));
            for (var i = 0; i < (area ? 12 : 13); i++) await game.Act("P1", new TapRuneCommand("TOKEN-COST-RUNE-" + i));
            await game.Act("P1", new PlayCardCommand("CARRIER", "VEN·128", []));
            await game.Act("P1", new RecycleRuneCommand("TOKEN-COST-RUNE-0"));
            await game.Act("P1", new ActivateAbilityCommand("CARRIER", "EMPOWER:0:0", [])); await game.Top();
            Assert.True(game.State.CardObjects["CARRIER"].IsEmpowered);
            await game.Act("P1", new PlayCardCommand("UNIT", "VEN·124", []));
            await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [],
                OptionalCosts: [Fixture.CostChoice(game.State, "UNIT", "CARRIER")]));
            await game.Top();
            var minions = Tokens(game.State); Assert.Equal(2, minions.Length);
            await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            var token = minions[0]; var other = minions[1]; var generation = game.State.CardObjects[token].ObjectGeneration;
            var factory = game.State.CardObjects[token].TokenFactoryCardNo;
            await game.Act("P1", new RecycleRuneCommand("TOKEN-COST-RUNE-1"));
            await game.Act("P1", new AssembleEquipmentCommand("GEAR", token, ["ASSEMBLE_YELLOW"])); await game.Top();
            var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
            await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["COPY:ALLY"]));
            Assert.Equal(no, game.State.CardObjects[token].CardNo); Assert.Equal(factory, game.State.CardObjects[token].TokenFactoryCardNo);
            Assert.False(game.State.CardObjects[token].IsEmpowered);
            if (!area)
            {
                await game.Act("P1", new RecycleRuneCommand("TOKEN-COST-RUNE-12"));
                var candidate = Assert.Single(game.PromptFor("P1").Candidates!, c => c.Action == CommandTypes.ActivateAbility);
                var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
                var selected = Assert.Single(requirements, row => Equals(row["sourceObjectId"], token) && Equals(row["abilityId"], "EMPOWER:0:0"));
                await game.Act("P1", new ActivateAbilityCommand(token, "EMPOWER:0:0", []) {
                    AbilityInstanceId = selected.GetValueOrDefault("abilityInstanceId") as string }); await game.Top();
                Assert.True(game.State.CardObjects[token].IsEmpowered);
            }
            for (var i = 0; i < copies; i++)
            {
                await game.Act("P1", new RecycleRuneCommand("TOKEN-COST-RUNE-" + (i + 2)));
                await game.Act("P1", new AssembleEquipmentCommand("SONG-" + i, token, ["ASSEMBLE_GREEN"])); await game.Top();
                Assert.Equal(no, game.State.CardObjects["SONG-" + i].AttachedRuleText!.CardNo);
            }
            await game.Act("P1", new PlayCardCommand("SECOND", "VEN·124", []));
            var prefix = game.Events().Length;
            await game.Act("P1", new ActivateAbilityCommand("SECOND", "EMPOWER:0:0", [],
                OptionalCosts: [Fixture.CostChoice(game.State, "SECOND", token)]));
            Assert.False(game.State.CardObjects.ContainsKey(token)); Assert.Equal(new[] { other }, Tokens(game.State));
            Assert.DoesNotContain(game.Events().Skip(prefix), e => e.Event.Kind is "DAMAGE_APPLIED" or "CARD_DRAWN" or "RUNES_CALLED");
            var parent = Assert.Single(game.State.StackItems, item => item.EmpowerActivation is not null);
            Assert.Equal(new ObjectBinding(token, generation), parent.EmpowerActivation!.DestructionCost!.Unit);
            Assert.Equal(factory, parent.EmpowerActivation.DestructionCost.TokenFactoryCardNo);
            Assert.Equal(1 + copies, game.State.TriggerQueue.Count + game.State.StackItems.Count - 1);
            if (game.State.TriggerQueue.Count > 0)
                await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            var ids = new HashSet<string>();
            for (var i = 0; i <= copies; i++)
            {
                var item = game.State.StackItems[^1]; Assert.Equal(token, item.SourceObjectId); Assert.Equal("P1", item.ControllerId);
                if (area)
                {
                    var birth = Assert.IsType<LastBreathBattlefieldDamageContext>(item.LastBreathBattlefieldDamage);
                    Assert.Equal("BASE", birth.SourceZone); Assert.Null(birth.BattlefieldObjectId);
                    Assert.Equal(new ObjectBinding(token, generation), birth.Source); Assert.Equal(factory, birth.SourceTokenFactoryCardNo);
                    Assert.True(ids.Add(birth.InstanceId));
                }
                else
                {
                    var birth = Assert.IsType<LastBreathUnitCreationContext>(item.LastBreathUnitCreation);
                    Assert.True(birth.SourceWasEmpowered); Assert.Equal(new ObjectBinding(token, generation), birth.Source);
                    Assert.Equal(factory, birth.SourceTokenFactoryCardNo); Assert.True(ids.Add(birth.InstanceId));
                }
                foreach (var view in ResolutionResult.BuildSnapshots(game.State).Values.Cast<object>().Append(ResolutionResult.BuildSpectatorSnapshot(game.State)))
                {
                    var raw = JsonSerializer.Serialize(view);
                    Assert.DoesNotContain("\"lastBreathBattlefieldDamage\"", raw, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("\"lastBreathUnitCreation\"", raw, StringComparison.OrdinalIgnoreCase);
                }
                var before = Tokens(game.State); prefix = game.Events().Length;
                await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand()); Assert.Equal(before, Tokens(game.State));
                Assert.False(game.State.CardObjects["SECOND"].IsEmpowered);
                await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
                var effects = game.Events().Skip(prefix).Select(e => e.Event).ToArray(); Assert.Single(effects, e => e.Kind == "TRIGGER_RESOLVED");
                Assert.DoesNotContain(effects, e => e.Kind is "DAMAGE_APPLIED" or "CARD_DRAWN" or "RUNES_CALLED");
                var after = Tokens(game.State); Assert.Equal(before.Length + (area ? 0 : 2), after.Length);
                foreach (var fresh in after.Except(before))
                {
                    var card = game.State.CardObjects[fresh]; Assert.Equal(1, card.Power); Assert.Equal("P1", card.OwnerId);
                    Assert.Equal("P1", card.ControllerId); Assert.False(card.IsEmpowered);
                }
                Assert.False(game.State.CardObjects.ContainsKey(token)); Assert.Contains(other, after);
                Assert.False(game.State.CardObjects["SECOND"].IsEmpowered); Assert.Equal(parent, game.State.StackItems[0]);
            }
            Assert.Single(game.State.StackItems); await game.Top(); Assert.True(game.State.CardObjects["SECOND"].IsEmpowered);
            Assert.Equal(3, game.State.CardObjects["SECOND"].Power); Assert.Equal(2 - copies, game.State.RunePools["P1"].Mana);
            Assert.Equal(0, game.State.RunePools["P1"].TotalPower); Assert.False(game.State.CardObjects.ContainsKey(token));
            var recycled = new[] { "TOKEN-COST-RUNE-0", "TOKEN-COST-RUNE-1" }.Concat(area ? [] : new[] { "TOKEN-COST-RUNE-12" })
                .Concat(Enumerable.Range(0, copies).Select(i => "TOKEN-COST-RUNE-" + (i + 2))).ToArray();
            Assert.Equal(recycled, game.State.PlayerZones["P1"].RuneDeck);
            Assert.Empty(game.State.TriggerQueue); Assert.Empty(game.State.StackItems); await game.Replay();
        }
        finally { await Flow.Save("created-copied-ceased-area-" + area + "-copies-" + copies, initial, game); }
    }

    private static string[] Tokens(MatchState state) => state.PlayerZones["P1"].Base
        .Where(id => state.CardObjects[id].Tags.Contains(CardObjectTags.MinionTokenFamily)).Order(StringComparer.Ordinal).ToArray();
}
