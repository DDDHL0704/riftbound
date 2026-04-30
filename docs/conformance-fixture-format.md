# Conformance Fixture 格式

更新时间：2026-04-29

## 1. 目的

Fixture 是规则依据、旧 Java 行为样本与 C# 新引擎之间的行为契约。迁移期间每个高价值场景都应导出同一份输入，并分别记录 PDF/FAQ 裁决、Java legacy oracle 输出与 C# 的事件、快照和提示结果。

核心链路：

```text
seed + initial setup + command log
  -> rules evidence from 5 PDF/FAQ docs + official card text
  -> Java legacy oracle events/snapshots/prompts
  -> C# replay events/snapshots/prompts
  -> canonical JSON diff
```

## 2. 当前最小格式

当前 P1 runner 已支持最小字段：

```json
{
  "schemaVersion": 1,
  "fixtureId": "p1-placeholder-pass-priority",
  "description": "human readable reason",
  "source": "java-oracle | manual-csharp-skeleton",
  "auditStatus": "NEEDS_RULE_AUDIT | RULE_AUDITED",
  "faqVersion": "optional FAQ file/date summary",
  "seed": 2603301001,
  "rulesEvidence": [
    {
      "source": "《符文战场》核心规则_260330.pdf",
      "locator": "page/chapter TBD",
      "note": "short non-verbatim summary"
    }
  ],
  "roomId": "fixture-room",
  "players": ["P1", "P2"],
  "commands": [
    {
      "playerId": "P1",
      "clientIntentId": "intent-pass-priority-1",
      "cmd": {
        "cmdType": "PASS_PRIORITY"
      }
    }
  ],
  "expected": {
    "finalTick": 1,
    "eventKinds": ["PASS_PRIORITY"],
    "promptActions": {
      "P1": ["PLAY_CARD", "ACTIVATE_ABILITY", "ASSEMBLE_EQUIPMENT", "MOVE_UNIT", "HIDE_CARD", "TAP_RUNE", "LEGEND_ACT", "PASS", "END_TURN"],
      "P2": ["WAIT"]
    }
  }
}
```

`expected.eventKinds` 表示写入事件日志的规则事件类型，而不是每次客户端重试返回的响应事件。P1 runner 会先为 fixture 中的玩家自动执行 `READY`，但比较时过滤 `PLAYER_READY` / `MATCH_STARTED` 等房间生命周期事件；重复 `clientIntentId` 必须不重复推进 tick，也不重复写入规则事件日志。`expected.events[]` 可以继续只写 `kind`，也可以按需补 `tick`、`sequence` 和 `payload`；`payload` 是局部匹配，只需要写本 fixture 关心的字段。

现有样例：

