using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class StandbyConsumerTests
{
    [Fact]
    public async Task ActualNoxianSaboteurMovementBlocksAnExistingLocalShadow()
    {
        var state = await StandbyReactionSfdTeemoTests.BuildClosedPriorityState("OGN·057/298", enemyCard: "OGN·018/298");
        Assert.Equal("F", state.ObjectLocations[StandbyReactionSfdTeemoTests.EnemyTargetObjectId].BattlefieldObjectId);
        Assert.True(state.CardObjects["SHADOW"].IsFaceDown);
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        Assert.DoesNotContain(prompt.Candidates ?? [], c => c.Action is CommandTypes.PlayCard or CommandTypes.RevealCard && c.Sources?.Any(s => s.Id == "SHADOW") == true);
        var before = MatchStateHasher.Hash(state);
        foreach (GameCommand command in new GameCommand[]
        {
            new PlayCardCommand("SHADOW", "OGN·057/298", ["HOST"]),
            new RevealCardCommand("SHADOW", "OGN·057/298", ["HOST"], "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "STACK")
        })
        {
            var rejected = await new CoreRuleEngine().ResolveAsync(state, new("blocked-shadow", "P1", command.CmdType), command, default);
            Assert.False(rejected.Accepted);
            Assert.Empty(rejected.Events);
            Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        }
        Restore(state);
    }

    [Theory]
    [InlineData("champion")]
    [InlineData("typed-power")]
    public async Task PrintedStandbyUsesRealChampionZoneOrTypedPowerWithoutMana(string branch)
    {
        var card = branch == "champion" ? "OGN·197/298" : "OGN·057/298";
        var state = StandbySourceRulesTests.Position(card);
        if (branch == "champion")
        {
            state = state with
            {
                PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
                {
                    ["P1"] = state.PlayerZones["P1"] with { Hand = state.PlayerZones["P1"].Hand.Where(id => id != "SHADOW").ToArray(), ChampionZone = ["SHADOW"] }
                },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["SHADOW"] = new("P1", "CHAMPION_ZONE") }
            };
        }
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools)
        {
            ["P1"] = branch == "typed-power" ? new(0, 0, new Dictionary<string, int> { ["purple"] = 1 }) : new(0, 1)
        }};
        var hidden = await OfficialInsightAndSpellLockTests.Act(state, "P1", new HideCardCommand("SHADOW", card, "BATTLEFIELD:F", ["STANDBY_A"]));
        Assert.Equal(0, hidden.State.RunePools["P1"].Mana);
        Assert.Equal(0, hidden.State.RunePools["P1"].TotalPower);
        Assert.DoesNotContain("SHADOW", hidden.State.PlayerZones["P1"].ChampionZone);
        Assert.DoesNotContain("SHADOW", hidden.State.PlayerZones["P1"].Hand);
        Assert.Equal("F", hidden.State.ObjectLocations["SHADOW"].BattlefieldObjectId);
        Assert.True(hidden.State.CardObjects["SHADOW"].IsFaceDown);
        Restore(hidden.State);
        ArmamentMightTests.PublicRecovery(hidden.State);
    }

    [Fact]
    public async Task FaceUpNightEdgeDoesNotAttachOrCreateItsConditionalAbility()
    {
        var state = StandbySourceRulesTests.Position("SFD·139/221");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools)
        {
            ["P1"] = new(20, 20)
        }};
        var played = await OfficialInsightAndSpellLockTests.Act(state, "P1", new PlayCardCommand("SHADOW", "SFD·139/221", []));
        Assert.Contains("SHADOW", played.State.PlayerZones["P1"].Base);
        Assert.Null(played.State.CardObjects["SHADOW"].AttachedToObjectId);
        Assert.DoesNotContain(played.State.StackItems, s => s.SourceObjectId == "SHADOW");
        Assert.DoesNotContain(played.Events, e => e.Kind is "TRIGGER_QUEUED" or "EQUIPMENT_ATTACHED");
        Restore(played.State);
    }

    [Theory]
    [InlineData("attach")]
    [InlineData("target-leaves")]
    [InlineData("source-leaves")]
    [InlineData("target-control")]
    [InlineData("source-control")]
    public async Task FaceDownNightEdgeConfirmsItsLocalTargetBeforeIndependentAttachmentResolution(string branch)
    {
        var state = await StandbySourceRulesTests.ReadyShadow("SFD·139/221");
        var resources = state.RunePools["P1"];
        var played = await OfficialInsightAndSpellLockTests.Act(state, "P1", new PlayCardCommand("SHADOW", "SFD·139/221", []));
        Assert.Equal(resources, played.State.RunePools["P1"]);
        Assert.Equal("F", played.State.ObjectLocations["SHADOW"].BattlefieldObjectId);
        Assert.Null(played.State.CardObjects["SHADOW"].AttachedToObjectId);
        var pending = Assert.IsType<PendingCardChoiceState>(played.State.PendingCardChoice);
        Assert.Equal(1, pending.RequiredCount);
        Assert.Equal(new[] { "HOST", "SHADOW-INSIDE" }, pending.LegalObjectIds.Order());
        Assert.DoesNotContain("SHADOW-OUTSIDE", pending.LegalObjectIds);
        Restore(played.State);
        var selected = await OfficialInsightAndSpellLockTests.Act(played.State, "P1",
            new ChooseCardsCommand(pending.ChoiceId, pending.ChoiceWindow, ["HOST"]));
        Assert.Null(selected.State.CardObjects["SHADOW"].AttachedToObjectId);
        var ability = Assert.Single(selected.State.StackItems, s => s.SourceObjectId == "SHADOW");
        Assert.True(ability.SourceConfirmed);
        Assert.Equal(selected.State.CardObjects["HOST"].ObjectGeneration, ability.TargetGenerations!["HOST"]);
        Assert.Equal(2, selected.State.CardObjects["SHADOW"].ObjectGeneration);
        state = selected.State;
        if (branch == "target-leaves") state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BASE");
        if (branch == "source-leaves")
        {
            state = LocalDestructionRecallTests.At(state, "SHADOW", "P1", "BASE");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            {
                ["SHADOW"] = state.CardObjects["SHADOW"] with { ObjectGeneration = state.CardObjects["SHADOW"].ObjectGeneration + 1, LastStandbyPlay = null }
            }};
        }
        if (branch == "target-control")
        {
            state = LocalDestructionRecallTests.At(state, "HOST", "P2", "BATTLEFIELD", "F");
        }
        if (branch == "source-control")
        {
            // CN 191.4.b: control of the source changes independently of the confirmed ability.
            state = LocalDestructionRecallTests.At(state, "SHADOW", "P2", "BATTLEFIELD", "F");
            Assert.Equal("P1", Assert.Single(state.StackItems, s => s.SourceObjectId == "SHADOW").ControllerId);
        }
        // These resolution-time mutations model a changed field; legality is checked again against bound generations and location.
        var done = await Top(state);
        if (branch is "attach" or "source-control")
        {
            Assert.Equal("HOST", done.State.CardObjects["SHADOW"].AttachedToObjectId);
            Assert.Single(done.Events, e => e.Kind == "EQUIPMENT_ATTACHED");
            Assert.Equal("F", done.State.ObjectLocations["SHADOW"].BattlefieldObjectId);
            var attached = Assert.Single(done.Events, e => e.Kind == "EQUIPMENT_ATTACHED");
            Assert.Equal("P1", attached.Payload["attachingPlayerId"]);
            Assert.Equal(branch == "source-control" ? "P2" : "P1", attached.Payload["controllerId"]);
            Assert.Equal(branch == "source-control" ? "P2" : "P1", done.State.CardObjects["SHADOW"].ControllerId);
            Restore(done.State);
            ArmamentMightTests.PublicRecovery(done.State);
        }
        else
        {
            Assert.Null(done.State.CardObjects["SHADOW"].AttachedToObjectId);
            Assert.DoesNotContain(done.Events, e => e.Kind == "EQUIPMENT_ATTACHED");
        }
    }
}
