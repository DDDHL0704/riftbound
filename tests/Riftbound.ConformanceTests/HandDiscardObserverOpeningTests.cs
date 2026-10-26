using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;
using Opening = Riftbound.ConformanceTests.CopiedDuelInsightOpeningTests.Opening;

namespace Riftbound.ConformanceTests;

public sealed class HandDiscardObserverOpeningTests(ITestOutputHelper output)
{
    private const string Unit = "OGN·202/298";
    private const string Spell = "VEN·008";
    private static readonly JsonSerializerOptions Json = Opening.JournalJson;

    [Theory]
    [InlineData(OfficialDeckFormat.CoreRules, "P1")]
    [InlineData(OfficialDeckFormat.CoreRules, "P2")]
    [InlineData(OfficialDeckFormat.ChinaStandard20260724, "P1")]
    [InlineData(OfficialDeckFormat.ChinaStandard20260724, "P2")]
    public async Task LegalBlankOpeningPaysDiscardThenResolvesTheOrdinarySkillBeforeTheLowerDamageSpell(
        OfficialDeckFormat format, string actor)
    {
        var opponent = actor == "P1" ? "P2" : "P1";
        var run = new Opening(actor == "P1" ? 18101 : 18102, format);
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(c => ChinaStandardBanList.IsAllowed(c, format)).ToArray() };
        string Rune(string color) => allowed.Cards.Where(c => c.CardCategory == "rune" && c.CardColorList.Contains(color))
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).First().CardNo;
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
        var deck = Assert.IsType<OfficialDecklist>(builder.Invoke(null, [allowed,
            allowed.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal), "OGN·251/298", Unit,
            new[] { Spell, Spell, Spell }, new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" },
            Enumerable.Repeat(Rune("red"), 6).Concat(Enumerable.Repeat(Rune("purple"), 6)).ToArray()]));
        var valid = OfficialDeckValidator.Validate(deck, catalog, format);
        Assert.True(valid.IsValid, string.Join("; ", valid.Errors));
        Assert.Equal(0, run.Initial.Tick); Assert.Equal(0, run.Initial.Seed);
        Assert.Empty(run.Initial.CardObjects); Assert.Equal(MatchStatuses.Seating, run.Initial.Status);
        var submitted = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        await run.Submit("P1", submitted); await run.Submit("P2", submitted);
        await run.Submit("P1", new ReadyCommand()); await run.Submit("P2", new ReadyCommand());
        foreach (var player in new[] { run.State.ActivePlayerId, run.State.OpeningSecondActionPlayerId! })
        {
            // Inspect only this player's current hand. No future deck order or
            // new seed is used to obtain the spell.
            var redraw = run.State.PlayerZones[player].Hand.Where(id => run.State.CardObjects[id].CardNo != Spell).Take(2).ToArray();
            await run.Submit(player, new MulliganCommand(redraw));
        }
        for (var turns = 0; run.State.TurnPlayerId != actor || Runes(run.State, actor).Count < 8
            || !run.State.PlayerZones[actor].Hand.Any(id => run.State.CardObjects[id].CardNo == Spell); turns++)
        {
            Assert.True(turns < 72, "This fixed legal deck must naturally draw one of its three spells within the finite setup.");
            await Drain(run); await run.Submit(run.State.TurnPlayerId, new EndTurnCommand());
        }
        await Save(run, actor, format, "setup-before-drain");
        await Drain(run);
        Assert.Equal(MatchPhases.Main, run.State.Phase);
        var source = Assert.Single(run.State.PlayerZones[actor].ChampionZone,
            id => run.State.CardObjects[id].CardNo == Unit);
        var purple = Runes(run.State, actor).First(id => run.State.CardObjects[id].Tags.Contains("COLOR:" + RuneTrait.Purple));
        await run.Submit(actor, new RecycleRuneCommand(purple));
        await Tap(run, actor);
        var mana = run.State.RunePools[actor].Mana;
        await run.Submit(actor, new PlayCardCommand(source, Unit, [], Destination: "BASE"));
        await Drain(run);
        Assert.Equal(mana - 5, run.State.RunePools[actor].Mana);
        Assert.Contains(source, run.State.PlayerZones[actor].Base);
        Assert.True(run.State.CardObjects[source].IsExhausted);
        Assert.Equal(5, run.State.CardObjects[source].Power);
        await run.Submit(actor, new EndTurnCommand()); await Drain(run);
        while (run.State.TurnPlayerId != actor)
        { await run.Submit(run.State.TurnPlayerId, new EndTurnCommand()); await Drain(run); }
        Assert.False(run.State.CardObjects[source].IsExhausted);
        var field = run.State.PlayerZones[actor].Battlefields.First(run.State.BattlefieldStates.ContainsKey);
        await run.Submit(actor, new MoveUnitCommand(source, "BASE", "BATTLEFIELD:" + field));
        await Drain(run); await Tap(run, actor);
        Assert.Equal(TimingStates.NeutralOpen, run.State.TimingState);
        Assert.Equal(actor, run.State.TurnPlayerId);
        Assert.True(run.State.CardObjects[source].IsExhausted);
        Assert.Equal(5, run.State.CardObjects[source].Power);
        var spell = run.State.PlayerZones[actor].Hand.First(id => run.State.CardObjects[id].CardNo == Spell);
        var discarded = run.State.PlayerZones[actor].Hand.First(id => id != spell);
        var before = run.State; var start = run.Frame().Events.Last().Sequence;
        var command = new PlayCardCommand(spell, Spell, [source], OptionalCosts: ["DISCARD_HAND_CARD:" + discarded]);
        await run.Cold(); await Save(run, actor, format, "before-cost");
        await run.Reject(opponent, command);
        await run.Submit(actor, command);
        Assert.Equal(before.RunePools[actor].Mana - 3, run.State.RunePools[actor].Mana);
        Assert.DoesNotContain(discarded, run.State.PlayerZones[actor].Hand);
        Assert.Contains(discarded, run.State.PlayerZones[actor].Graveyard);
        Assert.True(run.State.CardObjects[source].IsExhausted); Assert.Equal(5, run.State.CardObjects[source].Power);
        Assert.Equal(0, run.State.CardObjects[source].Damage);
        var ability = Assert.Single(run.State.StackItems, i => i.HandDiscardObserver is not null);
        Assert.Equal(source, ability.SourceObjectId); Assert.Equal(actor, ability.ControllerId);
        Assert.Equal(before.CardObjects[source].ObjectGeneration, ability.HandDiscardObserver!.Source.Generation);
        Assert.Equal(discarded, Assert.Single(ability.HandDiscardObserver.DiscardedCards).ObjectId);
        Assert.Equal(spell, ability.HandDiscardObserver.DiscardSourceObjectId);
        Assert.Equal(ability.StackItemId, run.State.StackItems.Last().StackItemId);
        Assert.Single(run.State.StackItems, i => i.SourceObjectId == spell);
        Assert.DoesNotContain(run.Frame().Events.Where(e => e.Sequence > start), e => e.Event.Kind == "TRIGGER_RESOLVED");
        await run.Cold(); await Save(run, actor, format, "pending-skill");
        var persisted = Persist(run.Frame()); var audit = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(persisted, new CoreRuleEngine(), default);
        Assert.Empty(audit);
        var coldJournal = new Journal();
        var cold = await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), coldJournal, new Store(persisted),
            NoopMatchPlayerStore.Instance, new(false, format)).GetOrCreateAsync(run.State.RoomId, default);
        var retainedCost = persisted.Commands.Last();
        var duplicate = await cold.SubmitAsync(retainedCost.PlayerId, retainedCost.ClientIntentId,
            GameCommandJsonMapper.Map(retainedCost.RawCommand!.Value), retainedCost.RawCommand, default);
        Assert.True(duplicate.Accepted); Assert.Empty(coldJournal.Entries);
        Assert.Equal(MatchStateHasher.Hash(run.State), MatchStateHasher.Hash(duplicate.State));
        await run.Submit(actor, new PassPriorityCommand());
        var originalNextFrame = run.Frame(); var originalNext = originalNextFrame.Commands.Last();
        var coldNext = await cold.SubmitAsync(originalNext.PlayerId, originalNext.ClientIntentId,
            GameCommandJsonMapper.Map(originalNext.RawCommand!.Value), originalNext.RawCommand, default);
        Assert.True(coldNext.Accepted, coldNext.ErrorMessage); Assert.Single(coldJournal.Entries);
        Assert.Equal(MatchStateHasher.Hash(run.State), MatchStateHasher.Hash(coldNext.State));
        Assert.Equal(MatchStateHasher.HashValue(originalNextFrame.Events.Where(e => e.Sequence > persisted.LastEventSequence).Select(e => e.Event).ToArray()),
            MatchStateHasher.HashValue(coldNext.Events));
        Assert.Equal(opponent, run.State.PriorityPlayerId);
        var responseRune = Runes(run.State, opponent).First(id => !run.State.CardObjects[id].IsExhausted);
        var responseMana = run.State.RunePools[opponent].Mana;
        await run.Submit(opponent, new TapRuneCommand(responseRune));
        Assert.Equal(responseMana + 1, run.State.RunePools[opponent].Mana);
        Assert.Equal(opponent, run.State.PriorityPlayerId);
        Assert.True(run.State.CardObjects[source].IsExhausted); Assert.Equal(5, run.State.CardObjects[source].Power);
        Assert.Equal(0, run.State.CardObjects[source].Damage);
        await run.Cold();
        await run.Submit(opponent, new PassPriorityCommand());
        Assert.False(run.State.CardObjects[source].IsExhausted);
        Assert.Equal(6, run.State.CardObjects[source].Power); Assert.Equal(0, run.State.CardObjects[source].Damage);
        Assert.Equal(1, Assert.Single(run.State.CardObjects[source].UntilEndOfTurnPowerModifiers).RequestedPowerDelta);
        Assert.Single(run.State.StackItems, i => i.SourceObjectId == spell);
        await run.Cold(); await Save(run, actor, format, "skill-resolved");
        await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand());
        await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Empty(run.State.StackItems); Assert.Contains(source, run.State.PlayerZones[actor].Battlefields);
        Assert.Equal(6, run.State.CardObjects[source].Power); Assert.Equal(5, run.State.CardObjects[source].Damage);
        Assert.Contains(spell, run.State.PlayerZones[actor].Graveyard);
        var events = run.Frame().Events.Where(e => e.Sequence > start).ToArray();
        Assert.Single(events, e => e.Event.Kind == "CARD_DISCARDED");
        var resolved = Assert.Single(events, e => e.Event.Kind == "TRIGGER_RESOLVED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), source));
        var damage = Assert.Single(events, e => e.Event.Kind == "DAMAGE_APPLIED"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), source));
        Assert.True(resolved.Sequence < damage.Sequence);
        Assert.DoesNotContain(events, e => e.Event.Kind == "UNIT_DESTROYED"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), source));
        await run.Cold(); await Save(run, actor, format, "spell-resolved");
        await Drain(run); await run.Submit(actor, new EndTurnCommand()); await Drain(run);
        Assert.Equal(5, run.State.CardObjects[source].Power); Assert.Equal(0, run.State.CardObjects[source].Damage);
        Assert.Empty(run.State.CardObjects[source].UntilEndOfTurnPowerModifiers);
        Assert.Contains(source, run.State.PlayerZones[actor].Battlefields);
        await run.Cold(); await Save(run, actor, format, "next-turn");
        var final = run.Frame(); Assert.Empty(final.ValidationErrors);
        Assert.DoesNotContain(final.Commands, c => c.CommandType.StartsWith("DEV_", StringComparison.Ordinal));
        Assert.True(run.OriginalNextMatches >= 5);
        output.WriteLine("HAND_DISCARD_ORDINARY_OPENING " + JsonSerializer.Serialize(new { actor, format = format.ToString(),
            originalSeed = run.Initial.Seed, source, spell, discarded, commands = final.Commands.Count,
            events = final.Events.Count, run.ColdCount, run.OriginalNextMatches,
            originalFullReplayMatched = true, realRegistryLoaded = true, registryDuplicateAppends = 0, registryOriginalNextMatches = 1, wholeCardCertification = false,
            regionTournamentEligibilityCertified = false }));
    }

    private static IReadOnlyList<string> Runes(MatchState s, string player)
        => s.PlayerZones[player].Base.Where(id => s.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)).ToArray();
    private static async Task Tap(Opening run, string actor)
    {
        foreach (var rune in Runes(run.State, actor).Where(id => !run.State.CardObjects[id].IsExhausted).ToArray())
            await run.Submit(actor, new TapRuneCommand(rune));
    }
    private static async Task Drain(Opening run)
    {
        for (var n = 0; n < 64; n++)
        {
            Assert.Equal(MatchStatuses.InProgress, run.State.Status);
            if (run.State.TriggerQueue.Count > 0)
            {
                var actor = ResolutionResult.BuildPrompts(run.State).Single(p => p.Value.EnabledActions().Contains(CommandTypes.OrderTriggers)).Key;
                var prompt = JsonSerializer.SerializeToElement(run.Prompt(actor), Opening.Json);
                var metadata = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == CommandTypes.OrderTriggers
                    && c.GetProperty("enabled").GetBoolean()).GetProperty("metadata");
                await run.Submit(actor, new OrderTriggersCommand(OrderedTriggerIds:
                    metadata.GetProperty("orderedTriggerIds").EnumerateArray().Select(v => v.GetString()!).ToArray()));
            }
            else if (run.State.PriorityPlayerId is { } priority) await run.Submit(priority, new PassPriorityCommand());
            else if (run.State.FocusPlayerId is { } focus) await run.Submit(focus, new PassFocusCommand());
            else return;
        }
        Assert.Fail("Finite setup continuation exceeded its explicit bound.");
    }
    private static MatchRecoveryFrame Persist(MatchRecoveryFrame frame)
        => JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
    private static async Task Save(Opening run, string actor, OfficialDeckFormat format, string phase)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_DISCARD_OPENING_EVIDENCE") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder); var path = Path.Combine(folder, $"{actor}-{format}-{phase}.json.gz");
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        await using var zip = new GZipStream(file, CompressionLevel.SmallestSize);
        await JsonSerializer.SerializeAsync(zip, run.Frame(), Json);
    }
    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore
    { public ValueTask<MatchRecoveryFrame?> LoadAsync(string room, CancellationToken token) => ValueTask.FromResult<MatchRecoveryFrame?>(frame); }
}