- `tests/Riftbound.ConformanceTests/Fixtures/p1-placeholder-pass-priority.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-turn-start.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-turn-start-short-rune-deck.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-turn-start-first-p2-extra-rune.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-turn-start-burnout.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-end-turn-advances-to-next-start.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-end-turn-special-cleanup.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-cleanup-repeats-until-stable.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-pass-priority-does-not-end-turn.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-fepr-priority-pass-resolves-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-fepr-resolves-latest-keeps-remaining-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-spell-duel-pass-focus-closes-window.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-turn-start-burnout-empty-graveyard-wins.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-punishment-damage-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-abyssal-hunt-damage-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-abyssal-hunt-face-down-damage-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-incinerate-damage-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-lotus-trap-doubles-next-damage.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-counterstorm-prevent-next-damage.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-noxian-guillotine-next-damage-destroys.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-imperial-decree-damage-destroys-unit.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-sprite-summon-create-sprite-base.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-sprite-burst-create-two-sprites-base.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-hextech-ray-damage-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-hextech-ray-damage-clears-end-turn.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-thundering-drop-attacking-damage-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-piercing-light-two-target-damage-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-thundering-sky-cost-reduced-damage-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-comet-strike-damage-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-final-spark-damage-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-super-mega-death-rocket-damage-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-center-stage-draw-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-center-stage-echo-draw-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-prophets-omen-draw-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-might-makes-right-draw-powerful-units.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-evolution-day-draw-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-mobilize-call-rune.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-mobilize-draws-if-rune-call-fails.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-catalyst-of-aeons-call-two-runes.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-catalyst-of-aeons-draws-if-rune-call-short.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-mind-and-balance-reduced-draw-then-call-rune.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-vengeance-destroy-unit-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-wellspring-of-hatred-destroy-battlefield-unit.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-detonation-destroy-battlefield-unit-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-hunt-the-weak-destroy-small-battlefield-unit.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-darkin-blade-destroy-target-controller-draw.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-quicksand-pit-destroy-battlefield-unit-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ruination-destroy-all-units.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-undertow-return-all-units.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-reprimand-return-battlefield-unit.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-gust-return-small-battlefield-unit.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-reconsider-return-friendly-call-rune.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-happenstance-return-friendly-and-enemy.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-hurricane-sweep-each-player-return-unit.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-custodian-judgment-unit-to-deck-top.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-custodian-judgment-unit-to-deck-bottom.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-battle-or-flight-move-battlefield-unit-to-base.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ride-the-wind-move-friendly-battlefield-unit-to-base-ready.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-charm-move-enemy-battlefield-unit-to-base.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-rising-dragon-kick-move-enemy-battlefield-unit-to-base.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-isolate-move-enemy-battlefield-unit-to-base.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-flash-move-two-friendly-battlefield-units-to-base.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-shield-wall-move-any-friendly-battlefield-units-to-base.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-playful-tentacles-move-total-power-eight.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-bait-move-enemy-unit-to-another-location.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-the-curtain-rises-echo-ready-unit.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-beatdown-ready-unit.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-hunt-ready-all-friendly-units.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-overcharged-energy-exhaust-friendly-damage-all-battlefield.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-punishment-lethal-damage-banishes-unit.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-punishment-banishes-if-destroyed-later.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-shattered-fire-draws-after-lethal-damage.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-shattered-fire-does-not-draw-without-destroy.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-starfall-damages-two-units.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-starfall-can-damage-same-unit-twice.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-duel-mutual-power-damage.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-gentleman-duel-power-then-mutual-damage.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-marching-orders-echo-mutual-power-damage.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-clash-of-giants-mutual-power-damage.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-icathian-rain-can-hit-same-unit-six-times.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-blade-whirlwind-damage-all-battlefield-units.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-blade-whirlwind-lethal-damage-destroys-units.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-cannon-barrage-damage-enemy-combat-units.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-production-surge-create-robot-draw.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-production-surge-reduced-by-mechanical.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-common-cause-create-four-minions-base.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-featherstorm-create-warhawks.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-sandcraft-echo-create-two-sand-soldiers-base.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-protect-the-emperor-create-sand-soldier.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-stay-away-stun-draw-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-disposal-order-draw-mode.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-disposal-order-recycle-opponent-graveyard.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-covert-sabotage-recycle-opponent-non-unit-hand-card.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-predictive-offensive-draw-one-recycle-other.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-card-trick-draw-one-recycle-rest.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-dragon-tiger-draw-unit-recycle-rest.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-dragon-tiger-no-unit-selection-recycle-all.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-reinforcements-no-selection-recycle-top-five.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-meditation-draw-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-salvage-draw-no-equipment.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-king-of-the-hill-draw-no-controlled-battlefields.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-meditation-exhaust-friendly-extra-draw.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-moonsilver-gift-discard-draw.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-revive-return-graveyard-unit.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-rewind-timeline-discard-hands-draw-four.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-sacrifice-destroy-friendly-powerful-draw-call-rune.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-soul-strangle-destroy-friendly-power-buff-draw.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-center-your-mind-draw-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-borrowed-history-draw-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-spoils-of-war-draw-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-spoils-of-war-reduced-after-enemy-unit-destroyed.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-dancing-grenade-base-unit-damage.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-stellar-convergence-two-target-damage-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-rocket-barrage-base-unit-mode-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-bellows-breath-up-to-three-units-damage.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-firestorm-damage-enemy-battlefield-units.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-crescent-strike-target-plus-splash.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-perfect-finale-draw-mode.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-perfect-finale-battlefield-damage-mode.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-perfect-finale-base-damage-mode.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-perfect-finale-battlefield-power-mode.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-highlander-bloodline-recall-if-destroyed.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-tactical-retreat-recall-if-destroyed.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-void-seeker-damage-draw-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-void-seeker-draw-burnout-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-rune-prison-stun-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-rune-prison-base-unit-stun-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-rune-prison-stun-expires-end-turn.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-kerplunk-stun-attacking-unit.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-kerplunk-echo-stun-attacking-unit.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-existential-dread-echo-stun-then-return.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-housecleaning-destroy-each-player-unit.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-kings-edict-destroy-enemy-unit.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-spirit-fire-destroy-total-power-four.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-zenith-blade-stun-enemy-battlefield-unit.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-skullcrack-stun-friendly-and-enemy-battlefield-units.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-heroic-charge-power-plus-stun.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-highway-robbery-enemy-unit-damage.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-highway-robbery-target-controller-draw-choice.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-last-breath-ready-damage-enemy-battlefield.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-convergent-mutation-match-friendly-power.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-practical-experience-power-plus-1.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-dueling-stance-friendly-power-plus-1.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-animal-friends-power-per-controlled-tag.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-stand-defiant-power-per-enemy-battlefield-unit.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-well-trained-power-draw-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-well-trained-power-expires-end-turn.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-savage-strength-echo-power-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-freeze-echo-power-minus-2.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-distance-break-dance-split-power-modifiers.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-switcheroo-swap-battlefield-unit-powers.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-cleave-overwhelm-attacking-power.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-blood-rush-echo-overwhelm-attacking-power.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-roaring-reckoning-discard-echo-overwhelm-attacking-power.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-power-punch-overwhelm-roam-attacking-power.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-parry-steadfast-barrier-defending-power.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-shoot-first-power-plus-5-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-tremendous-strength-power-plus-7.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-eclipse-power-minus-4.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-eclipse-power-minus-4-insight-recycle.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-moonfall-power-minus-10.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-glory-call-power-plus-3.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-last-stand-friendly-power-plus-3.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-decisive-strike-all-friendly-power-plus-2.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-grand-strategy-all-friendly-power-plus-5.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-back-to-back-two-friendly-power-plus-2.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-power-bind-echo-two-friendly-power-plus-1.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-danger-temperature-mechanical-power-plus-1.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-siphon-energy-battlefield-power-split.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-moonrise-enemy-battlefield-power-minus-2.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-smoke-bomb-power-floor-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-smoke-bomb-power-floor-expires-end-turn.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-extortion-power-floor-draw-stack.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/java-oracle/java-oracle-p1-pass.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/java-oracle/java-oracle-p1-end-turn.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/java-oracle/java-oracle-p1-duplicate-pass.fixture.json`

