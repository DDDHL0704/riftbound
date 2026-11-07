using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class UdyrBoonModeActivationTests
{
    private const string Udyr = "OGN·157/298";
    private const string Damage = "CONSUME_BOON_DAMAGE_2";
    private const string Stun = "CONSUME_BOON_STUN_UNIT";
    private const string Ready = "CONSUME_BOON_READY_SELF";
    private const string Roam = "CONSUME_BOON_ROAM_SELF";

    private static MatchState Position(int boons = 1, string targetOwner = "P2", string target = "OGN·219/298")
    {
        var s = AttachedRuleTextTests.Position(Udyr, 0);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(target, out var targetDefinition));
        var printedTarget = OfficialGarenConquestBattlefieldCountTests.Printed("TARGET", target, targetOwner);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["HOST"] = s.CardObjects["HOST"] with { Power = 6 + boons, BoonCount = boons, IsExhausted = true,
                Tags = s.CardObjects["HOST"].Tags.Concat(boons == 0 ? [] : new[] { CardObjectTags.Boon }).Distinct().Order().ToArray() },
            ["TARGET"] = printedTarget with { Tags = printedTarget.Tags.Concat(targetDefinition.SourceUnitTags
                .Split('|', StringSplitOptions.RemoveEmptyEntries)).Distinct().Order().ToArray() } } };
        return LocalDestructionRecallTests.At(s, "TARGET", targetOwner, "BATTLEFIELD", targetOwner == "P1" ? "F" : "OTHER");
    }
    private static MatchState Spell(MatchState s, string id, string no = "OGN·053/298")
        => AttackDamageDistributionTests.Spell(s, id, no, "P1");
    private static ActivateAbilityCommand Mode(string mode, string? instance = null)
        => new("HOST", mode, mode is Damage or Stun ? ["TARGET"] : []) { AbilityInstanceId = instance };
    private static async Task Grant(Driver game, string id)
    {
        await game.Act("P1", new PlayCardCommand(id, "OGN·053/298", ["HOST"])); await game.Top();
        Assert.Equal(1, game.State.CardObjects["HOST"].BoonCount);
    }
    private static string[] Uses(MatchState s) => s.UntilEndOfTurnEffects
        .Where(e => e.StartsWith("PRINTED_ACTIVATION_USED:", StringComparison.Ordinal)).ToArray();
    private static IReadOnlyList<string> Errors(Driver game, MatchState state)
    {
        var commands = game.Commands();
        return MatchRecoveryValidator.Validate(state.RoomId, commands[^1].CompletedEventSequence,
            commands, game.Events(), new Dictionary<string, RecoveredPlayerView>(), state, state.Tick);
    }

    [Theory]
    [InlineData(Damage)] [InlineData(Stun)] [InlineData(Ready)] [InlineData(Roam)]
    public async Task EachModeConsumesOneBoonBeforeItsResponseWindowAndUsesTheSharedEffect(string mode)
    {
        var game = new Driver(Position()); var initialPool = game.State.RunePools["P1"];
        await game.Act("P1", Mode(mode));
        var item = Assert.Single(game.State.StackItems);
        Assert.Equal(0, game.State.CardObjects["HOST"].BoonCount);
        Assert.Equal(6, game.State.CardObjects["HOST"].Power);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["HOST"].Tags);
        Assert.Equal(new ObjectBinding("HOST", game.State.CardObjects["HOST"].ObjectGeneration), item.SimpleActivation!.ConsumedSourceBoon);
        Assert.Equal(initialPool, game.State.RunePools["P1"]);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(0, game.State.CardObjects["TARGET"].Damage);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["TARGET"].UntilEndOfTurnEffects);
        Assert.DoesNotContain("ROAM", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        Assert.Single(Uses(game.State)); Assert.Single(game.Events(), e => e.Event.Kind == "BOON_CONSUMED");
        await game.Top();
        Assert.Equal(mode == Damage ? 2 : 0, game.State.CardObjects["TARGET"].Damage);
        Assert.Equal(mode == Stun, game.State.CardObjects["TARGET"].UntilEndOfTurnEffects.Contains("STUNNED"));
        Assert.Equal(mode != Ready, game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(mode == Roam, game.State.CardObjects["HOST"].UntilEndOfTurnEffects.Contains("ROAM"));
        Assert.Equal(6, game.State.CardObjects["HOST"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData(Damage)] [InlineData(Stun)]
    public async Task BattlefieldTargetsCanBeFriendlyAndFriendlySpellshieldAddsNoTax(string mode)
    {
        var s = Position(targetOwner: "P1", target: "SFD·085/221");
        s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = RunePool.Empty } };
        var game = new Driver(s); await game.Act("P1", Mode(mode)); await game.Top();
        Assert.Equal(mode == Damage ? 2 : 0, game.State.CardObjects["TARGET"].Damage);
        Assert.Equal(mode == Stun, game.State.CardObjects["TARGET"].UntilEndOfTurnEffects.Contains("STUNNED"));
        Assert.Equal(RunePool.Empty, game.State.RunePools["P1"]); await game.Replay();
    }

    [Theory]
    [InlineData(Damage)] [InlineData(Stun)]
    public async Task EnemySpellshieldIsPaidBeforeTheBoonAndInsufficientFeesAreAtomic(string mode)
    {
        var s = Position(target: "SFD·085/221");
        Assert.Contains("法盾2", s.CardObjects["TARGET"].Tags);
        s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = RunePool.Empty } };
        await Reject(s, "P1", Mode(mode));
        s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(0, 3) } };
        var game = new Driver(s); await game.Act("P1", Mode(mode));
        Assert.Equal(1, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(0, game.State.CardObjects["HOST"].BoonCount);
        await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData("no-boon")] [InlineData("base-target")] [InlineData("equipment")]
    [InlineData("missing-target")] [InlineData("self-mode-target")] [InlineData("extra-cost")]
    [InlineData("opponent")] [InlineData("closed")]
    public async Task InvalidCostsTargetsAndTimingNeverConsumeTheBoonOrMode(string branch)
    {
        var s = Position(branch == "no-boon" ? 0 : 1); var command = Mode(Stun); var actor = "P1";
        if (branch == "base-target") s = LocalDestructionRecallTests.At(s, "TARGET", "P2", "BASE");
        if (branch == "equipment") command = command with { TargetObjectIds = ["GEAR"] };
        if (branch == "missing-target") command = command with { TargetObjectIds = [] };
        if (branch == "self-mode-target") command = Mode(Ready) with { TargetObjectIds = ["HOST"] };
        if (branch == "extra-cost") command = command with { OptionalCosts = ["SPEND_MANA:1"] };
        if (branch == "opponent") actor = "P2";
        if (branch == "closed") s = s with { TimingState = TimingStates.NeutralClosed, PriorityPlayerId = "P1" };
        await Reject(s, actor, command); Assert.Empty(Uses(s));
    }

    [Fact]
    public async Task FourModesHaveSeparateHistoriesAndARealNewBoonCannotBuyAUsedMode()
    {
        var s = Spell(Spell(Spell(Position(), "BOON1"), "BOON2"), "BOON3");
        var game = new Driver(s);
        var modes = new[] { Ready, Damage, Stun, Roam };
        for (var i = 0; i < modes.Length; i++)
        {
            if (i > 0) { await Grant(game, "BOON" + i); await Reject(game.State, "P1", Mode(modes[0])); }
            await game.Act("P1", Mode(modes[i])); await game.Top();
            Assert.Equal(i + 1, Uses(game.State).Length);
        }
        Assert.Equal(4, game.Events().Count(e => e.Event.Kind == "BOON_CONSUMED"));
        await game.ColdCheckpoint(); await game.Replay();
    }

    [Fact]
    public async Task ARealTurnChangeResetsModesAndRemovesTheTemporaryRoam()
    {
        var initial = Spell(Position(), "BOON");
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["P2-DRAW"] = OfficialGarenConquestBattlefieldCountTests.Printed("P2-DRAW", "OGN·001/298", "P2") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["P2-DRAW"] = new("P2", "MAIN_DECK") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P2"] = initial.PlayerZones["P2"] with { MainDeck = ["P2-DRAW"] } } };
        var game = new Driver(initial);
        await game.Act("P1", Mode(Roam)); await game.Top(); await Grant(game, "BOON");
        await Reject(game.State, "P1", Mode(Roam));
        await game.Act("P1", new EndTurnCommand());
        Assert.Empty(Uses(game.State)); Assert.DoesNotContain("ROAM", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        await game.Act("P2", new EndTurnCommand());
        await game.Act("P1", Mode(Roam)); await game.Top();
        Assert.Single(Uses(game.State)); Assert.Contains("ROAM", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Theory]
    [InlineData(Ready)] [InlineData(Roam)]
    public async Task ARealReturnResponseCannotApplyTheSelfInstructionToTheHandObject(string mode)
    {
        var game = new Driver(Spell(Position(), "RETURN", "OGN·104/298"));
        await game.Act("P1", Mode(mode));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        var hash = game.State.CardObjects["HOST"];
        await game.Top(); Assert.Equal(hash, game.State.CardObjects["HOST"]);
        Assert.Equal("HAND", game.State.ObjectLocations["HOST"].Zone);
        Assert.DoesNotContain("ROAM", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Fact]
    public async Task GrantedRoamEnablesAnActualBattlefieldMoveThroughTheNormalMovementCommand()
    {
        var s = LocalDestructionRecallTests.At(Position(), "HOST", "P1", "BATTLEFIELD", "F");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["HOST"] = s.CardObjects["HOST"] with { IsExhausted = false } } };
        var move = new MoveUnitCommand("HOST", "BATTLEFIELD:F", "BATTLEFIELD:OTHER", ["ROAM"]);
        await Reject(s, "P1", move);
        var game = new Driver(s); await game.Act("P1", Mode(Roam)); await game.Top();
        await game.Act("P1", move);
        Assert.Equal("OTHER", game.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        await game.ColdCheckpoint(); await game.Replay();
    }

    [Fact]
    public async Task ARealControlChangeKeepsTheOriginalAbilityInstancesModeHistory()
    {
        var s = AttackDamageDistributionTests.Spell(Position(), "TAKE", "SFD·202/221", "P2");
        s = s with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var game = new Driver(s); var generation = game.State.CardObjects["HOST"].ObjectGeneration;
        await game.Act("P2", new HideCardCommand("TAKE", "SFD·202/221", "BATTLEFIELD:OTHER", ["STANDBY_A"]));
        await game.Act("P2", new EndTurnCommand());
        Assert.Equal("P1", game.State.TurnPlayerId);
        await game.Act("P1", Mode(Roam)); await game.Top();
        var used = Assert.Single(Uses(game.State)); var turn = game.State.TurnNumber;
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:OTHER", []));
        if (game.State.FocusPlayerId != "P2") await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        await game.Act("P2", new PlayCardCommand("TAKE", "SFD·202/221", ["HOST"])); await game.Top();
        Assert.Equal("P2", game.State.CardObjects["HOST"].ControllerId);
        Assert.Equal(generation, game.State.CardObjects["HOST"].ObjectGeneration);
        Assert.Equal(turn, game.State.TurnNumber);
        Assert.Equal(used, Assert.Single(Uses(game.State)));
        await game.ColdCheckpoint(); await game.Replay();
    }

    [Fact]
    public async Task PayingTheBoonCanDestroyTheDamagedSourceWithoutCancellingIndependentDamage()
    {
        var s = Position();
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["HOST"] = s.CardObjects["HOST"] with { Damage = 6 } } };
        var game = new Driver(s); await game.Act("P1", Mode(Damage));
        Assert.Equal("GRAVEYARD", game.State.ObjectLocations["HOST"].Zone);
        Assert.Single(game.State.StackItems); await game.Top();
        Assert.Equal(2, game.State.CardObjects["TARGET"].Damage);
        await game.Replay();
    }

    [Fact]
    public async Task AnActualSongGivesAnIndependentCopyAndARealReentryGivesANewPrintedInstance()
    {
        var s = Spell(Spell(Spell(Spell(Position(), "BOON1"), "BOON2"), "BOON3"), "RETURN", "OGN·104/298");
        var game = new Driver(s);
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"]));
        while (game.State.StackItems.Count > 0) await game.Top();
        var gear = game.State.CardObjects["GEAR"];
        var copy = $"ATTACHED:GEAR:{gear.ObjectGeneration}:{gear.AttachmentRevision}";
        await game.Act("P1", Mode(Ready)); await game.Top(); await Grant(game, "BOON1");
        await game.Act("P1", Mode(Ready, copy)); await game.Top();
        Assert.Equal(2, Uses(game.State).Length);
        await Grant(game, "BOON2"); await Reject(game.State, "P1", Mode(Ready)); await Reject(game.State, "P1", Mode(Ready, copy));
        var generation = game.State.CardObjects["HOST"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        await game.Act("P1", new PlayCardCommand("HOST", Udyr, [])); await Grant(game, "BOON3");
        Assert.True(game.State.CardObjects["HOST"].ObjectGeneration > generation);
        await Reject(game.State, "P1", Mode(Ready, copy));
        await game.Act("P1", Mode(Ready)); await game.Top();
        Assert.Equal(3, Uses(game.State).Length); await game.ColdCheckpoint(); await game.Replay();
    }

    [Theory]
    [InlineData(Damage)] [InlineData(Stun)]
    public async Task TargetsStayBoundToThePreCostIncarnationWhenTheirOwnBoonCostDestroysThem(string mode)
    {
        var initial = LocalDestructionRecallTests.At(Position(), "HOST", "P1", "BATTLEFIELD", "F");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { Damage = 6 } } };
        var game = new Driver(initial); var generation = game.State.CardObjects["HOST"].ObjectGeneration;
        await game.Act("P1", new ActivateAbilityCommand("HOST", mode, ["HOST"]));
        var item = Assert.Single(game.State.StackItems);
        Assert.Equal("GRAVEYARD", game.State.ObjectLocations["HOST"].Zone);
        Assert.True(game.State.CardObjects["HOST"].ObjectGeneration > generation);
        Assert.Equal(generation, item.TargetGenerations!["HOST"]);
        await game.ColdCheckpoint(); await game.Top();
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Theory]
    [InlineData(Damage, true)] [InlineData(Damage, false)] [InlineData(Ready, false)]
    public async Task ARealCopiedTokenMayCeaseWhilePayingWithoutInvalidatingTheConfirmedSkill(string mode, bool selfTarget)
    {
        var initial = Spell(Spell(Spell(Position(boons: 0), "COPY", "UNL-200/219"), "BOON"), "WOUND", "OGN·085/298");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("COPY", "UNL-200/219", ["HOST"]));
        await game.Top(); await game.Top();
        var image = Assert.Single(game.State.CardObjects.Values,
            card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo).ObjectId;
        await game.Act("P1", new PlayCardCommand("BOON", "OGN·053/298", [image])); await game.Top();
        await game.Act("P1", new MoveUnitCommand(image, "BASE", "BATTLEFIELD:F", []));
        while (game.State.SpellDuelState.IsActive)
            await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        await game.Act("P1", new PlayCardCommand("WOUND", "OGN·085/298", [image])); await game.Top();
        Assert.Equal(6, game.State.CardObjects[image].Damage);
        var generation = game.State.CardObjects[image].ObjectGeneration;
        string[] targets = mode == Damage ? [selfTarget ? image : "TARGET"] : [];
        await game.Act("P1", new ActivateAbilityCommand(image, mode, targets));
        Assert.False(game.State.CardObjects.ContainsKey(image));
        var item = Assert.Single(game.State.StackItems);
        Assert.NotNull(item.SimpleActivation!.CeasedSource);
        Assert.Equal(new ObjectBinding(image, generation), item.SimpleActivation.ConsumedSourceBoon);
        if (selfTarget) {
            Assert.Equal(generation, item.TargetGenerations![image]);
            Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, item.SimpleActivation.TargetTokenFactories![image]);
        }
        await game.ColdCheckpoint(); await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey(image));
        Assert.Equal(mode == Damage && !selfTarget ? 2 : 0, game.State.CardObjects["TARGET"].Damage);
        await game.Replay();
    }

    [Theory]
    [InlineData("removed")] [InlineData("generation")] [InlineData("mode")]
    [InlineData("extra")] [InlineData("receipt")]
    public async Task RecoveryAuthenticatesThePaidSourceAndModeUseAgainstTheOriginalHistory(string branch)
    {
        var game = new Driver(Position()); await game.Act("P1", Mode(Ready));
        var state = game.State; var used = Assert.Single(Uses(state));
        var replacement = branch switch {
            "generation" => used.Replace(":HOST:0:", ":HOST:999:", StringComparison.Ordinal),
            "mode" => used.Replace(Ready, Roam, StringComparison.Ordinal),
            _ => used };
        var forged = branch == "receipt" ? state with { StackItems = state.StackItems.Select(item => item with {
            SimpleActivation = item.SimpleActivation! with { ConsumedSourceBoon = null } }).ToArray() }
            : state with { UntilEndOfTurnEffects = state.UntilEndOfTurnEffects.Where(e => e != used)
                .Concat(branch == "removed" ? [] : branch == "extra" ? new[] { used, used } : new[] { replacement }).ToArray() };
        Assert.NotEmpty(Errors(game, forged));
        Assert.DoesNotContain(used, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state)));
        Assert.DoesNotContain("consumedSourceBoon", JsonSerializer.Serialize(ResolutionResult.BuildSpectatorSnapshot(state)));
        await game.Top(); await game.Replay();
    }
}
