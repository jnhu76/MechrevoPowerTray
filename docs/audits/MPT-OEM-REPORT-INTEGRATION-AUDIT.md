# 审计：外部 OEM 分析报告对当前代码的差距评估

**日期**: 2026-07-15
**上下文**: 基于 `C:\ODVP_Test\_analysis\reports\` 下 14 份外部分析报告，对 MechrevoPowerTray 当前代码（commit `ae6a70c` + `9018d5b`）进行逐项审计。

---

## 审计问题 1：当 WMI 方法无输出参数（output 为 null）时，`OemPowerModeService.SetMode` 如何处理？

**结论: 当前为 `Indeterminate`，应改为 `Accepted`**

- `WmiOemPowerModeBackend.InvokeSingleInstance` 在 `output is null` 时返回 `ReturnValue = null`（`WmiOemPowerModeBackend.cs:108-111`）
- `OemPowerModeService.SetMode` 在 `invoke.ReturnValue is null` 时返回 `Outcome = Indeterminate`（`OemPowerModeService.cs:113-120`）
- **报告证据**: `07-dynamic-evidence.md:76` 明确标注 `(void/no out params)`。`10-final-verdict.md:46` 确认 "SetOemPowerSwitch method has no out parameters"。
- 修复方向：当 backend 报告方法签名无输出参数时，`ReturnValue is null` 应视为 `Accepted`，而非 `Indeterminate`。

---

## 审计问题 2：`OemPowerModeService` 是否要求 `ReturnValue` 必须存在才能判定 Accepted？

**结论: 是，目前必须存在且为 0 才 Accepted**

- `OemPowerModeService.cs:113-120`：`invoke.ReturnValue is null` → `Indeterminate`
- `OemPowerModeService.cs:122-130`：`invoke.ReturnValue == 0` → `Accepted`
- `OemPowerModeService.cs:132-137`：其他 → `Rejected`
- **报告证据**: `07-dynamic-evidence.md:76` 证明该 WMI 方法无 output parameters、无 ReturnValue，因此如果调用本身没异常，结果应直接视为 Accepted。
- 修复方向：引入 `OemWmiMethodContract`，当 `HasReturnValue == false` 时跳过 ReturnValue 检查。

---

## 审计问题 3：当前代码是否读取 WMI 方法元数据（参数类型、是否为数组、输出参数签名）？

**结论: 否，完全未读取**

- `WmiOemPowerModeBackend` 中没有任何 `GetMethodParameters` 之前的 schema 探测逻辑
- 唯一的元数据交互是 `GetMethodParameters(MethodName)` 获得输入参数的 `ManagementBaseObject`（`WmiOemPowerModeBackend.cs:95`），但这不读取方法签名的 CIM 类型或输出参数定义
- **报告证据**: `07-dynamic-evidence.md:76` 给出 WMI 类定义：`SetOemPowerSwitch(u8Input: UInt8[]) → (void/no out params)`
- 修复方向：新增 `OemWmiMethodContract`，在 `ProbeActiveInstances` 后或初始化时读取 `MethodData` 的 `InParameters`/`OutParameters`。

---

## 审计问题 4：报告的 `u8Input: UInt8[]` 是数组类型，当前代码如何传参？

**结论: 当前以 `byte`（标量）赋值，方法是错的，但 WMI 引擎可能容忍**

- `WmiOemPowerModeBackend.cs:96`：`input[InputParameterName] = value`（`value` 是 `byte`）
- WMI 类定义中 `u8Input` 是 `UInt8[]`（数组），但 WMIC 命令行可接受 `u8Input=1`（标量）。
- 直接通过 `ManagementObject.InvokeMethod` 传标量给 `UInt8[]` 参数的行为未测试。
- **报告证据**: `07-dynamic-evidence.md:76` 证明 `u8Input` 的 CIM 类型是 `UInt8[]`。
- 修复方向：合约探测后动态决定传 `byte` 还是 `byte[]`；如果 `IsArray == true`，传 `new byte[] { value }`。

---

## 审计问题 5：供应商工具的"三效切换"（Power Plan + Power Slider + OEM WMI）当前代码覆盖了多少？

**结论: 仅覆盖 Power Plan + OEM WMI，缺少 Power Slider (Overlay)**

- `TrayApplicationContext.cs:20`：仅实例化 `WindowsPowerPlanService`
- `TrayApplicationContext.cs:192-215`：调用 `_oemService.SetModeAsync` 后，仅同步 Windows 电源计划（`_powerPlanService.SetForMode(mode)`）
- **报告证据**: `04-write-call-graph.md:12` 明确列出三个步骤：
  1. `PowerSchemeManager.SetActivePlan()` → `PowerSetActiveScheme`
  2. `PowerSliderManager.SetPowerSlider()` → `PowerSetActiveOverlayScheme`
  3. `WriteIOManager.WritePowerModeIO()` → WMI `SetOemPowerSwitch`
- `04-write-call-graph.md:60` 证明 `PowerSetActiveOverlayScheme(Guid)` 是 P/Invoke。
- 修复方向：新增 `WindowsOverlayService`，实现 `PowerSetActiveOverlayScheme` P/Invoke。切换时依次执行三效，各自独立报告结果。

---

## 审计问题 6：当三效切换部分失败时，UI 如何呈现？

**结论: 当前只有两效（电源计划 + OEM），且 OEM 失败时完全跳过电源计划**

- `TrayApplicationContext.cs:193-214`：
  - OEM Rejected → 直接返回（不执行电源计划）
  - OEM Indeterminate → 直接返回
  - OEM Accepted → 执行电源计划
- 如果 OEM 成功但电源计划失败，`ShowOemAccepted` 中显示 `ToolTipIcon.Warning`（`TrayApplicationContext.cs:235`）
- **缺少 overlay 的 UI 呈现**
- 修复方向：引入 `PowerModeSwitchResult` 复合记录，包含三个独立子结果。UI 按子结果分别显示成功/失败状态。

---

## 审计问题 7：`LastAcceptedMode` 的命名和语义是否暗示已知硬件状态？

**结论: 是，名称误导性地暗示硬件已确认接受**

- `AppSettings.cs:102`：属性名为 `LastAcceptedMode`
- `TrayApplicationContext.cs:337-338`：UI 显示 "上次 OEM 已接受请求：{mode.DisplayName()}"
- `TrayApplicationContext.cs:48`：`_hardwareStatusItem` 文本为 "当前硬件模式：未回读"
- **报告证据**: `10-final-verdict.md:17`："There is no independent OEM hardware/firmware mode readback anywhere in the system."
- 修复方向：重命名为 `LastOemRequestAcceptedMode`，UI 文本改为 "上次 OEM 请求已发送" 而非 "已接受请求"，明确这是"请求已送达"，不承诺硬件已切换。

---

## 审计问题 8：当前代码中是否存在任何自动重试逻辑？

**结论: 否，无自动重试**

- `OemPowerModeService.SetMode` 是单次调用，没有任何 `retry` 循环
- `WmiOemPowerModeBackend.InvokeSingleInstance` 无重试逻辑
- `TrayApplicationContext.SwitchModeAsync` 无重试
- **报告约束已验证**：符合 "no retry on timeout" 约束。

---

## 审计问题 9：当前代码中是否存在重新引入 HIGHEST 权限自动启动任务的路径？

**结论: 否，无任何创建计划任务的代码**

- `StartupTaskService` 仅包含 `GetStateAsync()` 查询和 `RemoveAsync()` 删除，无 `CreateAsync` 方法
- `StartupTaskMenuDisplay.cs` 中无创建选项
- `TrayApplicationContext` 中没有调用启动任务创建的路径
- AGENTS.md 明确声明 "v0.0.2 暂停登录自动启动：StartupTaskService 不含任何创建 HIGHEST 任务的代码路径"
- **报告约束已验证**：符合 "no restoring /RL HIGHEST autostart" 约束。

---

## 审计问题 10：唯一实例约束在机器级别还是会话级别？当前如何实现？

**结论: 会话级别（Local 前缀），使用命名互斥体**

- `Program.cs`（未展示完整代码，基于 AGENTS.md）：`Local\MechrevoPowerTray.SingleInstance`
- `Local\` 前缀表示**会话级别**互斥体（每用户每会话），非机器级别（`Global\`）
- `OemPowerModeService.SetMode` 中 `probe.ActiveInstanceCount > 1` → Rejected（`OemPowerModeService.cs:73-80`），这是 WMI 实例级别的约束
- **报告证据**: `07-dynamic-evidence.md:21` 确认只有一个实例 `ACPI\PNP0C14\WMID_0`
- 修复方向：保持 `Local\` 互斥体（适用于单用户场景）。如果后续需要机器级别单实例（服务场景），可改为 `Global\`，但当前架构不需要。

---

## 变更优先级总结

| 优先级 | 变更 | 影响范围 |
|--------|------|---------|
| P0 | 引入 `OemWmiMethodContract` 动态探测 WMI 方法签名 | 新文件 + `WmiOemPowerModeBackend` + `IOemPowerModeBackend` |
| P0 | 修复 `InvokeSingleInstance` 根据合约决定 byte/byte[] 和 null→Accepted | `WmiOemPowerModeBackend.cs` |
| P0 | 修复 `OemPowerModeService.SetMode` 在无输出参数时返回 Accepted | `OemPowerModeService.cs` |
| P1 | 新增 `WindowsOverlayService`（`PowerSetActiveOverlayScheme`） | 新文件 |
| P1 | 引入 `PowerModeSwitchResult` 复合结果记录 | `OperationResults.cs` |
| P1 | 拆分 `TrayApplicationContext` 切换逻辑为三效独立执行 | `TrayApplicationContext.cs` |
| P2 | 重命名 `LastAcceptedMode` → `LastOemRequestAcceptedMode` | `AppSettings.cs`, `AppSettingsStore.cs`, `TrayApplicationContext.cs` |
| P2 | 更新 UI 文本避免暗示已知硬件状态 | `TrayApplicationContext.cs` |
| P2 | 新增审计相关的测试用例 | `OemPowerModeServiceTests.cs`, 新测试文件 |
