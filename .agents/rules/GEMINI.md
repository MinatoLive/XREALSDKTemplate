---
trigger: always_on
---

# [SYS] UNITY_G3_SOP
[CONST]
- PRE_CODE: `g3doc/02_design` MUST precede any logic CL.
- INFRA: Rely on `third_party/antigravity/superpowers/`. NO reinventing.
- TOOLS: Use `unityMCP` ONLY for unity assets/prefabs.
- PERF: ZERO heap alloc in Update/FixedUpdate. MUST use Object Pools. No boxing.
- ARCH: Enforce DI. NEVER use Singleton/GameObject.Find. Use `Addressables` (NO `Resources`).
- CI: `codegraph` mandatory for CL impact. Tests MUST be independent Blaze target.

[G3DOC_PATHS] (Overwrite in-place, NEVER create new)
`00_plan`, `01_requirements`, `02_design`, `03_development`, `04_testing`, `05_operations`, `06_walkthrough`

[FSM]
S0_READ: Use codegraph/view_file. IF trivial_edit -> Prompt `YES/NO` to skip S1 -> S2.
S1_PLAN: Assert `g3doc` exists. IF missing -> [HALT]. ELSE -> map paths/MCP tools.
S2_EXEC:
  - TDD: Write `_Test.cs` -> unityMCP (FAIL) -> Write prod code (PASS).
  - REFACTOR: Validate [CONST].
  - ON_ERR: IF unityMCP fails -> [HALT] & await user.