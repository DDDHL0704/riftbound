using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Controlled component positions: they do not claim full-deck admission or L2/L3.
internal static class ThreeBattlefieldTimingProbe
{
    public static string Other(string player) => player == "P1" ? "P2" : "P1";

    public static MatchState Position(string player, string field, bool boon = false, bool equipment = false,
        bool armament = false, bool equipmentInitiallyReady = false)
    {
        var state = ConquestLifecycleRegressionTests.State(field, 3);
        var cards = state.CardObjects.ToDictionary();
        var zones = state.PlayerZones.ToDictionary();
        // Remove the legacy dummy runes; the response below uses a printed rune.
        cards.Remove("RUNE"); cards.Remove("RUNE2");
        zones["P1"] = zones["P1"] with { Base = zones["P1"].Base.Where(id => id is not ("RUNE" or "RUNE2")).ToArray() };
        cards["UNIT"] = cards["UNIT"] with { CardNo = "UNL-092/219", Power = boon ? 3 : 2,
            Tags = boon ? [CardObjectTags.UnitCard, CardObjectTags.Boon] : [CardObjectTags.UnitCard] };
        if (boon)
        {
            cards["ALLY"] = new("ALLY", cardNo: "OGN·175/298", power: 4,
                tags: [CardObjectTags.UnitCard, CardObjectTags.Boon], ownerId: "P1", controllerId: "P1");
            zones["P1"] = zones["P1"] with { Base = zones["P1"].Base.Concat(["ALLY"]).ToArray() };
            cards["ENEMY-BOON"] = new("ENEMY-BOON", cardNo: "OGN·175/298", power: 4,
                tags: [CardObjectTags.UnitCard, CardObjectTags.Boon], ownerId: "P2", controllerId: "P2");
            zones["P2"] = zones["P2"] with { Base = zones["P2"].Base.Concat(["ENEMY-BOON"]).ToArray() };
        }
        if (equipment)
        {
            cards["HOST"] = new("HOST", cardNo: "OGN·175/298", power: 3,
                tags: [CardObjectTags.UnitCard], ownerId: "P1", controllerId: "P1");
            cards["EQUIP"] = new("EQUIP", cardNo: armament ? "SFD·022/221" : "SFD·019/221",
                isExhausted: !equipmentInitiallyReady, tags: armament ? [CardObjectTags.EquipmentCard, "武装", "灵便"] : [CardObjectTags.EquipmentCard],
                attachedToObjectId: armament ? "HOST" : null, ownerId: "P1", controllerId: "P1");
            zones["P1"] = zones["P1"] with { Base = zones["P1"].Base.Concat(["HOST", "EQUIP"]).ToArray() };
        }

        // The opponent has an actual ready rune and a printed reaction spell.
        cards["REACTION-RUNE"] = new("REACTION-RUNE", cardNo: "UNL-R04", tags: [CardObjectTags.RuneCard], ownerId: "P2", controllerId: "P2");
        cards["GUST"] = new("GUST", cardNo: "OGN·169/298", tags: [CardObjectTags.SpellCard], ownerId: "P2", controllerId: "P2");
        zones["P2"] = zones["P2"] with { Base = zones["P2"].Base.Concat(["REACTION-RUNE"]).ToArray(), Hand = ["GUST"] };
        if (player == "P2")
        {
            static string? Swap(string? id) => id == "P1" ? "P2" : id == "P2" ? "P1" : id;
            cards = cards.ToDictionary(e => e.Key, e => e.Value with { OwnerId = Swap(e.Value.OwnerId), ControllerId = Swap(e.Value.ControllerId) });
            zones = new Dictionary<string, PlayerZones> { ["P1"] = zones["P2"], ["P2"] = zones["P1"] };
        }
        var locations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal);
        foreach (var (owner, z) in zones)
        {
            foreach (var id in z.Base) locations[id] = new(owner, "BASE");
            foreach (var id in z.Hand) locations[id] = new(owner, "HAND");
            foreach (var id in z.MainDeck) locations[id] = new(owner, "MAIN_DECK");
            foreach (var id in z.Graveyard) locations[id] = new(owner, "GRAVEYARD");
            foreach (var id in z.Battlefields) locations[id] = new(owner, "BATTLEFIELD", id);
        }
        Assert.Equal(cards.Keys.Order(StringComparer.Ordinal), locations.Keys.Order(StringComparer.Ordinal));
        return state with { RoomId = $"FIELD-TIMING-{field}-{player}", ActivePlayerId = player, TurnPlayerId = player,
            PlayerZones = zones, CardObjects = cards, ObjectLocations = locations,
            RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
    }

