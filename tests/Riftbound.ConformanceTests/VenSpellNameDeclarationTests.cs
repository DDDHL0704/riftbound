using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenSpellNameDeclarationTests
{
    [Fact]
    public async Task CatDeclaresAfterResponsesAndOnlyLocksEnemySpellsWhileOnABattlefield()
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·132", []));
        Assert.Null(game.State.PendingRuleChoice); Assert.Null(game.State.CardObjects["UNIT"].DeclaredSpellName);
        Assert.Equal(18, game.State.RunePools["P1"].Mana); Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        await Name(game, "风箱炎息");
        Assert.False(CardPermissionKeywordRules.IsSpellPlayProhibited(game.State, "P2", Spell()));
        await ReadyAndMove(game);
        Assert.True(CardPermissionKeywordRules.IsSpellPlayProhibited(game.State, "P2", Spell()));
        Assert.False(CardPermissionKeywordRules.IsSpellPlayProhibited(game.State, "P1", Spell()));
        await game.Act("P1", new EndTurnCommand());
        var command = new PlayCardCommand("SPELL", "SFD·080/221", ["ALLY"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P2", PlayCostPreviewTests.Request(game.State, command));
        Assert.False(quote.CanPay); Assert.Contains("此名称", quote.Message);
        Assert.DoesNotContain(game.PromptFor("P2").Candidates ?? [], c => c.Action == CommandTypes.PlayCard && c.Sources?.Any(s => s.Id == "SPELL") == true);
        await Reject(game.State, "P2", command);
        Assert.Equal("风箱炎息", CurrentPowerTargetTests.Snapshot(game.State).GetProperty("players").GetProperty("P1")
            .GetProperty("objects").GetProperty("UNIT").GetProperty("declaredSpellName").GetString());
        await game.Replay();
    }

    [Fact]
    public async Task MovingTheCatBackToBaseRestoresEnemySpellPermissionAndLeavingClearsItsDeclaration()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "BOUNCE", "OGN·104/298", "P1");
        initial = AttackDamageDistributionTests.Spell(initial, "MOVE", "OGN·043/298", "P2");
        foreach (var player in new[] { "P1", "P2" }) {
            var id = player + "-RUNE";
            initial = initial with {
                CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                    [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, player == "P2" ? "UNL-R02" : "UNL-R03", player) },
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                    [player] = initial.PlayerZones[player] with { Base = [..initial.PlayerZones[player].Base, id] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { [id] = new(player, "BASE") } };
        }
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·132", []));
        await Name(game, "风箱炎息");
        await ReadyAndMove(game);
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new TapRuneCommand("P2-RUNE"));
        await game.Act("P2", new RecycleRuneCommand("P2-RUNE"));
        await game.Act("P2", new PlayCardCommand("MOVE", "OGN·043/298", ["UNIT"])); await game.Top();
        Assert.Equal("BASE", game.State.ObjectLocations["UNIT"].Zone);
        Assert.False(CardPermissionKeywordRules.IsSpellPlayProhibited(game.State, "P2", Spell()));
        await game.Act("P2", new EndTurnCommand());
        await game.Act("P1", new TapRuneCommand("P1-RUNE"));
        await game.Act("P1", new PlayCardCommand("BOUNCE", "OGN·104/298", ["UNIT"])); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); Assert.Null(game.State.CardObjects["UNIT"].DeclaredSpellName);
        Assert.False(CardPermissionKeywordRules.IsSpellPlayProhibited(game.State, "P2", Spell())); await game.Replay();
    }

    [Fact]
    public async Task DeclarationCanNameAnOfficialSpellOutsideBothDecksAndRejectsUnitsOrInventedNames()
    {
        var game = new Driver(Position()); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·132", []));
        await game.Top(); var choice = game.State.PendingRuleChoice!;
        Assert.Contains(choice.Request.Options, o => o.Id == "公开处刑");
        foreach (var invalid in new[] { "虚构法术", "坠落猫咪" })
            await Reject(game.State, "P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [invalid]));
        await Reject(game.State, "P2", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["公开处刑"]));
        await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["公开处刑"]));
        Assert.Equal("公开处刑", game.State.CardObjects["UNIT"].DeclaredSpellName!.Name);
        var declared = game.State.CardObjects["UNIT"].DeclaredSpellName!;
        foreach (var forged in new[] { declared with { Name = "虚构法术" }, declared with { Source = declared.Source with { Generation = declared.Source.Generation + 1 } },
            declared with { Tick = game.State.Tick + 1 } }) {
            var bad = game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
                ["UNIT"] = game.State.CardObjects["UNIT"] with { DeclaredSpellName = forged } } };
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(bad), e => e.Contains("spell name declaration"));
        }
        await game.Replay();
    }

    [Fact]
    public async Task EarnedDeclarationSurvivesSourceLeavingButDoesNotMarkItsNewHandObject()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "BOUNCE", "OGN·104/298", "P1");
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·132", []));
        await game.Act("P1", new PlayCardCommand("BOUNCE", "OGN·104/298", ["UNIT"])); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); await Name(game, "风箱炎息");
        Assert.Null(game.State.CardObjects["UNIT"].DeclaredSpellName);
        Assert.False(CardPermissionKeywordRules.IsSpellPlayProhibited(game.State, "P2", Spell())); await game.Replay();
    }

    private static async Task ReadyAndMove(Driver game)
    {
        await game.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["UNIT"])); await game.Top();
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        for (var i = 0; i < 4 && game.State.TimingState == TimingStates.SpellDuelOpen; i++)
            await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
    }

    private static async Task Name(Driver game, string name)
    {
        await game.Top(); var choice = game.State.PendingRuleChoice!;
        Assert.Equal("DECLARE_SPELL_NAME", choice.Request.Kind);
        await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [name]));
    }
    private static CardBehaviorDefinition Spell() { Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·080/221", out var spell)); return spell; }
    private static MatchState Position() => AttackDamageDistributionTests.Spell(AttackDamageDistributionTests.Spell(
        VenUnitEntryBasicTests.Position("VEN·132"), "SPELL", "SFD·080/221", "P2"), "READY", "UNL-009/219", "P1");
    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-name", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
