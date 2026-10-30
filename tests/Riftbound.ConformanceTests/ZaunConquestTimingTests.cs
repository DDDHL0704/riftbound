using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ZaunConquestTimingTests
{
    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task ZaunAllowsARealResponseBeforeTheDiscardChoiceAndSubsequentDraw(string player)
    {
        var initial = ThreeBattlefieldTimingProbe.Position(player, "OGN·298/298");
        var trace = new ThreeBattlefieldTimingProbe.Trace(initial, "zaun-" + player);
        try
        {
            var conquered = await trace.Conquer(player);
            Assert.Contains(conquered.Events, e => e.Kind == "BATTLEFIELD_CONQUERED");
            Assert.DoesNotContain(conquered.Events, e => e.Kind is "HAND_CHOICE_REQUESTED" or "CARD_DISCARDED" or "CARD_DRAWN");
            Assert.Null(trace.State.PendingHandChoice);
            Assert.Equal(initial.PlayerZones[player].Hand, trace.State.PlayerZones[player].Hand);
            Assert.Equal(initial.PlayerZones[player].MainDeck, trace.State.PlayerZones[player].MainDeck);
            Assert.Contains(trace.State.StackItems, i => i.EffectKind == TriggerKinds.BattlefieldConquerDiscardDraw);

            await trace.ReactWithPaidGust(player, TriggerKinds.BattlefieldConquerDiscardDraw);
            Assert.DoesNotContain(trace.Events, e => e.Kind is "HAND_CHOICE_REQUESTED" or "CARD_DISCARDED" or "CARD_DRAWN");
            await trace.Top();
            var choice = Assert.IsType<PendingHandChoiceState>(trace.State.PendingHandChoice);
            Assert.Equal(player, choice.PlayerId);
            Assert.Contains("H2", choice.LegalObjectIds);
            Assert.DoesNotContain("DRAW1", trace.State.PlayerZones[player].Hand);
            var selected = await trace.Act(player, new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["H2"]));
            Assert.Contains("H2", trace.State.PlayerZones[player].Graveyard);
            Assert.Contains("DRAW1", trace.State.PlayerZones[player].Hand);
            var kinds = selected.Events.Select(e => e.Kind).ToArray();
            Assert.True(Array.IndexOf(kinds, "CARD_DISCARDED") >= 0);
            Assert.True(Array.IndexOf(kinds, "CARD_DRAWN") > Array.IndexOf(kinds, "CARD_DISCARDED"));
            var reported = Assert.Single(selected.Events, e => e.Kind == "BATTLEFIELD_TRIGGER_RESOLVED"
                && Equals(e.Payload.GetValueOrDefault("trigger"), TriggerKinds.BattlefieldConquerDiscardDraw));
            Assert.Equal("OGN·298/298", reported.Payload.GetValueOrDefault("battlefieldCardNo"));
            Assert.NotEqual("UNIT", reported.Payload.GetValueOrDefault("sourceObjectId"));
        }
        finally { trace.Save(); }
    }
}
