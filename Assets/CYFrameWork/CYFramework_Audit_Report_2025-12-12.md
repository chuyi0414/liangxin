# CYFramework 审查报告（基于 `CYFramework.md`）

**日期**：2025-12-12  
**目标**：对照 `Assets/CYFramework/CYFramework.md` 的架构/原则，对当前 `Assets/CYFramework/Runtime` 实现做一次“可落地”的审查：

- 性能/GC 风险（尤其是高频路径、WebGL/微信约束）
- 稳定性/正确性风险（潜在崩溃点、线程/平台不兼容）
- 中文注释覆盖与可读性
- `CY` 统一入口（Facade）便捷 API 完整性

---

## 0. 总览结论

### 0.1 优点（与 `CYFramework.md` 一致的部分）

- **分层清晰**：Infrastructure / Platform / Core / Gameplay / Debug 分层符合文档。
- **平台适配思路正确**：WeChat/Unity 适配器分目录，并且大量使用宏隔离 `System.IO` 等不兼容点。
- **大部分核心模块具备中文注释**：总体可读性较好，且关键模块（Save/Network/Bootstrap 等）注释较充足。
- **UI、Timer、Entity、Save 等模块已具备“可用”的工程化框架**：能支撑项目开发。

### 0.2 需要优先处理的问题（建议分级）

#### P0（高概率导致崩溃/严重卡顿）

- **UIManager 在遍历 `_openedPanels.Values` 时可能被面板逻辑修改集合导致异常**
  - 文件：`Runtime/Core/UI/UIManager.cs`
  - 风险：面板在 `InternalUpdate()` / `InternalLateUpdate()` 内若触发 `Open/Close`，会修改 `_openedPanels`，`foreach`/枚举器会抛 `InvalidOperationException`。
  - 结论：这是典型的“更新循环里遍历 Dictionary 的同时修改”的崩溃点，建议优先修。

- **ProcedureManager 的自动注册使用 `AppDomain.CurrentDomain.GetAssemblies()` + LINQ/反射扫描**
  - 文件：`Runtime/Core/Procedure/ProcedureManager.cs`
  - 风险：
    - 与文档 **WebGL/微信不支持 AppDomain** 的约束不一致（见 `CYFramework.md` 6.1 平台限制）。
    - 运行期扫描程序集 + `GetTypes()` + LINQ：初始化时可能产生明显卡顿 & GC。
  - 建议：WebGL/微信默认关闭自动扫描；改为“显式注册/限定程序集/Editor 生成注册表”。

- **ResourceLoader.UnloadUnused() 强制执行 `GC.Collect()`**
  - 文件：`Runtime/Core/Resource/ResourceLoader.cs`
  - 风险：强制 GC 往往带来不可控的帧尖刺，尤其在移动端/微信更明显。
  - 建议：仅在 Dev/Editor 下开放；或改为可配置/异步/延迟到 Loading 场景。

#### P1（性能/架构偏离，长期会拉低体验）

- **EventBus 延迟事件派发使用反射（每次到期都 `GetMethod/MakeGenericMethod/Invoke`）**
  - 文件：`Runtime/Core/Event/EventBus.cs`
  - 风险：虽然注释写“延迟事件较少”，但一旦业务使用多，会产生额外 CPU 开销与潜在 GC。
  - 建议：缓存 `Type -> delegate` 或缓存 `MethodInfo`，至少避免每次 `GetMethod`。

- **EventBus 的 `_pendingRemove` 清理实现是“对所有事件列表做 Remove 扫描”**
  - 当前实现：`ProcessPendingRemove()` 对 `_subscriptions.Values` 全量遍历 `list.Remove(sub)`。
  - 复杂度：事件类型多、订阅多时可能退化。
  - 建议：在 `EventSubscription` 里记录所属 `eventType` 或所属列表引用，做到 O(1) 定位。

- **ServiceLocator 的“拓扑排序”依赖 `ServiceRegistration.Dependencies`，但当前注册流程没有填充**
  - 文件：`Runtime/Infrastructure/ServiceLocator.cs`
  - 现状：排序更多依赖 `InitOrder`，拓扑排序逻辑目前“形同虚设”。
  - 建议：
    - 要么删掉 Dependencies 相关逻辑，避免误导；
    - 要么提供公开 API/Attribute（例如 `[DependsOn(typeof(X))]`）补齐依赖声明。

