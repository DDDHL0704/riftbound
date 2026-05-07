import { Check, Play, X } from "lucide-react";
import { useEffect, useMemo, useState, type ReactNode } from "react";
import { ActionPromptCandidateDto, ActionPromptChoiceDto, ActionPromptDto, GameCommand } from "../../types/protocol";
import { costText, keywordsText, objectTypeText, promptActionLabel, statusLabel } from "../../utils/formatters";
import { isHiddenObject } from "../../utils/hiddenInfo";
import { Button } from "../ui/Button";
import { StatusPill } from "../ui/StatusPill";
import { InspectedCard, objectStateLabels } from "./CardFace";

type CardDetailDrawerProps = {
  card?: InspectedCard;
  onClose: () => void;
  onCommand?: (command: GameCommand) => void;
  prompt?: ActionPromptDto;
};

export function CardDetailDrawer({ card, onClose, onCommand, prompt }: CardDetailDrawerProps) {
  if (!card) {
    return null;
  }

  const hidden = isHiddenObject(card.object) && !card.spec;
  const title = hidden ? "未公开卡牌" : card.spec?.cardName ?? card.object?.cardNo ?? card.objectId ?? "未知卡牌";
  const states = objectStateLabels(card.object);
  const sourceObjectId = card.objectId ?? card.object?.objectId;
  const sourceActions = hidden ? [] : sourceCandidatesFor(prompt, sourceObjectId);

  return (
    <div className="detail-layer" role="dialog" aria-modal="true" aria-label="卡牌详情">
      <button className="detail-scrim" onClick={onClose} type="button" aria-label="关闭卡牌详情" />
      <aside className="detail-drawer">
        <header>
          <div>
            <span className="eyebrow">卡牌详情</span>
            <h2>{title}</h2>
          </div>
          <Button icon={<X size={18} />} onClick={onClose} variant="ghost">关闭</Button>
        </header>
        <div className="detail-section">
          <StatusPill tone={hidden ? "warn" : "info"}>{hidden ? "隐藏信息" : objectTypeText(card.object, card.spec)}</StatusPill>
          <StatusPill tone="neutral">{card.spec?.cardNo ?? card.object?.cardNo ?? card.objectId ?? "无编号"}</StatusPill>
          {card.spec && <StatusPill tone={card.spec.conformanceTier === "full-official-rule-pass" ? "good" : "warn"}>{statusLabel(card.spec.status)}</StatusPill>}
        </div>
        {hidden ? (
          <p className="detail-muted">该对象未向当前玩家公开。前端只展示服务端 snapshot 允许的信息，不读取或推断卡名、费用、类型或规则文本。</p>
        ) : (
          <>
            <dl className="detail-grid">
              <div>
                <dt>费用</dt>
                <dd>{costText(card.spec)}</dd>
              </div>
              <div>
                <dt>战力</dt>
                <dd>{card.object?.effectivePower ?? card.object?.power ?? card.object?.basePower ?? "未知"}</dd>
              </div>
              <div>
                <dt>所属方</dt>
                <dd>{card.object?.ownerId ?? "未知"}</dd>
              </div>
              <div>
                <dt>控制方</dt>
                <dd>{card.object?.controllerId ?? "未知"}</dd>
              </div>
              <div>
                <dt>对象 ID</dt>
                <dd>{card.objectId ?? card.object?.objectId ?? "无"}</dd>
              </div>
              <div>
                <dt>位置</dt>
                <dd>{formatLocation(card.object?.location)}</dd>
              </div>
            </dl>
            <section className="detail-section">
              <strong>关键词</strong>
              <p>{keywordsText(card.spec)}</p>
            </section>
            <section className="detail-section">
              <strong>规则文本</strong>
              <p className="card-rules">{card.spec?.officialText || "服务端未提供卡面规则文本。"}</p>
            </section>
            <section className="detail-section">
              <strong>对象状态</strong>
              <p>{states.length ? states.join("、") : "正常"}</p>
              {(card.object?.tags?.length ?? 0) > 0 && <p className="detail-muted">标签：{card.object?.tags?.join("、")}</p>}
              {(card.object?.untilEndOfTurnEffects?.length ?? 0) > 0 && <p className="detail-muted">本回合效果：{card.object?.untilEndOfTurnEffects?.join("、")}</p>}
            </section>
            <section className="detail-section detail-actions">
              <strong>可执行操作</strong>
              {sourceActions.length === 0 ? (
                <p className="detail-muted">当前服务端 prompt 没有给这张牌可提交的操作。</p>
              ) : (
                <div className="detail-action-list">
                  {sourceActions.map((candidate) => {
                    const command = commandForSourceCandidate(candidate, sourceObjectId);
                    if (candidate.action === "PLAY_CARD") {
                      return (
                        <PlayCardComposer
                          candidate={candidate}
                          card={card}
                          key={candidate.action}
                          onClose={onClose}
                          onCommand={onCommand}
                          sourceObjectId={sourceObjectId}
                        />
                      );
                    }

                    if (candidate.action === "MOVE_UNIT") {
                      return (
                        <MoveUnitComposer
                          candidate={candidate}
                          key={candidate.action}
                          onClose={onClose}
                          onCommand={onCommand}
                          sourceObjectId={sourceObjectId}
                        />
                      );
                    }

                    if (candidate.action === "ASSEMBLE_EQUIPMENT") {
                      return (
                        <AssembleEquipmentComposer
                          candidate={candidate}
                          key={candidate.action}
                          onClose={onClose}
                          onCommand={onCommand}
                          sourceObjectId={sourceObjectId}
                        />
                      );
                    }

                    if (candidate.action === "ACTIVATE_ABILITY") {
                      return (
                        <ActivateAbilityComposer
                          candidate={candidate}
                          key={candidate.action}
                          onClose={onClose}
                          onCommand={onCommand}
                          sourceObjectId={sourceObjectId}
                        />
                      );
                    }

                    if (candidate.action === "LEGEND_ACT") {
                      return (
                        <LegendActionComposer
                          candidate={candidate}
                          key={candidate.action}
                          onClose={onClose}
                          onCommand={onCommand}
                          sourceObjectId={sourceObjectId}
                        />
                      );
                    }

                    if (candidate.action === "DECLARE_BATTLE") {
                      return (
                        <DeclareBattleComposer
                          candidate={candidate}
                          key={candidate.action}
                          onClose={onClose}
                          onCommand={onCommand}
                          sourceObjectId={sourceObjectId}
                        />
                      );
                    }

                    return (
                      <Button
                        disabled={!candidate.enabled || !command || !onCommand}
                        icon={<Play size={16} />}
                        key={candidate.action}
                        onClick={() => {
                          if (command && onCommand) {
                            onCommand(command);
                            onClose();
                          }
                        }}
                        title={command ? candidate.reason : "该操作还需要服务端提供目标、模式或费用选择后才能提交"}
                        variant={candidate.enabled && command ? "primary" : "ghost"}
                      >
                        {promptActionLabel(candidate)}
                      </Button>
                    );
                  })}
                </div>
              )}
            </section>
          </>
        )}
      </aside>
    </div>
  );
}

function sourceCandidatesFor(prompt: ActionPromptDto | undefined, sourceObjectId: string | undefined): ActionPromptCandidateDto[] {
  if (!prompt || !sourceObjectId) {
    return [];
  }

  return (prompt.candidates ?? []).filter((candidate) =>
    (candidate.sources ?? []).some((source) => source.id === sourceObjectId));
}

function commandForSourceCandidate(
  candidate: ActionPromptCandidateDto,
  sourceObjectId: string | undefined
): GameCommand | undefined {
  if (!sourceObjectId || !candidate.enabled) {
    return undefined;
  }

  if (candidate.action === "TAP_RUNE") {
    return { cmdType: "TAP_RUNE", sourceObjectId };
  }

  if (candidate.action === "RECYCLE_RUNE") {
    return { cmdType: "RECYCLE_RUNE", sourceObjectId };
  }

  return undefined;
}

