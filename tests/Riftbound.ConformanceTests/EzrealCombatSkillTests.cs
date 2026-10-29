using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class EzrealCombatSkillTests
{
    private const string Card = "SFD·082/221";
    private static readonly string[] Prints = [Card, "SFD·082a/221", "SFD·082b/221·P", "RAD·180*/167", "RAD·180/167"];
    public static IEnumerable<object[]> Roles() => Prints.SelectMany(no => new[] { false, true }.Select(defend => new object[] { no, defend }));
    public static IEnumerable<object[]> Numbers() => Prints.Select(no => new object[] { no });
    private static ActivateAbilityCommand Move() => new("HOST", P4ActivatedAbilityCatalog.EzrealBlueSwiftMoveAbilityId, []);
    private static MatchState Swift(string no = Card, bool attached = false)
    {
        var s = ArmamentMightTests.Position(unit: no, power: 3, attached: attached);
        s = LocalDestructionRecallTests.At(s, "HOST", "P1", "BATTLEFIELD", "F");
        if (attached) s = LocalDestructionRecallTests.At(s, "GEAR", "P1", "BATTLEFIELD", "F");
        return s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) {
            ["P1"] = new(0, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }) } };
    }
    private static MatchState Board(string no = Card, bool defend = false, bool copied = false)
    {
        var s = AhriCombatMightTests.Board(no, defend, copied);
        return s with { CardObjects = s.CardObjects.ToDictionary(p => p.Key,
            p => p.Key == (defend ? "HOST" : "ENEMY1") ? p.Value with { Power = 12 } : p.Value) };
    }
    private static void Check(MatchState s) { Restore(s); ArmamentMightTests.PublicRecovery(s); }

    [Theory, MemberData(nameof(Roles))]
    public async Task EveryPrintingConfirmsOneLocalEnemyAndDealsItsCurrentMightAfterResponses(string no, bool defend)
    {
        var source = defend ? "ENEMY1" : "HOST"; var target = defend ? "HOST" : "ENEMY1";
        var moved = await Act(Board(no, defend), "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var choice = Assert.IsType<PendingCardChoiceState>(moved.State.PendingCardChoice);
        Assert.Equal(defend ? "P2" : "P1", choice.PlayerId); Assert.Equal(1, choice.RequiredCount);
        Assert.Contains(target, choice.LegalObjectIds); Assert.DoesNotContain(source, choice.LegalObjectIds);
        Assert.Equal(0, moved.State.CardObjects[target].Damage); Check(moved.State);
        var confirmed = await AttackDamageDistributionTests.Confirm(moved.State, target);
        Assert.Single(confirmed.State.StackItems); Assert.Equal(0, confirmed.State.CardObjects[target].Damage);
        // A changed continuous Might value is read at resolution, not capture.
        var changed = confirmed.State with { CardObjects = confirmed.State.CardObjects.ToDictionary(p => p.Key,
            p => p.Key == source ? p.Value with { Power = 5 } : p.Value) };
        var result = await AttackDamageDistributionTests.Top(changed);
        Assert.Equal(5, result.State.CardObjects[target].Damage);
        Assert.Equal(5, result.State.CardObjects[source].Power);
        Assert.Equal(0, result.State.CardObjects[defend ? "ENEMY2" : "ENEMY3"].Damage); Check(result.State);
    }

    [Theory, MemberData(nameof(Numbers))]
    public async Task IllegalFriendlyTriggerTargetIsRejectedWithoutMutation(string no)
    {
        var moved = await Act(Board(no), "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var choice = moved.State.PendingCardChoice!;
        var hash = MatchStateHasher.Hash(moved.State);
        var result = await new CoreRuleEngine().ResolveAsync(moved.State, new("invalid-ezreal-target", "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOST"]), default);
        Assert.False(result.Accepted); Assert.Equal(hash, MatchStateHasher.Hash(result.State)); Assert.Empty(result.Events);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task RecallOfSourceOrTargetInvalidatesDamageAndKeepsPrivateIdentityHidden(bool targetLeaves)
    {
        var initial = EquipChainTests.Response(Board(), "OGN·169/298", "P2");
        var pending = await AhriCombatMightTests.Pending(initial, "ENEMY1");
        var passed = await Act(pending, "P1", new PassPriorityCommand());
        var response = await Act(passed.State, "P2", new PlayCardCommand("RESPONSE", "OGN·169/298", [targetLeaves ? "ENEMY1" : "HOST"]));
        var recalled = await Top(response.State); Check(recalled.State);
        var done = await Top(recalled.State);
        Assert.Equal(0, done.State.CardObjects["ENEMY1"].Damage); Check(done.State);
        if (!targetLeaves) {
            var stack = Assert.IsType<Dictionary<string, object?>>(Assert.Single(ResolutionResult.BuildSnapshots(recalled.State)["P2"].Stack));
            Assert.Equal("HIDDEN", stack["sourceObjectId"]);
        }
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task ANewSourceOrTargetGenerationCannotReceiveTheHistoricalDamage(bool targetChanges)
    {
        var pending = await AhriCombatMightTests.Pending(Board(), "ENEMY1");
        var id = targetChanges ? "ENEMY1" : "HOST";
        var changed = pending with { CardObjects = pending.CardObjects.ToDictionary(p => p.Key,
            p => p.Key == id ? p.Value with { ObjectGeneration = p.Value.ObjectGeneration + 1 } : p.Value) };
        var done = await Top(changed); Assert.Equal(0, done.State.CardObjects["ENEMY1"].Damage); Check(done.State);
    }

    [Fact]
    public async Task CounteredSkillDoesNoDamageAndDoesNotRemoveTheSource()
    {
        var pending = await AhriCombatMightTests.Pending(EquipChainTests.Response(Board(), "SFD·045/221", "P2"), "ENEMY1");
        var passed = await Act(pending, "P1", new PassPriorityCommand());
        var response = await Act(passed.State, "P2", new PlayCardCommand("RESPONSE", "SFD·045/221", [pending.StackItems.Single().StackItemId]));
        var done = await Top(response.State); Assert.Empty(done.State.StackItems);
        Assert.Equal(0, done.State.CardObjects["ENEMY1"].Damage); Assert.Contains("HOST", done.State.PlayerZones["P1"].Battlefields); Check(done.State);
    }

    [Fact]
    public async Task CopiedTextCreatesIndependentTargetedSkillsUsingTheHostsCurrentMight()
    {
        var pending = await AhriCombatMightTests.Pending(Board(copied: true), "ENEMY1", "ENEMY1");
        Assert.Equal(2, pending.StackItems.Count); Assert.Single(pending.StackItems, i => i.FieldContext!.Origin is not null); Check(pending);
        var first = await Top(pending); Assert.Equal(3, first.State.CardObjects["ENEMY1"].Damage);
        var done = await Top(first.State); Assert.Equal(6, done.State.CardObjects["ENEMY1"].Damage); Check(done.State);
    }

    [Theory, MemberData(nameof(Numbers))]
    public async Task SwiftMovePaysBlueOnEitherPlayersDuelFocusAndReturnsItsEquipment(string no)
    {
        foreach (var opponentTurn in new[] { false, true }) {
            var s = Swift(no, attached: true);
            s = s with { TurnPlayerId = opponentTurn ? "P2" : "P1", ActivePlayerId = "P1", FocusPlayerId = "P1",
                TimingState = TimingStates.SpellDuelOpen, SpellDuelState = new(true, false, "spell-duel:F", "F", "P1", [], [], []) };
            Check(s);
            Assert.Contains(CommandTypes.ActivateAbility, ResolutionResult.BuildPrompts(s)["P1"].Actions);
            var activated = await Act(s, "P1", Move()); Check(activated.State);
            Assert.Equal(0, activated.State.RunePools["P1"].TotalPower);
            Assert.Contains("HOST", activated.State.PlayerZones["P1"].Battlefields);
            var done = await Top(activated.State); Assert.Contains("HOST", done.State.PlayerZones["P1"].Base);
            Assert.Contains("GEAR", done.State.PlayerZones["P1"].Base);
            Assert.Equal("HOST", done.State.CardObjects["GEAR"].AttachedToObjectId); Check(done.State);
        }
    }

    [Theory, InlineData("focus"), InlineData("priority"), InlineData("closed"), InlineData("blue")]
    public async Task InvalidSwiftWindowOrPaymentCannotChangeState(string branch)
    {
        var s = Swift();
        s = branch switch {
            "focus" => s with { TimingState = TimingStates.SpellDuelOpen, FocusPlayerId = "P2",
                SpellDuelState = new(true, false, "spell-duel:F", "F", "P2", [], [], []) },
            "priority" => s with { TimingState = TimingStates.SpellDuelOpen, FocusPlayerId = "P1", PriorityPlayerId = "P2",
                SpellDuelState = new(true, true, "spell-duel:F", "F", "P1", [], [], []) },
            "closed" => s with { TimingState = TimingStates.NeutralClosed },
            _ => s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = RunePool.Empty } } };
        var hash = MatchStateHasher.Hash(s);
        var result = await new CoreRuleEngine().ResolveAsync(s, new("invalid-swift", "P1", CommandTypes.ActivateAbility), Move(), default);
        Assert.False(result.Accepted); Assert.Equal(hash, MatchStateHasher.Hash(result.State)); Assert.Empty(result.Events);
    }

    [Fact]
    public async Task SwiftMovementCannotMoveAReenteredObjectAndRejectsForgedSourceReceipts()
    {
        var activated = await Act(Swift(), "P1", Move()); var state = activated.State; Check(state);
        var item = Assert.Single(state.StackItems); var context = item.SelfMovement!;
        foreach (var forged in new[] { context with { CapturedTick = state.Tick + 1 }, context with { FaceRevision = 99 },
            context with { Source = context.Source with { Generation = context.Source.Generation + 1 } } }) {
            var invalid = state with { StackItems = [item with { SelfMovement = forged }] };
            Assert.NotEmpty(MinimumMightAuditTests.PublicErrors(invalid));
        }
        var changed = state with { CardObjects = state.CardObjects.ToDictionary(p => p.Key,
            p => p.Key == "HOST" ? p.Value with { ObjectGeneration = p.Value.ObjectGeneration + 1 } : p.Value) };
        var done = await Top(changed); Assert.Contains("HOST", done.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, done.State.RunePools["P1"].TotalPower); Check(done.State);
    }

    [Fact]
    public async Task PrivateSelfMovementReceiptIsNeverPartOfPlayerOrSpectatorStack()
    {
        var state = (await Act(Swift(), "P1", Move())).State;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = snapshots.ToDictionary(p => p.Key, p => new RecoveredPlayerView(p.Key, state.Tick, 0, p.Value, state.Tick, 0, prompts[p.Key]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        foreach (var snapshot in snapshots.Values.Append(spectator.SpectatorSnapshot)) {
            Assert.DoesNotContain("selfMovement", JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            var item = Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshot.Stack));
            item["selfMovement"] = Assert.Single(state.StackItems).SelfMovement;
            Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(state, views, spectator)); item.Remove("selfMovement");
        }
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(state, views, spectator));
    }
    [Theory, MemberData(nameof(Roles))]
    public async Task ActualCombatContributesNoDamageButPreservesMightAndLethalThreshold(string no, bool defend)
    {
        var s = await AhriCombatMightTests.Pending(Board(no, defend), defend ? "HOST" : "ENEMY1");
        s = (await Top(s)).State;
        var source = defend ? "ENEMY1" : "HOST";
        Assert.Equal(3, s.CardObjects[source].Power);
        var events = new List<GameEvent>();
        for (var step = 0; step < 30 && !events.Any(e => e.Kind == "BATTLE_CLOSED"); step++) {
            var prompts = ResolutionResult.BuildPrompts(s);
            var assignment = prompts.Values.FirstOrDefault(p => p.Candidates?.Any(c => c.Action == CommandTypes.AssignCombatDamage && c.Enabled) == true);
            ResolutionResult result;
            if (assignment is not null) {
                var metadata = JsonSerializer.SerializeToElement(assignment.View!.Metadata);
                if (metadata.GetProperty("assignableDamagePool").TryGetProperty(source, out var pool)) Assert.Equal(0, pool.GetInt32());
                if (metadata.GetProperty("lethalDamageThreshold").TryGetProperty(source, out var lethal))
                    Assert.Equal(3 - s.CardObjects[source].Damage, lethal.GetInt32());
                result = await Act(s, assignment.PlayerId, new AssignCombatDamageCommand(s.BattleState.BattleId!, "F", CombatTestDriver.Assignments(assignment)));
            } else if (s.PriorityPlayerId is { } priority) result = await Act(s, priority, new PassPriorityCommand());
            else if (s.FocusPlayerId is { } focus) result = await Act(s, focus, new PassFocusCommand());
            else {
                var actor = prompts.Values.First(p => p.Candidates?.Any(c => c.Action == CommandTypes.DeclareBattle && c.Enabled) == true).PlayerId;
                result = await Act(s, actor, new DeclareBattleCommand("F", s.BattleState.AttackerObjectIds, s.BattleState.DefenderObjectIds, ["COMBAT_ASSIGNMENT"]));
            }
            events.AddRange(result.Events); s = result.State;
        }
        Assert.Contains(events, e => e.Kind == "BATTLE_CLOSED");
        Assert.DoesNotContain(events, e => e.Kind == "DAMAGE_APPLIED" && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), source)
            && e.Payload.ContainsKey("combatRole")); Check(s);
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task SpellshieldIsPaidAfterChoosingBeforeTheDamageSkillCanBeAnswered(bool pay)
    {
        var s = Board(); s = s with { CardObjects = s.CardObjects.ToDictionary(p => p.Key,
            p => p.Key == "ENEMY1" ? p.Value with { Tags = [CardObjectTags.UnitCard, "法盾2"] } : p.Value) };
        var moved = await Act(s, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var choice = moved.State.PendingCardChoice!;
        var chosen = await Act(moved.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY1"]));
        var payment = Assert.IsType<PendingPaymentState>(chosen.State.PendingPayment);
        Assert.Equal(2, payment.PowerCost); Assert.Equal(0, payment.ExperienceCost); Assert.Null(chosen.State.PriorityPlayerId); Check(chosen.State);
        var paid = await Act(chosen.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
        Assert.Equal(s.RunePools["P1"].TotalPower - (pay ? 2 : 0), paid.State.RunePools["P1"].TotalPower);
        if (pay) paid = await Top(paid.State); else Assert.Empty(paid.State.StackItems);
        Assert.Equal(pay ? 3 : 0, paid.State.CardObjects["ENEMY1"].Damage); Check(paid.State);
    }

    [Fact]
    public async Task ActualBuffResponseChangesTheDamageAndSkillDamageRemainsIndependentOfCombatDamage()
    {
        var s = AttackDamageDistributionTests.Spell(Board(), "BUFF", "SFD·034/221", "P1");
        var pending = await AhriCombatMightTests.Pending(s, "ENEMY1");
        var buff = await Act(pending, "P1", new PlayCardCommand("BUFF", "SFD·034/221", ["HOST"]));
        var changed = await Top(buff.State); Assert.Equal(5, changed.State.CardObjects["HOST"].Power);
        var done = await Top(changed.State); Assert.Equal(5, done.State.CardObjects["ENEMY1"].Damage); Check(done.State);
    }

    [Fact]
    public async Task ASourceMovingToItsBaseInvalidatesThePreviouslyChosenBattlefieldEnemy()
    {
        var initial = AttackDamageDistributionTests.Spell(Board(), "FLASH", "OGS·011/024", "P1");
        var pending = await AhriCombatMightTests.Pending(initial, "ENEMY1");
        var response = await Act(pending, "P1", new PlayCardCommand("FLASH", "OGS·011/024", ["HOST"]));
        var moved = await Top(response.State); Assert.Equal("BASE", moved.State.ObjectLocations["HOST"].Zone); Check(moved.State);
        var done = await Top(moved.State); Assert.Equal(0, done.State.CardObjects["ENEMY1"].Damage); Check(done.State);
    }

    [Fact]
    public async Task WithoutAnyLocalEnemyTheMandatorySkillHasNoTargetChoiceAndCannotBlockFocus()
    {
        var s = Board();
        foreach (var id in new[] { "ENEMY1", "ENEMY2", "ENEMY3" })
            s = LocalDestructionRecallTests.At(s, id, "P2", "BASE");
        var moved = await Act(s, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Assert.Null(moved.State.PendingCardChoice); Assert.Empty(moved.State.StackItems);
        Assert.All(moved.State.CardObjects.Values, c => Assert.Equal(0, c.Damage)); Check(moved.State);
    }

    [Fact]
    public async Task ARecallResponseToSwiftMovementCannotRevealOrMoveThePrivateSource()
    {
        var initial = EquipChainTests.Response(Swift(), "OGN·169/298", "P2");
        var activated = await Act(initial, "P1", Move());
        var passed = await Act(activated.State, "P1", new PassPriorityCommand());
        var response = await Act(passed.State, "P2", new PlayCardCommand("RESPONSE", "OGN·169/298", ["HOST"]));
        var returned = await Top(response.State); Check(returned.State);
        Assert.Contains("HOST", returned.State.PlayerZones["P1"].Hand);
        var stack = Assert.IsType<Dictionary<string, object?>>(Assert.Single(ResolutionResult.BuildSnapshots(returned.State)["P2"].Stack));
        Assert.Equal("HIDDEN", stack["sourceObjectId"]);
        var done = await Top(returned.State); Assert.Contains("HOST", done.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(done.Events, e => e.Kind == "UNIT_MOVED_TO_BASE");
        Assert.All(done.Events.Where(e => e.Payload.ContainsKey("sourceObjectId")), e => Assert.NotEqual("HOST", e.Payload["sourceObjectId"])); Check(done.State);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task CopiedSwiftAbilityRetainsItsCapturedTextWhenTheCopyingEquipmentLeaves(bool gearLeaves)
    {
        var s = Swift(attached: true); s = s with { CardObjects = s.CardObjects.ToDictionary(p => p.Key,
            p => p.Key == "GEAR" ? p.Value with { CardNo = "SFD·059/221", AttachmentRevision = 1,
                AttachedRuleText = new(new("HOST", 0), Card, 1, s.Tick, 0) } : p.Value) };
        Check(s);
        var candidate = ResolutionResult.BuildPrompts(s)["P1"].Candidates!.Single(c => c.Action == CommandTypes.ActivateAbility);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
        var rows = requirements.Where(r => Equals(r.GetValueOrDefault("abilityId"), Move().AbilityId)).ToArray();
        Assert.Equal(2, rows.Length);
        var instanceId = Assert.IsType<string>(rows.Single(r => r.GetValueOrDefault("abilityInstanceId") is not null)["abilityInstanceId"]);
        var activated = await Act(s, "P1", Move() with { AbilityInstanceId = instanceId });
        Assert.NotNull(Assert.Single(activated.State.StackItems).SelfMovement!.Origin); Check(activated.State);
        s = activated.State;
        if (gearLeaves) {
            s = s with { PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                    ["P1"] = s.PlayerZones["P1"] with { Battlefields = s.PlayerZones["P1"].Battlefields.Where(id => id != "GEAR").ToArray(), Graveyard = ["GEAR"] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["GEAR"] = new("P1", "GRAVEYARD") },
                CardObjects = s.CardObjects.ToDictionary(p => p.Key, p => p.Key == "GEAR"
                    ? p.Value with { ObjectGeneration = 1, AttachedToObjectId = null, AttachedRuleText = null, AttachmentRevision = 0 } : p.Value) };
        }
        Check(s); var done = await Top(s); Assert.Contains("HOST", done.State.PlayerZones["P1"].Base); Check(done.State);
    }

}
