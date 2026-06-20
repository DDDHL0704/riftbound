import type {
  ActionPromptCommandTemplateBindingDto,
  ActionPromptCommandTemplateDto,
  GameCommand
} from "../types/protocol";

export type ActionPromptCommandTemplateSelection = {
  destinationId?: string;
  mode?: string;
  optionalCostIds?: string[];
  sourceId?: string;
  targetObjectIds?: string[];
};

export type ActionPromptCommandTemplateContext = {
  candidateMetadata?: Record<string, unknown> | null;
  requirement?: Record<string, unknown>;
};

type NormalizedActionPromptCommandTemplateContext = {
  candidateMetadata?: Record<string, unknown>;
  requirement?: Record<string, unknown>;
};

export function commandFromActionPromptTemplate(
  template: ActionPromptCommandTemplateDto | null | undefined,
  selection: ActionPromptCommandTemplateSelection,
  context: ActionPromptCommandTemplateContext | Record<string, unknown> | undefined
): GameCommand | undefined {
  if (!template?.cmdType || !Array.isArray(template.bindings)) {
    return undefined;
  }

  const normalizedContext = normalizeTemplateContext(context);
  const command: Record<string, unknown> = { cmdType: template.cmdType };
  for (const binding of template.bindings) {
    const value = commandTemplateValue(binding, selection, normalizedContext);
    if (isMissingCommandTemplateValue(value)) {
      if (binding.required) {
        return undefined;
      }
      if (binding.omitEmpty !== false) {
        continue;
      }
    }

    command[binding.field] = value;
  }

  return command as GameCommand;
}

function commandTemplateValue(
  binding: ActionPromptCommandTemplateBindingDto,
  selection: ActionPromptCommandTemplateSelection,
  context: NormalizedActionPromptCommandTemplateContext
): string | string[] | undefined {
  const rawValue = commandTemplateRawValue(binding, selection, context);
  if (binding.asArray) {
    if (Array.isArray(rawValue)) {
      return rawValue;
    }

    return typeof rawValue === "string" && rawValue.length > 0 ? [rawValue] : [];
  }

  return rawValue;
}

function commandTemplateRawValue(
  binding: ActionPromptCommandTemplateBindingDto,
  selection: ActionPromptCommandTemplateSelection,
  context: NormalizedActionPromptCommandTemplateContext
): string | string[] | undefined {
  switch (binding.source) {
    case "selectedSource":
      return selection.sourceId;
    case "selectedTarget":
      return selection.targetObjectIds?.[0];
    case "selectedTargets":
      return selection.targetObjectIds ?? [];
    case "selectedDestination":
      return selection.destinationId;
    case "selectedMode":
      return selection.mode;
    case "selectedOptionalCosts":
      return selection.optionalCostIds ?? [];
    case "candidateMetadata":
      return commandTemplateMetadataValue(binding, context.candidateMetadata);
    case "requirementMetadata":
      return commandTemplateMetadataValue(binding, context.requirement);
    default:
      return undefined;
  }
}

function commandTemplateMetadataValue(
  binding: ActionPromptCommandTemplateBindingDto,
  metadata: Record<string, unknown> | undefined
): string | string[] | undefined {
  if (!metadata) {
    return undefined;
  }

  const keys = [
    ...(binding.metadataKey ? [binding.metadataKey] : []),
    ...(Array.isArray(binding.metadataKeys) ? binding.metadataKeys : [])
  ];
  for (const key of keys) {
    const value = stringOrStringArrayMetadata(metadata, key);
    if (value) {
      return value;
    }
  }

  return undefined;
}

function normalizeTemplateContext(
  context: ActionPromptCommandTemplateContext | Record<string, unknown> | undefined
): NormalizedActionPromptCommandTemplateContext {
  if (!context) {
    return { candidateMetadata: undefined, requirement: undefined };
  }

  if ("candidateMetadata" in context || "requirement" in context) {
    return {
      candidateMetadata: isRecord(context.candidateMetadata) ? context.candidateMetadata : undefined,
      requirement: isRecord(context.requirement) ? context.requirement : undefined
    };
  }

  return { candidateMetadata: undefined, requirement: context };
}

function isMissingCommandTemplateValue(value: string | string[] | undefined): boolean {
  return value == null
    || (typeof value === "string" && value.trim().length === 0)
    || (Array.isArray(value) && value.length === 0);
}

function stringMetadata(record: Record<string, unknown>, key: string): string | undefined {
  const value = record[key];
  return typeof value === "string" && value.trim().length > 0 ? value : undefined;
}

function stringOrStringArrayMetadata(record: Record<string, unknown>, key: string): string | string[] | undefined {
  const stringValue = stringMetadata(record, key);
  if (stringValue) {
    return stringValue;
  }

  const value = record[key];
  if (!Array.isArray(value)) {
    return undefined;
  }

  const strings = value.map((item) => typeof item === "string" ? item.trim() : "");
  return strings.length > 0 && strings.every((item) => item.length > 0) ? strings : undefined;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
}