function formatLocation(location?: Record<string, unknown> | null): string {
  if (!location) {
    return "服务端未公开";
  }

  const playerId = typeof location.playerId === "string" ? location.playerId : "";
  const zone = typeof location.zone === "string" ? location.zone : "";
  const battlefield = typeof location.battlefieldObjectId === "string" ? location.battlefieldObjectId : "";
  return [playerId, zone, battlefield].filter(Boolean).join(" / ") || "服务端未公开";
}

type PlayCardSourceRequirement = {
  sourceObjectId: string;
  cardNo: string;
  displayName: string;
  mode?: string;
  modeLabel: string;
  minimumManaCost: number;
  minTargetCount: number;
  maxTargetCount: number;
  targetCountLabel: string;
  targetScopeLabel: string;
  allowsRepeatedTargets: boolean;
  targetChoicesByIndex: Record<string, ActionPromptChoiceDto[]>;
  destinationChoices: ActionPromptChoiceDto[];
  optionalCostChoices: ActionPromptChoiceDto[];
  composable: boolean;
  unsupportedReason?: string;
};

type MoveUnitSourceRequirement = {
  sourceObjectId: string;
  origin: string;
  originLabel: string;
  mode: string;
  modeLabel: string;
  destinationChoices: ActionPromptChoiceDto[];
  optionalCostChoices: ActionPromptChoiceDto[];
  requiredOptionalCosts: string[];
  composable: boolean;
  unsupportedReason?: string;
};

type AssembleEquipmentSourceRequirement = {
  sourceObjectId: string;
  equipmentCardNo: string;
  displayName: string;
  targetChoices: ActionPromptChoiceDto[];
  optionalCostChoices: ActionPromptChoiceDto[];
  requiredOptionalCosts: string[];
  powerCost: number;
  composable: boolean;
  unsupportedReason?: string;
};

type ActivateAbilitySourceRequirement = {
  sourceObjectId: string;
  cardNo: string;
  abilityId: string;
  displayName: string;
  abilityLabel: string;
  manaCost: number;
  powerCost: number;
  minTargetCount: number;
  maxTargetCount: number;
  targetCountLabel: string;
  targetScopeLabel: string;
  targetChoicesByIndex: Record<string, ActionPromptChoiceDto[]>;
  optionalCostChoices: ActionPromptChoiceDto[];
  requiredOptionalCosts: string[];
  exhaustsSource: boolean;
  resolvesImmediately: boolean;
  composable: boolean;
  unsupportedReason?: string;
};

type LegendActionSourceRequirement = {
  sourceObjectId: string;
  cardNo: string;
  abilityId: string;
  displayName: string;
  abilityLabel: string;
  manaCost: number;
  experienceCost: number;
  minTargetCount: number;
  maxTargetCount: number;
  targetCountLabel: string;
  targetScopeLabel: string;
  targetChoicesByIndex: Record<string, ActionPromptChoiceDto[]>;
  optionalCostChoices: ActionPromptChoiceDto[];
  requiredOptionalCosts: string[];
  timingLabel: string;
  exhaustsSource: boolean;
  resolvesImmediately: boolean;
  composable: boolean;
  unsupportedReason?: string;
};

type DeclareBattleSourceRequirement = {
  sourceObjectId: string;
  cardNo: string;
  displayName: string;
  minDefenderCount: number;
  maxDefenderCount: number;
  defenderCountLabel: string;
  targetChoicesByIndex: Record<string, ActionPromptChoiceDto[]>;
  battlefieldChoices: ActionPromptChoiceDto[];
  optionalCostChoices: ActionPromptChoiceDto[];
  requiredOptionalCosts: string[];
  composable: boolean;
  unsupportedReason?: string;
};

function PlayCardComposer({
  candidate,
  card,
  onClose,
  onCommand,
  sourceObjectId
}: {
  candidate: ActionPromptCandidateDto;
  card: InspectedCard;
  onClose: () => void;
  onCommand?: (command: GameCommand) => void;
  sourceObjectId?: string;
}) {
  const requirements = useMemo(
    () => playCardRequirementsFor(candidate, sourceObjectId),
    [candidate, sourceObjectId]
  );
  const [selectedMode, setSelectedMode] = useState<string>("");
  const [targetSelections, setTargetSelections] = useState<Record<number, string>>({});
  const [destination, setDestination] = useState<string>("");
  const [optionalCosts, setOptionalCosts] = useState<string[]>([]);

  useEffect(() => {
    if (requirements.length === 0) {
      setSelectedMode("");
      return;
    }

    const hasCurrentMode = requirements.some((requirement) => normalizedMode(requirement.mode) === selectedMode);
    if (!hasCurrentMode) {
      setSelectedMode(normalizedMode(requirements[0].mode));
    }
  }, [requirements, selectedMode]);

  const selectedRequirement = requirements.find((requirement) => normalizedMode(requirement.mode) === selectedMode) ?? requirements[0];
  const requirementKey = selectedRequirement
    ? `${selectedRequirement.sourceObjectId}:${normalizedMode(selectedRequirement.mode)}`
    : "";

  useEffect(() => {
    setTargetSelections({});
    setOptionalCosts([]);
    setDestination(selectedRequirement?.destinationChoices[0]?.id ?? "");
  }, [requirementKey, selectedRequirement]);

  if (!sourceObjectId || !card.object?.cardNo) {
    return (
      <article className="play-card-composer">
        <p className="detail-muted">服务端 snapshot 未公开该来源的对象 ID 或卡号，前端不会构造出牌命令。</p>
      </article>
    );
  }

  if (requirements.length === 0 || !selectedRequirement) {
    return (
      <article className="play-card-composer">
        <div className="composer-heading">
          <strong>{promptActionLabel(candidate)}</strong>
          <span>{candidate.reason}</span>
        </div>
        <p className="detail-muted">服务端尚未为这张牌提供可提交的出牌约束。</p>
      </article>
    );
  }

  const targetSlots = Array.from({ length: selectedRequirement.maxTargetCount }, (_, index) => index);
  const orderedTargets = targetSlots
    .map((targetIndex) => targetSelections[targetIndex])
    .filter((targetId): targetId is string => Boolean(targetId));
  const hasTargetGap = targetSlots.some((targetIndex) =>
    !targetSelections[targetIndex] && targetSlots.slice(targetIndex + 1).some((laterIndex) => Boolean(targetSelections[laterIndex])));
  const hasDuplicateTarget = !selectedRequirement.allowsRepeatedTargets
    && new Set(orderedTargets).size !== orderedTargets.length;
  const missingRequiredTargetChoice = targetSlots
    .slice(0, selectedRequirement.minTargetCount)
    .some((targetIndex) => (selectedRequirement.targetChoicesByIndex[String(targetIndex)] ?? []).length === 0);
  const targetCountValid = orderedTargets.length >= selectedRequirement.minTargetCount
    && orderedTargets.length <= selectedRequirement.maxTargetCount
    && !hasTargetGap
    && !hasDuplicateTarget
    && !missingRequiredTargetChoice;
  const canSubmit = Boolean(
    candidate.enabled
    && selectedRequirement.composable
    && targetCountValid
    && onCommand
  );

  return (
    <article className="play-card-composer">
      <div className="composer-heading">
        <strong>{promptActionLabel(candidate)}</strong>
        <span>{candidate.reason}</span>
      </div>
      <div className="composer-meta">
        <span>费用 {selectedRequirement.minimumManaCost}</span>
        <span>目标 {selectedRequirement.targetCountLabel} 个</span>
        <span>{selectedRequirement.targetScopeLabel}</span>
      </div>
      {requirements.length > 1 && (
        <ChoiceGroup label="模式">
          {requirements.map((requirement) => (
            <ChoiceButton
              active={normalizedMode(requirement.mode) === selectedMode}
              key={`${requirement.sourceObjectId}-${normalizedMode(requirement.mode)}`}
              onClick={() => setSelectedMode(normalizedMode(requirement.mode))}
            >
              {requirement.modeLabel}
            </ChoiceButton>
          ))}
        </ChoiceGroup>
      )}
      {selectedRequirement.destinationChoices.length > 0 && (
        <ChoiceGroup label="目的地">
          {selectedRequirement.destinationChoices.map((choice) => (
            <ChoiceButton
              active={destination === choice.id}
              key={choice.id}
              onClick={() => setDestination(choice.id)}
              title={choice.reason ?? undefined}
            >
              {choice.label}
            </ChoiceButton>
          ))}
        </ChoiceGroup>
      )}
      {targetSlots.map((targetIndex) => {
        const choices = selectedRequirement.targetChoicesByIndex[String(targetIndex)] ?? [];
        const required = targetIndex < selectedRequirement.minTargetCount;
        return (
          <ChoiceGroup key={targetIndex} label={`目标 ${targetIndex + 1}${required ? "" : "（可选）"}`}>
            {!required && (
              <ChoiceButton
                active={!targetSelections[targetIndex]}
                onClick={() => setTargetSelections((current) => withoutTargetAt(current, targetIndex))}
              >
                不选择
              </ChoiceButton>
            )}
            {choices.length === 0 && <span className="composer-warning">服务端没有给出该目标槽候选。</span>}
            {choices.map((choice) => {
              const alreadySelected = !selectedRequirement.allowsRepeatedTargets
                && orderedTargets.includes(choice.id)
                && targetSelections[targetIndex] !== choice.id;
              return (
                <ChoiceButton
                  active={targetSelections[targetIndex] === choice.id}
                  disabled={alreadySelected}
                  key={choice.id}
                  onClick={() => {
                    setTargetSelections((current) => ({ ...current, [targetIndex]: choice.id }));
                  }}
                  title={choice.reason ?? undefined}
                >
                  {choice.label}
                </ChoiceButton>
              );
            })}
          </ChoiceGroup>
        );
      })}
      {selectedRequirement.optionalCostChoices.length > 0 && (
        <ChoiceGroup label="可选费用">
          {selectedRequirement.optionalCostChoices.map((choice) => (
            <ChoiceButton
              active={optionalCosts.includes(choice.id)}
              key={choice.id}
              onClick={() => setOptionalCosts((current) => toggleValue(current, choice.id))}
              title={choice.reason ?? undefined}
            >
              {choice.label}
            </ChoiceButton>
          ))}
        </ChoiceGroup>
      )}
      {!selectedRequirement.composable && (
        <p className="composer-warning">{selectedRequirement.unsupportedReason || "服务端标记该操作当前不能由前端组合提交。"}</p>
      )}
      {!targetCountValid && selectedRequirement.composable && (
        <p className="composer-warning">请按服务端目标槽候选完成目标选择。</p>
      )}
      <Button
        disabled={!canSubmit}
        icon={<Check size={16} />}
        onClick={() => {
          if (!canSubmit || !onCommand) {
            return;
          }

          onCommand({
            cmdType: "PLAY_CARD",
            sourceObjectId,
            cardNo: selectedRequirement.cardNo,
            targetObjectIds: orderedTargets,
            mode: selectedRequirement.mode || undefined,
            destination: destination || undefined,
            optionalCosts: optionalCosts.length > 0 ? optionalCosts : undefined
          });
          onClose();
        }}
        variant={canSubmit ? "primary" : "ghost"}
      >
        确认打出
      </Button>
    </article>
  );
}

