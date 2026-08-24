# GDK Thinking Guides

Use these guides when a change crosses ownership boundaries or appears to repeat an existing framework pattern.

| Guide | Use when |
| --- | --- |
| [Code Reuse](./code-reuse-thinking-guide.md) | Adding helpers, components, async wrappers, lifecycle code, or constants |
| [Cross-Layer Changes](./cross-layer-thinking-guide.md) | Touching client/server contracts, Luban/Proto, UI/assets, generated code, or several runtime layers |

Before changing a value, symbol, ID, asset path, protocol field, or config key, search all consumers with `rg`. Verify review findings against the actual source and trust boundary before treating them as defects.
