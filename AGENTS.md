# AGENTS.md — Mechrevo Power Tray

Windows 托盘应用，通过 OEM WMI `SetOemPowerSwitch` 切换 MECHREVO WUJIE 16 Pro 性能模式（安静/均衡/性能）。

## 项目结构

```
MechrevoPowerTray.sln
src/MechrevoPowerTray/
├── Program.cs              # 入口：单实例互斥体 → TrayApplicationContext
├── App/
│   └── TrayApplicationContext.cs   # 托盘菜单、模式切换、UIs 事件
├── Models/
│   ├── OemPowerMode.cs             # enum (1=Quiet,2=Balanced,3=Performance) + 白名单校验
│   └── OperationResults.cs         # record 类型返回值
├── Services/
│   ├── IProcessRunner.cs           # 异步进程调用接口 + ProcessRunResult
│   ├── ProcessRunner.cs            # 并发 stdout/stderr 读取 + timeout + kill
│   ├── OemPowerModeService.cs      # WMI SetOemPowerSwitch 调用 + 诊断
│   ├── WindowsPowerPlanService.cs  # P/Invoke powrprof.dll PowerSetActiveScheme
│   ├── StartupTaskService.cs       # schtasks.exe 查询/创建/删除（XML 计划任务）
│   ├── StartupTaskMenuDisplay.cs   # 菜单显示逻辑（纯函数，可测试）
│   └── AppSettingsStore.cs         # %LOCALAPPDATA%\MechrevoPowerTray\settings.json
├── Properties/app.manifest        # requireAdministrator
├── MechrevoPowerTray.csproj       # net10.0-windows10.0.19041.0, WinExe, x64
├── icon.ico /.png
tests/MechrevoPowerTray.Tests/
├── MechrevoPowerTray.Tests.csproj
├── AppSettingsStoreTests.cs       # 设置持久化、损坏 JSON、旧版迁移
├── CombinedModeSwitchResultTests.cs
├── OemPowerModeServiceTests.cs    # 模式值 0/4/255 拒绝、WMI Accepted/Rejected/Indeterminate
├── OemSwitchNotificationTests.cs  # 通知语义验证（不接受"成功进入"等措辞）
├── ProcessRunnerTests.cs          # 超时、退出码、并发流
├── StartupTaskMenuDisplayTests.cs
├── StartupTaskServiceTests.cs     # 基于 IProcessRunner stub，不碰真实 Task Scheduler
└── WujieHardwareE2ETests.cs       # 真实硬件上的端到端验证（仅在 MECHREVO 硬件上运行）
```

## 构建

```powershell
dotnet restore
dotnet build src\MechrevoPowerTray\MechrevoPowerTray.csproj -c Release
```

自包含单文件 EXE（不需 .NET Runtime）：
```powershell
.\build-release.ps1   # 输出 artifacts\publish\win-x64\MechrevoPowerTray.exe
```

## 测试

```powershell
dotnet test .\tests\MechrevoPowerTray.Tests\MechrevoPowerTray.Tests.csproj
```

测试使用 IProcessRunner stub，不触及真实计划任务。

## 关键约束

- **管理员权限**：app.manifest `requireAdministrator`，程序始终需要 UAC 提权
- **TreatWarningsAsErrors** = true，所有警告视为错误
- **x64 only**：`PlatformTarget=x64`, `Prefer32Bit=false`
- **只允许 OEM 值 1/2/3**：任何其他值（含 0、4）必须拒绝，见 `OemPowerMode.IsWhitelisted()`
- **不做 EC 读写**、不调用其他 WMI 方法、不拼接 WMI 查询字符串
- **登录自动启动**：`StartupTaskService` 通过 `schtasks /Create /XML` 创建计划任务（含交互式令牌 + 最高权限），路径含空格时不会被截断；支持启用/禁用
- **不依赖本地化错误文本**：`StartupTaskService.RemoveAsync` 靠退出码判断，不用 stderr 字符串匹配
- **stdout/stderr 并发读取**：`ProcessRunner` 使用 `Task.WhenAny` + timeout + `process.Kill`
- **源文件全中文**：类名、注释、UI 字符串均为中文
- **唯一外部依赖**：`System.Management` NuGet 包（WMI 调用）
- **无 CI/CD** 配置

## 运行

启动后出现在系统托盘，双击切均衡，右键菜单切换模式/诊断/设置。
设置存储在 `%LOCALAPPDATA%\MechrevoPowerTray\settings.json`。
"登录后自动启动"通过计划任务实现（`schtasks /Create /XML` 含交互式令牌 + 最高权限），右键菜单可启用/禁用。

## 架构要点

- `Program.Main` → 命名互斥体（`Local\MechrevoPowerTray.SingleInstance`）防重复 → `TrayApplicationContext`
- `TrayApplicationContext` 拥有全部生命周期：NotifyIcon、ContextMenuStrip、Service 实例
- 模式切换走 WMI `SetOemPowerSwitch.u8Input`，Windows 电源计划同步走 `powrprof.dll!PowerSetActiveScheme`
- 诊断仅枚举 `SetOemPowerSwitch` 实例数/Active 状态
- `AppSettingsStore.Save` 使用临时文件 + atomic rename 保证写入不丢数据
- `ProcessRunner` 通过 `IProcessRunner` 接口注入，支持测试时 stub
- `StartupTaskService` 状态机：`Missing` / `Present` / `Invalid`
