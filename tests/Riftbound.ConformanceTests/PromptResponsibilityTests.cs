using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PromptResponsibilityTests
{
    [Fact]
    public void MainActionPromptsExposeServerResponsibilityForBothPlayers()
    {
        var state = new MatchState(
            "prompt-responsibility-room",
            12,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "seat-1",
                ["P2"] = "seat-2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen);

        var prompts = ResolutionResult.BuildPrompts(state);
        Assert.NotNull(prompts["P1"].View?.Responsibility);
        Assert.NotNull(prompts["P2"].View?.Responsibility);
        Assert.NotNull(prompts["P1"].ServerFlow);
        Assert.NotNull(prompts["P2"].ServerFlow);
        var p1Responsibility = prompts["P1"].View!.Responsibility!;
        var p2Responsibility = prompts["P2"].View!.Responsibility!;
        var p1Flow = prompts["P1"].ServerFlow!;
        var p2Flow = prompts["P2"].ServerFlow!;

        Assert.Equal(PromptTypes.MainAction, p1Responsibility.PromptType);
        Assert.Equal("P1", p1Responsibility.PromptPlayerId);
        Assert.Equal("P1", p1Responsibility.ResponsiblePlayerId);
        Assert.True(p1Responsibility.IsResponsiblePlayer);
        Assert.True(p1Responsibility.ActionableForPromptPlayer);
        Assert.Equal("PLAYER_ACTION", p1Responsibility.State);
        Assert.Contains("服务端候选", p1Responsibility.NextStep, StringComparison.Ordinal);

        Assert.Equal(PromptTypes.MainAction, p2Responsibility.PromptType);
        Assert.Equal("P2", p2Responsibility.PromptPlayerId);
        Assert.Equal("P1", p2Responsibility.ResponsiblePlayerId);
        Assert.False(p2Responsibility.IsResponsiblePlayer);
        Assert.False(p2Responsibility.ActionableForPromptPlayer);
        Assert.Equal("WAITING_PLAYER", p2Responsibility.State);
        Assert.Contains("等待 P1 处理主行动", p2Responsibility.NextStep, StringComparison.Ordinal);
        Assert.Equal(0, p2Responsibility.QueueCounts["stack"]);
        Assert.Equal(0, p2Responsibility.QueueCounts["triggerQueue"]);

        Assert.Equal("ready", p1Flow.State);
        Assert.Equal("可提交", p1Flow.StateLabel);
        Assert.Equal("提交给服务端", p1Flow.PrimaryLabel);
        Assert.Equal(PromptTypes.MainAction, p1Flow.PromptType);
        Assert.True(p1Flow.ActionableForPromptPlayer);
        Assert.Equal("P1", p1Flow.ResponsiblePlayerId);
        Assert.Contains(p1Flow.Lanes, lane => lane.Key == "stack" && lane.Count == 0);
        Assert.Contains(p1Flow.Steps, step => step.Key == "candidate" && step.State == "ready");

        Assert.Equal("waiting", p2Flow.State);
        Assert.Equal("等待", p2Flow.StateLabel);
        Assert.Equal("等待 P1", p2Flow.PrimaryLabel);
        Assert.False(p2Flow.ActionableForPromptPlayer);
        Assert.Equal("P1", p2Flow.ResponsiblePlayerId);
        Assert.Equal(0, p2Flow.QueueCounts["stack"]);
    }

    [Fact]
    public void ServerFlowTriggerLaneUsesSafeHeadlineOnly()
    {
        var state = new MatchState(
            "prompt-server-flow-trigger-room",
            12,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "seat-1",
                ["P2"] = "seat-2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen) with
        {
            TriggerQueue =
            [
                new TriggerQueueItemState(
                    "trigger-hidden",
                    "P2",
                    "P2-hidden-source",
                    "SECRET_HIDDEN_STANDBY_TRIGGER",
                    "UNIT_DESTROYED")
            ]
        };

        var flow = ResolutionResult.BuildPrompts(state)["P1"].ServerFlow!;
        var triggerLane = Assert.Single(flow.Lanes, lane => lane.Key == "trigger");

        Assert.Equal(1, triggerLane.Count);
        Assert.Equal("触发等待结算", triggerLane.Headline);
        Assert.DoesNotContain("SECRET_HIDDEN_STANDBY_TRIGGER", triggerLane.Headline, StringComparison.Ordinal);
        Assert.DoesNotContain("P2-hidden-source", triggerLane.Headline, StringComparison.Ordinal);
        Assert.DoesNotContain("P2-hidden-source", flow.RelatedObjectIds);
        Assert.DoesNotContain(flow.RelatedObjects, item => item.ObjectId == "P2-hidden-source");
    }

    [Fact]
    public void ServerFlowRelatedObjectIdsExposeVisibleRuleQueueObjects()
    {
        var tempResourceActionId = PaymentCostRules.TemporaryPaymentResourceActionId("temp-1");
        var state = new MatchState(
            "prompt-related-objects-room",
            12,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "seat-1",
                ["P2"] = "seat-2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["stack-source"] = PublicCard("stack-source", "P1"),
                ["stack-target"] = PublicCard("stack-target", "P2"),
                ["stack-rune"] = PublicCard("stack-rune", "P1"),
                ["payment-rune"] = PublicCard("payment-rune", "P1"),
                ["temp-source"] = PublicCard("temp-source", "P1"),
                ["trigger-source"] = PublicCard("trigger-source", "P1"),
                ["hidden-trigger-source"] = PublicCard(
                    "hidden-trigger-source",
                    "P2",
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby])
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["stack-source"] = new("P1", "STACK"),
                ["stack-target"] = new("P2", "BATTLEFIELD", "battlefield-1"),
                ["stack-rune"] = new("P1", "BASE"),
                ["payment-rune"] = new("P1", "BASE"),
                ["temp-source"] = new("P1", "BATTLEFIELD", "battlefield-1"),
                ["trigger-source"] = new("P1", "BATTLEFIELD", "battlefield-1"),
                ["hidden-trigger-source"] = new("P2", "BATTLEFIELD", "battlefield-1")
            },
            stackItems:
            [
                new StackItemState(
                    "stack-1",
                    "P1",
                    "stack-source",
                    "DAMAGE",
                    "OGN-001",
                    targetObjectIds: ["stack-target"],
                    optionalCosts: ["RECYCLE_RUNE:stack-rune", tempResourceActionId, "missing-object"])
            ],
            pendingPayment: new PendingPaymentState(
                "payment-1",
                "PAY_CARD",
                "P1",
                powerCost: 1,
                legalPaymentChoiceIds: ["RECYCLE_RUNE:payment-rune"],
                paymentResourceActionIds: [tempResourceActionId]),
            temporaryPaymentResources:
            [
                new TemporaryPaymentResourceState(
                    "temp-1",
                    "P1",
                    "temp-source",
                    "ability-1",
                    "PAY_CARD",
                    generatedPower: 1,
                    remainingPower: 1,
                    allowedPaymentKinds: [PaymentCostRules.RuneCostPaymentKind],
                    createdTick: 12)
            ],
            triggerQueue:
            [
                new TriggerQueueItemState("visible-trigger", "P1", "trigger-source", "VISIBLE_TRIGGER", "UNIT_ENTERED"),
                new TriggerQueueItemState("hidden-trigger", "P2", "hidden-trigger-source", "SECRET_HIDDEN_STANDBY_TRIGGER", "UNIT_DESTROYED")
            ]);

        var flow = ResolutionResult.BuildPrompts(state)["P1"].ServerFlow!;
        var related = flow.RelatedObjectIds;

        Assert.Contains("stack-source", related);
        Assert.Contains("stack-target", related);
        Assert.Contains("stack-rune", related);
        Assert.Contains("payment-rune", related);
        Assert.Contains("temp-source", related);
        Assert.Contains("trigger-source", related);
        Assert.DoesNotContain("hidden-trigger-source", related);
        Assert.DoesNotContain("missing-object", related);
        Assert.DoesNotContain(tempResourceActionId, related);
        Assert.Contains(flow.RelatedObjects, item => item.ObjectId == "stack-source" && item.Role == "结算来源");
        Assert.Contains(flow.RelatedObjects, item => item.ObjectId == "stack-target" && item.Role == "结算目标");
        Assert.Contains(flow.RelatedObjects, item => item.ObjectId == "stack-rune" && item.Role == "费用资源");
        Assert.Contains(flow.RelatedObjects, item => item.ObjectId == "payment-rune" && item.Role == "费用资源");
        Assert.Contains(flow.RelatedObjects, item => item.ObjectId == "temp-source" && item.Role == "费用资源");
        Assert.Contains(flow.RelatedObjects, item => item.ObjectId == "trigger-source" && item.Role == "触发来源");
        Assert.DoesNotContain(flow.RelatedObjects, item => item.ObjectId == "hidden-trigger-source");
        Assert.DoesNotContain(flow.RelatedObjects, item => item.ObjectId == "missing-object");
        Assert.DoesNotContain(flow.RelatedObjects, item => item.ObjectId == tempResourceActionId);
    }

    [Fact]
    public void ServerFlowRelatedObjectIdsKeepOpponentHandChoicesHidden()
    {
        var state = new MatchState(
            "prompt-related-hand-choice-room",
            12,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "seat-1",
                ["P2"] = "seat-2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["choice-source"] = PublicCard("choice-source", "P1"),
                ["p1-hand-card"] = PublicCard("p1-hand-card", "P1")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["choice-source"] = new("P1", "BATTLEFIELD", "battlefield-1"),
                ["p1-hand-card"] = new("P1", "HAND")
            },
            pendingHandChoice: new PendingHandChoiceState(
                "choice-1",
                "SELECT_HAND_CARD",
                "P1",
                requiredCount: 1,
                maxCount: 1,
                legalObjectIds: ["p1-hand-card"],
                sourceObjectId: "choice-source",
                effectKind: "HAND_CHOICE"));

        var prompts = ResolutionResult.BuildPrompts(state);
        Assert.Contains("choice-source", prompts["P1"].ServerFlow!.RelatedObjectIds);
        Assert.Contains("p1-hand-card", prompts["P1"].ServerFlow!.RelatedObjectIds);
        Assert.Contains("choice-source", prompts["P2"].ServerFlow!.RelatedObjectIds);
        Assert.DoesNotContain("p1-hand-card", prompts["P2"].ServerFlow!.RelatedObjectIds);
        Assert.Contains(prompts["P1"].ServerFlow!.RelatedObjects, item => item.ObjectId == "choice-source" && item.Role == "选择来源");
        Assert.Contains(prompts["P1"].ServerFlow!.RelatedObjects, item => item.ObjectId == "p1-hand-card" && item.Role == "可选手牌");
        Assert.Contains(prompts["P2"].ServerFlow!.RelatedObjects, item => item.ObjectId == "choice-source" && item.Role == "选择来源");
        Assert.DoesNotContain(prompts["P2"].ServerFlow!.RelatedObjects, item => item.ObjectId == "p1-hand-card");
    }

    private static CardObjectState PublicCard(
        string objectId,
        string ownerId,
        bool isFaceDown = false,
        IReadOnlyList<string>? tags = null)
    {
        return new CardObjectState(
            objectId,
            isFaceDown: isFaceDown,
            tags: tags ?? [CardObjectTags.UnitCard],
            ownerId: ownerId,
            controllerId: ownerId);
    }
}