## 2.1 P2 schema v2 草案

P2 fixture 已开始使用 `schemaVersion = 2`。当前 C# 侧已能读取以下字段，并能把 `initialState` 构造成真实权威 `MatchState`；`p2-preflight-turn-start.fixture.json` 已通过 `CoreRuleEngine` 验证普通回合开始行为，`p2-preflight-end-turn-advances-to-next-start.fixture.json` 已验证 `END_TURN` 后自动推进并结算下一回合开始，`p2-preflight-end-turn-special-cleanup.fixture.json` 已验证 `cardObjects` 中的伤害与本回合内效果会被特殊清理处理，`p2-preflight-cleanup-repeats-until-stable.fixture.json` 已验证特殊清理后的常规清理重复事件，`p2-preflight-pass-priority-does-not-end-turn.fixture.json` 已验证拒绝态不推进 tick 或事件。`ConformanceFixtureRunner.CompareExpected` 已开始通用比较 final tick、event kinds、event tick/sequence/payload 局部字段、prompt actions、最终 timing、符文池、分数、玩家区域、对象状态和结算链；后续继续扩展 snapshots canonical diff：

```json
{
  "schemaVersion": 2,
  "fixtureId": "p2-preflight-turn-start-runes-and-draw",
  "initialState": {
    "turnNumber": 1,
    "activePlayerId": "P2",
    "turnPlayerId": "P2",
    "phase": "TURN_START",
    "timingState": "NEUTRAL_CLOSED",
    "players": {
      "P2": {
        "mainDeck": ["P2-MAIN-001"],
        "runeDeck": ["P2-RUNE-001", "P2-RUNE-002"],
        "hand": []
      }
    },
    "runePools": {
      "P2": { "mana": 0, "power": 0 }
    },
    "cardObjects": {
      "P2-UNIT-001": {
        "damage": 2,
        "power": 3,
        "untilEndOfTurnPowerModifier": 0,
        "untilEndOfTurnEffects": ["effect-temp-power"],
        "isFaceDown": false,
        "isExhausted": false
      }
    }
  },
  "expected": {
    "finalState": {
      "phase": "MAIN",
      "timingState": "NEUTRAL_OPEN",
      "runePools": {
        "P2": { "mana": 0, "power": 0 }
      },
      "players": {
        "P2": {
          "hand": ["P2-MAIN-001"],
          "base": ["P2-RUNE-001", "P2-RUNE-002"]
        }
      },
      "stackItems": [],
      "cardObjects": {
        "P2-UNIT-001": {
          "damage": 0,
          "power": 3,
          "untilEndOfTurnPowerModifier": 0,
          "untilEndOfTurnEffects": [],
          "isFaceDown": false,
          "isExhausted": false
        }
      }
    },
    "events": [
      { "kind": "TURN_START_BEGAN" },
      { "kind": "RUNES_CALLED" },
      { "kind": "CARD_DRAWN" },
      { "kind": "RUNE_POOL_CLEARED" },
      { "kind": "MAIN_PHASE_BEGAN" }
    ],
    "prompts": {
      "P1": { "actionable": false, "actions": ["WAIT"] },
      "P2": { "actionable": true, "actions": ["END_TURN"] }
    }
  }
}
```

