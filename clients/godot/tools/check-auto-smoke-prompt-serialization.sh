#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
main_script="${repo_root}/clients/godot/scripts/Main.cs"

# SignalR callbacks may arrive off the Godot main thread. They can build and
# queue a prompt view, but must not launch a competing simulated submission.
if rg -U -q 'private void RenderPrompt\([\s\S]*?_ = RunAutoSmokePromptAsync\(view\);[\s\S]*?private void RenderEvents' "${main_script}"; then
  echo "RenderPrompt must not submit simulated actions off the Godot main thread." >&2
  exit 1
fi

# ApplyPrompt and the table-ready retry may both fire while a network submit is
# awaiting its receipt. They must only replace the pending latest view; one
# drain owns all simulated submissions in sequence.
rg -q 'private void ScheduleAutoSmokePrompt\(' "${main_script}"
rg -q 'private async Task DrainAutoSmokePromptQueueAsync\(' "${main_script}"
rg -U -q 'public void ApplyPrompt\([\s\S]*?ScheduleAutoSmokePrompt\(view\);[\s\S]*?private void RefreshPromptHighlights' "${main_script}"
rg -U -q 'public void ApplySnapshotSections\([\s\S]*?ScheduleAutoSmokePrompt\(_lastAppliedPromptView\);' "${main_script}"
rg -U -q 'DrainAutoSmokePromptQueueAsync\([\s\S]*?await RunAutoSmokePromptAsync\(' "${main_script}"
rg -U -q 'private void RenderPrompt\([\s\S]*?ObserveAutoSmokePromptTick\(view\);[\s\S]*?QueueMainThread\(nameof\(ApplyPrompt\), view\)' "${main_script}"
rg -U -q 'DrainAutoSmokePromptQueueAsync\([\s\S]*?IsAutoSmokePromptStale\(view\)[\s\S]*?await RunAutoSmokePromptAsync\(' "${main_script}"

if rg -q '_ = RunAutoSmokePromptAsync\(' "${main_script}"; then
  echo "Simulated prompt submissions must run only through the serialized drain." >&2
  exit 1
fi

echo "Auto-smoke prompt serialization checks passed."