function MoveUnitComposer({
  candidate,
  onClose,
  onCommand,
  sourceObjectId
}: {
  candidate: ActionPromptCandidateDto;
  onClose: () => void;
  onCommand?: (command: GameCommand) => void;
  sourceObjectId?: string;
}) {
  const requirements = useMemo(
    () => moveUnitRequirementsFor(candidate, sourceObjectId),
    [candidate, sourceObjectId]
  );
  const [selectedMode, setSelectedMode] = useState<string>("");
  const [destination, setDestination] = useState<string>("");
  const [optionalCosts, setOptionalCosts] = useState<string[]>([]);

  useEffect(() => {
    if (requirements.length === 0) {
      setSelectedMode("");
      return;
    }

    const hasCurrentMode = requirements.some((requirement) => requirement.mode === selectedMode);
    if (!hasCurrentMode) {
      setSelectedMode(requirements[0].mode);
    }
  }, [requirements, selectedMode]);

  const selectedRequirement = requirements.find((requirement) => requirement.mode === selectedMode) ?? requirements[0];
  const requirementKey = selectedRequirement
    ? `${selectedRequirement.sourceObjectId}:${selectedRequirement.mode}`
    : "";

  useEffect(() => {
    setOptionalCosts([]);
    setDestination(selectedRequirement?.destinationChoices[0]?.id ?? "");
  }, [requirementKey, selectedRequirement]);

  if (!sourceObjectId) {
    return (
      <article className="play-card-composer">
        <p className="detail-muted">服务端 snapshot 未公开该来源的对象 ID，前端不会构造移动命令。</p>
      </article>
    );
  }

  if (requirements.length === 0 || !selectedRequirement) {
    return (
      <article className="play-card-composer">
        <div className="composer-heading">
          <strong>{promptActionLabel(candidate)}</strong>
          <span>{candidate.reason}</span>
        </div>
        <p className="detail-muted">服务端尚未为这张牌提供可提交的移动约束。</p>
      </article>
    );
  }

  const requiredCosts = selectedRequirement.requiredOptionalCosts;
  const optionalChoices = selectedRequirement.optionalCostChoices.filter((choice) => !requiredCosts.includes(choice.id));
  const requiredCostLabels = requiredCosts.map((cost) =>
    selectedRequirement.optionalCostChoices.find((choice) => choice.id === cost)?.label ?? cost);
  const commandOptionalCosts = uniqueStrings([...requiredCosts, ...optionalCosts]);
  const canSubmit = Boolean(
    candidate.enabled
    && selectedRequirement.composable
    && selectedRequirement.origin
    && destination
    && onCommand
  );

  return (
    <article className="play-card-composer">
      <div className="composer-heading">
        <strong>{promptActionLabel(candidate)}</strong>
        <span>{candidate.reason}</span>
      </div>
      <div className="composer-meta">
        <span>来源 {selectedRequirement.originLabel}</span>
        <span>{selectedRequirement.modeLabel}</span>
      </div>
      {requirements.length > 1 && (
        <ChoiceGroup label="移动方式">
          {requirements.map((requirement) => (
            <ChoiceButton
              active={requirement.mode === selectedMode}
              key={`${requirement.sourceObjectId}-${requirement.mode}`}
              onClick={() => setSelectedMode(requirement.mode)}
            >
              {requirement.modeLabel}
            </ChoiceButton>
          ))}
        </ChoiceGroup>
      )}
      <ChoiceGroup label="目的地">
        {selectedRequirement.destinationChoices.map((choice) => (
          <ChoiceButton
            active={destination === choice.id}
            key={choice.id}
            onClick={() => setDestination(choice.id)}
            title={choice.reason ?? undefined}
          >
            {choice.label}
          </ChoiceButton>
        ))}
      </ChoiceGroup>
      {requiredCosts.length > 0 && (
        <div className="composer-meta">
          {requiredCostLabels.map((costLabel) => (
            <span key={costLabel}>费用 {costLabel}</span>
          ))}
        </div>
      )}
      {optionalChoices.length > 0 && (
        <ChoiceGroup label="可选费用">
          {optionalChoices.map((choice) => (
            <ChoiceButton
              active={optionalCosts.includes(choice.id)}
              key={choice.id}
              onClick={() => setOptionalCosts((current) => toggleValue(current, choice.id))}
              title={choice.reason ?? undefined}
            >
              {choice.label}
            </ChoiceButton>
          ))}
        </ChoiceGroup>
      )}
      {!selectedRequirement.composable && (
        <p className="composer-warning">{selectedRequirement.unsupportedReason || "服务端标记该移动当前不能由前端组合提交。"}</p>
      )}
      <Button
        disabled={!canSubmit}
        icon={<Check size={16} />}
        onClick={() => {
          if (!canSubmit || !onCommand) {
            return;
          }

          onCommand({
            cmdType: "MOVE_UNIT",
            sourceObjectId,
            origin: selectedRequirement.origin,
            destination,
            optionalCosts: commandOptionalCosts.length > 0 ? commandOptionalCosts : undefined
          });
          onClose();
        }}
        variant={canSubmit ? "primary" : "ghost"}
      >
        确认移动
      </Button>
    </article>
  );
}

