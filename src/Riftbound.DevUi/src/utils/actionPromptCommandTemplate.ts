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

export function commandFromActionPromptTemplate(
  template: ActionPromptCommandTemplateDto | null | undefined,
  selection: ActionPromptCommandTemplateSelection,
  requirement: Record<string, unknown> | undefined
): GameCommand | undefined {
  if (!template?.cmdType || !Array.isArray(template.bindings)) {
    return undefined;
  }

  const command: Record<string, unknown> = { cmdType: template.cmdType };
  for (const binding of template.bindings) {
    const value = commandTemplateValue(binding, selection, requirement);
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
  requirement: Record<string, unknown> | undefined
): string | string[] | undefined {
  const rawValue = commandTemplateRawValue(binding, selection, requirement);
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
  requirement: Record<string, unknown> | undefined
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
    case "requirementMetadata":
      return commandTemplateRequirementValue(binding, requirement);
    default:
      return undefined;
  }
}

function commandTemplateRequirementValue(
  binding: ActionPromptCommandTemplateBindingDto,
  requirement: Record<string, unknown> | undefined
): string | undefined {
  if (!requirement) {
    return undefined;
  }

  const keys = [
    ...(binding.metadataKey ? [binding.metadataKey] : []),
    ...(Array.isArray(binding.metadataKeys) ? binding.metadataKeys : [])
  ];
  for (const key of keys) {
    const value = stringMetadata(requirement, key);
    if (value) {
      return value;
    }
  }

  return undefined;
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
