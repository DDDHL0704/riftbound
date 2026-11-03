using Riftbound.Contracts;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadCopiedSimpleActivationTests
{
    private const string Riven = "RAD_114_EXHAUST_FRIENDLY_READY_UNIT";

    private static IReadOnlyDictionary<string, object?>[] Requirements(MatchState state, string ability)
    {
        var candidate = ResolutionResult.BuildPrompts(state)["P1"].Candidates?
            .FirstOrDefault(c => c.Action == CommandTypes.ActivateAbility && c.Enabled);
        if (candidate is null) return [];
        return Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"])
            .Where(row => Equals(row.GetValueOrDefault("sourceObjectId"), "HOST")
                && Equals(row.GetValueOrDefault("abilityId"), ability)).ToArray();
    }

    private static ActivateAbilityCommand ActivateRiven(string? instance = null)
        => new("HOST", Riven, ["HOST"], OptionalCosts: ["EXHAUST_FRIENDLY_UNIT:HOST"]) { AbilityInstanceId = instance };

    [Fact]
    public async Task ActualSongAssemblyGivesRivenIndependentNativeAndCopiedTurnUses()
    {
        var game = new Driver(AttachedRuleTextTests.Position("RAD·114/167", 0));
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"]));
        await game.Top();
        var rows = Requirements(game.State, Riven);
        Assert.Equal(2, rows.Length);
        var copyId = Assert.IsType<string>(Assert.Single(rows, row => row.GetValueOrDefault("abilityInstanceId") is not null)["abilityInstanceId"]);
        await game.Act("P1", ActivateRiven());
        await game.Top();
        Assert.Equal(copyId, Assert.Single(Requirements(game.State, Riven))["abilityInstanceId"]);
        await game.Act("P1", ActivateRiven(copyId));
        await game.Top();
        Assert.Empty(Requirements(game.State, Riven));
        Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        foreach (var command in new[] { ActivateRiven(), ActivateRiven(copyId) })
        {
            var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("repeat", "P1", CommandTypes.ActivateAbility), command, default);
            Assert.False(rejected.Accepted);
            Assert.Equal(MatchStateHasher.Hash(game.State), MatchStateHasher.Hash(rejected.State));
        }
        await game.Replay();
    }

    [Fact]
    public async Task TwoSongsSupplyThreeIndependentUses()
    {
        var game = new Driver(AttachedRuleTextTests.AddSong(AttachedRuleTextTests.Position("RAD·114/167", 0), "SONG2"));
        foreach (var gear in new[] { "GEAR", "SONG2" })
        {
            await game.Act("P1", new AssembleEquipmentCommand(gear, "HOST", ["ASSEMBLE_GREEN"]));
            await game.Top();
        }
        var rows = Requirements(game.State, Riven);
        Assert.Equal(3, rows.Length);
        foreach (var row in rows)
        {
            await game.Act("P1", ActivateRiven(row.GetValueOrDefault("abilityInstanceId") as string));
            await game.Top();
        }
        Assert.Empty(Requirements(game.State, Riven));
        await game.Replay();
    }

    [Fact]
    public async Task CapturedCopySurvivesActualDetachAndReattachmentRejectsItsOldId()
    {
        var game = new Driver(AttachedRuleTextTests.WithSpell(AttachedRuleTextTests.Position("RAD·114/167", 0), "P1", "SFD·011/221"));
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"]));
        await game.Top();
        var old = Assert.IsType<string>(Requirements(game.State, Riven).Single(row => row.GetValueOrDefault("abilityInstanceId") is not null)["abilityInstanceId"]);
        await game.Act("P1", ActivateRiven(old));
        var item = Assert.Single(game.State.StackItems);
        var context = item.SimpleActivation!;
        var origin = Assert.IsType<RuleTextInstanceBinding>(context.Origin);
        foreach (var forged in new[] {
            context with { Origin = null }, context with { RuleTextInstanceId = "forged" },
            context with { SourceFaceRevision = 99 },
            context with { Origin = origin with { Revision = 99 } },
            context with { Origin = origin with { CapturedTick = context.AcquiredTick + 1 } },
            context with { Origin = origin with { Equipment = origin.Equipment with { Generation = 99 } } } })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with { SimpleActivation = forged }] }));
        await game.Act("P1", new PlayCardCommand("SPELL", "SFD·011/221", ["HOST", "GEAR"]));
        await game.Top();
        Assert.Null(game.State.CardObjects["GEAR"].AttachedRuleText);
        await game.Top();
        Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"]));
        await game.Top();
        var fresh = Assert.IsType<string>(Requirements(game.State, Riven).Single(row => row.GetValueOrDefault("abilityInstanceId") is not null)["abilityInstanceId"]);
        Assert.NotEqual(old, fresh);
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("stale", "P1", CommandTypes.ActivateAbility), ActivateRiven(old), default);
        Assert.False(rejected.Accepted);
        Assert.Equal(MatchStateHasher.Hash(game.State), MatchStateHasher.Hash(rejected.State));
        await game.Act("P1", ActivateRiven(fresh));
        await game.Top();
        Assert.Null(Assert.Single(Requirements(game.State, Riven)).GetValueOrDefault("abilityInstanceId"));
        await game.Replay();
    }

    [Theory]
    [InlineData("RAD·021/167", "RAD_021_SWIFT_DISCARD_BANISH_REPLAY")]
    [InlineData("RAD·112/167", "RAD_112_EXHAUST_PLAY_BOMB")]
    public async Task CopiedEkkoAndZiggsPayTheirCostsAndResolveThroughTheSharedStack(string cardNo, string ability)
    {
        var initial = AttachedRuleTextTests.WithSpell(AttachedRuleTextTests.Position(cardNo, 0), "P1", "UNL-009/219");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Green] = 10, [RuneTrait.Red] = 10 }) } };
        var game = new Driver(initial);
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"]));
        await game.Top();
        var rows = Requirements(game.State, ability);
        Assert.Equal(2, rows.Length);
        var copy = Assert.IsType<string>(rows.Single(row => row.GetValueOrDefault("abilityInstanceId") is not null)["abilityInstanceId"]);
        var ekko = cardNo == "RAD·021/167";
        var command = new ActivateAbilityCommand("HOST", ability, ekko ? [] : ["F"],
            OptionalCosts: ekko ? ["DISCARD_HAND_CARD:SPELL"] : []) { AbilityInstanceId = copy };
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", command);
        Assert.Equal(mana - 1, game.State.RunePools["P1"].Mana);
        Assert.NotNull(Assert.Single(game.State.StackItems).SimpleActivation!.Origin);
        await game.Top();
        if (ekko)
        {
            Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard);
            Assert.NotNull(game.State.PendingEffectPlay);
            await game.Act("P1", new PlayCardCommand("HOST", cardNo, []));
            Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
            Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        }
        else
        {
            Assert.True(game.State.CardObjects["HOST"].IsExhausted);
            var bomb = Assert.Single(game.State.CardObjects.Values, card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo);
            Assert.Equal("F", game.State.ObjectLocations[bomb.ObjectId].BattlefieldObjectId);
        }
        await game.Replay();
    }

    [Fact]
    public async Task SongKeepsItsRivenActivationAfterSpectaclesChangeTheHostsFace()
    {
        var initial = AttachedRuleTextTests.AddSong(ShadySpectaclesTests.Position("OGN·096/298"), "SONG");
        initial = initial with { Tick = 0, CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", "RAD·114/167", "P1") } };
        var game = new Driver(initial);
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "HOST", ["ASSEMBLE_GREEN"]));
        await game.Top();
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_YELLOW"]));
        await game.Top();
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["COPY:COPY-SOURCE"]));
        Assert.Equal("OGN·096/298", game.State.CardObjects["HOST"].CardNo);
        var row = Assert.Single(Requirements(game.State, Riven));
        Assert.Equal("RAD·114/167", row["cardNo"]);
        await game.Act("P1", ActivateRiven(Assert.IsType<string>(row["abilityInstanceId"])));
        Assert.Equal("RAD·114/167", Assert.Single(game.State.StackItems).CardNo);
        await game.Top();
        Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Empty(Requirements(game.State, Riven));
        await game.Replay();
    }
}
