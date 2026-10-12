# RoomCardItem 数据绑定与交互重构计划

## 需求收集与背景

根据我们在对话中的沟通，需要对 `RoomCardItem` 组件进行核心方向的重构：
1. **防止事件重复注册**：将按钮的事件监听移出 `BindData` 等会被列表循环调用的函数中。
2. **正确处理封面图片**：明确 `cover_image` 的组件类型为 `RawImage`，并预留真实的异步加载接入点。
3. **复合UI样式动态解析**：处理由底图和文字组成的复合型组件（`status` 和 `stream_type`），根据不同状态动态渲染不同主题颜色。
4. **时间精准格式化**：解析后端的 ISO 8601 UTC 时间并展示为短格式（`MM/dd HH:mm`）。
5. **前置拦截校验**：进入房间前检测状态，只有符合“正在进行”条件的房间才允许唤起入会指令。
6. **单一选中与样式交互**：重新引入单选高亮交互，默认状态使用 `unclick` 样式配置，被点击的单个卡片变更为 `onclick` 样式并重置互斥其他卡片。
7. **【新增】禁止面板拖拽的 UI 解耦查找方案**：为了防止预制体嵌套或频繁更新导致的丢失引用（Missing Reference），禁止在 `RoomCardItem` 中暴露 `public GameObject` 等供面板拖拽。采用按标记名字自动收集的方案解耦。

---

## 实施细节 (Implementation Details)

我们将直接修改 `RoomCardItem.cs` 并新增 `UIMark.cs`，实施如下技术方案：

### 1. 新增 UIMark 标记系统 (UI 解耦核心)
- 新建一个轻量级组件 `UIMark.cs`（仅包含一个 `public string markName` 字段）。
- 在 `RoomCardItem.cs` 中，将原本暴露为 `public` 的 `GameObject` 和 `Button` 全部修改为 `private` 变量。
- 在 `Awake()` 阶段，调用一次 `GetComponentsInChildren<UIMark>(true)` 统一收集卡片内所有的 UI 节点，建立字典映射。
- 利用字典（或封装的 `GetNode` 方法），在内部通过 `markName` 完成对应 `private` 字段的初始化填充（例如 `cover_image = GetNode("cover_image")`）。

### 2. 生命周期与资源初始化 (Awake & OnDestroy)
- **Awake**:
  - 除了解析 `UIMark`，在获取到 `Login_btn` 后，只为其注册**一次**点击事件，彻底根除反复调用 `BindData` 造成的监听器冗余堆积和闭包内存泄漏问题。
- **OnDestroy**:
  - 当卡片被销毁回收时，判断自身是否为全局静态 `currentSelectedCard`，若是则将其置空，防止野指针引发报错。

### 3. 状态机互斥与入会校验 (Click Logic)
点击 `Login_btn` 时触发核心交互逻辑：
- **第一步：前置入会状态拦截**：
  检测 `currentRoomItem.status`。如果是已结束的房间（如状态不是 `"LIVE"` 或指定状态），则弹出拦截提示（通过 Log），阻止其继续调用 `RoomJoinController.Instance.JoinRoom` 发起业务请求。
- **第二步：互斥高亮切换**：
  若允许点击且该卡片未被选中，通过全局静态变量 `currentSelectedCard` 获取上一个卡片实例：
  - 调用旧卡片的 `ApplyStyle("unclick")` 方法退回默认样式。
  - 将全局静态指针更新为当前自身。
  - 调用自身的 `ApplyStyle("onclick")` 方法，应用从持久化配置中读出的高亮点击样式。

### 4. 数据纯净绑定机制 (BindData)
- **组件提取**：利用之前写的 `SetText` 方法快速赋值。
- **时间格式化处理**：
  引入 `System.DateTime.TryParse` 解析 `roomItem.created_at`，并将格式化指令 `.ToString("MM/dd HH:mm")` 的输出写入 `started_at` 节点文本。
- **复合 UI 逻辑 (Status & StreamType)**：
  在绑定时，除了文本，同时获取 `status` / `stream_type` 挂载在自己身上的 `Image`（底图边框）。根据返回的 `status` 数据，匹配不同的文本颜色和底色（如红色代表 LIVE 直播中，灰色代表 ENDED 已结束）。
- **图片加载存根**：
  在 `cover_image` 提取 `RawImage` 后写下 TODO 代码块，等待后续插入图片网络加载库（如 ImageLoader）。
- **样式初始化锁定**：
  `BindData` 完成所有赋值后，默认触发一次 `ApplyStyle("unclick")` 将组件置于默认未点击的非激活态。

---

## User Review Required

> [!IMPORTANT]  
> 增补的“方案 C（UIMark 解耦）” 的代码结构细节已经更新在文档中！
> 
> 请您仔细确认以上的实施细节。如果确认无误，请直接回复 **“同意执行”**。我将立即创建 `UIMark.cs` 并重构 `RoomCardItem.cs`！