- **`CY` 统一入口的“懒创建路径”初始化行为不一致**
  - 文件：`Runtime/CY.cs`
  - 现状：
    - `Entity/UI` 懒创建时会主动调用 `Initialize()`；
    - `Timer/Procedure/DataTable` 懒创建时没有调用 `Initialize()`。
  - 风险：如果项目侧绕过 `CYBootstrap` 直接使用 `CY.Timer/CY.Procedure`，会出现“某些系统没初始化”的隐性问题。
  - 建议：统一策略：
    - 要么 `CY` 懒创建全部显式 `Initialize()`；
    - 要么强制要求只能通过 `CYBootstrap` 初始化（并在 `CY` 里做更强的保护/报错）。

---

## 1. 模块级审查细节

### 1.1 Infrastructure

#### 1.1.1 `CYBootstrap`

- **优点**：
  - 注册平台适配器（WeChat/Native）符合文档“平台原生亲和”。
  - 暂停处理符合文档“微信审核红线：切后台静音+暂停逻辑”。
- **建议**：
  - `IVibrationAdapter` 在 Editor 下可能未注册（宏条件排除了 Editor）；如需要在 Editor 调试震动逻辑，可考虑提供 Editor Stub 或统一注册一个“可用但永远返回不支持”的实现。

#### 1.1.2 `ServiceLocator`

- **优点**：基本生命周期管理齐全（InitializeAll/DisposeAll/Scoped 清理）。
- **问题/建议**：
  - “拓扑排序依赖声明”目前不生效（见 0.2 P1）。
  - `BuildInitOrder()` 的依赖解析使用 `Type.GetType(string)`：跨 asmdef/命名空间时容易失败；若要保留依赖排序，建议改为存 `Type` 而不是 string。

### 1.2 Event（EventBus）

- **优点**：结构体事件、优先级插入、自动解绑思路符合“零 GC 事件流”的方向。
- **风险点**：
  - 延迟事件反射派发（P1）。
  - `Unsubscribe<T>` 在 `foreach` 中 `list.Remove(sub)`（虽然通常 break 立即退出不一定抛异常，但可读性/安全性较差，建议改成 for-loop）。
  - `SubscribeAll(object target)` 使用反射扫描方法：建议明确为“开发便利功能”，避免在性能敏感对象上频繁调用。

### 1.3 Resource（ResourceLoader）

- **现状**：当前实现以 `Resources` 为主，Addressables/AB 为预留。
- **风险点**：
  - `UnloadUnused()` 强制 `GC.Collect()`（P0）。
- **一致性建议**：
  - `ResourceLoaderConfig` 中的 `_cacheSizeMB/_enableRefCount` 当前未真正生效：建议要么补齐策略（LRU/RefCount），要么移除字段避免误导。

### 1.4 Procedure（ProcedureManager）

- **问题**：自动注册对平台约束不友好（P0）。
- **建议落地方案（按推荐度）**：
  1) **最推荐**：Editor 阶段扫描并生成“流程注册表”（代码/ScriptableObject），运行期只读表，不扫描程序集。
  2) 限定程序集：由项目显式传入 `Assembly`，禁止默认扫全域。
  3) 平台降级：`#if UNITY_WEBGL || CY_WECHAT` 时默认关闭 AutoRegister。

### 1.5 UI（UIManager + MVVM）

- **P0 风险**：见 0.2（集合遍历中被修改）。
- **建议修复方向**：
  - UI 更新循环不要直接遍历 `_openedPanels.Values`。
  - 可选做法：维护一个“稳定的打开面板列表”（List）用于更新；open/close 时增删；在 update 内对“待关闭/待打开”做延迟队列处理。

- **MVVM 现状**：`ViewModel`/`ObservableList` 能用，但并非严格 0GC。
  - `ViewModel` 使用 `Dictionary<string, object>` 存储属性值：value type 会装箱。
  - 建议：把 MVVM 作为“UI 层便利工具”，在文档/注释中明确“不要用于高频数值刷新（如每帧血条）”。

### 1.6 Platform（WeChat/Unity）

- **总体评价**：方向正确，宏隔离清晰。
- **可改进点**：
  - `WeChatNetworkAdapter` 的 Editor 模拟使用 `UnityWebRequest.Post(url, body)`：该 API 更像表单提交，不一定能模拟 JSON POST；若要更贴近线上，可改成 UploadHandlerRaw（仅建议，不必强制）。
  - `WeChatWebSocket.Send(byte[] data)` 当前仅 warning：如果业务需要二进制，可定义编码（base64）或补 JS 桥。