    public static int EffectivePower(MatchState state, string player, string objectId)
        => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)[player], new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .GetProperty("players").GetProperty(player).GetProperty("objects").GetProperty(objectId).GetProperty("effectivePower").GetInt32();

    public sealed class Trace
    {
        private readonly MatchState initial;
        private readonly string name;
        private readonly List<object> steps = [];
        private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
        public MatchState State { get; private set; }
        public List<GameEvent> Events { get; } = [];

        public Trace(MatchState initial, string name)
        {
            this.initial = initial;
            this.name = name;
            State = initial;
        }

        public async Task<ResolutionResult> Act(string player, GameCommand command)
        {
            var before = State;
            var result = await new CoreRuleEngine().ResolveAsync(before,
                new PlayerIntent($"three-field-{name}-{steps.Count}", player, command.CmdType), command, CancellationToken.None);
            steps.Add(new { player, command = JsonSerializer.SerializeToElement(command, command.GetType(), json),
                beforeTick = before.Tick, result.Accepted, result.ErrorCode, result.ErrorMessage,
                result.Events, state = result.State, result.Prompts });
            Events.AddRange(result.Events);
            State = result.State;
            Assert.True(result.Accepted, result.ErrorMessage);
            return result;
        }

        public async Task<ResolutionResult> Conquer(string player)
        {
            Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(State));
            await Act(player, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
            await Act(player, new PassFocusCommand());
            return await Act(Other(player), new PassFocusCommand());
        }

        public async Task<ResolutionResult> Top()
        {
            await Act(Assert.IsType<string>(State.PriorityPlayerId), new PassPriorityCommand());
            return await Act(Assert.IsType<string>(State.PriorityPlayerId), new PassPriorityCommand());
        }

        public async Task Choose(bool accept, string target, bool targetMustBeChosen)
        {
            var selectedTarget = false;
            for (var i = 0; i < 4 && State.PendingCardChoice is { } choice; i++)
            {
                string[] selected;
                if (!accept)
                {
                    Assert.Equal(0, choice.RequiredCount);
                    selected = [];
                }
                else if (choice.LegalObjectIds.Contains(target, StringComparer.Ordinal))
                {
                    if (target == "ALLY")
                    {
                        Assert.Contains("UNIT", choice.LegalObjectIds);
                        Assert.DoesNotContain("ENEMY-BOON", choice.LegalObjectIds);
                    }
                    selected = [target]; selectedTarget = true;
                }
                else if (choice.LegalObjectIds.Contains("BF", StringComparer.Ordinal)) selected = ["BF"];
                else { Assert.Fail("Unsupported confirmation options: " + string.Join(",", choice.LegalObjectIds)); return; }
                await Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected));
                if (!accept) break;
            }
            Assert.Null(State.PendingCardChoice);
            if (accept && targetMustBeChosen) Assert.True(selectedTarget, "The paid object must be selected before responses.");
        }

        public async Task ReactWithPaidGust(string actor, string expectedEffect)
        {
            var opponent = Other(actor);
            if (State.PriorityPlayerId == actor) await Act(actor, new PassPriorityCommand());
            Assert.Equal(opponent, State.PriorityPlayerId);
            await Act(opponent, new TapRuneCommand("REACTION-RUNE"));
            Assert.True(State.CardObjects["REACTION-RUNE"].IsExhausted);
            var cast = await Act(opponent, new PlayCardCommand("GUST", "OGN·169/298", ["UNIT"]));
            Assert.Equal(1, Assert.Single(cast.Events, e => e.Kind == "COST_PAID").Payload["totalManaCost"]);
            await Top();
            Assert.Contains("UNIT", State.PlayerZones[actor].Hand);
            Assert.Contains(State.StackItems, i => i.EffectKind == expectedEffect);
        }

        public void Save()
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_THREE_FIELD_EVIDENCE") is not { Length: > 0 } path) return;
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, name + ".json"), JsonSerializer.Serialize(new { initial, steps, final = State }, json));
        }
    }
}

public sealed class ThreeBattlefieldTimingControlTests
{
    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task NeutralBattlefieldFixtureActuallyConquersWithoutAnyOfTheThreeEffects(string player)
    {
        var trace = new ThreeBattlefieldTimingProbe.Trace(ThreeBattlefieldTimingProbe.Position(player, "OGN·296/298"), "control-" + player);
        try
        {
            var result = await trace.Conquer(player);
            Assert.Contains(result.Events, e => e.Kind == "BATTLEFIELD_CONQUERED");
            Assert.Null(result.State.PendingCardChoice); Assert.Null(result.State.PendingHandChoice);
            Assert.DoesNotContain(result.Events, e => e.Kind is "CARD_DISCARDED" or "CARD_DRAWN" or "BOON_CONSUMED" or "EQUIPMENT_READIED");
        }
        finally { trace.Save(); }
    }
}
