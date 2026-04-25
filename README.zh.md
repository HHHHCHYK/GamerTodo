# HeyeTodo

专为**独立游戏开发者**打造的跨平台待办事项与项目管理应用。

> Also available in: [English](README.md)

| | |
|---|---|
| 客户端 | Avalonia 12 (C#) — Windows & macOS |
| 服务端 | ASP.NET Core 10 + EF Core + PostgreSQL |
| 实时通信 | SignalR (WebSocket) |
| 本地存储 | SQLite（本地优先） |
| 认证 | JWT（访问令牌 + 刷新令牌） |

## 功能特性（MVP）

- 多用户注册与登录，多设备同步
- 本地优先架构：完整离线支持，冲突感知同步
- 任务增删改查，支持**列表视图**和**甘特图视图**（共享同一数据模型）
- 用户可选角色：制作人 / 设计师 / 美术 / 程序员 / 音效设计师
  - 可选零个、一个或多个角色，界面随角色自适应调整
- 按优先级与依赖关系自动排序
  - 基于规则的引擎（拓扑排序 + 优先级权重）
  - 可选 LLM 辅助（服务端代理模式 / 客户端密钥模式）
- 小游戏区（MVP 中为占位入口，功能保留）
- 双语界面（中文 / 英文），自动跟随系统语言

## 仓库结构

```
HeyeTodo/
├── src/
│   ├── HeyeTodo.Shared/    共享 DTO、枚举、契约
│   ├── HeyeTodo.Server/    ASP.NET Core 后端（Api / Application / Domain / Infrastructure）
│   └── HeyeTodo.Client/    Avalonia 桌面客户端
├── deploy/                 Docker / 部署资产
├── scripts/                辅助脚本（macOS 签名占位等）
└── HeyeTodo.sln
```

## 开发环境快速上手

### 前置依赖

- .NET SDK 10.x
- Docker Desktop（通过 `deploy/docker-compose.yml` 运行 PostgreSQL）
- `dotnet-ef` 本地工具 — 执行 `dotnet tool restore` 安装

### 启动服务端

```bash
dotnet tool restore
dotnet user-secrets --project src/HeyeTodo.Server set "Jwt:SigningKey" "<你的随机32字节以上密钥>"
cd deploy
docker compose up -d postgres
cd ../src/HeyeTodo.Server
dotnet run
```

在 PowerShell 中生成随机签名密钥：

```powershell
$key = [Convert]::ToBase64String((1..48 | ForEach-Object { [byte](Get-Random -Max 256) }))
dotnet user-secrets --project src/HeyeTodo.Server set "Jwt:SigningKey" $key
```

手动执行数据库迁移（如需）：

```bash
dotnet dotnet-ef database update \
  --project src/HeyeTodo.Server/HeyeTodo.Server.csproj \
  --startup-project src/HeyeTodo.Server/HeyeTodo.Server.csproj
```

开发模式下，交互式 API 文档（Scalar）地址：`http://localhost:5254/scalar/v1`

### 启动客户端

```bash
cd src/HeyeTodo.Client
dotnet run
```

## 打包发布

构建脚本位于 `artifacts/scripts/`（里程碑 M8 新增）。

### Windows — 便携 zip

```powershell
pwsh ./artifacts/scripts/publish-windows.ps1 -Version 0.1.0
```

输出：

| 路径 | 说明 |
|------|------|
| `artifacts/releases/client-win-x64` | 自包含发布目录 |
| `artifacts/releases/HeyeTodo-client-win-x64-<version>.zip` | 便携 zip 包 |

> MVP 阶段包未签名。

### Windows — MSIX

```powershell
pwsh ./artifacts/scripts/publish-windows-msix.ps1 -Version 0.1.0
```

要求：Windows SDK 的 `makeappx.exe` 需在 `PATH` 中可用。

输出：`artifacts/releases/HeyeTodo-client-win-x64-<version>.msix`（未签名）

> 如需广泛分发，请在发布前单独完成签名。

### macOS — 应用包与 dmg

在 macOS 上执行：

```bash
bash ./artifacts/scripts/publish-macos.sh Release osx-arm64 0.1.0
```

输出：

| 路径 | 说明 |
|------|------|
| `artifacts/releases/HeyeTodo.app` | 应用包 |
| `artifacts/releases/HeyeTodo-osx-arm64-<version>.dmg` | 磁盘镜像 |

若 `hdiutil` 不可用，脚本仍会生成 `.app` 包，跳过 dmg 创建。

由于 MVP 包未签名，macOS 下载后可能触发隔离。首次启动前清除隔离属性：

```bash
xattr -d com.apple.quarantine /Applications/HeyeTodo.app
```

签名集成占位点位于 `scripts/mac/sign.sh`。

## 里程碑

完整里程碑计划（M0 至 M8）请参阅 [`docs/ROADMAP.md`](docs/ROADMAP.md)。

## 自托管部署

HeyeTodo 优先支持自托管部署。

### 最低环境要求

- Docker Engine 或 Docker Desktop
- 指向 API 容器的主机名或反向代理入口
- 至少 32 字节的随机 JWT 签名密钥
- PostgreSQL 数据的持久化存储卷

### 快速启动

1. 编辑 `deploy/docker-compose.yml`，替换默认 JWT 签名密钥。
2. 将 `Cors__AllowedOrigins__0`（如需可添加更多条目）设置为桌面客户端实际使用的 Origin。
3. 在 `deploy/` 目录下执行 `docker compose up -d`。
4. 确认服务端可通过 `http://localhost:8080` 或反向代理访问。
5. 在桌面客户端的**设置**中，将服务器基础 URL 指向你的服务端地址。

### 手动数据库迁移

服务端启动时会自动执行 EF Core 迁移。如需手动执行：

```bash
dotnet dotnet-ef database update \
  --project ../src/HeyeTodo.Server/HeyeTodo.Server.csproj \
  --startup-project ../src/HeyeTodo.Server/HeyeTodo.Server.csproj
```

### 环境变量说明

| 变量 | 说明 |
|------|------|
| `ASPNETCORE_ENVIRONMENT` | `Production` 或 `Development` |
| `ASPNETCORE_URLS` | 服务端监听地址 |
| `ConnectionStrings__Default` | PostgreSQL 连接字符串 |
| `Jwt__Issuer` | JWT 签发方 |
| `Jwt__Audience` | JWT 受众 |
| `Jwt__SigningKey` | HMAC 签名密钥（≥ 32 字节，请妥善保管） |
| `Jwt__AccessTokenMinutes` | 访问令牌有效期（分钟） |
| `Jwt__RefreshTokenDays` | 刷新令牌有效期（天） |
| `Cors__AllowedOrigins__0` | 第一个允许的 CORS 来源 |
| `Cors__AllowedOrigins__1` | 第二个允许的 CORS 来源（可选） |

### 反向代理说明

将 API 置于 Nginx、Caddy、Traefik 等反向代理后时：

- 将 HTTP 流量转发至容器的 `8080` 端口
- 为 `/ws/sync` 路径保留 WebSocket 升级支持
- 确保外部 Origin 与 `Cors__AllowedOrigins__*` 保持一致
- 生产环境建议在代理层终止 TLS

### 备份建议

至少备份以下内容：

- PostgreSQL 数据卷 `heyetodo-postgres-dev-data`
- 部署配置值，尤其是 JWT 签名密钥和 CORS 设置

恢复操作前请先停止服务栈，避免替换数据库文件或还原 Docker 卷快照时造成数据损坏。

## 参与贡献

欢迎提交 Issue 和 Pull Request。重大变更请先开 Issue 讨论。

## 许可证

待定