function AssembleEquipmentComposer({
  candidate,
  onClose,
  onCommand,
  sourceObjectId
}: {
  candidate: ActionPromptCandidateDto;
  onClose: () => void;
  onCommand?: (command: GameCommand) => void;
  sourceObjectId?: string;
}) {
  const requirements = useMemo(
    () => assembleEquipmentRequirementsFor(candidate, sourceObjectId),
    [candidate, sourceObjectId]
  );
  const selectedRequirement = requirements[0];
  const [targetObjectId, setTargetObjectId] = useState<string>("");
  const [optionalCosts, setOptionalCosts] = useState<string[]>([]);

  useEffect(() => {
    setTargetObjectId(selectedRequirement?.targetChoices[0]?.id ?? "");
    setOptionalCosts([]);
  }, [selectedRequirement]);

  if (!sourceObjectId) {
    return (
      <article className="play-card-composer">
        <p className="detail-muted">服务端 snapshot 未公开该装备来源的对象 ID，前端不会构造装配命令。</p>
      </article>
    );
  }

  if (requirements.length === 0 || !selectedRequirement) {
    return (
      <article className="play-card-composer">
        <div className="composer-heading">
          <strong>{promptActionLabel(candidate)}</strong>
          <span>{candidate.reason}</span>
        </div>
        <p className="detail-muted">服务端尚未为这张装备提供可提交的装配约束。</p>
      </article>
    );
  }

  const requiredCosts = selectedRequirement.requiredOptionalCosts;
  const optionalChoices = selectedRequirement.optionalCostChoices.filter((choice) => !requiredCosts.includes(choice.id));
  const requiredCostLabels = requiredCosts.map((cost) =>
    selectedRequirement.optionalCostChoices.find((choice) => choice.id === cost)?.label ?? cost);
  const commandOptionalCosts = uniqueStrings([...requiredCosts, ...optionalCosts]);
  const canSubmit = Boolean(
    candidate.enabled
    && selectedRequirement.composable
    && targetObjectId
    && onCommand
  );

  return (
    <article className="play-card-composer">
      <div className="composer-heading">
        <strong>{promptActionLabel(candidate)}</strong>
        <span>{candidate.reason}</span>
      </div>
      <div className="composer-meta">
        <span>{selectedRequirement.displayName}</span>
        <span>符能费用 {selectedRequirement.powerCost}</span>
      </div>
      <ChoiceGroup label="装配目标">
        {selectedRequirement.targetChoices.length === 0 && <span className="composer-warning">服务端没有给出可装配单位。</span>}
        {selectedRequirement.targetChoices.map((choice) => (
          <ChoiceButton
            active={targetObjectId === choice.id}
            key={choice.id}
            onClick={() => setTargetObjectId(choice.id)}
            title={choice.reason ?? undefined}
          >
            {choice.label}
          </ChoiceButton>
        ))}
      </ChoiceGroup>
      {requiredCosts.length > 0 && (
        <div className="composer-meta">
          {requiredCostLabels.map((costLabel) => (
            <span key={costLabel}>费用 {costLabel}</span>
          ))}
        </div>
      )}
      {optionalChoices.length > 0 && (
        <ChoiceGroup label="可选费用">
          {optionalChoices.map((choice) => (
            <ChoiceButton
              active={optionalCosts.includes(choice.id)}
              key={choice.id}
              onClick={() => setOptionalCosts((current) => toggleValue(current, choice.id))}
              title={choice.reason ?? undefined}
            >
              {choice.label}
            </ChoiceButton>
          ))}
        </ChoiceGroup>
      )}
      {!selectedRequirement.composable && (
        <p className="composer-warning">{selectedRequirement.unsupportedReason || "服务端标记该装配当前不能由前端组合提交。"}</p>
      )}
      <Button
        disabled={!canSubmit}
        icon={<Check size={16} />}
        onClick={() => {
          if (!canSubmit || !onCommand) {
            return;
          }

          onCommand({
            cmdType: "ASSEMBLE_EQUIPMENT",
            sourceObjectId,
            targetObjectId,
            optionalCosts: commandOptionalCosts.length > 0 ? commandOptionalCosts : undefined
          });
          onClose();
        }}
        variant={canSubmit ? "primary" : "ghost"}
      >
        确认装配
      </Button>
    </article>
  );
}

