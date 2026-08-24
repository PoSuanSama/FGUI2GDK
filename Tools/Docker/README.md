# GDK Docker 本地部署

此部署运行五个容器：

- `gdk-mongodb`：MongoDB 7，数据默认保存到 `D:/Docker/main/data/gdk-mongodb`。
- `gdk-server`：使用 .NET 8 运行 `Localhost` 主游戏进程。
- `gdk-admin`：运行管理后台，健康后再启动 Agent。
- `gdk-agent`：运行 Agent 心跳与部署服务；Docker 中关闭子进程管理，避免与 `gdk-server` 重复启动。
- `gdk-log-retention`：限制服务端文件日志总占用，超过 5 GB 时删除最旧日志至 4 GB。

MongoDB 和三个应用容器均配置了健康检查。`gdk-server` 会验证 Server 进程和
RouterManager 端口，`gdk-agent` 会验证 Agent 主进程；Docker Desktop 和 Rider
应在启动完成后显示健康状态。`gdk-log-retention` 是常驻清理进程，正常状态为 Running。

NLog 的单个日志文件达到 50 MB 时会自动轮转。`gdk-log-retention` 每 60 秒检查一次
日志目录；可在启动前通过 `GDK_LOG_MAX_SIZE_MB`、`GDK_LOG_TARGET_SIZE_MB` 和
`GDK_LOG_CHECK_INTERVAL_SECONDS` 调整总上限、清理目标和检查间隔。清理目标必须小于总上限。

Admin 的配置管理页面提供 `LogTestIntervalSeconds` 配置：`0` 关闭测试日志，
`1-3600` 表示向实时日志页面写入 `LogTest` 日志的间隔秒数。保存后动态生效，无需重启容器。

Docker 中服务器进程由 Compose 管理，因此管理后台的服务器启动、停止和重启操作不可用；
请使用本页的 `docker compose` 命令管理容器。非 Docker 部署不设置
`GDK_AGENT_MANAGE_PROCESSES=false`，Agent 仍会按原有方式管理服务器子进程。

首次启动前，在仓库根目录发布服务端和动态加载的 Hotfix：

```powershell
dotnet publish DotNet/App/DotNet.App.csproj -c Release -o Publish/Server
dotnet publish DotNet/Hotfix/DotNet.Hotfix.csproj -c Release -o Publish/Server
```

启动与停止：

```powershell
docker compose -f Tools/Docker/compose.yaml up -d
docker compose -f Tools/Docker/compose.yaml down
```

切换到 Unity `ClientServer` 本地开发时，只运行 MongoDB，并将其仅绑定到本机
`127.0.0.1:27017`。这样 Unity 可以使用 `mongodb://127.0.0.1`，同时不会与
Docker 版游戏进程争用端口：

```powershell
docker compose -f Tools/Docker/compose.yaml -f Tools/Docker/compose.unity.yaml up -d --no-deps mongodb
docker stop gdk-server gdk-admin gdk-agent
```

恢复完整 Docker 部署时，重新使用基础 Compose 文件启动全部服务即可：

```powershell
docker compose -f Tools/Docker/compose.yaml up -d
```

管理后台地址为 `http://localhost:5200`。首次启动默认账户为 `admin` / `admin123`；可在启动前通过 `GDK_ADMIN_PASSWORD` 环境变量修改密码。

如需更换持久化目录，请在启动前设置 `GDK_DOCKER_DATA_ROOT`。该目录下会保存 MongoDB、Admin LiteDB 和服务端日志。