---

## 2. 中文注释覆盖审查（建议补齐清单）

整体中文注释覆盖度 **较好**。建议重点补齐“会被频繁改动/容易出坑”的模块：

- `Runtime/Core/Procedure/ProcedureManager.cs`
  - 建议补充：为什么 WebGL/微信不建议扫程序集、推荐的注册方式、以及默认配置策略。

- `Runtime/Core/Resource/ResourceLoader.cs`
  - 建议补充：`UnloadUnusedAssets`/`GC.Collect` 的使用场景说明（例如只在 Loading/Dev）。

- `Runtime/Core/Event/EventBus.cs`
  - 建议补充：延迟事件会装箱与反射派发的成本边界（比如“每帧不建议 >N 个延迟事件”）。

- `Runtime/CY.cs`
  - 建议补充：`CY` 的生命周期约束（推荐必须有 `CYBootstrap`），以及懒创建路径是否会 `Initialize()` 的统一规则。

---

## 3. `CY` 统一入口：建议补齐的高价值便捷 API

当前 `CY.cs` 已提供：日志、Timer、Event、Resource.Load/LoadAsync、Audio（BGM/SFX）、UI（Toast/Confirm/Alert）等。

建议新增（按收益排序）：

### 3.1 资源/场景

- `CY.Unload(string path)` / `CY.UnloadUnused(bool forceGC = false)`（forceGC 仅 Dev）
- `CY.Instantiate(string path, Transform parent = null)` / `CY.InstantiateAsync(...)`
- `CY.LoadScene(string name, ...)` / `CY.LoadSceneAsync(...)`

### 3.2 网络

- `CY.HttpGet(string url, Action<string> ok, Action<string> err = null)`（如果当前 NetworkService 支持回调式）
- `CY.HttpPostJson(string url, object body, ...)`（统一 JSON 序列化入口）
- `CY.WebSocketConnect/Send/Close` 的快速包装

### 3.3 存档

- `CY.Save<T>(string key, T data)` / `CY.Load<T>(string key, T defaultValue = default)` 的快捷包装
- `CY.DeleteSave(string key)` / `CY.HasSave(string key)`

### 3.4 实体/UI

- `CY.Open<TPanel>(...)` / `CY.Close<TPanel>()`（对 UIManager 的直接转发，简化项目层调用）
- `CY.ShowEntity<T>(...) / CY.HideEntity(...)`（如项目层经常调用，可统一入口）

### 3.5 平台能力

- `CY.VibrateShort()` / `CY.VibrateLong()`（通过 `IVibrationAdapter` 转发）
- `CY.IsWeChat / CY.IsWebGL / CY.IsNative`（统一平台判断，减少项目散落宏）

---

## 4. 与 `CYFramework.md` 的一致性/偏离点汇总

- **偏离 1**：文档强调 WebGL/微信不依赖 AppDomain；但 `ProcedureManager.AutoRegisterAll` 默认依赖 `AppDomain.GetAssemblies`。
- **偏离 2**：文档强调“零 GC/稳定帧时间”；但 `ResourceLoader.UnloadUnused()` 强制 `GC.Collect()`，可能造成帧尖刺。
- **偏离 3**：EventBus 宣称零 GC 事件流；但延迟事件目前使用装箱 + 反射派发（虽可接受但需边界说明/优化）。

---

## 5. 建议的落地行动清单（建议你按优先级处理）

### P0（建议本周内）

1) 修复 `UIManager` 更新循环的“集合修改导致崩溃”问题。
2) `ProcedureManager`：WebGL/微信默认禁用自动程序集扫描；提供替代注册方案。
3) `ResourceLoader.UnloadUnused()`：移除/开关 `GC.Collect()`（至少 Dev-only）。

### P1（建议随后迭代）

1) EventBus：延迟事件派发缓存反射信息；优化 pendingRemove 的定位方式。
2) ServiceLocator：要么补齐依赖声明能力，要么删掉拓扑排序相关残留。
3) `CY` 懒创建路径初始化行为统一（避免“某些系统没初始化”的隐患）。

---

## 6. 备注

本报告以“对照文档 + 关注真实落地风险”为目标：优先抓 **崩溃/卡顿/平台不兼容**，其余属于“可持续演进”的工程优化项。
