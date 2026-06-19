import type { PromptCommandBindingSummary } from "./promptInteraction";

const internalFieldPattern = /\b(?:serverPaymentState|resourceLedgerBeforePayment)\b/i;

export function commandBindingFieldKey(binding: PromptCommandBindingSummary, index: number): string {
  return binding.source === "requirementMetadata" ? `server-metadata-${index}` : binding.field;
}

export function commandBindingDisplayLabel(
  binding: PromptCommandBindingSummary,
  fallbackLabel: string
): string {
  if (binding.source === "requirementMetadata") {
    return serverInjectedFieldLabel(binding.required);
  }

  return commandFieldDisplayLabel(fallbackLabel);
}

export function commandFieldDisplayLabel(value: string): string {
  const trimmed = value.trim();
  const required = trimmed.endsWith("*");
  const withoutRequiredMark = required ? trimmed.slice(0, -1) : trimmed;
  if (withoutRequiredMark.startsWith("服务端:") || internalFieldPattern.test(withoutRequiredMark)) {
    return serverInjectedFieldLabel(required);
  }

  return trimmed;
}

function serverInjectedFieldLabel(required: boolean): string {
  return `服务端字段${required ? "*" : ""}`;
}
