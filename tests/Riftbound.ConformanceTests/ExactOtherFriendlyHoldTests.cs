using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ExactOtherFriendlyHoldTests
{
    public static IEnumerable<object[]> Prints() => new[] { "VEN·042", "VEN·042a", "VEN·170", "VEN·138", "VEN·138a" }.Select(no => new object[] { no });

    [Theory]
    [MemberData(nameof(Prints))]
    public async Task CurrentFivePrintingsHaveExactHeldConditionInsteadOfUnconditionalEffects(string no)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == no);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var printed));
        var score = no.StartsWith("VEN·138");
        Assert.Equal(score ? 7 : 6, printed.SourceUnitPower);
        var trigger = Assert.Single(TriggerParser.Parse(card.CardEffect), s => s.Kind == (score ? "UNIT_BATTLEFIELD_HELD_SCORE" : TriggerKinds.UnitBattlefieldHeldDraw));
        Assert.Equal(TriggerTimings.BattlefieldHeld, trigger.Timing);
        // The current source's conditional text must be retained in the spec.
        Assert.Contains("有且仅有一个", trigger.Text);
        Assert.Equal(1, trigger.RequiredExactOtherFriendlyBattlefieldUnits);
    }

    public static IEnumerable<object[]> CountCases() => Prints().SelectMany(row => new[] { 0, 1, 2 }.Select(count => new object[] { row[0], count }));

    [Theory]
    [MemberData(nameof(CountCases))]
    public async Task ActualOwnTurnHoldQueuesOnlyAtExactlyOneOtherFriendlyUnit(string no, int companions)
    {
        var initial = Position(no, companions);
        var driver = Driver(initial, no.Replace('·','-') + "-count-" + companions);
        var beforeHand = initial.PlayerZones["P1"].Hand.Count;
        await driver.Act("P2", new EndTurnCommand());
        var triggers = driver.State.StackItems.Select(x => x.HeldContext).Concat(driver.State.TriggerQueue.Select(x => x.HeldContext)).Where(x => x?.Kind.StartsWith("EXACT_OTHER_") == true).ToArray();
        Assert.Equal(companions == 1 ? 1 : 0, triggers.Length);
        await Drain(driver);
        Assert.Equal(no.StartsWith("VEN·138") && companions == 1 ? 2 : 1, driver.State.PlayerScores["P1"]);
        Assert.Equal(beforeHand + (!no.StartsWith("VEN·138") && companions == 1 ? 2 : 1), driver.State.PlayerZones["P1"].Hand.Count);
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData("VEN·042", false)]
    [InlineData("VEN·042", true)]
    [InlineData("VEN·138", false)]
    [InlineData("VEN·138", true)]
    public async Task CapturedConditionRemainsSatisfiedAfterResponseReturnsTheSourceOrItsCompanion(string no, bool returnSource)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(no, 1), "RETURN", "OGN·104/298", "P1");
        initial = LocalDestructionRecallTests.At(initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
            { ["RESPONSE-RUNE"] = new("RESPONSE-RUNE", cardNo: "OGN·007/298", ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.RuneCard, "COLOR:red"]) } }, "RESPONSE-RUNE", "P1", "BASE");
        var driver = Driver(initial, no.Replace('·','-') + "-return-" + (returnSource ? "source" : "friend"));
        await driver.Act("P2", new EndTurnCommand());
        Assert.Single(driver.State.StackItems, x => x.HeldContext?.Kind.StartsWith("EXACT_OTHER_") == true);
        Assert.Equal("P1", driver.State.PriorityPlayerId);
        await driver.Act("P1", new TapRuneCommand("RESPONSE-RUNE"));
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [returnSource ? "HOST" : "FRIEND"]));
        await driver.Top();
        Assert.Equal("HAND", driver.State.ObjectLocations[returnSource ? "HOST" : "FRIEND"].Zone);
        await Drain(driver);
        Assert.Equal(no.StartsWith("VEN·138") ? 2 : 1, driver.State.PlayerScores["P1"]);
        // One ordinary draw plus the independent captured effect for the draw family.
        Assert.Equal(no.StartsWith("VEN·138") ? 2 : 3,
            driver.State.PlayerZones["P1"].Hand.Count);
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData("VEN·042", 1)]
    [InlineData("VEN·042", 2)]
    [InlineData("VEN·138", 1)]
    [InlineData("VEN·138", 2)]
    public async Task CopiedTextCreatesIndependentHoldTriggersAndKeepsCapturedOrigins(string no, int songs)
    {
        var initial = Position(no, 1) with { ActivePlayerId = "P1", TurnPlayerId = "P1" };
        for (var i = 0; i < songs; i++) initial = AttachedRuleTextTests.AddSong(initial, "SONG-" + i);
        var driver = Driver(initial, no.Replace('·','-') + "-songs-" + songs);
        for (var i = 0; i < songs; i++)
        {
            await driver.Act("P1", new AssembleEquipmentCommand("SONG-" + i, "HOST", ["ASSEMBLE_GREEN"]));
            await driver.Top();
        }
        await driver.Act("P1", new EndTurnCommand());
        await driver.Act("P2", new EndTurnCommand());
        var contexts = driver.State.TriggerQueue.Select(x => x.HeldContext).Concat(driver.State.StackItems.Select(x => x.HeldContext))
            .Where(x => x?.Kind.StartsWith("EXACT_OTHER_") == true).ToArray();
        Assert.Equal(songs + 1, contexts.Length);
        Assert.Equal(songs, contexts.Count(x => x!.Origin is not null));
        await Drain(driver);
        Assert.Equal(no.StartsWith("VEN·138") ? songs + 2 : 1, driver.State.PlayerScores["P1"]);
        // Initial hand plus two normal own starts; each text instance draws once on Hold.
        Assert.Equal(no.StartsWith("VEN·138") ? 1 : songs + 2, driver.State.PlayerZones["P1"].Hand.Count);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task AdditionalHeldScoreCanWinAfterTheNormalHoldPoint()
    {
        var initial = Position("VEN·138", 1);
        initial = initial with { PlayerScores = new Dictionary<string,int>(initial.PlayerScores) { ["P1"] = 6 } };
        var driver = Driver(initial, "score-victory");
        await driver.Act("P2", new EndTurnCommand());
        Assert.Equal(7, driver.State.PlayerScores["P1"]);
        Assert.Null(driver.State.WinnerPlayerId);
        await Drain(driver);
        Assert.Equal(8, driver.State.PlayerScores["P1"]);
        Assert.Equal("P1", driver.State.WinnerPlayerId);
        Assert.Equal(MatchStatuses.Finished, driver.State.Status);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task CurrentSourceAdditionsPreserveAllElevenActualDeckInputs()
    {
        var current = await OfficialCardCatalog.LoadDefaultAsync();
        var added = Prints().Select(x => (string)x[0]).ToHashSet(StringComparer.Ordinal);
        var previous = current with { Total = current.Total - added.Count, Cards = current.Cards.Where(c => !added.Contains(c.CardNo)).ToArray() };
        var before = PreconstructedDeckCatalog.Build(previous).ToDictionary(d => d.Id, StringComparer.Ordinal);
        var after = PreconstructedDeckCatalog.Build(current).ToDictionary(d => d.Id, StringComparer.Ordinal);
        Assert.Equal(11, before.Count);
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
        foreach (var id in before.Keys) Assert.Equal(JsonSerializer.Serialize(before[id].Decklist), JsonSerializer.Serialize(after[id].Decklist));
    }

    [Theory]
    [InlineData("count")]
    [InlineData("amount")]
    [InlineData("effect")]
    [InlineData("card")]
    [InlineData("origin")]
    [InlineData("instance")]
    public async Task ColdRecoveryRejectsForgedCapturedConditionalTextAndEffect(string mutation)
    {
        var initial = Position("VEN·042", 1) with { ActivePlayerId = "P1", TurnPlayerId = "P1" };
        initial = AttachedRuleTextTests.AddSong(initial, "SONG");
        var driver = Driver(initial, "recover-" + mutation);
        await driver.Act("P1", new AssembleEquipmentCommand("SONG", "HOST", ["ASSEMBLE_GREEN"]));
        await driver.Top();
        await driver.Act("P1", new EndTurnCommand());
        await driver.Act("P2", new EndTurnCommand());
        var queued = Assert.Single(driver.State.TriggerQueue, x => x.HeldContext?.Origin is not null);
        var context = queued.HeldContext!;
        var changed = mutation switch {
            "count" => context with { RequiredExactOtherFriendlyBattlefieldUnits = 0 },
            "amount" => context with { Amount = 2 },
            "card" => context with { CardNo = "VEN·138" },
            "origin" => context with { Origin = null },
            "instance" => context with { RuleTextInstanceId = "PRINTED:HOST:0" },
            _ => context
        };
        var forged = driver.State with { TriggerQueue = driver.State.TriggerQueue.Select(x => x.TriggerId == queued.TriggerId
            ? x with { HeldContext = changed, EffectKind = mutation == "effect" ? "HOLD_EXACT_OTHER_SCORE" : x.EffectKind } : x).ToArray() };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
        await Drain(driver);
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData("VEN·042")]
    [InlineData("VEN·138")]
    public async Task OriginalAndCopiedCapturedHoldSurviveSourceReturnAndArmamentDetachment(string no)
    {
        var initial = Position(no, 1) with { ActivePlayerId = "P1", TurnPlayerId = "P1" };
        initial = AttachedRuleTextTests.AddSong(initial, "SONG");
        initial = AttackDamageDistributionTests.Spell(initial, "RETURN", "OGN·104/298", "P1");
        initial = LocalDestructionRecallTests.At(initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
            { ["RESPONSE-RUNE"] = new("RESPONSE-RUNE", cardNo: "OGN·007/298", ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.RuneCard, "COLOR:red"]) } }, "RESPONSE-RUNE", "P1", "BASE");
        var driver = Driver(initial, no.Replace('·','-') + "-copied-source-return");
        await driver.Act("P1", new AssembleEquipmentCommand("SONG", "HOST", ["ASSEMBLE_GREEN"]));
        await driver.Top();
        await driver.Act("P1", new EndTurnCommand());
        await driver.Act("P2", new EndTurnCommand());
        var order = ResolutionResult.BuildPrompts(driver.State)["P1"];
        Assert.Contains(order.Candidates!, c => c.Action == CommandTypes.OrderTriggers && c.Enabled);
        await driver.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: driver.State.TriggerQueue.Select(x => x.TriggerId).ToArray()));
        Assert.Equal(2, driver.State.StackItems.Count);
        await driver.Act("P1", new TapRuneCommand("RESPONSE-RUNE"));
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        await driver.Top();
        Assert.Equal("HAND", driver.State.ObjectLocations["HOST"].Zone);
        Assert.Null(driver.State.CardObjects["SONG"].AttachedToObjectId);
        Assert.Single(driver.State.StackItems, x => x.HeldContext?.Origin is not null);
        await Drain(driver);
        Assert.Equal(no.StartsWith("VEN·138") ? 3 : 1, driver.State.PlayerScores["P1"]);
        Assert.Equal(no.StartsWith("VEN·138") ? 2 : 4, driver.State.PlayerZones["P1"].Hand.Count);
        await Replay(initial, driver);
    }

    [Theory]
    [MemberData(nameof(Prints))]
    public async Task OpponentTurnDoesNotTriggerTheFriendlyUnitsHold(string no)
    {
        var initial = Position(no, 1) with { ActivePlayerId = "P1", TurnPlayerId = "P1" };
        var driver = Driver(initial, no.Replace('·','-') + "-opponent-turn");
        await driver.Act("P1", new EndTurnCommand());
        Assert.Equal("P2", driver.State.TurnPlayerId);
        Assert.DoesNotContain(driver.State.StackItems, x => x.HeldContext?.Kind.StartsWith("EXACT_OTHER_") == true);
        Assert.DoesNotContain(driver.State.TriggerQueue, x => x.HeldContext?.Kind.StartsWith("EXACT_OTHER_") == true);
        Assert.Equal(0, driver.State.PlayerScores["P1"]);
        Assert.Empty(driver.State.PlayerZones["P1"].Hand);
        await Replay(initial, driver);
    }

    internal static MatchState Position(string no, int companions)
    {
        var s = SacredProtectorTests.DefenderPosition(companions);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var printed));
        return s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects)
            { ["HOST"] = s.CardObjects["HOST"] with { CardNo = no, Power = printed.SourceUnitPower, ManaCost = printed.ManaCost,
                Tags = printed.SourceUnitTags.Split('|', StringSplitOptions.RemoveEmptyEntries).Append(CardObjectTags.UnitCard).Distinct().Order(StringComparer.Ordinal).ToArray() } } };
    }

    internal static async Task Drain(StandbyDefendObserverTests.Driver driver)
    {
        for (var step = 0; step < 50 && (driver.State.TurnStartStep is not null || driver.State.StackItems.Count > 0 || driver.State.TriggerQueue.Count > 0); step++)
        {
            if (driver.State.Status == MatchStatuses.Finished) break;
            var order = ResolutionResult.BuildPrompts(driver.State).Values.FirstOrDefault(p => p.Candidates?.Any(c => c.Action == CommandTypes.OrderTriggers && c.Enabled) == true);
            if (order is not null)
                await driver.Act(order.PlayerId, new OrderTriggersCommand(OrderedTriggerIds: driver.State.TriggerQueue.Where(t => t.ControllerId == order.PlayerId).Select(t => t.TriggerId).ToArray()));
            else
                await driver.Act(driver.State.PriorityPlayerId!, new PassPriorityCommand());
        }
        Assert.True(driver.State.Status == MatchStatuses.Finished || driver.State.TurnStartStep is null && driver.State.StackItems.Count == 0);
    }

    internal static StandbyDefendObserverTests.Driver Driver(MatchState state, string branch)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_EXACT_HOLD_EVIDENCE");
        return new(state, string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, branch));
    }

    internal static async Task Replay(MatchState initial, StandbyDefendObserverTests.Driver driver)
    {
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State, new CoreRuleEngine(), default, driver.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        if (driver.NativeCommands > 0) Assert.Equal(driver.Commands().Length, driver.NativeCommands);
        driver.Export("steps.json", driver.Steps);
        driver.Export("replay.json", new { nativeCommands = driver.NativeCommands, accepted = driver.Commands().Length,
            coldRestores = driver.Commands().Length, fullJournalReplay = replay });
    }
}
