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
        var game = new Driver(initial);
        try
        {
            Assert.DoesNotContain(game.State.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.MinionTokenFamily));
            Assert.Equal(12, game.State.CardObjects.Values.Count(c => c.OwnerId == "P1" && c.Tags.Contains(CardObjectTags.RuneCard)));
            for (var i = 0; i < 12; i++) await game.Act("P1", new TapRuneCommand("TOKEN-COST-RUNE-" + i));
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
                Assert.Equal(new[] { "TOKEN-COST-RUNE-0", "TOKEN-COST-RUNE-1" }, game.State.PlayerZones["P1"].RuneDeck);
                await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
                Assert.Equal("P1", game.State.TurnPlayerId); Assert.Empty(game.State.PlayerZones["P1"].RuneDeck);
                foreach (var rune in new[] { "TOKEN-COST-RUNE-0", "TOKEN-COST-RUNE-1" })
                { Assert.Contains(rune, game.State.PlayerZones["P1"].Base); Assert.False(game.State.CardObjects[rune].IsExhausted); }
                Assert.Equal(12, game.State.CardObjects.Values.Count(c => c.OwnerId == "P1" && c.Tags.Contains(CardObjectTags.RuneCard)));
                Assert.Equal(0, game.State.RunePools["P1"].Mana);
                for (var i = 0; i < 4 + copies; i++) await game.Act("P1", new TapRuneCommand("TOKEN-COST-RUNE-" + i));
                await game.Act("P1", new RecycleRuneCommand("TOKEN-COST-RUNE-0"));
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
            Assert.Equal(3, game.State.CardObjects["SECOND"].Power); Assert.Equal(area ? 2 - copies : 0, game.State.RunePools["P1"].Mana);
            Assert.Equal(0, game.State.RunePools["P1"].TotalPower); Assert.False(game.State.CardObjects.ContainsKey(token));
            var recycled = (area ? new[] { "TOKEN-COST-RUNE-0", "TOKEN-COST-RUNE-1" } : new[] { "TOKEN-COST-RUNE-0" })
                .Concat(Enumerable.Range(0, copies).Select(i => "TOKEN-COST-RUNE-" + (i + 2))).ToArray();
            Assert.Equal(recycled, game.State.PlayerZones["P1"].RuneDeck);
            Assert.Empty(game.State.TriggerQueue); Assert.Empty(game.State.StackItems); await game.Replay();
        }
        finally { await Flow.Save("created-copied-ceased-area-" + area + "-copies-" + copies, initial, game); }
    }

    [Theory]
    [InlineData(0)] [InlineData(2)]
    public async Task ARealCreatedCopiedAndMovedTokenKeepsItsBattlefieldDamageAfterCeasing(int copies)
    {
        var initial = Fixture.Position("OGN·190/298", copies);
        initial = Flow.Add(initial, "RAY", "OGN·009/298", "P2", "HAND");
        for (var i = 0; i < 12; i++) initial = Flow.Add(initial, "AREA-P2-RUNE-" + i, "UNL-R01", "P2", "BASE");
        var reserve = Enumerable.Range(2, 10).Select(i => "AREA-P2-RUNE-" + i).ToArray();
        initial = initial with {
            ObjectLocations = initial.ObjectLocations.ToDictionary(p => p.Key, p => reserve.Contains(p.Key)
                ? new ObjectLocationState("P2", "RUNE_DECK") : p.Value),
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P2"] = initial.PlayerZones["P2"] with {
                Base = initial.PlayerZones["P2"].Base.Except(reserve).ToArray(), RuneDeck = reserve } } };
        var game = new Driver(initial);
        try
        {
            for (var i = 0; i < 12; i++) await game.Act("P1", new TapRuneCommand("TOKEN-COST-RUNE-" + i));
            await game.Act("P1", new PlayCardCommand("CARRIER", "VEN·128", []));
            await game.Act("P1", new RecycleRuneCommand("TOKEN-COST-RUNE-0"));
            await game.Act("P1", new ActivateAbilityCommand("CARRIER", "EMPOWER:0:0", [])); await game.Top();
            Assert.True(game.State.CardObjects["CARRIER"].IsEmpowered);
            await game.Act("P1", new PlayCardCommand("UNIT", "VEN·124", []));
            await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [],
                OptionalCosts: [Fixture.CostChoice(game.State, "UNIT", "CARRIER")]));
            await game.Top(); var minions = Tokens(game.State); Assert.Equal(2, minions.Length); await game.Top();
            var token = minions[0]; var other = minions[1]; var generation = game.State.CardObjects[token].ObjectGeneration;
            var factory = game.State.CardObjects[token].TokenFactoryCardNo; Assert.NotNull(factory);
            await game.Act("P1", new RecycleRuneCommand("TOKEN-COST-RUNE-1"));
            await game.Act("P1", new AssembleEquipmentCommand("GEAR", token, ["ASSEMBLE_YELLOW"])); await game.Top();
            var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
            await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["COPY:ALLY"]));
            for (var i = 0; i < copies; i++)
            {
                await game.Act("P1", new RecycleRuneCommand("TOKEN-COST-RUNE-" + (i + 2)));
                await game.Act("P1", new AssembleEquipmentCommand("SONG-" + i, token, ["ASSEMBLE_GREEN"])); await game.Top();
            }
            await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
            Assert.False(game.State.CardObjects[token].IsExhausted); Assert.False(game.State.CardObjects[other].IsExhausted);
            await game.Act("P1", new MoveUnitCommand(token, "BASE", "BATTLEFIELD:BF", [], [token, other]));
            await BattleWinnerPhaseBoundaryTests.PassFocus(game);
            Assert.Equal("BF", game.State.ObjectLocations[token].BattlefieldObjectId);
            Assert.Equal("BF", game.State.ObjectLocations["GEAR"].BattlefieldObjectId);
            Assert.Equal(generation, game.State.CardObjects[token].ObjectGeneration);
            await game.Act("P1", new EndTurnCommand());
            await game.Act("P2", new TapRuneCommand("AREA-P2-RUNE-0"));
            await game.Act("P2", new TapRuneCommand("AREA-P2-RUNE-1"));
            await game.Act("P2", new RecycleRuneCommand("AREA-P2-RUNE-0"));
            await game.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", []));
            await game.Act("P2", new PlayCardCommand("RAY", "OGN·009/298", [token])); await game.Top();
            Assert.False(game.State.CardObjects.ContainsKey(token)); Assert.False(game.State.ObjectLocations.ContainsKey(token));
            Assert.DoesNotContain(token, game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(0, game.State.CardObjects[other].Damage); Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
            Assert.Equal(1 + copies, game.State.TriggerQueue.Count + game.State.StackItems.Count);
            if (game.State.TriggerQueue.Count > 0)
                await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            var instances = new HashSet<string>();
            for (var i = 0; i <= copies; i++)
            {
                var birth = Assert.IsType<LastBreathBattlefieldDamageContext>(game.State.StackItems[^1].LastBreathBattlefieldDamage);
                Assert.Equal(new ObjectBinding(token, generation), birth.Source); Assert.Equal(factory, birth.SourceTokenFactoryCardNo);
                Assert.Equal("P1", birth.ControllerId); Assert.Equal("BATTLEFIELD", birth.SourceZone); Assert.Equal("BF", birth.BattlefieldObjectId);
                Assert.True(instances.Add(birth.InstanceId));
                foreach (var view in ResolutionResult.BuildSnapshots(game.State).Values.Cast<object>().Append(ResolutionResult.BuildSpectatorSnapshot(game.State)))
                    Assert.DoesNotContain("\"lastBreathBattlefieldDamage\"", JsonSerializer.Serialize(view), StringComparison.OrdinalIgnoreCase);
                var prefix = game.Events().Length;
                await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
                Assert.DoesNotContain(game.Events().Skip(prefix), e => e.Event.Kind is "DAMAGE_APPLIED" or "UNIT_DESTROYED");
                await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
                var effects = game.Events().Skip(prefix).Select(e => e.Event).ToArray();
                Assert.Single(effects, e => e.Kind == "TRIGGER_RESOLVED");
                var damage = effects.Where(e => e.Kind == "DAMAGE_APPLIED").ToArray(); Assert.Equal(i == 0 ? 2 : 0, damage.Length);
                Assert.All(damage, e => Assert.Equal(4, Convert.ToInt32(e.Payload["damage"])));
                foreach (var death in effects.Where(e => e.Kind == "UNIT_DESTROYED"))
                {
                    var cause = Assert.IsType<DestructionAttribution>(death.Payload["destructionAttribution"]);
                    Assert.Equal(new ObjectBinding(token, generation), cause.Source); Assert.Equal("OGN·190/298", cause.CardNo);
                    Assert.Equal("P1", cause.ControllerId); Assert.Equal("P1", cause.ResponsiblePlayerId); Assert.False(cause.FromSpell);
                }
                Assert.False(game.State.CardObjects.ContainsKey(token)); Assert.Equal(0, game.State.CardObjects["ALLY"].Damage);
                Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
                Assert.True(game.State.BattleState.IsActive); Assert.Equal("BF", game.State.BattleState.BattlefieldObjectId);
                if (i == 0)
                    Assert.Contains("invalid private battle scope lifetime or players", OfficialInsightAndSpellLockTests.Errors(game.State with {
                        UntilEndOfTurnEffects = game.State.UntilEndOfTurnEffects.Where(e => !e.StartsWith("BATTLE_RESPONSE_DECLARATION_CONTEXT:", StringComparison.Ordinal)).ToArray() }));
                Assert.Equal(copies - i, game.State.StackItems.Count);
            }
            Assert.False(game.State.CardObjects.ContainsKey(other)); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
            Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId); Assert.Empty(game.State.TriggerQueue);
            foreach (var player in new[] { "P1", "P2" })
                Assert.Equal(12, game.State.CardObjects.Values.Count(c => c.OwnerId == player && c.Tags.Contains(CardObjectTags.RuneCard)));
            var scores = game.State.PlayerScores.ToDictionary(); await BattleWinnerPhaseBoundaryTests.PassFocus(game);
            Assert.False(game.State.BattleState.IsActive); Assert.False(game.State.SpellDuelState.IsActive); Assert.Null(game.State.PendingBattleResult);
            Assert.Single(game.Events(), e => e.Event.Kind == "BATTLE_NO_RESULT"); Assert.Single(game.Events(), e => e.Event.Kind == "BATTLE_CLOSED");
            Assert.Equal(scores, game.State.PlayerScores); Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BATTLEFIELD_CONQUERED");
            Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, e => e.StartsWith("BATTLE_LIMITED_", StringComparison.Ordinal)
                || e.StartsWith("BATTLE_ROLE_", StringComparison.Ordinal));
            await game.Replay();
        }
        finally { await Flow.Save("created-copied-ceased-battlefield-area-copies-" + copies, initial, game); }
    }

    private static string[] Tokens(MatchState state) => state.PlayerZones["P1"].Base
        .Where(id => state.CardObjects[id].Tags.Contains(CardObjectTags.MinionTokenFamily)).Order(StringComparer.Ordinal).ToArray();
}
