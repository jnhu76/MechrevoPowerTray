# Mechrevo Power Tray

面向 MECHREVO WUJIE16 Pro 的 Windows 10/11 托盘性能模式切换工具。

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
- 可选登录后以最高权限启动
- 可选启动时恢复上次模式，默认关闭
- WMI/OEM 接口诊断
- 单实例运行
- 所有 OEM 参数均经过固定白名单校验

> OEM WMI 接口没有可靠的“读取当前模式”方法，因此界面显示的是“本程序上次成功请求的模式”，不是硬件状态回读。

## 环境

- Windows 10 2004 或更高版本 / Windows 11
- x64
- .NET 10 SDK（开发和普通构建）
- 发布脚本会生成自包含单文件 EXE，目标电脑不需要另装 .NET Runtime

## 构建

在项目根目录执行：

```powershell
dotnet restore
dotnet build .\src\MechrevoPowerTray\MechrevoPowerTray.csproj -c Release
```

## 发布单文件版本

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\build-release.ps1
```

输出目录：

```text
artifacts\publish\win-x64\
```

主要文件：

```text
MechrevoPowerTray.exe
```

## 使用

1. 启动 `MechrevoPowerTray.exe`
2. 接受 UAC 管理员权限请求
3. 在系统托盘右键图标
4. 选择安静、均衡或性能
5. 首次切换前可以先点“运行诊断”

## 登录启动

托盘菜单中的“登录后自动启动”会创建：

```text
计划任务：MechrevoPowerTray
触发器：当前用户登录
权限：最高权限
```

关闭该选项会删除该计划任务。

## 安全边界

本项目明确禁止：

- 调用 `SetOemPowerSwitch` 的 0、4 或其他未知值
- 调用未知 WMI OEM 方法
- 读写 EC 地址
- 加载 SparkIO、ECIO、SmIo 等未知驱动接口
- 通过 `wmic.exe` 拼接字符串执行命令
- 将任意字符串暴露为硬件控制参数

## 已知限制

- 程序需要管理员权限
- OEM 方法可能因 BIOS、驱动或原厂服务状态变化而不可用
- Windows 电源计划切换成功不代表 OEM 性能模式切换成功
- “高性能”电源计划在部分 Windows 设备上可能不可用；此时 OEM 模式仍可成功，程序会给出警告
