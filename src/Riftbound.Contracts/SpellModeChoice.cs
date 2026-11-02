namespace Riftbound.Contracts;

// Each chosen mode has its own targets. The same object can be selected by
// different modes without permitting duplicate targets within a single mode.
public sealed record SpellModeChoice(string Mode, IReadOnlyList<string> TargetObjectIds);