function ActivateAbilityComposer({
  candidate,
  onClose,
  onCommand,
  sourceObjectId
}: {
  candidate: ActionPromptCandidateDto;
  onClose: () => void;
  onCommand?: (command: GameCommand) => void;
  sourceObjectId?: string;
}) {
  const requirements = useMemo(
    () => activateAbilityRequirementsFor(candidate, sourceObjectId),
    [candidate, sourceObjectId]
  );
  const [selectedAbilityId, setSelectedAbilityId] = useState<string>("");
  const [targetSelections, setTargetSelections] = useState<Record<number, string>>({});
  const [optionalCosts, setOptionalCosts] = useState<string[]>([]);

  useEffect(() => {
    if (requirements.length === 0) {
      setSelectedAbilityId("");
      return;
    }

    const hasCurrentAbility = requirements.some((requirement) => requirement.abilityId === selectedAbilityId);
    if (!hasCurrentAbility) {
      setSelectedAbilityId(requirements[0].abilityId);
    }
  }, [requirements, selectedAbilityId]);

  const selectedRequirement = requirements.find((requirement) => requirement.abilityId === selectedAbilityId) ?? requirements[0];
  const requirementKey = selectedRequirement
    ? `${selectedRequirement.sourceObjectId}:${selectedRequirement.abilityId}`
    : "";

  useEffect(() => {
    setTargetSelections({});
    setOptionalCosts([]);
  }, [requirementKey]);

  if (!sourceObjectId) {
    return (
      <article className="play-card-composer">
        <p className="detail-muted">服务端 snapshot 未公开该来源的对象 ID，前端不会构造能力命令。</p>
      </article>
    );
  }

  if (requirements.length === 0 || !selectedRequirement) {
    return (
      <article className="play-card-composer">
        <div className="composer-heading">
          <strong>{promptActionLabel(candidate)}</strong>
          <span>{candidate.reason}</span>
        </div>
        <p className="detail-muted">服务端尚未为这张牌提供可提交的激活能力约束。</p>
      </article>
    );
  }

  const targetSlots = Array.from({ length: selectedRequirement.maxTargetCount }, (_, index) => index);
  const orderedTargets = targetSlots
    .map((targetIndex) => targetSelections[targetIndex])
    .filter((targetId): targetId is string => Boolean(targetId));
  const hasTargetGap = targetSlots.some((targetIndex) =>
    !targetSelections[targetIndex] && targetSlots.slice(targetIndex + 1).some((laterIndex) => Boolean(targetSelections[laterIndex])));
  const missingRequiredTargetChoice = targetSlots
    .slice(0, selectedRequirement.minTargetCount)
    .some((targetIndex) => (selectedRequirement.targetChoicesByIndex[String(targetIndex)] ?? []).length === 0);
  const targetCountValid = orderedTargets.length >= selectedRequirement.minTargetCount
    && orderedTargets.length <= selectedRequirement.maxTargetCount
    && !hasTargetGap
    && !missingRequiredTargetChoice;
  const requiredCosts = selectedRequirement.requiredOptionalCosts;
  const optionalChoices = selectedRequirement.optionalCostChoices.filter((choice) => !requiredCosts.includes(choice.id));
  const requiredCostLabels = requiredCosts.map((cost) =>
    selectedRequirement.optionalCostChoices.find((choice) => choice.id === cost)?.label ?? cost);
  const commandOptionalCosts = uniqueStrings([...requiredCosts, ...optionalCosts]);
  const canSubmit = Boolean(
    candidate.enabled
    && selectedRequirement.composable
    && targetCountValid
    && onCommand
  );

  return (
    <article className="play-card-composer">
      <div className="composer-heading">
        <strong>{promptActionLabel(candidate)}</strong>
        <span>{candidate.reason}</span>
      </div>
      <div className="composer-meta">
        <span>{selectedRequirement.displayName}</span>
        <span>法力 {selectedRequirement.manaCost}</span>
        <span>符能 {selectedRequirement.powerCost}</span>
        <span>目标 {selectedRequirement.targetCountLabel} 个</span>
        {selectedRequirement.exhaustsSource && <span>横置来源</span>}
        {selectedRequirement.resolvesImmediately && <span>立即结算</span>}
      </div>
      {requirements.length > 1 && (
        <ChoiceGroup label="能力">
          {requirements.map((requirement) => (
            <ChoiceButton
              active={requirement.abilityId === selectedAbilityId}
              key={`${requirement.sourceObjectId}-${requirement.abilityId}`}
              onClick={() => setSelectedAbilityId(requirement.abilityId)}
            >
              {requirement.abilityLabel}
            </ChoiceButton>
          ))}
        </ChoiceGroup>
      )}
      <div className="composer-meta">
        <span>{selectedRequirement.abilityLabel}</span>
        <span>{selectedRequirement.targetScopeLabel}</span>
      </div>
      {targetSlots.map((targetIndex) => {
        const choices = selectedRequirement.targetChoicesByIndex[String(targetIndex)] ?? [];
        const required = targetIndex < selectedRequirement.minTargetCount;
        return (
          <ChoiceGroup key={targetIndex} label={`目标 ${targetIndex + 1}${required ? "" : "（可选）"}`}>
            {!required && (
              <ChoiceButton
                active={!targetSelections[targetIndex]}
                onClick={() => setTargetSelections((current) => withoutTargetAt(current, targetIndex))}
              >
                不选择
              </ChoiceButton>
            )}
            {choices.length === 0 && <span className="composer-warning">服务端没有给出该目标槽候选。</span>}
            {choices.map((choice) => (
              <ChoiceButton
                active={targetSelections[targetIndex] === choice.id}
                key={choice.id}
                onClick={() => setTargetSelections((current) => ({ ...current, [targetIndex]: choice.id }))}
                title={choice.reason ?? undefined}
              >
                {choice.label}
              </ChoiceButton>
            ))}
          </ChoiceGroup>
        );
      })}
      {requiredCosts.length > 0 && (
        <div className="composer-meta">
          {requiredCostLabels.map((costLabel) => (
            <span key={costLabel}>费用 {costLabel}</span>
          ))}
        </div>
      )}
      {optionalChoices.length > 0 && (
        <ChoiceGroup label="可选费用">
          {optionalChoices.map((choice) => (
            <ChoiceButton
              active={optionalCosts.includes(choice.id)}
              key={choice.id}
              onClick={() => setOptionalCosts((current) => toggleValue(current, choice.id))}
              title={choice.reason ?? undefined}
            >
              {choice.label}
            </ChoiceButton>
          ))}
        </ChoiceGroup>
      )}
      {!selectedRequirement.composable && (
        <p className="composer-warning">{selectedRequirement.unsupportedReason || "服务端标记该能力当前不能由前端组合提交。"}</p>
      )}
      {!targetCountValid && selectedRequirement.composable && (
        <p className="composer-warning">请按服务端目标槽候选完成目标选择。</p>
      )}
      <Button
        disabled={!canSubmit}
        icon={<Check size={16} />}
        onClick={() => {
          if (!canSubmit || !onCommand) {
            return;
          }

          onCommand({
            cmdType: "ACTIVATE_ABILITY",
            sourceObjectId,
            abilityId: selectedRequirement.abilityId,
            targetObjectIds: orderedTargets,
            optionalCosts: commandOptionalCosts.length > 0 ? commandOptionalCosts : undefined
          });
          onClose();
        }}
        variant={canSubmit ? "primary" : "ghost"}
      >
        确认激活
      </Button>
    </article>
  );
}

