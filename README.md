# Mechrevo Power Tray

面向 **MECHREVO WUJIE 16 Pro**（无界 16 Pro）的 Windows 托盘性能模式切换工具。

本项目是对 PowerModeUtilMonitor 的修复与改进版本——原项目在特定 BIOS/驱动版本下存在接口调用异常，本版重新梳理了 WMI OEM 调用流程，修复了模式切换的稳定性问题，并移除了所有未经 OEM 白名单验证的接口调用。

## 已确认的 OEM 接口

程序只调用以下三个已经由原厂程序验证过的值：

| 模式 | `SetOemPowerSwitch.u8Input` |
|---|---:|
| 安静 | 1 |
| 均衡 | 2 |
| 性能 | 3 |

程序不会枚举、猜测或写入其他数值，也不会访问 EC 寄存器。

## 功能

- 托盘菜单切换安静、均衡、性能三档
- 可选同步 Windows 电源计划
- 记录上一次成功请求的模式
- 可选启动时恢复上次模式，默认关闭
- WMI/OEM 接口诊断
- 单实例运行
- 所有 OEM 参数均经过固定白名单校验
- 自定义透明背景圆角图标（256×256 PNG → 多尺寸 ICO）

> OEM WMI 接口没有可靠的"读取当前模式"方法，因此界面显示的是"本程序上次成功请求的模式"，不是硬件状态回读。

## 功能状态（v0.0.2）

### 已暂停：登录自动启动

v0.0.2 **暂停**提供"登录后自动启动"功能。原因：

- 当前程序以便携 EXE 形式分发，用户可能从任意目录（包括 `%USERPROFILE%\Downloads`、`Desktop` 等用户可写目录）运行
- 将用户可写目录中的 EXE 设为高权限（`/RL HIGHEST`）计划任务存在权限提升风险
- 恶意用户或进程可以在下次登录前替换该 EXE，获得管理员执行权限

**旧任务处理**：如果您从 v0.0.1 升级，程序会自动检测已存在的旧版计划任务 `MechrevoPowerTray`，并在托盘菜单中提供"删除旧版启动任务"按钮。

**后续恢复**：后续版本会提供 MSI 安装器，将程序部署到受保护目录（`Program Files\MechrevoPowerTray`）后重新开放此功能。

## 构建

### 环境要求

- Windows 10 2004 或更高版本 / Windows 11
- x64
- .NET 10 SDK

### 普通 Release 构建

```powershell
dotnet restore
dotnet build .\src\MechrevoPowerTray\MechrevoPowerTray.csproj -c Release
```

### 发布自包含单文件 EXE

```powershell
.\build-release.ps1
```

输出目录：`artifacts\publish\win-x64\MechrevoPowerTray.exe`

发布脚本生成自包含单文件 EXE，目标电脑不需要安装 .NET Runtime。启用单文件压缩后体积约 **58MB**（未压缩约 142MB）。

### 运行测试

```powershell
dotnet test .\tests\MechrevoPowerTray.Tests\MechrevoPowerTray.Tests.csproj
```

## 使用

1. 启动 `MechrevoPowerTray.exe`
2. 接受 UAC 管理员权限请求
3. 在系统托盘右键图标
4. 选择安静、均衡或性能
5. 首次切换前可以先点"运行诊断"

## 安全边界

本项目明确禁止：

- 调用 `SetOemPowerSwitch` 的 0、4 或其他未知值
- 调用未知 WMI OEM 方法
- 读写 EC 地址
- 加载 SparkIO、ECIO、SmIo 等未知驱动接口
- 通过 `wmic.exe` 拼接字符串执行命令
- 将任意字符串暴露为硬件控制参数
- 从用户可写目录创建高权限计划任务
- 依赖本地化 stderr 文本判断计划任务状态

## 已知限制

- 程序需要管理员权限（通过 app.manifest 声明）
- OEM 方法可能因 BIOS、驱动或原厂服务状态变化而不可用
- Windows 电源计划切换成功不代表 OEM 性能模式切换成功
- "高性能"电源计划在部分 Windows 设备上可能不可用；此时 OEM 模式仍可成功，程序会给出警告
- 本程序仅适用于 MECHREVO WUJIE 16 Pro（无界 16 Pro），其他机型未经验证
- v0.0.2 暂停了登录自动启动功能（详见"功能状态"章节）
