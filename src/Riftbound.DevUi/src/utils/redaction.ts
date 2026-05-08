const internalTextRedactions: Array<{ pattern: RegExp; replacement: string }> = [
  { pattern: /\bSTACK-\d+-[A-Z0-9_-]+\b/g, replacement: "结算链项目" },
  { pattern: /\bP\d+-[A-Z0-9][A-Z0-9_-]*(?:-\d{3,})?\b/g, replacement: "对象" },
  { pattern: /\bhidden-\d+\b/g, replacement: "隐藏对象" },
  { pattern: /\b(?:cleanup|task):[a-z0-9:-]+\b/gi, replacement: "服务端任务" }
];

export function redactInternalText(value: string): string {
  return internalTextRedactions
    .reduce((current, redaction) => current.replace(redaction.pattern, redaction.replacement), value)
    .replace(/\s+/g, " ")
    .trim();
}
