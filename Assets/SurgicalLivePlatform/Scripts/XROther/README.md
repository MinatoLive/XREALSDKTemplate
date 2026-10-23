# XROther：原生优先的手部射线排查与“按下即点击”

> 状态：**原生优先版计划已批准；Phase 0 只读诊断组件已添加，交互逻辑未修改**
> 目录用途：本任务所有新增运行时代码、诊断与说明统一放在此目录，便于整体回滚。

**两份计划，默认仅启用原生优先版：**

| 计划 | 状态 | 约束 |
| :--- | :--- | :--- |
| [原生优先版](file:///D:/UnityProject/XREALSDKTemplate/g3doc/00_plan/hand_ray_pinch_stability_press_plan.md) | **已批准 / 默认启用** | 只用原生/官方 API；不覆盖 pointer；不自定义 reader；仅 UI 按下即点击 |
| [授权重写版](file:///D:/UnityProject/XREALSDKTemplate/g3doc/00_plan/hand_ray_full_override_refactor_plan.md) | **暂停 / 未启用** | 仅在用户明确要求时启用；允许覆盖官方 C# API、嵌入 XRI、自定义 reader/Aim Pose、大规模改造 |

> 本 README 描述的是“原生优先版”的目录规划。除非用户明确要求启用“授权重写版”，否则不新增 `PinchHysteresisButtonReader`、`HandAimPoseStabilizer` 等覆盖文件。

---

## 1. 功能定位与硬约束

### 1.1 本目录做什么

| 能力 | 是否做 | 说明 |
| :--- | :---: | :--- |
| 原生阈值/漂移诊断 | 是 | 只读采集，不改原生输入/pointer |
| XRI 官方稳定化参数 A/B | 是 | 只改 Inspector 官方参数，不新增覆盖层 |
| 按下即点击 | 是 | 仅 Unity UI 时序，已确认允许 |
| 自定义模拟量 reader | **否** | 禁止绕过原生 `indexPressed` |
| Aim Pose/pointer 覆盖 | **否** | 禁止在 Unity 侧覆盖原生 pointer |
| 修改 SDK/AAR/PackageCache | **否** | 不修改原生库和 XRI 包 |

### 1.2 用户约束

- `0.1 → 0.4` 只是示例，**不是需求**；
- 用户不确定阈值来源；
- **禁止以绕过原生的方法实现**；
- 漂移优先用原生/官方 API 处理；工程侧只保留已确认的 UI 点击时序适配器。

---

## 2. 当前 Unity 场景基线（MCP 实测）

### 2.1 活动场景：`Test_HandsDemo`

路径：`Assets/Samples/XR Interaction Toolkit/3.3.2/Hands Interaction Demo/Test_HandsDemo.unity`

场景根对象：`XR Origin Hands (XR Rig)`、`XR Interaction Manager`、`EventSystem`、`Directional Light`、`TableHandle`、`Table`、`Permissions Manager`。

| 对象 | 路径 | 关键组件/当前值 |
| :--- | :--- | :--- |
| EventSystem | `EventSystem` | `EventSystem` + `XRUIInputModule` |
| 左手射线 | `XR Origin Hands (XR Rig)/Camera Offset/Left Hand/Near-Far Interactor` | `NearFarInteractor`、`InteractionAttachController`、`SphereInteractionCaster`、`CurveInteractionCaster`、`CurveVisualController` |
| 左手 Select reader | `.../Left Hand/Near-Far Interactor/Select Input` | `ReleaseThresholdButtonReader`，`press=1.0`、`release=0.9` |
| 左手 UI Press reader | `.../Left Hand/Near-Far Interactor/UI Press Input` | `ReleaseThresholdButtonReader`，`press=1.0`、`release=0.9` |
| 右手射线 | `.../Right Hand/Near-Far Interactor` | 结构同左手 |
| 左/右 Aim Pose | `.../Left Hand/Aim Pose`、`.../Right Hand/Aim Pose` | `TrackedPoseDriver`：`RotationAndPosition`、`UpdateAndBeforeRender` |
| 手部 caster | `CurveInteractionCaster` | `enableStabilization=true`、`P=0.25`、`A=20`、`aimTarget=NearFarInteractor`、`castOrigin=Aim Pose` |
| Wrist 按钮 | `.../Hand Menu With Button Activation/.../Text Button` | `Button.onClick` → `ToggleGameObject.ToggleActiveState`；`EventTrigger` 有 PointerDown/PointerClick 音频 |
| Table 按钮 | `Table/Front/Touchpad Flat/Buttons/.../TouchPad Button 1~9` | `Button.onClick` → 对应 `Toggle.isOn`；`EventTrigger` 有音频 |
| 手部追踪组件 | `LeftHandQuestVisual`、`LeftHandAndroidXRVisual`、`RightHandQuestVisual`、`RightHandAndroidXRVisual` | 全部 **inactive**；不能作为运行时手腕参考 |

> **本期范围锁定：** 仅 `Test_HandsDemo`。其他场景不在本期范围，不做 reader、caster、UI 或验收项说明。

> 本目录**不替换 reader、不覆盖 Aim Pose**；本场景仅做诊断、官方参数 A/B 和 UI 点击时序适配。

### 2.2 Phase 0 6DOF 基线（2026-10-21，非受控）

| 项 | 结果 |
| :--- | :--- |
| 设备/追踪 | XREAL X4000，`MODE_6DOF` |
| 样本 | 131 组 / 27.62 s |
| 左手 wrist 可用 | 95 / 131 |
| 右手 wrist 可用 | 119 / 131 |
| 左手 pinch / idx | 0.510~0.858 / **idx 始终 0** |
| 右手 idx=1 时 pinch | **0.779~1.0**（均值 0.938） |
| 右手 idx=0 时 pinch 最大值 | **1.0**（原生 bool 不是单一模拟量阈值） |
| 第一段捏合保持 | 手腕位移 1 mm，Aim 位移 **9.4 mm** |
| caster | `A=20`、`P=0.25`、`aimTarget` 开启 |

详细报告：[hand_ray_phase0_baseline_2026-10-21.md](file:///D:/UnityProject/XREALSDKTemplate/g3doc/04_testing/hand_ray_phase0_baseline_2026-10-21.md)

### 2.3 Phase 1 受控矩阵 v1（2026-10-21，单次）

8 阶段受控提示，369 组有效阶段样本：

| 阶段/指标 | 结果 |
| :--- | :--- |
| 左手捏合 | `idx=1` 仅当 pinch=1.000；`idx=0` 时 pinch 最大 0.764 |
| 右手捏合 | `idx=1` 时 pinch 0.848~1.000（均值 0.991）；`idx=0` 时仍出现 pinch=1.000 |
| HeadYawLeft | 双手 wrist 均未追踪，阶段无效 |
| HeadPitchUp/Down | wrist 静止 + 相机变化帧对中，Aim 角度变化 3~5°，相关系数不一致 |
| 捏合保持 | 手腕整体移动较大，Aim 位移不能单独归因头动 |
| caster | 仍为 `A=20/P=0.25/aimTarget`，A/B 未做 |

详细报告：[hand_ray_phase1_controlled_2026-10-21.md](file:///D:/UnityProject/XREALSDKTemplate/g3doc/04_testing/hand_ray_phase1_controlled_2026-10-21.md)

### 2.4 Phase 1 受控矩阵 v2（2026-10-22，相机前提示）

545 组样本，头动阶段双手基本全程追踪：

| 阶段/指标 | 结果 |
| :--- | :--- |
| HeadYawLeft | L 38/38、R 34/38 wrist；wrist 静止帧对中 Aim 角度 5.16°/3.31° |
| HeadYawRight | Aim 角度 4.81°/4.21°；相机范围 60.62° |
| HeadPitchUp | Aim 角度 4.08°/4.74°；相机范围 36.58° |
| HeadPitchDown | Aim 角度 4.82°/4.54°；相机范围 39.60° |
| RightPinchHold | R wrist 单步最大 5.9 mm，Aim 仍变化 **4.54°/4.5 mm** |
| 原生阈值 | L `idx=1` pinch 0.957~1.0；R 0.984~1.0；`idx=0` 时仍可达 0.965 |
| caster | 仍为 `A=20/P=0.25/aimTarget`，A/B 未做 |

详细报告：[hand_ray_phase1_controlled_2026-10-22.md](file:///D:/UnityProject/XREALSDKTemplate/g3doc/04_testing/hand_ray_phase1_controlled_2026-10-22.md)

### 2.5 Phase 2 caster A/B/C 首轮对比（单次）

| 指标 | A（A=20） | B（关闭） | C（A=2/P=0.05） |
| :--- | :---: | :---: | :---: |
| 头动阶段平均 Aim 角度范围 | 7.68° | **7.14°** | 10.09° |
| wrist 静止帧 Aim 均值/最大角度 | 1.60° / 4.46° | **1.51° / 4.25°** | 1.54° / 4.75° |
| 捏合保持平均 Aim 角度范围 | **3.09°** | 3.81° | 3.54° |
| 捏合保持 hc 均值角度 | 1.26° | **1.22°** | 1.42° |

**结论：** 三组都存在 wrist 静止时的 Aim 头动耦合，关闭/降低 caster 稳定化没有根治；B 头动略好但捏合保持更差，C 头动阶段更差。单次差异受动作波动影响，**暂保留 A=20/P=0.25 基线**，三组各重复 3 次后再定参。

详细报告：[hand_ray_phase2_caster_abc_2026-10-22.md](file:///D:/UnityProject/XREALSDKTemplate/g3doc/04_testing/hand_ray_phase2_caster_abc_2026-10-22.md)

---

## 3. 目录与文件规划

```text
Assets/SurgicalLivePlatform/Scripts/XROther/
├─ README.md                        // 本文件：功能、逻辑、注释、链路
├─ HandRayDiagnostics.cs            // Phase 0 只读诊断；define/Inspector 开关，发布构建不输出
├─ PressDownClickRules.cs           // 纯规则：Button/ScrollRect/Interactor 过滤
├─ PressDownClickRelay.cs           // 组件：pointerDown → Button.onClick（仅 UI 时序）
├─ UITargetLockRules.cs             // 方案 A：只锁普通 Button，排除拖拽/ScrollRect
├─ UITargetLockController.cs        // 方案 A：按 pointerId 记录锁定目标，超时自动清理
├─ UIPressTargetResolver.cs         // 共享：Button/ScrollRect/真实 Drag 判定
├─ PinchAimLockRules.cs             // 方案 B：pinch 锁定/释放规则
└─ PinchAimPoseLock.cs              // 方案 B：半捏合冻结 Aim Pose（仅普通 Button）
```

测试：

```text
Assets/SurgicalLivePlatform/Tests/Editor/
├─ PressDownClickRulesTests.cs
├─ PressDownClickRelayTests.cs
├─ UITargetLockRulesTests.cs
└─ PinchAimLockRulesTests.cs
```

**不会创建**（已否决）：

```text
PinchHysteresisRules.cs
PinchHysteresisButtonReader.cs
HandAimStabilityRules.cs
HandAimPoseStabilizer.cs
```

原因：前者绕过原生 `indexPressed`，后者覆盖原生 pointer/Aim Pose，均违反“全部仅原生/官方 API”的约束。

命名空间统一为 `SurgicalLivePlatform`，测试命名空间为 `SurgicalLivePlatform.Tests`。

> Phase 0 诊断需要 `Unity.InputSystem`（`InputActionReference`）与 `Unity.XR.XREAL`（`GetTrackingType`）程序集引用，已加入 `SurgicalLivePlatform.Scripts.asmdef`；仅用于诊断，不改变交互逻辑。

---

## 4. 功能与逻辑

### 4.1 `HandRayDiagnostics`（只读诊断）

**功能：** Phase 0 采集真实数据，不改变输入、pointer 或 UI 行为。

**采集字段：**

```text
pinchStrengthIndex / indexPressed
selectInput.ReadIsPerformed / ReadWasPerformedThisFrame / ReadWasCompletedThisFrame
uiPressInput.ReadIsPerformed / ReadWasPerformedThisFrame
AimPose.localPosition / localRotation / position / rotation
caster origin / rayEndPoint / effectiveCastOrigin
Camera / Camera Offset / XR Origin 的 pose
UI pointerDown / pointerClick 时间戳
XREALPlugin.GetTrackingType()
```

**要求：**

- 频率限制 5~10 Hz，避免日志刷屏；
- `#if HANDRAY_DIAGNOSTICS` 或 Inspector 开关；
- 发布构建不编译/不输出；
- 只读，不写 Transform，不替代输入。

**用途：** 确认原生阈值来源、0.4 是否可达、漂移与头动的相关性、本场景的 caster 基线。

---

### 4.2 原生阈值核查（无代码）

**原则：**

```text
indexPressed 是原生捏合判定的唯一来源
  → 不新增模拟量 reader
  → 不改 ReleaseThresholdButtonReader 的 bool 分支
  → 0.4 只是示例，不作为实施目标
```

**处理路径：**

| 情况 | 处理 |
| :--- | :--- |
| 原生提供官方阈值配置 | 只使用官方接口；0.35/0.40/0.45 仅作为 A/B 候选 |
| 原生不提供配置 | 保持原生 `1.0/0.9 + indexPressed`；记录结论；必要时向 XREAL 提交需求 |
| 有人提议自定义 reader | 代码评审直接拒绝；本目录不创建该文件 |

---

### 4.3 XRI 官方稳定化参数 A/B（无代码）

**功能：** 只通过 Inspector 调整手部 `CurveInteractionCaster` 的官方参数，观察漂移与延迟。

**参数：**

| 参数 | 作用 |
| :--- | :--- |
| `enableStabilization` | 是否启用稳定化 |
| `positionStabilization` | 位置稳定化 |
| `angleStabilization` | 角度稳定化 |
| `aimTarget` | 端点锁定目标 |

**A/B 组：**

| 组 | `Test_HandsDemo` 设置 |
| :--- | :--- |
| A（现状） | `A=20`、`P=0.25`、`aimTarget=NearFarInteractor` |
| B | `enableStabilization=0`、`P=0`、`A=0` |
| C | `A=2`、`P=0.05` |
| D | `A=10`、`P=0.10` |

**要求：**

- 不新增 Aim Pose 覆盖组件；
- 不改 `pointerPosition/pointerRotation`；
- 每组记录漂移、滞后、点击成功率；
- 参数改动可一键恢复。

---

### 4.4 `PressDownClickRelay`（唯一允许的工程侧逻辑）

**功能：** 手部射线 UI 点击从 release 改为 press。适配器挂在 EventSystem，订阅 `XRUIInputModule.pointerDown`，在按下时触发目标 `Button.onClick`，并阻止同一按压在 release 时再次触发。

**边界：**

- 读取原生 UI press 事件，不修改原生捏合判定；
- 不修改 pointer/Aim Pose；
- 不修改 XRI 包；
- 不逐个按钮迁移监听。

**过滤条件：**

```text
必须是 TrackedDeviceEventData
必须是 NearFarInteractor（手部射线；Poke/控制器不处理）
目标层级必须能找到 Button
Button 必须 active、interactable
默认排除活动 ScrollRect 内可滚动内容中的按钮
```

**流程：**

```text
XRUIInputModule.ProcessPointerButton(Pressed)
  → pointerDown 事件
  → PressDownClickRelay 找到 Button
  → eventData.eligibleForClick = false
  → 记录 PendingPress(pointerId, Button)
  → LateUpdate：再次校验 Button，调用 button.onClick.Invoke()
  → release 时 UIInputModule 因 eligibleForClick=false 不再触发 pointerClick
```

**已知行为变化：**

- `Test_HandsDemo` Wrist `Text Button` 会在按下时切换 `ToggleGameObject`；`EventTrigger.PointerClick` 音频不再触发，`PointerDown` 音频保留。
- Table `TouchPad Button 1~9` 在手部射线指向时会在按下时切换对应 Toggle；Poke 路径不受影响。
- 若后续启用 `Hand Menu ScrollView`，ScrollRect 内按钮默认保持 release 语义。
- 按下点击与目标锁定已解耦：`shouldHandle` 只决定是否在 `pointerDown` 执行 `onClick`；`shouldLock` 只决定是否记录目标锁定。Drag/Slider/ScrollRect 内容不执行按下点击，保留原生拖拽/滚动语义。

### 4.5 `UITargetLockController`（方案 A）

**功能：** 按下普通 Button 时按 `pointerId` 记录锁定目标；`pointerUp` 或 `maxLockSeconds` 后释放。它只维护“按下目标”，不冻结射线、不改 pointer。

**锁定资格：**

```text
手部 NearFarInteractor
+ 目标为 Button 且 active/interactable
+ 不是真实 Drag 目标（排除 ScrollRect/Slider/自定义 IBeginDragHandler/IDragHandler）
+ EventTrigger 只有音频/Click 条目时仍可锁定；只有存在 Drag 条目才视为可拖拽
```

**与现有逻辑的关系：**

- `PressDownClickRelay` 在 pointerDown 时先调用 `UITargetLockController.Lock`，再触发按下点击；
- `pointerUp` 时 `Unlock`；
- 拖拽 UI 和 TableHandle 不锁定，继续走原生 UI drag / `XRGrabInteractable`；
- 不修改 Aim Pose、reader、pointer。

**参数：**

| 参数 | 默认 | 说明 |
| :--- | :---: | :--- |
| `maxLockSeconds` | 2 s | 超时自动释放锁定 |
| `excludeScrollRect`（Relay） | true | 排除活动 ScrollRect 内按钮 |
| `logHandledPresses`（Relay） | false | 调试日志 |

### 4.6 `PinchAimPoseLock`（模拟量区间锁定 + 越阈点击）

**状态（2026-10-23）：** 按 [模拟量区间锁定计划](file:///D:/UnityProject/XREALSDKTemplate/g3doc/00_plan/analog_pinch_band_click_plan.md) 改为读取 `selectInput.ReadValue()`。用户已确认方案，跳过 EditMode 测试，直接真机验证。

**当前实现：**

- `Update`（`DefaultExecutionOrder(-200)`）每帧读取 `interactor.selectInput.ReadValue()`；
- `value > minValue` 且目标是可锁定 Button 时冻结 Aim Pose；`value ≤ minValue` 才解锁；
- 锁定期间首次 `value > maxValue` 时对锁定 Button 调用一次 `onClick`，每个锁定周期最多一次；
- 未锁定时若 `value > maxValue`：只要能解析到可点击 Button，也会直接点击一次并在同帧锁定（越过锁定区间的容错）；目标失效则不点击；
- 对可锁定 Button 在 `XRUIInputModule.pointerDown` 中置 `eligibleForClick=false`，屏蔽原生 release click；非 Button 目标（ScrollRect/Slider/3D 抓取/其他 Selectable）保持原生；
- 锁定时取当前 `effectiveCastOrigin` 写入 Aim Pose，并把 caster 的 `positionStabilization/angleStabilization` 置 0；解锁恢复原值；
- `[BeforeRenderOrder(90)]` 再写一次 Aim Pose；
- 边缘目标（最近 `300 ms` 悬停、`25%` 扩展矩形）仍可锁定，但点击只由 `maxValue` 触发；
- `interactor.hasSelection` 为 true、真实 Drag、ScrollRect 内 Button 均不锁。

**参数：**

| 参数 | 默认 | 说明 |
| :--- | :---: | :--- |
| `minValue` | 0.90 | 高于该值进入锁定；≤ 该值解锁 |
| `maxValue` | 0.98 | 锁定期间首次高于该值点击一次 |
| `excludeScrollRect` | true | ScrollRect 内按钮不锁 |
| `edgeTolerancePercent` | 0.25 | 按钮 Rect 宽高向外扩展的容差比例 |
| `lastHoverMemorySeconds` | 0.3 | 最近悬停按钮的记忆时间 |
| `logStateChanges` | true（真机调试期） | lock/unlock/click 日志 |

> Main2 已恢复左右手 `HandRayHitIndicator`（RayHit 圆环）与 `HandRayDash.mat` 线材质，圆环尺寸为 `0.005/0.016/0.06`（基础尺寸 ×2）。线型：默认白色，未锁定虚线，锁定实线，`value > maxValue` 蓝色；圆环的独立 hover 锁定已关闭，只在 `PinchAimPoseLock` 冻结时固定。冻结 Aim Pose 后射线端点固定，但线起点 `PinchPointStabilized` 仍跟手；如需整条线静止需另外冻结线起点。真实捏合模拟量存在左右手差异，`minValue/maxValue` 必须按真机日志逐手标定；Main2 当前场景值为 `minValue=0.30`、`maxValue=0.98`。

### 4.7 射线视觉（现有组件）

`Test_HandsDemo` 左右手 `Near-Far Interactor` 已挂载现有组件：

- `HandRayLineStyleController`：`HandRayDash.mat`、`keepFullLengthWhenNoHit=true`；默认白色，未锁定为虚线，`PinchAimPoseLock.isLocked` 后为实线，`value > maxValue` 时在 `[BeforeRenderOrder(102)]` 覆盖为蓝色；无命中时保持 `maxVisualCurveDistance`（10 m）长线，不回缩。
- `HandRayHitIndicator`：每只手一个 `RayHit` 对象（MeshFilter + MeshRenderer + `HandRayHit.mat`）；圆环随捏合缩小、孔径归零变实心；空闲白色，锁定/按下为绿色。颜色通过 MPB `_Color` 写入，不修改共享材质。Main2 中圆环基础尺寸 ×2（`scalePerMeter=0.005`、`minScale=0.016`、`maxScale=0.06`），并关闭独立 hover 锁定，只在 `PinchAimPoseLock` 冻结时固定。
- 未新增射线显示脚本/Shader/材质；只扩展现有 `HandRayHitIndicator` 与 `HandRayVisualRules`。

> 视觉组件只读交互状态，不改变 reader、pointer、点击或 3D 选择。

---

## 5. 调用链路

### 5.1 原生 pointer 链路（不覆盖）

```text
XREAL 原生 pointerPosition/pointerRotation
  → Aim Position/Rotation 动作
  → Aim Pose 的 TrackedPoseDriver（原生写入，不覆盖）
  → NearFarInteractor
  → CurveInteractionCaster（仅调官方稳定化参数）
  → UI/3D 命中 + CurveVisualController 画线
```

### 5.2 原生捏合与点击链路

```text
<XREALHandTracking>{Hand}/indexPressed
  → Select / UI Press（bool）
  → ReleaseThresholdButtonReader（保持 1.0/0.9 + 原生 bool）
  → NearFarInteractor.selectInput / uiPressInput
  → XRInteractionManager / XRUIInputModule
  → PressDownClickRelay（pointerDown → Button.onClick）
  → 业务监听（登录、切换、关闭、Toggle 等）
```

### 5.3 视觉链路（不修改）

```text
XR Hands ThumbTip/IndexTip
  → PinchPointFollow + OneEuroFilterVector3
  → Pinch Point Stabilized（可见线起点）
  → CurveVisualController
  → HandRayLineStyleController / HandRayHitIndicator（只读 selectInput.ReadValue()）
```

---

## 6. 场景挂载说明

### 6.1 `Test_HandsDemo`

| 对象 | 操作 |
| :--- | :--- |
| `EventSystem` | 挂 `PressDownClickRelay`，引用 `XRUIInputModule` 与左右手 NearFarInteractor |
| 手部 `CurveInteractionCaster` | 只改官方稳定化参数做 A/B；起点 `A=20`、`P=0.25`、`aimTarget=NearFarInteractor` |
| `HandRayDiagnostics` | Phase 0 临时挂载；完成后禁用/移除 |
| reader / Aim Pose | **不修改** |

**回滚：** 禁用 `PressDownClickRelay` 即恢复 release click；恢复 caster 原参数即回到基线。

---

## 7. 注释规范

| 层级 | 规范 |
| :--- | :--- |
| Public 类型/方法 | 使用 XML `<summary>`，说明输入、输出、单位、坐标系和副作用 |
| 纯规则 | 注释只解释“为什么这样判定”，不重复代码字面含义 |
| 时序 | 对 `LateUpdate`、`pointerDown`、`eligibleForClick` 说明与标准 UI 的关系 |
| 坐标系 | 诊断中明确 `world` / `tracking` / `local` |
| 性能 | 不在 `Update`/`LateUpdate` 中分配堆内存；容器预分配；日志限频 |
| 场景路径 | 对 `Test_HandsDemo` 的对象路径/参数在注释或 Inspector Tooltip 中注明 |
| 合规 | 注释明确“不修改原生输入/pointer；仅 UI 时序” |

> 默认不写“逐行解释代码”的注释；只在隐藏约束、时序依赖、坐标系、原生限制处写“为什么”。

---

## 8. 测试与验收

### 8.1 EditMode

- `PressDownClickRulesTests`：Button/ScrollRect/Interactable/控制器过滤。
- 既有 `HandRayVisualRulesTests`、`HandFrustumMonitorTests` 不回归。
- **不新增 reader 测试**，因为本版不实现自定义 reader。

### 8.2 PlayMode / 真机

| 场景 | 观测 | 目标 |
| :--- | :--- | :--- |
| 手不动，头 yaw ±30° | Aim Pose/射线漂移 | 记录基线；官方参数 A/B 对比 |
| 手不动，头 pitch ±20° | 同上 | 同上 |
| 捏合保持 10 s | 端点是否离开按钮 | 记录；官方参数 A/B |
| 捏合中移动手 | 射线是否跟手 | 记录 |
| `Test_HandsDemo` Wrist Text Button | 按下即切换、无重复、音频 | 通过 |
| `Test_HandsDemo` Table TouchPad 1~9 | 按下即切换、Poke 不回归 | 通过 |
| `Test_HandsDemo` ScrollRect（如启用） | 拖动不误触 | 0 次 |
| 控制器 | 原有语义 | 不变 |
| Profiler | 稳态 GC | 0 B |

### 8.3 合规验收

- 代码审计确认：无自定义 reader、无 Aim Pose 覆盖、无原生库/包修改；
- `Git diff` 只包含 `XROther` 与测试/场景配置；
- 0.4 不作为验收值；若原生不可配置，保持原生。

---

## 9. 风险与开关

| 风险 | 开关/缓解 |
| :--- | :--- |
| 原生阈值不可配置 | 保持原生；向 XREAL 提交需求；不绕过 |
| 官方稳定化仍不能根治漂移 | 输出证据，向 XREAL 反馈；不覆盖 pointer |
| 稳定化参数过度 | A/B 多组；可回滚 |
| 滚动列表误触 | `PressDownClickRelay.excludeScrollRect=true` |
| `Text Button` 点击音频变化 | 验收确认；必要时补充按下音效 |
| Table Toggle 按下即切换 | 保留原生 select；如误触明显，按区域/白名单过滤 |
| 只手配置遗漏 | 逐只手核对 `Test_HandsDemo` 的左右手 reader/Aim Pose/EventSystem 引用 |
| 诊断代码进入发布 | `#if HANDRAY_DIAGNOSTICS` + Inspector 开关；发布前检查 |

---

## 10. 实施状态

- [x] 计划书修订版
- [x] 本 README
- [x] 原生优先版计划已批准（2026-10-21，默认启用）
- [x] 授权重写版计划已暂停（未启用）
- [x] Phase 0 诊断组件 `HandRayDiagnostics.cs` + 编辑器 Play Mode 冒烟
- [x] Phase 0 真机 6DOF 非受控基线（XREAL X4000，131 组样本 / 27.62 s）
- [x] Phase 1 受控序列 v1（8 阶段，369 组有效阶段样本）
- [x] Phase 1 受控序列 v2（相机前提示，545 组样本，双手追踪良好）
- [ ] Phase 1 受控矩阵重复 3 次（固定手部、明确单手捏合）
- [x] Phase 1 原生阈值核查结论（无代码：原生 pinch≈0.96~1.0）
- [x] Phase 2 caster A/B/C 首轮（A=20 / 关闭 / A=2/P=0.05）
- [ ] Phase 2 每组重复 3 次后再定参
- [x] Phase 3 按下即点击适配器（Rules + Relay + 8/8 EditMode 测试 + APK）
- [x] Phase 3 真机 UI 点击验证（2026-10-22 用户确认效果良好）
- [x] Phase 4 EditMode 全量测试（45/45 通过）
- [x] Phase 4 场景/代码级回归（ScrollRect/Poke/控制器）
- [x] 方案 A UI 目标锁定（Rules + Controller，全量 52/52 tests）
- [x] 方案 B Aim Pose 锁定（Rules + Resolver + Lock，全量 59/59 tests）
- [x] 方案 B 真机验证：**失败**（模拟量误触发；Aim Pose 冻结但实际投射点漂移 12.5~19.4 cm）
- [x] 方案 B 修复实现（原生按下触发 + 锁定实际投射姿态；EditMode 59/59，Play Mode 状态验证通过）
- [x] 边缘锁定（25% 容差 + 最近 300 ms 悬停）与补点击去重（EditMode 71/71，Play Mode 冒烟通过）
- [x] 模拟量区间锁定 + 越阈单击（`PinchAimPoseLock` 改写；Main2 左右手 `min=0.90`、`max=0.98`、日志开启）
- [ ] 模拟量区间真机验证（按用户要求跳过 EditMode，直接 APK 真机观察）
- [x] 射线视觉挂载（现有 `HandRayLineStyleController` / `HandRayHitIndicator`；虚线/长线/实线/圆环变色）
- [ ] 方案 B/视觉/边缘真机验证（无误锁；捏合期间端点位移 ≤ 2 cm；释放恢复；圆环颜色与边缘点击）
- [ ] 场景漂移 A/B 真机验证（原始样例 vs 当前场景；基线 APK 已构建安装）
- [ ] caster A/B 每组重复 3 次
- [ ] 设备 Profiler/GC 正式采样
- [ ] 如头动耦合仍不可接受，向 XREAL 反馈

> Phase 0/1 已在 `Test_HandsDemo` 挂载只读诊断组件并完成两次受控序列；Phase 2 完成 A/B/C 首轮；Phase 3 适配器已通过用户真机 UI 验证；Phase 4 单轮回归完成（EditMode 45/45）；方案 A UI 目标锁定与方案 B Aim Pose 锁定已实现，全量 EditMode **59/59** 通过，方案 B 待真机验证。未修改 reader 或业务 `onClick`；方案 B 在用户明确批准下覆盖 Aim Pose（仅普通 Button），禁用 `PinchAimPoseLock` 即可恢复原生 pointer。基线报告见 `g3doc/04_testing/hand_ray_phase0_baseline_2026-10-21.md`，v1/v2 报告见 `hand_ray_phase1_controlled_2026-10-21.md`、`hand_ray_phase1_controlled_2026-10-22.md`，A/B/C 报告见 `hand_ray_phase2_caster_abc_2026-10-22.md`。默认只启动原生优先版；方案 B 为单独批准的实验。

---

## 11. 相关文档

- [手部射线原生优先排查与按下即点击计划书](file:///D:/UnityProject/XREALSDKTemplate/g3doc/00_plan/hand_ray_pinch_stability_press_plan.md)
- [手部射线全链路覆盖与大规模改造计划书（授权重写版）](file:///D:/UnityProject/XREALSDKTemplate/g3doc/00_plan/hand_ray_full_override_refactor_plan.md)
- [caster A/B/C 对比报告](file:///D:/UnityProject/XREALSDKTemplate/g3doc/04_testing/hand_ray_phase2_caster_abc_2026-10-22.md)
- [Phase 4 回归记录](file:///D:/UnityProject/XREALSDKTemplate/g3doc/04_testing/phase4_regression_2026-10-22.md)
- [捏合射线锁定/拖拽可行性方案（仅方案，未启用）](file:///D:/UnityProject/XREALSDKTemplate/g3doc/00_plan/pinch_ray_lock_drag_feasibility_plan.md)
- [长捏合漂移与按钮点击丢失评估](file:///D:/UnityProject/XREALSDKTemplate/g3doc/00_plan/pinch_drift_ui_stability_plan.md)
- [捏合射线漂移与防抖-延迟平衡](file:///D:/UnityProject/XREALSDKTemplate/g3doc/00_plan/ray_drift_jitter_latency_plan.md)
- [方案 B 捏合锁定修复计划](file:///D:/UnityProject/XREALSDKTemplate/g3doc/00_plan/pinch_aim_lock_fix_plan.md)
- [射线视觉、边缘锁定与场景漂移 A/B 计划](file:///D:/UnityProject/XREALSDKTemplate/g3doc/00_plan/ray_visual_edge_lock_drift_ab_plan.md)
