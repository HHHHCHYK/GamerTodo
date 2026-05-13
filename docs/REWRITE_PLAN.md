# HeyeTodo 重写计划

> **创建日期**: 2026-04-29
>
> **当前阶段**: 第一阶段 — 主界面骨架（已完成）

---

## 阶段目标

只保留一个主界面框架，包含：
1. 左侧导航栏（只有一个"测试界面"入口）
2. 右侧主内容（只显示一行"测试界面"文字）
3. 没有登录注册功能，启动后直接进入主界面

---

## 当前项目结构

```
HeyeTodo/
├── HeyeTodo.sln
│
└── src/
    ├── HeyeTodo.Shared/
    │   └── Models/                     ← 极简数据模型（当前阶段未使用，为后续准备）
    │       ├── TaskModel.cs
    │       ├── TaskState.cs
    │       ├── TaskPriorityLevel.cs
    │       ├── ProjectModel.cs
    │       ├── UserProfileModel.cs
    │       └── WorkspaceSnapshot.cs
    │
    └── HeyeTodo.Client/
        ├── ViewModels/
        │   ├── ViewModelBase.cs
        │   ├── MainWindowViewModel.cs    ← 只持有一个 Current 属性
        │   └── TestPageViewModel.cs     ← 空壳 ViewModel
        │
        ├── Views/
        │   ├── MainWindow.axaml / .cs    ← 左右分栏布局（220px 导航 + 内容区）
        │   └── TestPageView.axaml / .cs  ← 只显示"测试界面"文字
        │
        ├── App.axaml                     ← 只使用 FluentTheme
        ├── App.axaml.cs                  ← 启动后直接显示主界面
        ├── AppHost.cs                    ← DI 注册
        ├── ViewLocator.cs
        ├── Program.cs
        └── HeyeTodo.Client.csproj
```

---

## 已删除的内容

| 内容 | 说明 |
|------|------|
| 登录 / 注册页面 | 当前阶段不需要身份系统 |
| ShellViewModel / ShellView | 导航布局直接合一到 MainWindow |
| TaskList / Planning / Settings 等页面 | 当前阶段不保留功能页面 |
| SplashWindow / MiniGames / RolePanels / RoleSelection | 非核心页面 |
| Storage 目录（IDataStore / JsonFileDataStore / DataStoreSchema） | 当前阶段不使用存储 |
| Infrastructure 目录（AppSettings / AppPaths / Navigation / Logging / Localization） | 当前阶段不需要基础设施 |
| Application 目录（Sync / Tasks / Planning） | 已不适用 |
| Data 目录（LocalDbContext / Repositories / Entities） | 已不适用 |
| 旧任务时间轴控件 | 依赖旧 ViewModel |
| PixelCozyTheme.axaml | 去美化，回归 Fluent 默认 |
| NuGet: Microsoft.Extensions.Http | 当前不需要 HTTP 客户端 |
| NuGet: System.Security.Cryptography.ProtectedData | 当前不需要加密存储 |

---

## 界面布局

```
┌──────────────────────────────────────────────┐
│ HeyeTodo                                     │
├───────────────┬──────────────────────────────┤
│  导航栏 (220px) │  主内容区                    │
│               │                              │
│  测试界面      │  测试界面                     │
│               │                              │
└───────────────┴──────────────────────────────┘
```

---

## 构建验证

```bash
dotnet build HeyeTodo.sln -v minimal
# 输出: 在 0.7 秒内生成 已成功
```

---

## 下一步规划（第二阶段）

第一阶段只完成了主界面空壳。第二阶段需要填充真实功能，按优先级排列：

1. 选择数据存储方案（JSON 文件 / 内存）
2. 任务列表页：项目 CRUD + 任务 CRUD
3. 其他页面按需添加

---

## 决策记录

| 决策 | 选择 | 理由 |
|------|------|------|
| 应用启动后 | 直接进入主界面 | 不需要身份系统，去掉登录阻碍 |
| 导航项数量 | 只有"测试界面"一个 | 先确认布局，功能页面后续再加 |
| 客户端存储 | 当前阶段不使用 | 没有业务数据，不需要存储 |
| 样式 | 只使用 Fluent 默认 | 不分散精力在 UI 美化上 |
