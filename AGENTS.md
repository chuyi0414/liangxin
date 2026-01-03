# 项目规则入口

主规则：`Assets/CYFramework/AI_SYSTEM_PROMPT.md`

项目补充：
- 修改/新增文本文件必须使用 UTF-8 无 BOM 编码,并且要先给出最优解决方案，等待用户确认才能修改/新增文本。
- 事件使用 `CY.Event`，事件必须是 `struct`，发布用 `CY.Event.Post(ref evt)`。
- 日志使用 `CY.Log / CY.LogInfo / CY.LogWarning / CY.LogError`。
- 计时使用 `CY.Timer.Delay / Loop / NextFrame`。
- 流程使用 `CY.Procedure.Start/Change/ChangeProcedure<T>()`，新增流程后生成注册表：`CYFramework/Generate Procedure Registry` → `Assets/CYFramework/Resources/CYFramework/ProcedureRegistry.asset`。
- 2D 坐标为 XY 平面；导航混用 `HybridNavigationAgent`（NavMeshPlus + A*）。
- NavMeshPlus 运行时烘焙：`BuildNavMeshAsync` 前必须 `Physics2D.SyncTransforms()`；DDOL 场景用 `RootSources2d` 指定根。
- 数据表 CSV 路径：`Assets/_Game/Resources/DataTable/...`；`LoadProcedure` 负责加载，`UnitManager` 只做缓存查询。
- 单位体系：`UnitEntity` + `UnitManager`；创建敌人用 `UnitManager.TryCreateEnemy(...)`。
- **注释要求**：所有代码必须逐行添加详细中文注释；每个字段、每个方法也必须有详细中文注释。
- **注释风格**：常用关键词（如 if/else/for）和“方法体开始/结束”这类基础结构不用注释。