function LegendActionComposer({
  candidate,
  onClose,
  onCommand,
  sourceObjectId
}: {
  candidate: ActionPromptCandidateDto;
  onClose: () => void;
  onCommand?: (command: GameCommand) => void;
  sourceObjectId?: string;
}) {
  const requirements = useMemo(
    () => legendActionRequirementsFor(candidate, sourceObjectId),
    [candidate, sourceObjectId]
  );
  const [selectedAbilityId, setSelectedAbilityId] = useState<string>("");
  const [targetSelections, setTargetSelections] = useState<Record<number, string>>({});

  useEffect(() => {
    if (requirements.length === 0) {
      setSelectedAbilityId("");
      return;
    }

    const hasCurrentAbility = requirements.some((requirement) => requirement.abilityId === selectedAbilityId);
    if (!hasCurrentAbility) {
      setSelectedAbilityId(requirements[0].abilityId);
    }
  }, [requirements, selectedAbilityId]);

  const selectedRequirement = requirements.find((requirement) => requirement.abilityId === selectedAbilityId) ?? requirements[0];
  const requirementKey = selectedRequirement
    ? `${selectedRequirement.sourceObjectId}:${selectedRequirement.abilityId}`
    : "";

  useEffect(() => {
    setTargetSelections({});
  }, [requirementKey]);

  if (!sourceObjectId) {
    return (
      <article className="play-card-composer">
        <p className="detail-muted">服务端 snapshot 未公开该传奇来源的对象 ID，前端不会构造传奇行动命令。</p>
      </article>
    );
  }

  if (requirements.length === 0 || !selectedRequirement) {
    return (
      <article className="play-card-composer">
        <div className="composer-heading">
          <strong>{promptActionLabel(candidate)}</strong>
          <span>{candidate.reason}</span>
        </div>
        <p className="detail-muted">服务端尚未为这张传奇提供可提交的传奇行动约束。</p>
      </article>
    );
  }

  const targetSlots = Array.from({ length: selectedRequirement.maxTargetCount }, (_, index) => index);
  const orderedTargets = targetSlots
    .map((targetIndex) => targetSelections[targetIndex])
    .filter((targetId): targetId is string => Boolean(targetId));
  const hasTargetGap = targetSlots.some((targetIndex) =>
    !targetSelections[targetIndex] && targetSlots.slice(targetIndex + 1).some((laterIndex) => Boolean(targetSelections[laterIndex])));
  const missingRequiredTargetChoice = targetSlots
    .slice(0, selectedRequirement.minTargetCount)
    .some((targetIndex) => (selectedRequirement.targetChoicesByIndex[String(targetIndex)] ?? []).length === 0);
  const targetCountValid = orderedTargets.length >= selectedRequirement.minTargetCount
    && orderedTargets.length <= selectedRequirement.maxTargetCount
    && !hasTargetGap
    && !missingRequiredTargetChoice;
  const requiredCosts = selectedRequirement.requiredOptionalCosts;
  const requiredCostLabels = requiredCosts.map((cost) =>
    selectedRequirement.optionalCostChoices.find((choice) => choice.id === cost)?.label ?? cost);
  const canSubmit = Boolean(
    candidate.enabled
    && selectedRequirement.composable
    && targetCountValid
    && onCommand
  );

  return (
    <article className="play-card-composer">
      <div className="composer-heading">
        <strong>{promptActionLabel(candidate)}</strong>
        <span>{candidate.reason}</span>
      </div>
      <div className="composer-meta">
        <span>{selectedRequirement.displayName}</span>
        <span>{selectedRequirement.timingLabel}</span>
        <span>法力 {selectedRequirement.manaCost}</span>
        <span>经验 {selectedRequirement.experienceCost}</span>
        <span>目标 {selectedRequirement.targetCountLabel} 个</span>
        {selectedRequirement.exhaustsSource && <span>横置传奇</span>}
        {selectedRequirement.resolvesImmediately && <span>立即结算</span>}
      </div>
      {requirements.length > 1 && (
        <ChoiceGroup label="能力">
          {requirements.map((requirement) => (
            <ChoiceButton
              active={requirement.abilityId === selectedAbilityId}
              key={`${requirement.sourceObjectId}-${requirement.abilityId}`}
              onClick={() => setSelectedAbilityId(requirement.abilityId)}
            >
              {requirement.abilityLabel}
            </ChoiceButton>
          ))}
        </ChoiceGroup>
      )}
      <div className="composer-meta">
        <span>{selectedRequirement.abilityLabel}</span>
        <span>{selectedRequirement.targetScopeLabel}</span>
      </div>
      {targetSlots.map((targetIndex) => {
        const choices = selectedRequirement.targetChoicesByIndex[String(targetIndex)] ?? [];
        const required = targetIndex < selectedRequirement.minTargetCount;
        return (
          <ChoiceGroup key={targetIndex} label={`目标 ${targetIndex + 1}${required ? "" : "（可选）"}`}>
            {!required && (
              <ChoiceButton
                active={!targetSelections[targetIndex]}
                onClick={() => setTargetSelections((current) => withoutTargetAt(current, targetIndex))}
              >
                不选择
              </ChoiceButton>
            )}
            {choices.length === 0 && <span className="composer-warning">服务端没有给出该目标槽候选。</span>}
            {choices.map((choice) => (
              <ChoiceButton
                active={targetSelections[targetIndex] === choice.id}
                key={choice.id}
                onClick={() => setTargetSelections((current) => ({ ...current, [targetIndex]: choice.id }))}
                title={choice.reason ?? undefined}
              >
                {choice.label}
              </ChoiceButton>
            ))}
          </ChoiceGroup>
        );
      })}
      {requiredCosts.length > 0 && (
        <div className="composer-meta">
          {requiredCostLabels.map((costLabel) => (
            <span key={costLabel}>费用 {costLabel}</span>
          ))}
        </div>
      )}
      {!selectedRequirement.composable && (
        <p className="composer-warning">{selectedRequirement.unsupportedReason || "服务端标记该传奇行动当前不能由前端组合提交。"}</p>
      )}
      {!targetCountValid && selectedRequirement.composable && (
        <p className="composer-warning">请按服务端目标槽候选完成目标选择。</p>
      )}
      <Button
        disabled={!canSubmit}
        icon={<Check size={16} />}
        onClick={() => {
          if (!canSubmit || !onCommand) {
            return;
          }

          onCommand({
            cmdType: "LEGEND_ACT",
            sourceObjectId,
            abilityId: selectedRequirement.abilityId,
            targetObjectIds: orderedTargets,
            optionalCosts: requiredCosts.length > 0 ? requiredCosts : undefined
          });
          onClose();
        }}
        variant={canSubmit ? "primary" : "ghost"}
      >
        确认传奇行动
      </Button>
    </article>
  );
}

function DeclareBattleComposer({
  candidate,
  onClose,
  onCommand,
  sourceObjectId
}: {
  candidate: ActionPromptCandidateDto;
  onClose: () => void;
  onCommand?: (command: GameCommand) => void;
  sourceObjectId?: string;
}) {
  const requirements = useMemo(
    () => declareBattleRequirementsFor(candidate, sourceObjectId),
    [candidate, sourceObjectId]
  );
  const selectedRequirement = requirements[0];
  const [defenderSelections, setDefenderSelections] = useState<Record<number, string>>({});
  const [battlefieldId, setBattlefieldId] = useState<string>("");
  const requirementKey = selectedRequirement?.sourceObjectId ?? "";

  useEffect(() => {
    setDefenderSelections({});
    setBattlefieldId(selectedRequirement?.battlefieldChoices[0]?.id ?? "");
  }, [requirementKey, selectedRequirement]);

  if (!sourceObjectId) {
    return (
      <article className="play-card-composer">
        <p className="detail-muted">服务端 snapshot 未公开攻击来源的对象 ID，前端不会构造战斗声明命令。</p>
      </article>
    );
  }

  if (requirements.length === 0 || !selectedRequirement) {
    return (
      <article className="play-card-composer">
        <div className="composer-heading">
          <strong>{promptActionLabel(candidate)}</strong>
          <span>{candidate.reason}</span>
        </div>
        <p className="detail-muted">服务端尚未为这张牌提供可提交的战斗声明约束。</p>
      </article>
    );
  }

  const defenderSlots = Array.from({ length: selectedRequirement.maxDefenderCount }, (_, index) => index);
  const orderedDefenders = defenderSlots
    .map((targetIndex) => defenderSelections[targetIndex])
    .filter((targetId): targetId is string => Boolean(targetId));
  const hasTargetGap = defenderSlots.some((targetIndex) =>
    !defenderSelections[targetIndex] && defenderSlots.slice(targetIndex + 1).some((laterIndex) => Boolean(defenderSelections[laterIndex])));
  const missingRequiredDefenderChoice = defenderSlots
    .slice(0, selectedRequirement.minDefenderCount)
    .some((targetIndex) => (selectedRequirement.targetChoicesByIndex[String(targetIndex)] ?? []).length === 0);
  const defenderCountValid = orderedDefenders.length >= selectedRequirement.minDefenderCount
    && orderedDefenders.length <= selectedRequirement.maxDefenderCount
    && !hasTargetGap
    && !missingRequiredDefenderChoice;
  const requiredCosts = selectedRequirement.requiredOptionalCosts;
  const requiredCostLabels = requiredCosts.map((cost) =>
    selectedRequirement.optionalCostChoices.find((choice) => choice.id === cost)?.label ?? cost);
  const canSubmit = Boolean(
    candidate.enabled
    && selectedRequirement.composable
    && defenderCountValid
    && battlefieldId
    && onCommand
  );

  return (
    <article className="play-card-composer">
      <div className="composer-heading">
        <strong>{promptActionLabel(candidate)}</strong>
        <span>{candidate.reason}</span>
      </div>
      <div className="composer-meta">
        <span>{selectedRequirement.displayName}</span>
        <span>攻击者 1</span>
        <span>防守者 {selectedRequirement.defenderCountLabel}</span>
      </div>
      <ChoiceGroup label="战场">
        {selectedRequirement.battlefieldChoices.map((choice) => (
          <ChoiceButton
            active={battlefieldId === choice.id}
            key={choice.id}
            onClick={() => setBattlefieldId(choice.id)}
            title={choice.reason ?? undefined}
          >
            {choice.label}
          </ChoiceButton>
        ))}
      </ChoiceGroup>
      {defenderSlots.map((targetIndex) => {
        const choices = selectedRequirement.targetChoicesByIndex[String(targetIndex)] ?? [];
        const required = targetIndex < selectedRequirement.minDefenderCount;
        return (
          <ChoiceGroup key={targetIndex} label={`防守单位 ${targetIndex + 1}${required ? "" : "（可选）"}`}>
            {!required && (
              <ChoiceButton
                active={!defenderSelections[targetIndex]}
                onClick={() => setDefenderSelections((current) => withoutTargetAt(current, targetIndex))}
              >
                不选择
              </ChoiceButton>
            )}
            {choices.length === 0 && <span className="composer-warning">服务端没有给出该防守槽候选。</span>}
            {choices.map((choice) => {
              const alreadySelected = orderedDefenders.includes(choice.id)
                && defenderSelections[targetIndex] !== choice.id;
              return (
                <ChoiceButton
                  active={defenderSelections[targetIndex] === choice.id}
                  disabled={alreadySelected}
                  key={choice.id}
                  onClick={() => setDefenderSelections((current) => ({ ...current, [targetIndex]: choice.id }))}
                  title={choice.reason ?? undefined}
                >
                  {choice.label}
                </ChoiceButton>
              );
            })}
          </ChoiceGroup>
        );
      })}
      {requiredCosts.length > 0 && (
        <div className="composer-meta">
          {requiredCostLabels.map((costLabel) => (
            <span key={costLabel}>费用 {costLabel}</span>
          ))}
        </div>
      )}
      {!selectedRequirement.composable && (
        <p className="composer-warning">{selectedRequirement.unsupportedReason || "服务端标记该战斗声明当前不能由前端组合提交。"}</p>
      )}
      {!defenderCountValid && selectedRequirement.composable && (
        <p className="composer-warning">请按服务端防守槽候选完成防守单位选择。</p>
      )}
      <Button
        disabled={!canSubmit}
        icon={<Check size={16} />}
        onClick={() => {
          if (!canSubmit || !onCommand) {
            return;
          }

          onCommand({
            cmdType: "DECLARE_BATTLE",
            battlefieldId,
            attackerObjectIds: [sourceObjectId],
            defenderObjectIds: orderedDefenders,
            optionalCosts: requiredCosts.length > 0 ? requiredCosts : undefined
          });
          onClose();
        }}
        variant={canSubmit ? "primary" : "ghost"}
      >
        确认声明战斗
      </Button>
    </article>
  );
}

