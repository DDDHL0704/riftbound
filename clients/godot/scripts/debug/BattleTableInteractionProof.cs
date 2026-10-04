using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using Riftbound.Contracts;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;
using CardDictionary = Godot.Collections.Dictionary;
using CardArray = Godot.Collections.Array<Godot.Collections.Dictionary>;

namespace Riftbound.GodotClient.Debug;

// Exercises table inputs against a saved real server prompt. No local rules engine is used.
public partial class BattleTableInteractionProof : Control
{
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    public override async void _Ready()
    {
        try
        {
            var path = OS.GetCmdlineUserArgs().First(arg => arg.StartsWith("--prompt="))[9..];
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var prompt = document.RootElement.GetProperty("payload");
            var candidates = prompt.GetProperty("candidates").EnumerateArray().ToArray();
            var moveCandidate = candidates.First(candidate => candidate.GetProperty("action").GetString() == "MOVE_UNIT");
            var playCandidate = candidates.First(candidate => candidate.GetProperty("action").GetString() == "PLAY_CARD");
            var screen = GD.Load<PackedScene>("res://scenes/screens/MatchScreen.tscn").Instantiate<MatchScreen>();
            AddChild(screen);
            CardDictionary Visible(string id) => new() { ["objectId"] = id, ["cardName"] = "测试单位", ["visible"] = true };
            var movement = new MovementOverlay { TableMode = true }; screen.ComposerHost.AddChild(movement);
            var play = new PlayCardOverlay { TableMode = true }; screen.ComposerHost.AddChild(play);
            var source = moveCandidate.GetProperty("metadata").GetProperty("sourceRequirements")[0].GetProperty("sourceObjectId").GetString()!;
            Check(movement.Open(moveCandidate, "TABLE-PROOF", 1, Visible, label => label, source), "Movement candidate must open");
            Check(movement.TableSelectedObjects.SequenceEqual(new[] { source }), "Clicking a unit must preselect exactly that unit");
            Check(!movement.TryToggleTableSource("not-authorized"), "Unknown source cannot be selected");
            Check(!movement.TrySelectTableDestination("BATTLEFIELD:invented"), "Unknown destination cannot be selected");
            Check(movement.TryToggleTableSource(source) && !movement.TableSelectedObjects.Any(), "Click toggles source off");
            Check(movement.TryToggleTableSource(source) && movement.TableSelectedObjects.Contains(source), "Click toggles source on");
            var next = movement.TableSources.FirstOrDefault(id => id != source);
            if (next is not null) Check(movement.TryToggleTableSource(next) && movement.TableSelectedObjects.Count() == 2, "Group movement must retain two selected sources");
            var submitCount = 0;
            movement.Confirmed += (destination, ids) => { submitCount++; Check(ids.Contains(source), "Submission must preserve source identity"); };
            var moveConfirm = (Button)movement.FindChild("ConfirmMovementButton", true, false);
            moveConfirm.EmitSignal(Button.SignalName.Pressed); moveConfirm.EmitSignal(Button.SignalName.Pressed);
            Check(submitCount == 1 && movement.Visible && moveConfirm.Disabled, "Pending movement must stay open and reject double submission");
            movement.ApplyReceipt("old", 0, true, "old approval"); Check(movement.Visible, "Stale receipt cannot close current movement");
            movement.ApplyReceipt("TABLE-PROOF", 1, false, "测试拒绝");
            Check(movement.Visible && !moveConfirm.Disabled && movement.TableSelectedObjects.Contains(source), "Rejection must retain selected units for correction");
            movement.ApplyReceipt("TABLE-PROOF", 1, true, "accepted"); Check(!movement.Visible, "Accepted movement closes composer");

            var spell = playCandidate.GetProperty("metadata").GetProperty("sourceRequirements").EnumerateArray()
                .First(requirement => requirement.GetProperty("minTargetCount").GetInt32() > 0);
            var spellId = spell.GetProperty("sourceObjectId").GetString()!;
            PlayCostPreviewRequestDto? latest = null; play.PreviewRequested += request => latest = request;
            Check(play.Open(playCandidate, "PLAY-PROOF", 2, Visible, spellId), "Targeted card must open");
            Check(!play.TrySelectTableObject("not-authorized"), "Unknown target cannot enter the command");
            var target = play.TableTargets.First(); Check(play.TrySelectTableObject(target), "Clicking server target must select it");
            Check(latest is not null && latest.Command.TargetObjectIds.Contains(target), "Preview must receive the exact table-selected target");
            var old = latest!;
            play.Open(playCandidate, "PLAY-PROOF-NEW", 3, Visible, spellId);
            var confirm = (Button)play.FindChild("ConfirmPlayCardButton", true, false);
            play.ApplyQuote(new(old.RequestId, old.PromptId, old.SnapshotTick, true, true, "old quote"));
            Check(confirm.Disabled, "A previous prompt cannot authorize the new table selection");
            play.Hide();

            CardDictionary Player(string id) => new()
            {
                ["playerId"] = id, ["score"] = 2, ["mainDeckCount"] = 20, ["runeDeckCount"] = 6,
                ["resources"] = "法力 3 · 符能 2", ["handHiddenCount"] = 5,
                ["legend"] = new CardArray { Visible(id + "-legend") }, ["hero"] = new CardArray { Visible(id + "-hero") },
                ["base"] = new CardArray { Visible(id + "-unit") }, ["baseRunes"] = new CardArray { Visible(id + "-rune") },
                ["hand"] = new CardArray { Visible(id + "-hand") }, ["graveyard"] = new CardArray(), ["banished"] = new CardArray()
            };
            CardDictionary Lane(string id) => new()
            {
                ["battlefieldId"] = id, ["site"] = new CardArray { Visible(id) }, ["selfUnits"] = new CardArray { Visible(id + "-own") },
                ["opponentUnits"] = new CardArray { Visible(id + "-opponent") }, ["selfStandby"] = new CardArray(), ["opponentStandby"] = new CardArray()
            };
            screen.RenderSections(new CardArray { new() { ["kind"] = "wireTable", ["self"] = Player("own"), ["opponent"] = Player("opponent"),
                ["lanes"] = new CardArray { Lane("one"), Lane("two") }, ["turnNumber"] = 4, ["winningScore"] = 8 } });
            screen.SetComposerVisible(false);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(screen.ActionBar.GetGlobalRect().End.Y <= screen.GetGlobalRect().End.Y + 1, "Bottom action bar must stay within viewport");
            Check(screen.TableLayout.OpponentHand.GetChildren().OfType<OfficialCardView>().All(card => !card.TryGetVisibleCard(out _)), "Opponent hand must remain anonymous");
            screen.SetComposerVisible(true); play.Open(playCandidate, "BOUNDS", 4, Visible, spellId);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(confirm.GetGlobalRect().End.Y <= screen.GetGlobalRect().End.Y + 1, "Composer submit button must stay within viewport");
            Check(confirm.GetGlobalRect().End.X <= screen.GetGlobalRect().End.X + 1, "Composer submit button must stay within width");
            using var visibleEvent = JsonDocument.Parse("""{"kind":"STACK_ITEM_RESOLVED","description":"STACK-RAW-ID 结算","objectRefs":[{"role":"来源","cardNo":"OGN·009/298","isHidden":false,"isFaceDown":false}]}""");
            Check(BattleEventPresenter.Describe(visibleEvent.RootElement, "own", _ => "海克斯射线") == "海克斯射线已结算", "Activity must show card names instead of stack identifiers");
            using var hiddenEvent = JsonDocument.Parse("""{"kind":"STACK_ITEM_RESOLVED","objectRefs":[{"role":"来源","cardNo":"POISON","isHidden":true}]}""");
            Check(BattleEventPresenter.Describe(hiddenEvent.RootElement, "own", _ => throw new InvalidOperationException("Hidden identity lookup")) == "隐藏卡牌已结算", "Activity must not resolve hidden identities");
            var controller = new PromptInteractionController();
            CardDictionary Choice(string id) => new() { ["id"] = id, ["label"] = id, ["objectIds"] = new Godot.Collections.Array<string> { "unit" } };
            var ambiguousPrompt = new CardDictionary { ["promptId"] = "ALIASES", ["snapshotTick"] = 1L, ["actions"] = new CardArray
            { new() { ["action"] = "ACTIVATE_ABILITY", ["enabled"] = true, ["hasTemplate"] = true, ["selectionSteps"] = new CardArray
                { new() { ["role"] = "source", ["required"] = true, ["choices"] = new CardArray { Choice("ability-a"), Choice("ability-b") } } } } } };
            controller.Load(ambiguousPrompt); Check(controller.SelectAction("ACTIVATE_ABILITY"), "Action must be available");
            Check(!controller.TrySelectSource("unit") && !controller.TrySelectObject("unit") && controller.Current?.CanSubmit == false,
                "One card with two abilities must not silently choose the first ability");
            Check(controller.TrySelectChoice("source", "ability-b") && controller.Current?.CanSubmit == true, "Explicit server alias must remain selectable");
            ambiguousPrompt["snapshotTick"] = 2L; controller.Load(ambiguousPrompt);
            Check(controller.Current is null, "A new snapshot must discard the previous card choice");
            GD.Print("BATTLE_TABLE_INTERACTION_PASS"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
