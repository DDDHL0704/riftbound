using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class PlayPowerSpendSelectionTests
{
    [Fact]
    public async Task OrdinaryBirthEarnedResourcesChooseTheOtherColorAndSurviveTheActualRegistry()
    {
        var initial = MatchReplayInitialStateBuilder.FromSeats("ordinary-selected-power", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        Assert.Equal(0, initial.Tick); Assert.Equal(0, initial.Seed); Assert.Equal(MatchStatuses.Seating, initial.Status);
        Assert.Empty(initial.CardObjects); Assert.Empty(initial.ObjectLocations); Assert.All(initial.RunePools.Values, pool => Assert.Equal(RunePool.Empty, pool));
        var run = new Run(initial, ordinary: true);
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "empty-observed-deck-repeat-openings.json")));
        var recipe = Assert.Single(fixture.RootElement.GetProperty("branches").EnumerateArray(), row => row.GetProperty("cardNo").GetString() == "UNL-032/219");
        foreach (var row in recipe.GetProperty("acceptedCommands").EnumerateArray())
        {
            var actor = row.GetProperty("playerId").GetString()!;
            var node = JsonNode.Parse(row.GetProperty("rawCommand").GetRawText())!.AsObject();
            var command = GameCommandJsonMapper.Map(JsonSerializer.SerializeToElement(node));
            if (command is SubmitDeckCommand deck)
            {
                var catalog = await OfficialCardCatalog.LoadDefaultAsync();
                var list = new OfficialDecklist(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
                foreach (var format in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 })
                {
                    var validation = OfficialDeckValidator.Validate(list, catalog, format);
                    Assert.True(validation.IsValid, string.Join(';', validation.Errors));
                }
            }
            // Only the new room's envelope identity is refreshed; all recipe payload fields are retained.
            var current = run.Wire(actor, command);
            node["promptId"] = current.GetProperty("promptId").GetString(); node["snapshotTick"] = run.State.Tick;
            var result = await run.Repeat(actor, "recipe-" + run.Entries.Count, JsonSerializer.SerializeToElement(node));
            Assert.True(result.Accepted, result.ErrorMessage);
            await run.Cold("recipe-" + run.Entries.Count);
        }
        Assert.Equal(25, run.Entries.Count);
        var second = run.State.PendingCardChoice!; Assert.Equal("DECK_EFFECT", second.ChoiceWindow); Assert.Equal(0, second.RequiredCount);
        await run.Act("P1", new ChooseCardsCommand(second.ChoiceId, second.ChoiceWindow, []));
        var existing = run.State.RunePools["P1"];
        Assert.Equal(1, existing.TotalPower); Assert.Equal(1, existing.PowerByTrait.GetValueOrDefault("green"));
        const string purpleRune = "P1-RUNE-UNL-R05-11";
        Assert.Contains(purpleRune, run.State.PlayerZones["P1"].Base);
        await run.Act("P1", new RecycleRuneCommand(purpleRune));
        Assert.Equal(Colors(("green", 1), ("purple", 1)), run.State.RunePools["P1"].PowerByTrait);
        await run.Cold("funded-observed-play"); await run.Registry("registry-before-selected-payment");
        var source = Assert.Single(run.State.PendingEffectPlay!.Sources).Key;
        Assert.Equal("OGN·194/298", run.State.CardObjects[source].CardNo);
        var commandToPlay = new PlayCardCommand(source, "OGN·194/298", [], OptionalCosts: ["POWER_SPEND:purple:1"]);
        var quote = await run.Quote("P1", commandToPlay); Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(1, quote.Cost!.GenericPower); Assert.Empty(quote.Cost.PowerByTrait);
        var accepted = await run.Submit("P1", commandToPlay); Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(Colors(("green", 1)), run.State.RunePools["P1"].PowerByTrait);
        AssertFee(accepted, quote, 1);
        await run.Cold("selected-ordinary-payment"); await run.Registry("registry-after-selected-payment");

        // Same total, coherent views and spectator, but another wallet remainder.
        // The exact original selector, commands, events and birth must prove which color was spent.
        var original = run.Frame();
        var forgedState = run.State with { RunePools = new Dictionary<string, RunePool>(run.State.RunePools) {
            ["P1"] = run.State.RunePools["P1"] with { PowerByTrait = Colors(("purple", 1)) } } };
        var forged = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(run.Frame(forgedState), Web), Web)!;
        Assert.Equal(Hash(original.Commands), Hash(forged.Commands)); Assert.Equal(Hash(original.Events), Hash(forged.Events));
        Assert.Equal(Hash(initial), Hash(forged.ReplayInitialState!));
        var structure = MatchRecoveryValidator.Validate(forged.RoomId, forged.LastEventSequence, forged.Commands, forged.Events,
            forged.PlayerViews, forged.AuthoritativeState, forged.CurrentTick, forged.SpectatorReplayFrame);
        Assert.Empty(structure);
        var rejected = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(forged, new CoreRuleEngine(), default);
        run.Save("coherent-wallet-replay", new { structuralErrors = structure, replayErrors = rejected, original, forged });
        Assert.Contains(rejected, error => error.Contains("hash", StringComparison.OrdinalIgnoreCase));
        await run.Registry("registry-rejects-changed-wallet", forged);
        await run.Registry("registry-original-still-recovers");
        var remainingPermission = run.State.PendingEffectPlay!;
        await run.Act(remainingPermission.PlayerId, new ChooseCardsCommand(remainingPermission.ChoiceId, "EFFECT_PLAY", []));
        await run.Cold("ordinary-continued-after-tamper");
        Assert.DoesNotContain(run.Entries, entry => entry.CommandType == "DEV_SEED_SCENARIO");
    }

    private sealed class FrameStore(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken token)
            => ValueTask.FromResult<MatchRecoveryFrame?>(roomId == frame.RoomId ? frame : null);
    }

    private sealed class RecordedPlayers : IMatchPlayerStore
    {
        private readonly Dictionary<(string Room, string Player), string> hashes = [];
        public int Writes { get; private set; }
        public ValueTask SavePlayerSessionAsync(string room, string player, string seat, string hash, CancellationToken token, long openingSeed = 0)
        { hashes[(room, player)] = hash; Writes++; return ValueTask.CompletedTask; }
        public async ValueTask CommitRoomSeatAsync(MatchJournalEntry entry, string hash, IMatchJournal journal, CancellationToken token)
        { await SavePlayerSessionAsync(entry.RoomId, entry.PlayerId, entry.AuthoritativeState.Seats[entry.PlayerId], hash, token, entry.OpeningSeed); await journal.RecordAsync(entry, token); }
        public ValueTask<bool> HasReconnectTokenHashAsync(string room, string player, string hash, CancellationToken token)
            => ValueTask.FromResult(hashes.TryGetValue((room, player), out var stored) && stored == hash);
    }
}