function ChoiceGroup({ children, label }: { children: ReactNode; label: string }) {
  return (
    <div className="composer-choice-group">
      <span>{label}</span>
      <div className="composer-choice-list">{children}</div>
    </div>
  );
}

function ChoiceButton({
  active,
  children,
  disabled,
  onClick,
  title
}: {
  active: boolean;
  children: ReactNode;
  disabled?: boolean;
  onClick: () => void;
  title?: string;
}) {
  return (
    <button
      className={`composer-choice${active ? " is-active" : ""}`}
      disabled={disabled}
      onClick={onClick}
      title={title}
      type="button"
    >
      {children}
    </button>
  );
}

function playCardRequirementsFor(candidate: ActionPromptCandidateDto, sourceObjectId?: string): PlayCardSourceRequirement[] {
  if (!sourceObjectId) {
    return [];
  }

  const rawRequirements = candidate.metadata?.sourceRequirements;
  if (!Array.isArray(rawRequirements)) {
    return [];
  }

  return rawRequirements
    .map(parsePlayCardRequirement)
    .filter((requirement): requirement is PlayCardSourceRequirement =>
      Boolean(requirement && requirement.sourceObjectId === sourceObjectId));
}

function moveUnitRequirementsFor(candidate: ActionPromptCandidateDto, sourceObjectId?: string): MoveUnitSourceRequirement[] {
  if (!sourceObjectId) {
    return [];
  }

  const rawRequirements = candidate.metadata?.sourceRequirements;
  if (!Array.isArray(rawRequirements)) {
    return [];
  }

  return rawRequirements
    .map(parseMoveUnitRequirement)
    .filter((requirement): requirement is MoveUnitSourceRequirement =>
      Boolean(requirement && requirement.sourceObjectId === sourceObjectId));
}

function assembleEquipmentRequirementsFor(
  candidate: ActionPromptCandidateDto,
  sourceObjectId?: string
): AssembleEquipmentSourceRequirement[] {
  if (!sourceObjectId) {
    return [];
  }

  const rawRequirements = candidate.metadata?.sourceRequirements;
  if (!Array.isArray(rawRequirements)) {
    return [];
  }

  return rawRequirements
    .map(parseAssembleEquipmentRequirement)
    .filter((requirement): requirement is AssembleEquipmentSourceRequirement =>
      Boolean(requirement && requirement.sourceObjectId === sourceObjectId));
}

function activateAbilityRequirementsFor(
  candidate: ActionPromptCandidateDto,
  sourceObjectId?: string
): ActivateAbilitySourceRequirement[] {
  if (!sourceObjectId) {
    return [];
  }

  const rawRequirements = candidate.metadata?.sourceRequirements;
  if (!Array.isArray(rawRequirements)) {
    return [];
  }

  return rawRequirements
    .map(parseActivateAbilityRequirement)
    .filter((requirement): requirement is ActivateAbilitySourceRequirement =>
      Boolean(requirement && requirement.sourceObjectId === sourceObjectId));
}

function legendActionRequirementsFor(
  candidate: ActionPromptCandidateDto,
  sourceObjectId?: string
): LegendActionSourceRequirement[] {
  if (!sourceObjectId) {
    return [];
  }

  const rawRequirements = candidate.metadata?.sourceRequirements;
  if (!Array.isArray(rawRequirements)) {
    return [];
  }

  return rawRequirements
    .map(parseLegendActionRequirement)
    .filter((requirement): requirement is LegendActionSourceRequirement =>
      Boolean(requirement && requirement.sourceObjectId === sourceObjectId));
}

function declareBattleRequirementsFor(
  candidate: ActionPromptCandidateDto,
  sourceObjectId?: string
): DeclareBattleSourceRequirement[] {
  if (!sourceObjectId) {
    return [];
  }

  const rawRequirements = candidate.metadata?.sourceRequirements;
  if (!Array.isArray(rawRequirements)) {
    return [];
  }

  return rawRequirements
    .map(parseDeclareBattleRequirement)
    .filter((requirement): requirement is DeclareBattleSourceRequirement =>
      Boolean(requirement && requirement.sourceObjectId === sourceObjectId));
}

function parseDeclareBattleRequirement(value: unknown): DeclareBattleSourceRequirement | undefined {
  const record = asRecord(value);
  if (!record) {
    return undefined;
  }

  const sourceObjectId = stringField(record, "sourceObjectId");
  if (!sourceObjectId) {
    return undefined;
  }

  return {
    sourceObjectId,
    cardNo: stringField(record, "cardNo"),
    displayName: stringField(record, "displayName") || sourceObjectId,
    minDefenderCount: numberField(record, "minDefenderCount"),
    maxDefenderCount: numberField(record, "maxDefenderCount"),
    defenderCountLabel: stringField(record, "defenderCountLabel") || "1 个防守单位",
    targetChoicesByIndex: choiceRecord(record.targetChoicesByIndex),
    battlefieldChoices: choiceList(record.battlefieldChoices),
    optionalCostChoices: choiceList(record.optionalCostChoices),
    requiredOptionalCosts: stringList(record.requiredOptionalCosts),
    composable: booleanField(record, "composable", true),
    unsupportedReason: nullableStringField(record, "unsupportedReason")
  };
}

