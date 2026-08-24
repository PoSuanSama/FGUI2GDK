# Server Logging

Use ET `Log` at application boundaries; NLog routing and sinks are configured in `Config/NLog/NLog.config`.

## Levels

- `Log.Debug`: detailed development-only flow useful during diagnosis.
- `Log.Info`: successful lifecycle transitions and operator actions.
- `Log.Warning`: recoverable failures or degraded behavior that needs attention.
- `Log.Error`: failed operations, unhandled task failures, or corrupted invariants.

Include stable context such as scene, process, fiber, player, request type, or target path. `Admin2S_ReloadHandler.cs` logs the requested reload kind, while Agent handlers include the process or deployment target.

Log a failure once where it is handled. Keep the exception object or full exception text for unexpected failures; use a concise message in protocol responses.

Never log tokens, passwords, private keys, complete connection strings, or arbitrary request payloads. Do not add high-frequency per-frame/per-tick information logs without a bounded diagnostic need.
