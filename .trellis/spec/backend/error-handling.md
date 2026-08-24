# Server Error Handling

## Message Boundaries

Message handlers derive from the typed ET handler for their request and response and use `UniTask`. Validate request values before side effects. Expected request failures should set the response error/message contract and return; unexpected failures should be caught once at the boundary, recorded on the response, and logged with the original exception.

`DotNet/Hotfix/Server/Admin/Admin2S_ReloadHandler.cs` shows the local shape:

```csharp
response.Error = 1;
response.Success = false;
response.Message = $"Reload failed: {e.Message}";
Log.Error($"Admin reload failed: {e}");
```

## Services And Async Work

- Preserve the original exception when translating failures.
- Complete or fault `AutoResetUniTaskCompletionSource` on every path; see `FiberInit_Admin.cs`.
- Define cancellation and shutdown cleanup for timers, background services, network calls, and process operations.
- Do not catch an exception merely to continue with partial or invalid state.

## Avoid

- Silent `catch` blocks.
- Returning success after a failed side effect.
- Logging the same exception at every layer.
- Including secrets or full untrusted payloads in a response or log.