function parseLegendActionRequirement(value: unknown): LegendActionSourceRequirement | undefined {
  const record = asRecord(value);
  if (!record) {
    return undefined;
  }

  const sourceObjectId = stringField(record, "sourceObjectId");
  const abilityId = stringField(record, "abilityId");
  if (!sourceObjectId || !abilityId) {
    return undefined;
  }

  return {
    sourceObjectId,
    cardNo: stringField(record, "cardNo"),
    abilityId,
    displayName: stringField(record, "displayName") || abilityId,
    abilityLabel: stringField(record, "abilityLabel") || abilityId,
    manaCost: numberField(record, "manaCost"),
    experienceCost: numberField(record, "experienceCost"),
    minTargetCount: numberField(record, "minTargetCount"),
    maxTargetCount: numberField(record, "maxTargetCount"),
    targetCountLabel: stringField(record, "targetCountLabel") || "0",
    targetScopeLabel: stringField(record, "targetScopeLabel") || "服务端目标",
    targetChoicesByIndex: choiceRecord(record.targetChoicesByIndex),
    optionalCostChoices: choiceList(record.optionalCostChoices),
    requiredOptionalCosts: stringList(record.requiredOptionalCosts),
    timingLabel: stringField(record, "timingLabel") || "服务端窗口",
    exhaustsSource: booleanField(record, "exhaustsSource"),
    resolvesImmediately: booleanField(record, "resolvesImmediately"),
    composable: booleanField(record, "composable", true),
    unsupportedReason: nullableStringField(record, "unsupportedReason")
  };
}

function parseActivateAbilityRequirement(value: unknown): ActivateAbilitySourceRequirement | undefined {
  const record = asRecord(value);
  if (!record) {
    return undefined;
  }

  const sourceObjectId = stringField(record, "sourceObjectId");
  const abilityId = stringField(record, "abilityId");
  if (!sourceObjectId || !abilityId) {
    return undefined;
  }

  return {
    sourceObjectId,
    cardNo: stringField(record, "cardNo"),
    abilityId,
    displayName: stringField(record, "displayName") || abilityId,
    abilityLabel: stringField(record, "abilityLabel") || abilityId,
    manaCost: numberField(record, "manaCost"),
    powerCost: numberField(record, "powerCost"),
    minTargetCount: numberField(record, "minTargetCount"),
    maxTargetCount: numberField(record, "maxTargetCount"),
    targetCountLabel: stringField(record, "targetCountLabel") || "0",
    targetScopeLabel: stringField(record, "targetScopeLabel") || "服务端目标",
    targetChoicesByIndex: choiceRecord(record.targetChoicesByIndex),
    optionalCostChoices: choiceList(record.optionalCostChoices),
    requiredOptionalCosts: stringList(record.requiredOptionalCosts),
    exhaustsSource: booleanField(record, "exhaustsSource"),
    resolvesImmediately: booleanField(record, "resolvesImmediately"),
    composable: booleanField(record, "composable", true),
    unsupportedReason: nullableStringField(record, "unsupportedReason")
  };
}

function parseAssembleEquipmentRequirement(value: unknown): AssembleEquipmentSourceRequirement | undefined {
  const record = asRecord(value);
  if (!record) {
    return undefined;
  }

  const sourceObjectId = stringField(record, "sourceObjectId");
  const equipmentCardNo = stringField(record, "equipmentCardNo");
  if (!sourceObjectId || !equipmentCardNo) {
    return undefined;
  }

  return {
    sourceObjectId,
    equipmentCardNo,
    displayName: stringField(record, "displayName") || equipmentCardNo,
    targetChoices: choiceList(record.targetChoices),
    optionalCostChoices: choiceList(record.optionalCostChoices),
    requiredOptionalCosts: stringList(record.requiredOptionalCosts),
    powerCost: numberField(record, "powerCost"),
    composable: booleanField(record, "composable", true),
    unsupportedReason: nullableStringField(record, "unsupportedReason")
  };
}

function parseMoveUnitRequirement(value: unknown): MoveUnitSourceRequirement | undefined {
  const record = asRecord(value);
  if (!record) {
    return undefined;
  }

  const sourceObjectId = stringField(record, "sourceObjectId");
  const origin = stringField(record, "origin");
  const mode = stringField(record, "mode");
  if (!sourceObjectId || !origin || !mode) {
    return undefined;
  }

  return {
    sourceObjectId,
    origin,
    originLabel: stringField(record, "originLabel") || origin,
    mode,
    modeLabel: stringField(record, "modeLabel") || mode,
    destinationChoices: choiceList(record.destinationChoices),
    optionalCostChoices: choiceList(record.optionalCostChoices),
    requiredOptionalCosts: stringList(record.requiredOptionalCosts),
    composable: booleanField(record, "composable", true),
    unsupportedReason: nullableStringField(record, "unsupportedReason")
  };
}

function parsePlayCardRequirement(value: unknown): PlayCardSourceRequirement | undefined {
  const record = asRecord(value);
  if (!record) {
    return undefined;
  }

  const sourceObjectId = stringField(record, "sourceObjectId");
  const cardNo = stringField(record, "cardNo");
  if (!sourceObjectId || !cardNo) {
    return undefined;
  }

  return {
    sourceObjectId,
    cardNo,
    displayName: stringField(record, "displayName") || cardNo,
    mode: nullableStringField(record, "mode"),
    modeLabel: stringField(record, "modeLabel") || "默认",
    minimumManaCost: numberField(record, "minimumManaCost"),
    minTargetCount: numberField(record, "minTargetCount"),
    maxTargetCount: numberField(record, "maxTargetCount"),
    targetCountLabel: stringField(record, "targetCountLabel") || "0",
    targetScopeLabel: stringField(record, "targetScopeLabel") || "服务端目标",
    allowsRepeatedTargets: booleanField(record, "allowsRepeatedTargets"),
    targetChoicesByIndex: choiceRecord(record.targetChoicesByIndex),
    destinationChoices: choiceList(record.destinationChoices),
    optionalCostChoices: choiceList(record.optionalCostChoices),
    composable: booleanField(record, "composable", true),
    unsupportedReason: nullableStringField(record, "unsupportedReason")
  };
}

function choiceRecord(value: unknown): Record<string, ActionPromptChoiceDto[]> {
  const record = asRecord(value);
  if (!record) {
    return {};
  }

  return Object.fromEntries(
    Object.entries(record).map(([key, choices]) => [key, choiceList(choices)])
  );
}

function choiceList(value: unknown): ActionPromptChoiceDto[] {
  if (!Array.isArray(value)) {
    return [];
  }

  return value
    .map((choice): ActionPromptChoiceDto | undefined => {
      const record = asRecord(choice);
      if (!record) {
        return undefined;
      }

      const id = stringField(record, "id");
      const label = stringField(record, "label");
      if (!id || !label) {
        return undefined;
      }

      return {
        id,
        label,
        reason: nullableStringField(record, "reason")
      };
    })
    .filter((choice): choice is ActionPromptChoiceDto => Boolean(choice));
}

function withoutTargetAt(current: Record<number, string>, targetIndex: number): Record<number, string> {
  const next = { ...current };
  delete next[targetIndex];
  return next;
}

function toggleValue(values: string[], value: string): string[] {
  return values.includes(value)
    ? values.filter((current) => current !== value)
    : [...values, value];
}

function uniqueStrings(values: string[]): string[] {
  return Array.from(new Set(values.filter(Boolean)));
}

function normalizedMode(mode?: string): string {
  return mode?.trim() ?? "";
}

function asRecord(value: unknown): Record<string, unknown> | undefined {
  return value && typeof value === "object" && !Array.isArray(value)
    ? value as Record<string, unknown>
    : undefined;
}

function stringField(record: Record<string, unknown>, key: string): string {
  const value = record[key];
  return typeof value === "string" ? value : "";
}

function nullableStringField(record: Record<string, unknown>, key: string): string | undefined {
  const value = record[key];
  return typeof value === "string" && value.trim().length > 0 ? value : undefined;
}

function numberField(record: Record<string, unknown>, key: string): number {
  const value = record[key];
  return typeof value === "number" ? value : 0;
}

function booleanField(record: Record<string, unknown>, key: string, fallback = false): boolean {
  const value = record[key];
  return typeof value === "boolean" ? value : fallback;
}

function stringList(value: unknown): string[] {
  if (!Array.isArray(value)) {
    return [];
  }

  return value.filter((item): item is string => typeof item === "string" && item.trim().length > 0);
}