schema v2 目前已支持 P2 初始状态和 expected 中的事件 tick/sequence/payload 局部匹配、turn/phase/timing、符文池、玩家区域、对象状态（含 `damage`、`power`、`untilEndOfTurnPowerModifier`、`untilEndOfTurnEffects`、`isFaceDown`、`isAttacking`、`isDefending`、`isExhausted`、`tags`）、`winnerPlayerId`，以及 FEPR/法术对决所需的 `priorityPlayerId`、`passedPriorityPlayerIds`、`stackItems`、`focusPlayerId`、`passedFocusPlayerIds`。`initialState.seed` 已接入权威 `MatchState.seed`，先用于多张卡牌同时回收到主牌堆底部和燃尽回收洗匀时的可回放随机顺序。`CompareExpected` 已接入出牌与回合结束组合 fixture，下一步继续把更多 P2 fixture 从手写断言迁移到通用 expected diff。

## 3. Fixture 后续必须补齐

Fixture 需要输出更完整字段：

| 字段 | 说明 |
|---|---|
| `rulesVersion` | 例如 `rules-260330`。 |
| `faqVersion` | 涉及 FAQ 时记录文件名或日期。 |
| `catalogVersion` | 例如 `official-2026-04-27`。 |
| `javaCommit` | oracle 基线 commit，例如 `75bf7cf`。 |
| `rulesEvidence` | PDF/FAQ 文件名、页码或章节、非原文摘要。 |
| `seed` | 洗牌、随机选择、随机分配的确定性 seed。 |
| `initialState` | 起手、牌库、战场、资源、特殊场面。 |
| `commands` | 玩家意图日志，必须保留原始 `cmd` JSON。 |
| `legacyOracle.events` | 旧 Java 输出的事件，仅作历史对照。 |
| `legacyOracle.snapshots.P1/P2` | 旧 Java 输出的玩家视角快照，仅作历史对照。 |
| `legacyOracle.prompts.P1/P2` | 旧 Java 输出的行动提示，仅作历史对照。 |
| `expected` | 按五份 PDF/FAQ 与官网卡面裁决后的 C# 期望结果。 |

## 4. Canonical JSON 规则

对比时忽略：

- 真实时间戳。
- SignalR/WebSocket connection id。
- 服务端 build id。
- 非语义 JSON 属性排序。

对比时必须保留：

- `tick` / `sequence`。
- `event kind`。
- `zone`。
- `ownerId` / `controllerId`。
- 公开与隐藏信息边界。
- `prompt` 可执行行动。
- 费用、目标、响应窗口和结算链状态。

## 5. 第一批 Java Fixture 清单

P1 先导出 10 条：

1. P1/P2 加入和视角快照。
2. 幂等重复提交。
3. 符文横置/回收。
4. EndTurn/Pass。
5. 基础单位打出。
6. 基础移动。
7. 基础战斗得分。
8. 基础法术伤害。
9. 装备装配。
10. owner/controller 边界。

只有当 C# runner 能消费 Java exporter 输出，并且 fixture 已补齐 PDF/FAQ 规则依据后，后续规则迁移才进入正式 conformance 节奏。

P2 第一批 fixture 的规则审查顺序见 `docs/p2-rules-preflight.md`。其中 `p2-turn-start-runes-and-draw`、`p2-end-turn-special-cleanup`、`p2-pass-priority-does-not-end-turn`、`p2-fepr-priority-pass-resolves-stack`、`p2-fepr-resolves-latest-keeps-remaining-stack`、`p2-spell-duel-pass-focus-closes-window`、`p2-turn-start-burnout-empty-graveyard-wins`、`p2-play-punishment-damage-stack` 是进入核心规则实现前的优先门禁。

## 6. 当前导出命令

Java oracle exporter 当前位于旧项目 server 测试层：

```bash
mvn -pl server -am \
  -Dtest=OracleFixtureExportTest \
  -Dsurefire.failIfNoSpecifiedTests=false \
  -Doracle.fixture.outputDir=/Users/dinghaolin/MyProjects/riftbound-dotnet/tests/Riftbound.ConformanceTests/Fixtures/java-oracle \
  test
```

当前已导出：

- `java-oracle-p1-pass.fixture.json`
- `java-oracle-p1-end-turn.fixture.json`
- `java-oracle-p1-duplicate-pass.fixture.json`

C# 侧当前已把 `PASS`、`END_TURN`、重复 `PASS` 的事件日志和 prompt actions 对齐到旧 Java 行为。`ConformanceFixture` 已能读取可选 `rulesEvidence`、`faqVersion`、`auditStatus`、`legacyOracle`、P2 `initialState` 和 richer `expected`；Java exporter 已输出 `legacyOracle`，并暂时保留旧 `oracle` 兼容字段。现有 3 条 legacy fixture 已补细化 evidence，但仍标记为 `NEEDS_RULE_AUDIT`。当前已确认 `PASS -> TURN_ENDED` 是 legacy mismatch candidate；若后续 PDF/FAQ 裁决与 Java 行为冲突，expected 应以 PDF/FAQ 为准。
