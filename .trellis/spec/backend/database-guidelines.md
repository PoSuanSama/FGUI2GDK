# Server Data And Configuration

## Persistence

Use the persistence technology already owned by the feature. The Admin site uses a disposable LiteDB wrapper in `DotNet/Hotfix/Server/Admin/Services/AdminDatabase.cs`; its default database is rooted under `AppContext.BaseDirectory/Data`, and tests/tools can pass an explicit path through the second constructor. Dispose the database with its owning service lifetime.

MongoDB is optional infrastructure for ET server features. Do not introduce a new database or ORM for a local feature without an explicit dependency decision.

## Luban And Protocol Data

- `Design/Excel/` and `Design/Proto/` are facts of record.
- `Config/` and `Unity/Assets/Res/**/Luban/` contain derived runtime data.
- Generated C# under a `Generate/` directory is derived output.

Change the Excel/Proto schema or generator configuration first, run the repository exporter, then review source and generated diffs together. Preserve field defaults, IDs, opcodes, table names, and mixed-version compatibility.

## Safety

Validate external paths and configuration at the boundary. `DotNet/Hotfix/Server/Agent/Admin2Agent_DeployFileHandler.cs` resolves deployment paths against the application root and rejects traversal before writing.

Never commit production credentials, connection strings with secrets, user data, or local database files.
