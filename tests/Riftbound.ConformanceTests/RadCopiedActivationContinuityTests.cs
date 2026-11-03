using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadCopiedActivationContinuityTests
{
    private const string Ability = "RAD_114_EXHAUST_FRIENDLY_READY_UNIT";

    private static async Task<(Driver Game, string Source)> Position(bool token, bool attached)
    {
        var state = OfficialCopyIdentityTests.AddSpell(OfficialCopyIdentityTests.Position("RAD·114/167"), "RETURN", "OGN·104/298");
        state = AttachedRuleTextTests.AddSong(state, "SONG");
        state = LocalDestructionRecallTests.At(state with {
            Tick = 0,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["READY"] = OfficialGarenConquestBattlefieldCountTests.Printed("READY", "OGN·001/298", "P1") with { IsExhausted = true } }
        }, "READY", "P1", "BASE");
        if (!token)
            state = LocalDestructionRecallTests.At(state with {
                CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                    ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", "RAD·114/167", "P1") }
            }, "HOST", "P1", "BASE");
        var game = new Driver(state);
        var source = "HOST";
        if (token)
        {
            await game.Act("P1", new PlayCardCommand("C", "UNL-200/219", ["TARGET"]));
            await game.Top(); await game.Top();
            source = Assert.Single(game.State.CardObjects.Values, card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo).ObjectId;
        }
        if (attached)
        {
            await game.Act("P1", new AssembleEquipmentCommand("SONG", source, ["ASSEMBLE_GREEN"]));
            await game.Top();
        }
        return (game, source);
    }

    private static ActivateAbilityCommand Activate(MatchState state, string source, bool attached, string target = "READY")
        => new(source, Ability, [target], OptionalCosts: ["EXHAUST_FRIENDLY_UNIT:" + source]) {
            AbilityInstanceId = attached ? $"ATTACHED:SONG:0:{state.CardObjects["SONG"].AttachmentRevision}" : null };

    [Fact]
    public async Task RecoveryRejectsForgedDepartureReceiptsForARealCopiedSource()
    {
        var (game, source) = await Position(token: true, attached: true);
        await game.Act("P1", Activate(game.State, source, attached: true));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [source]));
        await game.Top();
        var item = Assert.Single(game.State.StackItems);
        var context = item.SimpleActivation!;
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, context.SourceTokenFactoryCardNo);
        var receipt = Assert.IsType<CeasedSimpleActivationSource>(context.CeasedSource);
        foreach (var forged in new[] {
            context with { CeasedSource = null },
            context with { SourceTokenFactoryCardNo = null },
            context with { CeasedSource = receipt with { DepartedTick = context.AcquiredTick - 1 } },
            context with { CeasedSource = receipt with { DepartedTick = game.State.Tick + 1 } },
            context with { CeasedSource = receipt with { SourceFaceRevision = receipt.SourceFaceRevision + 1 } },
            context with { CeasedSource = receipt with { TokenFactoryCardNo = "OGN·273/298" } } })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with {
                StackItems = [item with { SimpleActivation = forged }] }), error => error.Contains("printed activation context is invalid"));
        await game.Top();
        await game.Replay();
    }
}
