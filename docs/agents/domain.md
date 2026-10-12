# Domain Docs

This repository uses one domain context: Riftbound gameplay and its delivery. Backend projects, React DevUi and Godot are implementations and clients of that domain, not separate contexts by folder alone.

Before exploring, read root GLOSSARY.md and relevant docs/adr/ entries when they exist. If absent, proceed without creating empty documents.

Create terms as they are resolved through domain-modeling; the glossary contains terminology only, with no implementation detail. Use the glossary's canonical terms in reviews, tickets and tests.

Create an ADR only for a hard-to-reverse, surprising choice with a real trade-off; surface conflicts with an existing ADR before changing that choice.

Configuration is user-editable under docs/agents/.
