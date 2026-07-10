using System.IO;
using System.Linq;
using Godot;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class FocusedPromptOverlayProof : Control
{
    private const string TriggerMode = "trigger";
    private const string DamageMode = "damage";

    public override async void _Ready()
    {
        MinimalTheme.Apply(this);
        var mode = ArgumentValue("--riftbound-focused-overlay-proof=") ?? TriggerMode;
        var shown = mode switch
        {
            TriggerMode => ShowTriggerFixture(),
            DamageMode => ShowDamageFixture(),
            _ => false
        };
        if (!shown)
        {
            GD.PushError($"Unable to show focused overlay proof mode: {mode}");
            GetTree().Quit(1);
            return;
        }

        for (var frame = 0; frame < 4; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        await CaptureWhenRequested();
    }

    private bool ShowTriggerFixture()
    {
        var action = ServerTriggerFixtureAction();
        if (!SpecialPromptCommandBuilder.TryReadOrderTriggers(action, out _, out var reason))
        {
            GD.PushError($"Server trigger fixture was rejected: {reason}");
            return false;
        }

        var overlay = GetNode<TriggerOrderOverlay>("TriggerOrderOverlay");
        return overlay.ShowPrompt(action, out reason) || ReportDisabled("trigger", reason);
    }

    private bool ShowDamageFixture()
    {
        var action = ServerDamageFixtureAction();
        if (!SpecialPromptCommandBuilder.TryReadDamageAssignmentPrompt(action, out _, out var reason))
        {
            GD.PushError($"Server damage fixture was rejected: {reason}");
            return false;
        }

        var overlay = GetNode<DamageAssignmentOverlay>("DamageAssignmentOverlay");
        return overlay.ShowPrompt(action, out reason) || ReportDisabled("damage", reason);
    }

    private static bool ReportDisabled(string mode, string reason)
    {
        GD.PushError($"Focused {mode} overlay fixture is disabled: {reason}");
        return false;
    }

    private static Godot.Collections.Dictionary ServerTriggerFixtureAction()
    {
        const string candidateJson = """
        {
          "action": "ORDER_TRIGGERS",
          "enabled": true,
          "metadata": {
            "orderedTriggerIds": ["TRIGGER-P2-1", "TRIGGER-P2-2", "TRIGGER-P1-1", "TRIGGER-P1-2"],
            "triggerIds": ["TRIGGER-P2-1", "TRIGGER-P2-2", "TRIGGER-P1-1", "TRIGGER-P1-2"],
            "triggerChoices": [
              { "id": "TRIGGER-P2-1", "label": "对手战场效果" },
              { "id": "TRIGGER-P2-2", "label": "对手单位效果" },
              { "id": "TRIGGER-P1-1", "label": "我方符文回响" },
              { "id": "TRIGGER-P1-2", "label": "我方单位效果" }
            ],
            "legalOrderingConstraints": {
              "crossControllerReorderingAllowed": false,
              "withinControllerReorderingAllowed": true,
              "preserveControllerBlocks": true,
              "legalResolutionControllerBlockOrder": [
                { "controllerId": "P2", "triggerIds": ["TRIGGER-P2-1", "TRIGGER-P2-2"] },
                { "controllerId": "P1", "triggerIds": ["TRIGGER-P1-1", "TRIGGER-P1-2"] }
              ]
            }
          }
        }
        """;

        return new Godot.Collections.Dictionary
        {
            ["action"] = "ORDER_TRIGGERS",
            ["enabled"] = true,
            ["candidateJson"] = candidateJson
        };
    }

    private static Godot.Collections.Dictionary ServerDamageFixtureAction()
    {
        const string candidateJson = """
        {
          "action": "ASSIGN_COMBAT_DAMAGE",
          "enabled": true,
          "metadata": {
            "battleId": "battle:BF-DAMAGE",
            "battlefieldId": "BF-DAMAGE",
            "assignableDamagePool": { "P1-ATTACKER": 5 },
            "requiredAssignments": [
              {
                "sourceObjectId": "P1-ATTACKER",
                "damage": 5,
                "legalTargetObjectIds": ["P2-GUARD", "P2-BACKROW"]
              }
            ],
            "legalTargets": {
              "P1-ATTACKER": ["P2-GUARD", "P2-BACKROW"]
            },
            "lethalDamageThreshold": {
              "P2-GUARD": 2,
              "P2-BACKROW": 1
            },
            "assignmentChoices": [
              { "id": "P1-ATTACKER->P2-GUARD", "label": "先锋 -> 守军" },
              { "id": "P1-ATTACKER->P2-BACKROW", "label": "先锋 -> 后排" }
            ],
            "battleParticipants": [
              { "objectId": "P1-ATTACKER", "role": "ATTACKER", "power": 5, "damage": 0 },
              { "objectId": "P2-GUARD", "role": "DEFENDER", "power": 2, "damage": 0 },
              { "objectId": "P2-BACKROW", "role": "DEFENDER", "power": 1, "damage": 0 }
            ]
          }
        }
        """;

        return new Godot.Collections.Dictionary
        {
            ["action"] = "ASSIGN_COMBAT_DAMAGE",
            ["enabled"] = true,
            ["candidateJson"] = candidateJson
        };
    }

    private async System.Threading.Tasks.Task CaptureWhenRequested()
    {
        var resourcePath = ArgumentValue("--riftbound-proof-capture=");
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            return;
        }

        await ToSignal(GetTree().CreateTimer(0.5), Godot.Timer.SignalName.Timeout);
        var absolutePath = ProjectSettings.GlobalizePath(resourcePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        RenderingServer.ForceDraw();
        RenderingServer.ForceSync();
        using var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(absolutePath);
        if (error != Error.Ok)
        {
            GD.PushError($"Unable to save focused overlay proof: {error}");
        }

        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    private static string? ArgumentValue(string prefix)
    {
        return OS.GetCmdlineUserArgs()
            .FirstOrDefault(argument => argument.StartsWith(prefix, System.StringComparison.Ordinal))?[prefix.Length..];
    }
}
