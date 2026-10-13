using Riftbound.Contracts;
using Riftbound.Engine;
using System.Text.Json;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SecretArtMercyCurrentTests
{
    internal const string Card = "OGN·053/298";

    [Fact]
    public async Task ActualSpellAmplifiesAllCurrentFriendlyBoonsAcrossLocations()
    {
        var initial = Position();
        var driver = Driver(initial, "all-friendly-boons");
        await driver.Act("P1", new PlayCardCommand("MERCY", Card, ["HOST"]));
        await driver.Top();
        Assert.Equal(7, driver.State.CardObjects["HOST"].Power);
        Assert.Equal(8, Power(driver.State, "HOST"));
        Assert.Equal(7, Power(driver.State, "BOONED"));
        Assert.Equal(5, Power(driver.State, "OTHER_UNIT"));
        Assert.Equal(6, Power(driver.State, "ENEMY"));
        Assert.Contains("MERCY", driver.State.PlayerZones["P1"].Graveyard);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task ARealReturnResponseSkipsTheTargetBoonButKeepsTheIndependentGlobalEffect()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", "P1");
        var driver = Driver(initial, "invalid-target-global-survives");
        await driver.Act("P1", new PlayCardCommand("MERCY", Card, ["HOST"]));
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        await driver.Top();
        var result = await driver.Top();
        Assert.Equal("HAND", driver.State.ObjectLocations["HOST"].Zone);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BOON_GRANTED");
        Assert.Equal(7, Power(driver.State, "BOONED"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task LaterBoonGainsUseTheExistingAmplifierAndItsBonusExpiresAtRealTurnEnd()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "DISPLAY", "OGN·270/298", "P1");
        var driver = Driver(initial, "later-boon-expiry");
        await Cast(driver);
        await driver.Act("P1", new PlayCardCommand("DISPLAY", "OGN·270/298", ["OTHER_UNIT"]));
        await driver.Top();
        Assert.Equal(6, driver.State.CardObjects["OTHER_UNIT"].Power);
        Assert.Equal(7, Power(driver.State, "OTHER_UNIT"));
        await driver.Act("P1", new EndTurnCommand());
        Assert.True(driver.State.TurnNumber > initial.TurnNumber);
        Assert.Equal(7, Power(driver.State, "HOST"));
        Assert.Equal(6, Power(driver.State, "OTHER_UNIT"));
        Assert.Equal(6, Power(driver.State, "BOONED"));
        Assert.Contains(CardObjectTags.Boon, driver.State.CardObjects["HOST"].Tags);
        Assert.Empty(Markers(driver.State));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualEchoAndSeparateSpellsCreateIndependentAmplifiersWithoutDuplicatingBoons(bool echo)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "MERCY2", Card, "P1");
        if (echo) initial = initial with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var driver = Driver(initial, echo ? "echo-two-instances" : "two-spells");
        await driver.Act("P1", echo
            ? new PlayCardCommand("MERCY", Card, ["BOONED"], OptionalCosts: ["ECHO"], RepeatChoices: [new("", ["BOONED"])])
            : new PlayCardCommand("MERCY", Card, ["BOONED"]));
        var resolved = await driver.Top();
        Assert.DoesNotContain(resolved.Events, e => e.Kind == "BOON_GRANTED");
        if (!echo)
        {
            await driver.Act("P1", new PlayCardCommand("MERCY2", Card, ["BOONED"]));
            await driver.Top();
        }
        Assert.Equal(2, Markers(driver.State).Length);
        Assert.Equal(2, Markers(driver.State).Distinct().Count());
        Assert.Equal(6, driver.State.CardObjects["BOONED"].Power);
        Assert.Equal(8, Power(driver.State, "BOONED"));
        Assert.Equal(6, Power(driver.State, "HOST"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task ActualControlChangeAppliesTheNewControllersBonusToTheMovedUnit()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "STEAL", "SFD·202/221", "P1");
        initial = LocalDestructionRecallTests.At(initial, "BOONED", "P2", "BATTLEFIELD", "OTHER");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["OTHER"] = initial.CardObjects["OTHER"] with { ControllerId = "P2", CardNo = "OGN·278/298" },
            ["BOONED"] = initial.CardObjects["BOONED"] with { OwnerId = "P2", ControllerId = "P2" } } };
        var driver = Driver(initial, "current-controller");
        await Cast(driver);
        Assert.Equal(6, Power(driver.State, "BOONED"));
        await driver.Act("P1", new PlayCardCommand("STEAL", "SFD·202/221", ["BOONED"]));
        await driver.Top();
        Assert.Equal("P1", driver.State.CardObjects["BOONED"].ControllerId);
        Assert.Equal(7, Power(driver.State, "BOONED"));
        Assert.Equal(8, Power(driver.State, "HOST"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task AmplifiedCurrentMightIsUsedByActualLethalDamageCleanup()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "GRENADE", DancingGrenadeTests.Card, "P1");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BOONED"] = initial.CardObjects["BOONED"] with { Damage = 4 } } };
        var driver = Driver(initial, "lethal-threshold");
        await Cast(driver);
        await driver.Act("P1", new PlayCardCommand("GRENADE", DancingGrenadeTests.Card, ["BOONED"]));
        await driver.Top();
        var pending = Assert.IsType<PendingEffectPlayState>(driver.State.PendingEffectPlay);
        await driver.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        Assert.Equal(6, driver.State.CardObjects["BOONED"].Damage);
        Assert.Equal(7, Power(driver.State, "BOONED"));
        Assert.Contains("BOONED", driver.State.PlayerZones["P1"].Battlefields);
        await driver.Act("P1", new EndTurnCommand());
        Assert.Contains("BOONED", driver.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, driver.State.CardObjects["BOONED"].Damage);
        Assert.Equal(6, Power(driver.State, "BOONED"));
        Assert.Empty(Markers(driver.State));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task ActualCombatDamagePoolsAndLethalThresholdUseTheAmplifiedCurrentMight()
    {
        var initial = Position();
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BATTLEFIELD", "F");
        initial = SacredProtectorTests.Unit(initial, "SECOND_ENEMY", "P2");
        initial = LocalDestructionRecallTests.At(initial, "OTHER_UNIT", "P1", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { CardNo = "OGN·001/298", Power = 5, IsExhausted = false },
            ["OTHER_UNIT"] = initial.CardObjects["OTHER_UNIT"] with { IsExhausted = false },
            ["F"] = initial.CardObjects["F"] with { ControllerId = "P2" } } };
        initial = LocalDestructionRecallTests.At(initial, "BOONED", "P1", "BASE");
        var driver = Driver(initial, "combat-current-might");
        await Cast(driver);
        Assert.Equal(7, Power(driver.State, "HOST"));
        await driver.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", [], SourceObjectIds: ["HOST", "OTHER_UNIT"]));
        var events = new List<GameEvent>(); var checkedPool = false; var checkedThreshold = false;
        for (var step = 0; step < 30 && !events.Any(e => e.Kind == "BATTLE_CLOSED"); step++)
        {
            var prompts = ResolutionResult.BuildPrompts(driver.State);
            var assignment = prompts.Values.FirstOrDefault(p => p.Candidates?.Any(c => c.Action == CommandTypes.AssignCombatDamage && c.Enabled) == true);
            ResolutionResult result;
            if (assignment is not null)
            {
                var metadata = JsonSerializer.SerializeToElement(assignment.View!.Metadata);
                if (metadata.GetProperty("assignableDamagePool").TryGetProperty("HOST", out var pool))
                { Assert.Equal(7, pool.GetInt32()); checkedPool = true; }
                if (metadata.GetProperty("lethalDamageThreshold").TryGetProperty("HOST", out var threshold))
                { Assert.Equal(7 - driver.State.CardObjects["HOST"].Damage, threshold.GetInt32()); checkedThreshold = true; }
                result = await driver.Act(assignment.PlayerId, new AssignCombatDamageCommand(driver.State.BattleState.BattleId!,
                    driver.State.BattleState.BattlefieldObjectId!, CombatTestDriver.Assignments(assignment)));
            }
            else if (driver.State.PriorityPlayerId is { } priority) result = await driver.Act(priority, new PassPriorityCommand());
            else if (driver.State.FocusPlayerId is { } focus) result = await driver.Act(focus, new PassFocusCommand());
            else
            {
                var actor = prompts.Values.First(p => p.Candidates?.Any(c => c.Action == CommandTypes.DeclareBattle && c.Enabled) == true).PlayerId;
                var battle = driver.State.BattleState;
                result = await driver.Act(actor, new DeclareBattleCommand(battle.BattlefieldObjectId!, battle.AttackerObjectIds,
                    battle.DefenderObjectIds, ["COMBAT_ASSIGNMENT"]));
            }
            events.AddRange(result.Events);
        }
        Assert.True(checkedPool, "The actual attacker assignment must expose HOST's amplified pool.");
        Assert.True(checkedThreshold, "The actual defender assignment must expose HOST's amplified lethal threshold.");
        Assert.Contains(events, e => e.Kind == "BATTLE_CLOSED");
        Assert.Equal(7, events.Where(e => e.Kind == "DAMAGE_APPLIED" && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "HOST")
            && e.Payload.ContainsKey("combatRole")).Sum(e => Convert.ToInt32(e.Payload["damage"])));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("turn")]
    [InlineData("controller")]
    [InlineData("source")]
    [InlineData("source-generation")]
    [InlineData("card")]
    [InlineData("stack")]
    [InlineData("future-stack")]
    [InlineData("execution")]
    [InlineData("amount")]
    [InlineData("duplicate")]
    public async Task RecoveryRejectsIndependentGlobalAmplifierProvenanceTampering(string mutation)
    {
        var driver = new StandbyDefendObserverTests.Driver(Position());
        await Cast(driver);
        var marker = Assert.Single(Markers(driver.State)); var f = marker.Split('|');
        if (mutation == "turn") f[1] = "999999";
        if (mutation == "controller") f[2] = Encode("FORGED");
        if (mutation == "source") f[3] = Encode("OTHER");
        if (mutation == "source-generation") f[4] = "999999";
        if (mutation == "card") f[5] = Encode("OGN·173/298");
        if (mutation == "stack") f[6] = Encode("FORGED");
        if (mutation == "future-stack") f[6] = Encode("STACK-999999-MERCY");
        if (mutation == "execution") f[7] = "-1";
        if (mutation == "amount") f[8] = "999999";
        var bad = driver.State with { UntilEndOfTurnEffects = mutation == "duplicate" ? [..driver.State.UntilEndOfTurnEffects, marker]
            : driver.State.UntilEndOfTurnEffects.Select(e => e == marker ? string.Join('|', f) : e).ToArray() };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(bad), e => e.Contains("temporary friendly Boon amplifier"));
    }

    internal static string Encode(string s) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s));

    [Fact]
    public async Task RealStandbyKeepsItsChosenTargetLocalWhileAmplifyingBoonsAtOtherLocations()
    {
        var initial = LocalDestructionRecallTests.At(Position(), "HOST", "P1", "BATTLEFIELD", "F");
        initial = SacredProtectorTests.Unit(initial, "BASE_BOON", "P1");
        initial = LocalDestructionRecallTests.At(initial, "BASE_BOON", "P1", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BASE_BOON"] = initial.CardObjects["BASE_BOON"] with { Power = 6,
                Tags = initial.CardObjects["BASE_BOON"].Tags.Append(CardObjectTags.Boon).Order(StringComparer.Ordinal).ToArray() } } };
        const string windRune = "MERCY-PURPLE-RUNE";
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            [windRune] = new(windRune, cardNo: "UNL-R05", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.RuneCard, "COLOR:purple"]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { [windRune] = new("P1", "BASE") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with {
                Base = initial.PlayerZones["P1"].Base.Append(windRune).ToArray() } } };
        var driver = Driver(initial, "standby-global");
        await driver.Act("P1", new HideCardCommand("MERCY", Card, "BATTLEFIELD:F", ["STANDBY_A"]));
        var next = await driver.Act("P1", new EndTurnCommand());
        await TurnSequenceTestDriver.Complete(next, (player, command) => new(driver.Act(player, command)));
        Assert.Equal(initial.TurnNumber + 1, driver.State.TurnNumber);
        next = await driver.Act("P2", new EndTurnCommand());
        await TurnSequenceTestDriver.Complete(next, (player, command) => new(driver.Act(player, command)));
        Assert.Equal(initial.TurnNumber + 2, driver.State.TurnNumber);
        foreach (var rune in driver.State.PlayerZones["P1"].Base.Where(id => driver.State.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)
            && !driver.State.CardObjects[id].IsExhausted && id != windRune).Take(2).ToArray())
            await driver.Act("P1", new TapRuneCommand(rune));
        await driver.Act("P1", new RecycleRuneCommand(windRune));
        await driver.Act("P1", new PlayCardCommand("WIND", "OGN·173/298", ["OTHER_UNIT"], Destination: "BATTLEFIELD:F"));
        var prompt = ResolutionResult.BuildPrompts(driver.State)["P1"];
        var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P1", new("quote", prompt.PromptId!, driver.State.Tick,
            new("MERCY", Card, ["BASE_BOON"])));
        Assert.False(quote.IsValid);
        await driver.Act("P1", new PlayCardCommand("MERCY", Card, ["HOST"]));
        await driver.Top();
        Assert.Equal(8, Power(driver.State, "HOST"));
        Assert.Equal(7, Power(driver.State, "BASE_BOON"));
        Assert.Equal(7, Power(driver.State, "BOONED"));
        await driver.Top();
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("powerDelta")]
    [InlineData("effectivePower")]
    [InlineData("sourceObjectId")]
    [InlineData("sourceCardNo")]
    [InlineData("duration")]
    [InlineData("condition")]
    [InlineData("lifecycle")]
    [InlineData("sourceOrder")]
    [InlineData("sourceDependencyObjectIds")]
    [InlineData("effectId")]
    public async Task PublicRecoveryRejectsForgedBoonAmplifierViewFields(string key)
    {
        var driver = new StandbyDefendObserverTests.Driver(Position());
        await Cast(driver);
        var s = driver.State; var snapshots = ResolutionResult.BuildSnapshots(s); var prompts = ResolutionResult.BuildPrompts(s);
        var views = s.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, s.Tick, 0, snapshots[id], s.Tick, 0, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(s.RoomId, s.Tick, 0, [], s);
        foreach (var snapshot in snapshots.Values.Append(spectator.SpectatorSnapshot))
        {
            var effects = Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(snapshot.Timing["continuousEffects"]);
            var effect = effects.First(e => Equals(e.GetValueOrDefault("effectKind"), "TEMPORARY_FRIENDLY_BOON_MIGHT"));
            var old = effect[key]; effect[key] = key == "effectId" ? "BOON_MIGHT:" + effect["targetObjectId"]
                : key is "powerDelta" or "effectivePower" or "sourceOrder" ? 999999 : "FORGED";
            Assert.NotEmpty(MatchRecoveryValidator.Validate(s.RoomId, 0, [], [], views, s, s.Tick, spectator));
            effect[key] = old;
        }
    }

    [Fact]
    public async Task ReturningThePhysicalSpellToPrivateHandDoesNotCancelItsCapturedAmplifier()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "ANNIE", "OGS·010/024", "P1");
        var driver = Driver(initial, "source-private-hand");
        await Cast(driver);
        await driver.Act("P1", new PlayCardCommand("ANNIE", "OGS·010/024", [], Destination: "BASE"));
        for (var step = 0; step < 8 && driver.State.ObjectLocations["MERCY"].Zone != "HAND"; step++)
        {
            if (driver.State.PendingCardChoice is { } choice)
                await driver.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["MERCY"]));
            else if (driver.State.PriorityPlayerId is { } priority)
                await driver.Act(priority, new PassPriorityCommand());
            else Assert.Fail("The played Annie must expose its actual graveyard choice or stack response window.");
        }
        Assert.Equal("HAND", driver.State.ObjectLocations["MERCY"].Zone);
        Assert.Equal(8, Power(driver.State, "HOST"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }
    internal static string[] Markers(MatchState s) => s.UntilEndOfTurnEffects.Where(e => e.StartsWith("FRIENDLY_BOON_MIGHT|", StringComparison.Ordinal)).ToArray();
    internal static async Task<ResolutionResult> Cast(StandbyDefendObserverTests.Driver driver)
    {
        await driver.Act("P1", new PlayCardCommand("MERCY", Card, ["HOST"]));
        return await driver.Top();
    }

    internal static int Power(MatchState s, string id)
    {
        var card = s.CardObjects[id];
        var player = card.ControllerId ?? s.PlayerZones.Single(p => p.Value.Base.Concat(p.Value.Battlefields).Contains(id)).Key;
        return CurrentPowerTargetTests.Snapshot(s).GetProperty("players").GetProperty(player)
            .GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();
    }

    internal static MatchState Position()
    {
        var s = AttackDamageDistributionTests.Spell(RideTheWindCurrentMovementTests.Position(), "MERCY", Card, "P1");
        foreach (var (id, player) in new[] { ("BOONED", "P1"), ("OTHER_UNIT", "P1"), ("ENEMY", "P2") })
        {
            s = SacredProtectorTests.Unit(s, id, player);
            if (id != "BOONED") s = LocalDestructionRecallTests.At(s, id, player, "BASE");
        }
        return s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["BOONED"] = s.CardObjects["BOONED"] with { Power = 6, Tags = s.CardObjects["BOONED"].Tags.Append(CardObjectTags.Boon).Order(StringComparer.Ordinal).ToArray() },
            ["ENEMY"] = s.CardObjects["ENEMY"] with { Power = 6, Tags = s.CardObjects["ENEMY"].Tags.Append(CardObjectTags.Boon).Order(StringComparer.Ordinal).ToArray() } } };
    }

    internal static StandbyDefendObserverTests.Driver Driver(MatchState s, string branch)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_MERCY_EVIDENCE");
        return new(s, string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, branch));
    }
}
