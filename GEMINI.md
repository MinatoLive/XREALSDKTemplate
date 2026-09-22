# Google Antigravity Unity 6.0 Rules

**【豁免条款】**：如果你识别到当前任务属于“极小规模的简单维护”（例如：改错别字、变量重命名、简单参数调整等），允许暂时不被繁琐的文档流阻塞，但你必须在执行前主动向用户提问（交由用户确认 yes or no），获得同意后方可直接修改，若回答no,则以就依旧严格地遵循本文件中的所有规则。





## 1. 核心技术栈与 Google 基建集成 (Core Tech & Infra)
- **Core Library**: superpowers (位于第三方库 	hird_party/antigravity/superpowers/，严禁在业务代码中重新实现该库已有的底层算法或能力)。
- **Toolchain**:
  - **unityMCP**: 作为与 Unity 编辑器及 Google Blaze/Bazel 构建系统交互的唯一桥梁。所有资产打包与预制体生成必须通过 unityMCP 转化为 Blaze 可识别的 target。禁止用生成unity tools栏按钮的方式来让我操作才能实现的方式。
  - **codegraph**: 深度对接 Google Kythe/CodeSearch。修改代码前，必须通过 codegraph 生成基于内部依赖图的影响范围分析（Impact Analysis），并附加在 Critique 的 CL (Changelist) 描述中。

## 2. 强制文档前置工作流 (Doc-First / Design Doc Protocol)
**【拦截规则】**：在未完成 Google Design Doc（One-pager 或全量设计文档）并通过团队 Review 前，严禁开启 CL 编写业务逻辑代码。

### 2.1 基于 g3doc 的文档体系 (g3doc Architecture)
放弃零散的 Markdown，全面采用 `g3doc` 规范，按五大类目组织，路径需严格对应：
- `g3doc/00_plan/`：批准的 implementation_plan
- `g3doc/01_requirements/`：PRD 与产品需求说明。
- `g3doc/02_design/`：架构图、数据结构、`codegraph` 依赖图谱以及标准的 Google Design Doc（需包含 Alternatives Considered 章节）。
- `g3doc/03_development/`：API 契约、`superpowers` 库的接入指南。
- `g3doc/04_testing/`：TAP 测试策略、覆盖率目标。
- `g3doc/05_operations/`：SRE 运维手册、CI/CD Presubmit 配置说明。
- `g3doc/06_walkthrough/`：详细的功能操作指南和验收列表。
- `g3doc/07_test/`：验收时的测试问题归档。
*注：每次提交代码若涉及逻辑变更，必须包含同一 CL 中的对应 `g3doc` 更新。文档均用中文。*
*注2：如果后续有需要重载、重写、修改以前的 plan 以及 walkthrough，则直接在原本的文档上进行修改，不要创建新文件。*

## 3. 严格 TDD 与 TAP 集成测试流程 (Strict TDD & TAP)
遵循 Red -> Green -> Refactor 原则，并与 Google Test Automation Platform (TAP) 强制绑定。
1. **Red**: 使用 Unity Test Framework 编写用例，通过 unityMCP 触发本地跑测，确认失败。
2. **Green**: 编写符合 **Google C# Style Guide** 的生产代码。
3. **Refactor**: 优化依赖，使用 codegraph 确认模块解耦。
4. **Presubmit**: 测试代码必须作为独立的 Blaze test target 提交。TAP 上的覆盖率若低于项目设定的阈值（如 80%），Critique 上的 CL 将被 Tricorder 自动 Block（打上 Code-Review -1 或 Unresolved 标签）。
5. 测试代码不要打包到运行时的assets中去。测试相关脚本都放一起。

## 4. 生产级代码规范与 Readability (Production & Readability)
- **Readability**: 所有 C# 代码必须严格符合 Google C# 规范。优先使用依赖注入（DI），严禁滥用 Unity 的 Singleton 或 GameObject.Find。
- **性能红线 (Performance)**: 
  - 禁止在 Update/FixedUpdate 中进行堆内存分配，防止触发垃圾回收 (GC) 导致帧率抖动。
  - 必须使用对象池 (Object Pool) 复用实体。
- **资源管理**: 废弃 Resources 目录，全面采用 Addressables，并通过内部 CDN 与 Blaze 资产管道进行分发和版本控制。

## 5. 自动化校验与 Critique 拦截机制 (Automated Checks & Critique Hooks)
AI 辅助工具（如 Cursor）在生成或修改代码时，必须执行以下自检，并将校验机制嵌入 Presubmit 流程：
- [ ] **Design Doc Check**: 检查当前目录下是否有对应的 g3doc 更新？如果没有，停止编码，提示开发者先撰写/更新文档。
- [ ] **Dependency Check**: 检查是否越权引入了未经声明的 superpowers 模块或其他业务线模块？（由 codegraph 在 Presubmit 时通过 Tricorder 报错）。
- [ ] **Test Coverage**: 对应的 _Test.cs 文件是否涵盖了新增的 Public APIs？
- 代码提交至 Critique 后，Tricorder 会运行静态扫描。任何 Unity 性能反模式（如装箱操作、频繁的 Transform 层级修改）将自动生成 Unresolved Comment 拦截提交。

## 6. 异常处理
- **Unity MCP 连接异常时**:你停下，提示用户来进行帮你处理 。

