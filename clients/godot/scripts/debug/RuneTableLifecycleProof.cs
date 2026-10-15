using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Riftbound.GodotClient.Ui;
using CardArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using CardDictionary = Godot.Collections.Dictionary;

namespace Riftbound.GodotClient.Debug;

public partial class RuneTableLifecycleProof : Control
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    public override async void _Ready()
    {
        try
        {
            var screen = GD.Load<PackedScene>("res://scenes/screens/MatchScreen.tscn").Instantiate<MatchScreen>();
            AddChild(screen);
            screen.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
            screen.Size = new Vector2(1440, 900);
            var table = new CardDictionary
            {
                ["kind"] = "wireTable", ["tick"] = 116L,
                ["self"] = new CardDictionary
                {
                    ["playerId"] = "own", ["baseRunes"] = new CardArray
                    {
                        new() { ["objectId"] = "own-rune", ["cardName"] = "符文", ["visible"] = true }
                    }
                },
                ["opponent"] = new CardDictionary { ["playerId"] = "opponent", ["handHiddenCount"] = 4 },
                ["lanes"] = new CardArray(), ["winningScore"] = 8
            };
            Button[] RuneButtons() => screen.TableLayout.SelfPublicZones.FindChildren("*", "Button", true, false)
                .OfType<Button>().Where(button => button.Text is "回收" or "批量").ToArray();
            var recycled = new List<string>(); var batches = 0;
            screen.RuneRecycleRequested += recycled.Add;
            screen.RuneBatchRequested += () => batches++;
            screen.RenderSections(new CardArray { table });
            foreach (var latePrompt in new[] { true, false })
            {
                screen.SetRuneActions(["own-rune"], ["own-rune"], false);
                var previous = RuneButtons();
                Check(previous.Length == 2 && previous.All(button => !button.Disabled), "Previous match must have real enabled rune controls");
                // ClearMatchResult uses this same path when returning to the lobby.
                screen.RenderSections([]);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Check(previous.All(button => !GodotObject.IsInstanceValid(button)), "Previous controls must actually be freed");
                if (latePrompt) screen.SetRuneActions([], [], false);
                screen.ClearPromptStates();
                // Object IDs can recur for the same player in a different room.
                var next = table.Duplicate(true); next["tick"] = 0L;
                screen.RenderSections(new CardArray { next });
                var current = RuneButtons();
                Check(current.Length == 2 && current.All(button => button.Disabled), "A new table must wait for its own rune prompt");
                screen.SetRuneActions(["own-rune"], ["own-rune"], false);
                Check(current.All(button => !button.Disabled), "The fresh prompt must enable the new controls");
                current.Single(button => button.Text == "回收").EmitSignal(Button.SignalName.Pressed);
                current.Single(button => button.Text == "批量").EmitSignal(Button.SignalName.Pressed);
                screen.SetRuneActions(["own-rune"], ["own-rune"], true);
                Check(current.All(button => button.Disabled), "Pending commands must disable the new controls");
            }
            Check(recycled.SequenceEqual(new[] { "own-rune", "own-rune" }) && batches == 2,
                "Each new native control must emit exactly its current action");
            GD.Print("RUNE_TABLE_LIFECYCLE_PASS: 2 freed-control transitions, 4 native signals");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
