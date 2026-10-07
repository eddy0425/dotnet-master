# src/Using 架构核实与下一轮重构方案

> 范围：`DotNet.Drawing`、`DotNet.HalconCore`、`DotNet.HalconAlgo`、`DotNet.HalconUI`、`DotNet.VisionMaster`（外加 `UnitTest/DotNet.SamplePlugin`）
> 现状：上一轮计划的阶段 0～5 已完成（见 `d1e3bfc`～`40dadb2`），测试方法 780 个（`[TestMethod]` 749 + `[DataTestMethod]` 31；Drawing 127、HalconAlgo 296、HalconUI 253、VisionMaster 104）
> 前提（不变）：**一个算法 = 一个类**，只依赖 Core + Drawing，零登记，可外置。见 `AGENTS.md`。

---

## 0. 结论

**当前架构的方向是对的，不需要推倒重来。** 它已经是一个标准的「微内核（Microkernel / Plug-in）架构」：

- 内核契约：`ParaStrategyBase<TPara>` + 能力接口 + `[Algo]`
- 插件发现：`AlgoCatalog`
- 宿主：`VisionMaster`，只认契约、按能力接口判断

对一个「单机、WinForms、Halcon、算法会不断增加、需要第三方 dll 扩展」的机器视觉工具来说，这就是最合适的架构风格。分层架构、DDD、Clean Architecture、数据流节点图这些都不比它更合适（理由见 §4）。

但「插件契约」现在只做到了**算法能接进来**，还没做到**插件能自由扩展、能在机台上可靠运行**。主要还差五件事：

| # | 缺口 | 一句话 |
|---|---|---|
| G1 | **契约（SDK）和运行时混在一个程序集里** | 插件引用的 `DotNet.HalconCore` 里还有方案读写、流程引擎、插件加载器，公开面太宽，契约版本没法稳定 |
| G2 | **执行和显示没有真正分开** | `Run(context, display)` 在执行完后立即用即时模式绘制；画面一缩放 / 重绘，叠加图形就丢了；没法放到工作线程跑 |
| G3 | **流程跑在 UI 线程** | 连续运行靠 WinForms `Timer` + 同步 `Run`，Halcon 算子执行期间界面卡住，相机触发也接不进来 |
| G4 | **扩展点是封闭的** | 页签（`TabPageEnum`）、参数种类（`ParamKind`）、输出类型（`OutEnum` + `OutputBuilder`）、交互（`IRoiHost`）都是固定集合；插件需要的东西不在里面，就只能改宿主 |
| G5 | **插件加载不够稳健** | 任何一个插件有问题，整个 `plugins\` 目录都被丢弃；不支持子目录 / 私有依赖；参数类没有版本与迁移 |

推荐的目标是：**「微内核 + 三层契约」**——SDK（插件只看到它）/ Runtime（无界面的宿主内核）/ Shell（WinForms 外壳），并把执行结果改成「数据 + 保留模式叠加层」。下面分别说明。

---

## 1. 现状核实

### 1.1 依赖图（实测 csproj）

```
DotNet.Drawing            ← halcondotnet, Newtonsoft
  ↑
DotNet.HalconCore         ← Drawing, halcondotnet, Newtonsoft
  ↑            ↑
HalconAlgo   HalconUI(WinForms)
  ↑            ↑
  └── VisionMaster ──┘     ← 还引用 DotNet.Logging、SunnyUI
plugins\*.dll             ← 只引用 Drawing + HalconCore（SamplePlugin 已验证）
```

依赖方向正确：Algo 和 UI 互不引用。但架构测试只守住了一半：`CoreContractTests.HalconAlgo_ReferencesOnlyContractAndHalcon` 只检查 HalconAlgo 的引用白名单；Core 不引用 WinForms、HalconUI 不引用 HalconAlgo 目前都没有测试。

### 1.2 已经做对、应当保留的部分

- **单类插件模型**：`ParaStrategyBase<TPara>` 固定执行顺序 `ResetOutputs → Execute → Render → 状态文本`，子类只填算法本身；能力接口（`IRoiEditable`、`ITemplateEditable`、`IImageProducer`）按需实现。
- **声明式参数**：`ParamBuilder` 取代了控件槽位，`TrySetValue` 自带「是否真的变了」的判断，`When` 支持条件显示。
- **声明式输出**：`OutputBuilder` 一次声明同时生成变量树和解析器。
- **稳定引用**：`SourceRef` 按 `ToolId + 路径` 定位，改名不断链；`FlowRunner.Validate` 在运行前校验引用顺序与类型。
- **稳定身份**：`[Algo("fit.arc-midpoint", ...)]` 的键写进方案文件，与类名无关；`MissingTool` 在插件缺失时保留原始 JSON 并原样写回。
- **失败语义统一**：`RunResult` 取代 bool + 红字；失败后基类保证输出全部复位。
- **插件校验**：`AlgoCatalog` 一次列出全部问题，拒绝自带共享程序集副本，检查契约主版本号。

### 1.3 问题清单（带证据）

| # | 问题 | 证据 | 影响 |
|---|---|---|---|
| A1 | SDK 与运行时同一程序集 | `HalconCore` 里同时有插件需要的 `ParaStrategyBase`/`ParamBuilder`/`OutputBuilder`，和只有宿主需要的 `AlgoCatalog`（含 `Assembly.LoadFrom`）、`FlowRunner`、`FlowScheme`、`MissingTool` | 插件能看到、也能调用宿主内部 API；运行时一改就要动契约版本号（`AssemblyVersion 1.0.0.0`），或者干脆不敢改 |
| A2 | 即时模式绘制，重绘即丢失 | 滚轮缩放、中键平移、双击复位在 `HWindowMouse` 里都是 `ClearWindow` + `DispObj(HoImage)`；尺寸变化走 `HWindowImage.Fun_ReDisplay`，同样只画图。`Render(IHDisplay, ...)` 画完就没有记录。只有 ROI / 模板两种编辑模式靠 `DispRectMouse` / `DispModelMouse` 在鼠标事件里自己补画 | 缩放、拖动、窗口尺寸变化后运行结果的叠加图形消失，要重新运行才能看到；「补画」逻辑分散在各鼠标处理器里 |
| A3 | `Render` 拿到的是整个显示窗口 | `IHDisplay` 包含 `SetImage`、`DispImage`、`ClearWinDisp`、`DrawRegionAsync` 等 | 算法在绘制阶段可以换图、清屏、甚至发起交互，契约没有约束住 |
| A4 | 执行不能离开 UI 线程 | `MainForm._loopTimer`（WinForms Timer，100 ms）→ `RunFlow()` 同步执行；`Run` 内部直接调 `Render`。后台线程绘制目前靠 `UiThreadDisplay`：**每一次** `Disp*` 调用都同步 `Control.Invoke` 回 UI 线程。流程的初始图像取的是 `_display.Display.HoImage`（显示窗口拥有的句柄） | 算子耗时期间界面无响应；放到后台线程后，一帧要同步 Invoke 几十次，UI 线程只要同步等待后台线程就会死锁；窗口换图时会释放旧图，后台线程手里的初始图像随之悬空 |
| A5 | 显示数据挂在策略对象上，个别算法为跨线程打了补丁 | `FitArcMidpointStrategy` 的 `_pendingRenderData` + `Interlocked.Exchange` + 公开的 `TakeRenderData()`（给「无界面运行、机台在任意线程取走绘制」用）；`FitLine`、`MatchStrategyBase` 没有线程原语，但把只供绘制的数据（`_searchRegion`、`_used`/`_removed`、`_hits`）存成字段，要到下一轮 / `Close` / `Dispose` 时才由 `ClearRenderData` 释放 | 每个算法自己管「显示数据的所有权、生命周期与线程」，这本应由框架负责 |
| A6 | 页签是封闭枚举，且含算法专用页 | `TabPageEnum { FileImage, Parameter, Region, Matching, Display }`；`ParaForm` 的 Designer 里有 5 个固定 `TabPage` 和 5 个 `ParamPanel` | 插件不能新增页签；`FileImage`、`Matching` 是具体算法族的概念，却写在内核里 |
| A7 | ROI / 模板页的交互由宿主写死 | `ParaForm.Designer` 里写死了两组 5 种形状单选（ROI、模板各一组）、5 个绘制按钮（新建 / 修改区域，新建 / 修改 / 编辑模板）、ROI 信息文本框；`HModelUI` 缩略图在 `ParaForm` 构造时 `new` 出来；页签显示与否按 `tool is IRoiEditable` / `ITemplateEditable` 判断 | 插件需要「多个 ROI」「点选」「标定板」之类的交互时，只能改宿主 |
| A8 | `IRoiHost` 带有匹配算法族的回调 | `SetModelPara(...)`、`DrawDone(modelPath, ...)` 只有 `MatchStrategyBase` 在用；`HDisplayUI`（一个 UserControl）直接实现了 `IRoiHost`。`SetRectPara` / `SetModelPara` 不只是「回填面板」，还会切换 `HDisplayUI.DrawType`，让 `DispRectMouse` / `DispModelMouse` 在鼠标事件里持续显示 ROI / 模板轮廓 | 通用交互接口被一个算法族绑定；新的算法族要么借用这两个方法，要么改接口 |
| A9 | 参数种类不够 | `ParamKind` 只有 Source/Choice/Int/Double/Flag/Folder；`MergeRegionStrategy` 因为没有列表参数只能固定 `SourceCount = 6` 个槽位 | 缺文本、文件、按钮（动作）、可变长列表；这仍然是「宿主决定插件能声明什么」 |
| A10 | 输出类型不够且不可扩展 | `OutEnum` 有 17 个值，但 `OutputBuilder` 公开的只有 Image/Region/Number/Point/Line/Coord；`AddCommon` 是 internal | 插件输出不了圆、文本、布尔、数组；`OutEnum` 是封闭枚举，新类型只能改 Core |
| A11 | 变量树多一层多余的契约 | 数据源已经只有 `Outputs`（基类的 `GenTreeNode` 就是遍历 `Outputs`），但 `ValueForm` 仍通过 `ITreeNodeProvider` + `ITreeVisualizer` 构建；`ITreeBranch.ReusePointStructure`、`CommonNodes` 没有任何调用方，只剩实现（`TreeVisualizer`、测试里的 `FakeTree`） | 多一套要维护的公开契约，插件作者会误以为要自己实现 |
| A12 | 插件加载「一损俱损」 | `MainForm.LoadCatalog` 捕获 `AlgoCatalogException` 后 `AlgoCatalog.Load(builtIn)`，整个 `plugins\` 被丢弃；`LoadPlugins` 只扫 `plugins\*.dll` 一层，目录里只要有一个共享程序集副本，就一个插件都不加载 | 一个坏插件让所有好插件也不可用；插件的私有依赖和插件本身混在同一层，都会被当作候选插件扫描（私有依赖里如果有原生 dll，会被报成「不是有效的 .NET 程序集」，连带所有插件失效） |
| A13 | 参数类没有版本 | `FlowScheme` 只存 `{AlgoKey, Id, Name, Para}`；格式版本 `FormatVersion` 是整份方案的，不是每个算法的 | 插件改了参数类（改名、改单位、拆字段），旧方案会被静默读成默认值 |
| A14 | 同族基类与通用积木只在 HalconAlgo 内 | `EdgeFitStrategyBase`、`MatchStrategyBase`、`RotateStrategyBase`、`RoiEditing`（`internal`，依赖 `IRoiHost`）、`EdgeMeasurePipeline`、`RobustFitPipeline`（`public`，纯 Halcon 计算）都在 HalconAlgo | 外置插件要写一个新的拟合 / 匹配变体，要么引用 HalconAlgo（违反「只依赖 Core」），要么复制代码 |
| A15 | 工具实例同时是「配置 + 运行结果 + 执行器」 | 输出是策略对象上的属性（`ArcMidpoint`、`Region`…），`RunContext.Upstream` 直接给出上游策略对象 | 同一个流程不能并发跑两帧；这对单相机顺序检测是可以接受的，但必须**明确约束为「一个流程同时只有一个执行者」** |

> A15 不打算改：拆成「定义 / 实例 / 结果」三个类会违背「一个算法 = 一个类」，收益（同流程多帧并发）在当前场景里用不上。方案是用 Runtime 的执行会话保证单线程访问（见 §2.4）。

---

## 2. 目标架构

### 2.1 总体：微内核 + 三层契约

```
┌──────────────────────────── Shell（WinForms 外壳）────────────────────────────┐
│ DotNet.VisionMaster   组合根：主窗、流程窗、工具箱、信息窗；只组装，不含业务     │
│ DotNet.HalconUI       显示控件、叠加层渲染、参数面板、交互编辑器（按能力注册）     │
└───────────────────────────────────────────────────────────────────────────────┘
                 ↓ 只依赖 Runtime + SDK
┌──────────────────────────── Runtime（无界面内核）─────────────────────────────┐
│ DotNet.VisionRuntime（新）  插件加载、算法目录、流程引擎、执行会话（工作线程）、   │
│                            方案读写、参数迁移；可以被控制台 / 服务 / 测试直接使用  │
└───────────────────────────────────────────────────────────────────────────────┘
                 ↓ 只依赖 SDK
┌──────────────────────────── SDK（插件只看到这一层）───────────────────────────┐
│ DotNet.HalconCore    [Algo]、ParaStrategyBase、能力接口、ParamBuilder、           │
│                      OutputBuilder、RunContext、RunResult、SourceRef、IOverlay、  │
│                      交互请求（IInteraction*）                                   │
│ DotNet.Drawing       几何、颜色、Halcon 辅助                                     │
└───────────────────────────────────────────────────────────────────────────────┘
                 ↑ 实现 SDK
   DotNet.HalconKit（新，可选）  通用积木：RoiEditing、EdgeMeasurePipeline、RobustFitPipeline
                 ↑ 按需引用
   DotNet.HalconAlgo（内置插件）      plugins\<名称>\*.dll（外置插件）
```

依赖规则（用 `CoreContractTests` 的方式写成架构测试）：

| 程序集 | 允许引用 |
|---|---|
| Drawing | BCL、halcondotnet、Newtonsoft |
| HalconCore（SDK） | 上面 + Drawing |
| HalconKit | 上面 + HalconCore，**不得**引用 WinForms |
| HalconAlgo / 外置插件 | 上面 + HalconCore，可选 HalconKit |
| VisionRuntime | 上面 + HalconCore，**不得**引用 System.Windows.Forms，**不得**引用 HalconAlgo |
| HalconUI | 上面 + HalconCore + VisionRuntime + WinForms，**不得**引用 HalconAlgo |
| VisionMaster | 全部（它是组合根），内置算法程序集只用来传给 `AlgoCatalog.Load` |

### 2.2 SDK 收窄（解决 A1、A11、A14）

**移出 Core → VisionRuntime**：`AlgoCatalog`、`AlgoCatalogException`、`FlowRunner` 及其结果类型、`FlowScheme`、`MissingTool`。

**留在 Core**：插件编写时需要的一切——`AlgoAttribute`、`AlgoInfo`（只读元数据）、`ParaStrategyBase`、能力接口、`ParamBuilder`/`ParamItem`、`OutputBuilder`/`OutputItem`、`RunContext`、`CoordFollow`、`RunResult`、`SourceRef`、`DisplayOptions`、`IOverlay`（新）。`StrategyExtensions`（`FindTool`、`Describe`）**也留在 Core**：`RunContext.Describe` / `Find` 依赖它，算法的报错文字（如 `LineRotImageStrategy` 的 `context.Describe(...)`）也经由它生成。

**删除**：`ITreeNodeProvider`、`ITreeVisualizer`、`ITreeBranch`。变量树由 Shell 直接根据 `IOutputProvider.Outputs` 生成。

**通用积木单独成库，不进 Core**：`EdgeMeasurePipeline`、`RobustFitPipeline` 是纯 Halcon 计算，`RoiEditing` 只依赖交互接口。它们变动频繁，放进 Core 会变成受快照测试和契约版本约束的公开 API，与「收窄 SDK」相悖。所以新建 `DotNet.HalconKit`（只引用 Core + Drawing），插件按需引用，独立编版本，并加入 `AlgoCatalog.SharedAssemblies`（插件不得自带副本）。`RoiEditing` 依赖 `IRoiHost`，要等阶段 10 的交互接口定型后再迁。同族基类（`EdgeFitStrategyBase`、`MatchStrategyBase`、`RotateStrategyBase`）**仍留在 HalconAlgo**：它们和内置算法一起演进，还没稳定到可以作为对外 API。外置插件需要时，先用 Kit 组合，等基类稳定后再考虑提升到 Kit。

**契约版本**：Core 的 `AssemblyVersion` 只在 SDK 有破坏性变更时升主版本；Runtime 独立编版本。`AlgoCatalog.ContractMajorVersion` 现在读的是 `typeof(AlgoCatalog).Assembly`，搬到 Runtime 之后必须改成读 Core 程序集（例如 `typeof(IParaStrategy).Assembly`），否则校验的是 Runtime 的版本号。在架构测试里加一份公开 API 快照（反射列出公开类型与成员，和仓库里的基线文件比对），任何公开面变化都要显式更新基线。

### 2.3 执行与显示分离：保留模式叠加层（解决 A2、A3、A5）

把「绘制」从「对窗口下命令」改成「产出一份显示数据」：

```csharp
// SDK：只能画，不能换图、清屏、交互。图元覆盖内置算法 Render 里实际用到的全部 Disp* 
public interface IOverlay
{
    void Add(HObject obj, DrawStyle style = null);     // 复制句柄，叠加层拥有副本
    void Add(Point2d point, DrawStyle style = null);
    void Add(IReadOnlyList<Point2d> points, DrawStyle style = null);
    void Add(CvLine line, DrawStyle style = null);
    void Add(CvArrow arrow, DrawStyle style = null);
    void Add(CvCircle circle, DrawStyle style = null);
    void Add(CvCoord coord, DrawStyle style = null);
    void Add(CvRegion region, DrawStyle style = null);
    void AddRect2(Point2d center, double phi, double length1, double length2, DrawStyle style = null);
    void Text(string message, Point2d position, DrawStyle style = null);
}

// 基类
protected virtual void Render(IOverlay overlay, RunResult result) { }

// 执行入口：叠加层由调用方创建、拥有、释放；传 null 表示只计算不绘制
RunResult Run(RunContext context, IOverlay overlay);
```

- **`RunResult` 保持不可变、不持有叠加层**。`RunResult` 会被策略存成 `LastResult`，还经由「结果 / 文本显示」两个输出暴露给下游，生命周期跟着策略走；如果让它拥有可释放的 `OverlayList`，就分不清该由谁释放、什么时候释放。所以改成谁调用 `Run` 谁创建 `OverlayList`、谁负责释放：`FlowRunner` / `FlowSession` 每步创建一份，放进 `FlowStepResult.Overlay`，跟着整帧结果交给 Shell。
- 基类在 `Execute` 之后、**仍在执行线程上**调用 `Render(overlay, result)`，把状态文本也写进同一个 `OverlayList`。
- **底图**：图像类工具（`FileImageStrategy`、`RotateStrategyBase`）现在在 `Render` 里调 `display.DispImage(Image)` 换底图，而 `IOverlay` 不能换图。改为由宿主决定底图：整帧显示时取流程里最后一个成功的 `IImageProducer.Image`（和 `FlowRunner` 推导「当前图像」的规则一致），单步显示时取该步的 `RunContext.CurrentImage` 或其自身输出。所以这两个算法的 `Render` 只需删掉 `DispImage` 这一行。
- Shell 拿到结果后在 UI 线程把底图 + `OverlayList` 交给显示控件；显示控件**保存**当前叠加层，所有重画路径（`HWindowMouse` 的缩放 / 平移 / 双击复位、`Fun_ReDisplay` 的尺寸变化）都改成先画图、再重放叠加层。`DispRectMouse` / `DispModelMouse` 里补画 ROI / 模板轮廓的逻辑可以改成往同一个保留层里放「编辑层」，不再各自补画。
- `FitArcMidpointRenderData` 的取走 / 发布机制（`Interlocked`、公开的 `TakeRenderData`）删除：无界面运行时也能拿到 `OverlayList`，不再需要「留在槽里等机台取走」。`FitLine`、`MatchStrategyBase` 的绘制专用字段仍然要从 `Execute` 传到 `Render`，但可以在 `Render` 写完叠加层后立即释放，不必等到下一轮 / `Close`。
- 迁移期保留旧签名 `Render(IHDisplay, RunResult)`，用一个适配器把 `IHDisplay` 的 `Disp*` 调用录制进 `OverlayList`（`DispImage` 忽略，由上面的底图规则代替）；全部算法迁完后删掉旧签名。

### 2.4 执行会话：流程在工作线程运行（解决 A4、A15）

```csharp
// Runtime
public sealed class FlowSession : IDisposable
{
    // 专用工作线程（不是线程池）：Halcon 句柄与算法实例只在这个线程上被执行访问
    public Task<FlowRunResult> RunAsync(HObject image, CancellationToken ct);
    public Task<FlowStepResult> RunStepAsync(int index, HObject image, CancellationToken ct);
    public bool IsBusy { get; }
    public event EventHandler<FlowRunResult> Completed;   // Shell 订阅后 BeginInvoke 到 UI 线程渲染
}
```

- **一个流程同时只有一个执行者**：同一个 `FlowSession` 的请求串行排队。画 ROI、建模板、改流程结构前，Shell 必须等会话空闲（现有的 `HostBusy` / `EnsureLoopStopped` 规则沿用，换成判断 `IsBusy`）。
- **参数写回不要求停机**：连续运行时边跑边调阈值是现场最常见的用法。参数面板的写回（`ParamItem.TrySetValue` + `ParamsChanged`）作为一个请求投递到会话队列，在两帧之间、在会话线程上执行，这样仍然满足「单执行者」；界面在请求执行完之前把输入框显示为待生效。会改动 HObject 的交互（ROI、模板）不走这条路，仍然要求会话空闲。
- **输入图像由会话拥有**：`RunAsync` 收到的图像先复制一份再排队，不能直接用 `_display.Display.HoImage`，因为显示窗口换图时会释放旧图（`HWindowImage.AdoptImage`）。
- 连续运行不再用 WinForms `Timer`，改为会话内的循环加取消令牌；相机触发直接调 `RunAsync`。取消只在两个工具之间生效，正在执行的 Halcon 算子不能中断；单个工具耗时过长要靠工具自己的参数（如匹配超时）来限制。
- 交互式操作（`DrawROIAsync`、`SetTemplateAsync`）仍在 UI 线程，因为它们本来就要等用户操作。会话忙时禁止发起交互。
- `RunContext` 的「借用句柄」约定不变：句柄在会话线程上产生和读取，UI 线程只读 `OverlayList` 里的副本。
- 叠加层取代显示接口之后，`UiThreadDisplay`（逐次 `Invoke` 的包装）不再需要，在本阶段删除。

### 2.5 开放扩展点（解决 A6、A7、A8、A9、A10）

**页签：从枚举改成字符串**

```csharp
p.Page("参数")                       // 取代 Tab(TabPageEnum.Parameter)
 .Group("边缘")
 .Int("阈值", ...);
```

- `ParamItem.Page` 是字符串；宿主按声明顺序生成页签，不再有固定 Designer 页。内置常量 `Pages.Parameter`、`Pages.Display` 只是推荐名字。
- `TabPageEnum` 保留一个版本并标 `[Obsolete]`，`Tab(TabPageEnum)` 映射到对应字符串。

**参数种类：补齐常用项**

| 新增 | 用途 |
|---|---|
| `Text` | 字符串（条码期望值、文件名前缀…） |
| `File` | 文件路径，带过滤器 |
| `Action` | 按钮，执行一个策略方法（「重新示教」「清除模板」） |
| `SourceList` | 可变长来源列表，取代 `MergeRegion` 的 6 个固定槽位 |

**输出类型：放开 `OutputBuilder`**

- 公开 `Circle`、`Text`、`Flag`、`Numbers`（数组）。
- `OutEnum` 不再作为判断类型的唯一依据：`OutputItem` 增加 `ValueType`（CLR 类型），`FlowRunner.Validate` 和来源选择窗口用 `ValueType` 的可赋值关系判断兼容；`OutEnum` 只决定图标和显示方式。插件因此可以输出自定义类型（只能被认识它的下游插件引用）。

**交互：从固定的 ROI / 模板页改为「编辑器按能力注册」**

- `IRoiHost` 拆分：
  - `IInteractionHost`（SDK）：`CurrentImage`（建模板要用，现在是 `host.Display.HoImage`）、`Feedback`（一个 `IOverlay`，交互过程中的提示图形与文字，现在是 `host.Display.Disp*` / `DispText`）、`DrawRegionAsync`、`DrawRegionModAsync`、`ShowRoi(CvRegion)`（取代 `SetRectPara`：既回填面板，也让显示控件持续显示该 ROI）。不再暴露整个 `IHDisplay`。
  - 匹配专用的 `SetModelPara`、`DrawDone` 从通用接口删除，改为 `ITemplateEditable` 自己在 `TemplateView` 里暴露、并发出 `TemplateChanged` 事件，由 Shell 的模板编辑器订阅。
- `IRoiHost` 由一个独立的适配器类实现，不再让 `HDisplayUI`（UserControl）直接实现。
- Shell 内部：`ParaForm` 不再在 Designer 里写死 ROI 页 / 模板页，改为一组「能力编辑器」：

```csharp
// HalconUI（Shell 内部扩展点，不对插件开放）
interface ICapabilityEditor
{
    bool Supports(IParaStrategy tool);   // 例如 tool is IRoiEditable
    string Page { get; }                 // 显示在哪一页
    Control Create(EditorContext ctx);
}
```

  内置 `RoiEditor`（`IRoiEditable`）和 `TemplateEditor`（`ITemplateEditable`）。以后新增一种交互能力，是在 SDK 加一个能力接口、在 Shell 加一个编辑器，**与具体算法无关**；插件只实现能力接口。

> 不让插件自带 WinForms 控件：那样插件就依赖了 WinForms 和宿主控件库，违反「只依赖 Core」，宿主换 UI 技术时插件也要跟着重写。插件能做的交互，一律通过能力接口 + 宿主提供的编辑器实现。

### 2.6 插件加载加固（解决 A12、A13）

**目录结构**

```
plugins\
  SamplePlugin\
    DotNet.SamplePlugin.dll        ← 入口程序集（与目录同名，或由 plugin.json 指定）
    SomePrivateDep.dll             ← 私有依赖，不扫描
    plugin.json（可选）             ← { "entry": "...", "id": "...", "version": "..." }
  Legacy.dll                       ← 兼容：根目录下的 dll 仍按旧规则当作入口
```

- 私有依赖：入口程序集用 `Assembly.LoadFrom` 加载时，CLR 本来就会到它所在的目录里找依赖，所以插件放进各自的子目录后，私有依赖**不需要**额外处理；要改的只是扫描规则——每个子目录只加载入口程序集，不再把目录里的每个 dll 都当成候选插件。已知限制：两个插件带了同名、但版本不同的未强签名依赖时，先加载的那个生效。确实遇到时再加 `AppDomain.AssemblyResolve`，现在不做。共享程序集（halcondotnet、Core、Drawing、Kit）始终用宿主的那一份。
- **逐个插件隔离失败**：`AlgoCatalog.Load` 返回 `CatalogLoadReport`（每个插件的状态：已加载 / 被拒绝 + 原因），而不是全部失败就抛异常。坏插件只让它自己不可用；方案里引用它的工具照常变成 `MissingTool`。内置程序集出问题仍然直接抛出（属于程序错误）。
- 信息窗口显示加载报告，取代现在弹出一个大对话框后退回只用内置算法的做法。
- 仍然**不做** AppDomain 隔离和热卸载（理由见 §4）。

**参数版本与迁移**

```csharp
[Algo("fit.arc-midpoint", "圆弧中点", Group = "测量", ParaVersion = 2)]
public class FitArcMidpointStrategy : EdgeFitStrategyBase<FitArcMidpoint>
{
    // 基类虚方法，读到的版本低于 ParaVersion 时由 Runtime 调用；在 JSON 层面改，改完再反序列化
    protected override void MigratePara(JObject para, int fromVersion) { ... }
}
```

- 用基类的虚方法而不是新增能力接口：迁移不是一种「能力」，而是每个参数类都可能需要的东西，放在基类里更符合「一个类」的写法，宿主也不必多做一次类型判断。
- 这里用到了 Newtonsoft 的 `JObject`，等于把 Newtonsoft 写进了 SDK 契约（Core 已经因为 `SourceRef` 上的 `[JsonConstructor]` 依赖它）。这是**有意的决定**：要迁移参数的插件需要引用 Newtonsoft.Json，并且要用宿主的同一份（加入 `SharedAssemblies`）。
- 方案里每个工具多存一个 `ParaVersion`。没有标版本的视为 1。
- 只是改容器类型（数组 ↔ `List<T>`）、新增有默认值的字段，JSON 形状不变，**不需要**迁移；只有改名、改单位、拆字段、改语义时才需要。
- 读到的版本高于当前插件时，不尝试读取，直接作为 `MissingTool` 保留原文（防止旧程序把新方案读坏后又写回）。

### 2.7 重构后插件作者看到的样子

```csharp
[Algo("sample.region-area", "区域面积", Group = "示例", Order = 10)]
public class RegionAreaStrategy : ParaStrategyBase<RegionAreaPara>
{
    public double Area { get; private set; }

    protected override void DeclareParams(ParamBuilder p)
    {
        p.Page(Pages.Parameter)
         .Source("区域来源", () => inPara.RegionIn, v => inPara.RegionIn = v, OutEnum.Region)
         .Int("最小面积", () => inPara.MinArea, v => inPara.MinArea = v, min: 0);
    }

    protected override void DeclareOutputs(OutputBuilder o) => o.Number("面积", () => Area);

    protected override void ResetOutputs() => Area = 0;

    protected override RunResult Execute(RunContext context)
    {
        HObject region = context.ResolveRegion(inPara.RegionIn, null);
        HOperatorSet.AreaCenter(region, out HTuple area, out _, out _);
        Area = area.TupleSum().D;
        return Area < inPara.MinArea ? RunResult.Warn($"面积:{Area:F0} 偏小") : RunResult.Ok($"面积:{Area:F0}");
    }

    protected override void Render(IOverlay overlay, RunResult result)
    {
        // 只描述要画什么；在哪个线程画、缩放后怎么重画，由宿主负责
    }
}
```

和现在相比，插件作者的写法几乎不变（`Tab` → `Page`、`Render` 的参数换成 `IOverlay`），变化都在框架和宿主内部。

---

## 3. 分阶段实施计划

原则同上一轮：每个阶段都能独立编译、测试并合入；新旧写法在迁移期并存；改动后跑受影响项目的测试，全部通过再提交。

### 阶段 7：拆出 Runtime（3～4 天，低风险，纯搬家）

- [x] 新建 `src/Using/DotNet.VisionRuntime`（.NET Framework 4.5.2，x64，与其它项目一致），加入解决方案。
- [x] 把 `AlgoCatalog`、`AlgoCatalogException`、`FlowRunner`（含 `FlowFailurePolicy`、`FlowStepResult`、`FlowRunResult`、`FlowIssue`）、`FlowScheme`、`MissingTool` 移过去，命名空间改为 `DotNet.VisionRuntime`。`StrategyExtensions` 留在 Core（`RunContext` 依赖它）。
- [x] 处理跨程序集访问点（实测只有这些）：`AlgoInfo` 的 `internal` 构造函数（`AlgoCatalog` 在用）改为公开的 `AlgoInfo.From(AlgoAttribute, Type)`；`ContractMajorVersion` 改为读 Core 程序集的版本号；`SharedAssemblies` 加上 `DotNet.VisionRuntime`。`RunContext.CurrentImage` 的 `internal set` 没有任何调用方，直接改成只读即可。不加 `InternalsVisibleTo`。
- [x] HalconUI、VisionMaster、测试项目改引用；新建 `DotNet.VisionRuntime.Tests`，迁入 `FlowTests`、`SoakTests`，以及 `CoreContractTests` 里关于目录 / 插件加载的部分（`BuiltIn_*`、`Create_*`、`Load_*`、`Plugin_*`）；契约部分（`ParamBuilder`、`OutputBuilder`、`SourceRef`、`RunContext`、基类执行顺序）留在原处。`Plugin_DirWithSharedAssemblyCopy_Rejected` 现在复制的是 `typeof(AlgoCatalog).Assembly`，要改成复制 Core 程序集。
- [x] 架构测试补齐：Core 不引用 WinForms；Runtime 不引用 WinForms、不引用 HalconAlgo；HalconUI 不引用 HalconAlgo（现在只有 HalconAlgo 的白名单测试）。
- [x] 删除 `ITreeNodeProvider` / `ITreeVisualizer` / `ITreeBranch`，`ValueForm` 改为直接遍历 `Outputs`；`HalconUI.TreeVisualizer` 一并删除。HalconAlgo.Tests 里 6 处 `GenTreeNode` 测试与 `FakeTree` 改为断言 `Outputs`。
- [x] 公开 API 快照测试（Core 一份基线文件）。

### 阶段 8：保留模式叠加层（1～1.5 周）★

- [x] SDK 增加 `IOverlay`，Runtime 增加 `OverlayList`（拥有 HObject 副本，`IDisposable`）；`FlowStepResult` 持有本步的 `OverlayList`，`RunResult` 不变。
- [x] 基类新增 `Render(IOverlay, RunResult)`；旧 `Render(IHDisplay, RunResult)` 标 `[Obsolete]`，由录制适配器转到新管线（`DispImage` 忽略）。（实施：11 个内置算法在本阶段一并迁完，旧签名已无调用方，按"迁完后删掉旧签名"直接删除，未保留过渡适配器）
- [x] 底图规则：宿主按 `IImageProducer` 决定底图，`FileImageStrategy`、`RotateStrategyBase` 的 `Render` 删掉 `DispImage`。
- [x] `HDisplay` 保存当前叠加层；`HWindowMouse` 的缩放 / 平移 / 双击复位和 `Fun_ReDisplay` 都改成画图后重放；新结果到来时替换并释放旧叠加层。
- [x] 逐个迁移内置算法（11 个）；删除 `FitArcMidpointRenderData` 的取走 / 发布机制；`FitLine`、`MatchStrategyBase` 的绘制字段改为 `Render` 之后立即释放。
- [x] `MissingTool.Run` 改为写入叠加层，不再直接调 `display.DispText`。
- [x] 测试：缩放 / 平移后叠加层仍在；同一份 `OverlayList` 释放后句柄计数归零；长跑内存验收沿用阶段 5 的方法。

### 阶段 9：执行会话与工作线程（1 周）

- [x] Runtime 增加 `FlowSession`（专用线程 + 请求队列 + 取消）。
- [x] `IParaStrategy.Run` 的第二个参数从 `IHDisplay` 换成 `IOverlay`（为 null 时只计算不绘制），签名变为 `Run(RunContext, IOverlay)`；`FlowRunner` 不再接收 `IHDisplay`。（提前到阶段 8 完成：两个重载并存会让 `Run(ctx, null)` 产生二义性）
- [x] `MainForm` 去掉 `_loopTimer`，连续运行 / 单步 / 测试运行都走 `FlowSession`；输入图像先复制再交给会话；结果通过 `BeginInvoke` 回到 UI 线程渲染与写信息窗口。删除 `UiThreadDisplay`。
- [x] 会话忙时：禁用 ROI / 模板绘制、流程增删排序（复用 `HostBusy`）；参数写回改为投递到会话队列，在两帧之间执行。
- [x] 测试：运行期间 UI 线程可响应（用假算法 `Thread.Sleep` 验证）；取消能在两个工具之间生效；同一会话的请求严格串行；连续运行中改参数，下一帧生效且不会和执行交错。

### 阶段 10：开放扩展点（1.5～2 周）

- [x] `ParamItem.Page` 字符串化，`ParamBuilder.Page(string)`；`Tab(TabPageEnum)` 标 `[Obsolete]` 并映射。
- [x] `ParaForm` 去掉 Designer 里的固定页签与 5 个 `ParamPanel`，按声明动态生成。
- [x] 新增参数种类 `Text`、`File`、`Action`、`SourceList`；`MergeRegion` 改用 `SourceList`，`RegionSources` 从 `SourceRef[]` 改为 `List<SourceRef>`。JSON 都是数组，旧方案直接读得进来，不需要参数迁移；只需在读入后去掉补齐用的空槽（`Local`），并用旧方案样本做回归测试。
- [x] `OutputBuilder` 公开 `Circle`、`Text`、`Flag`、`Numbers`；`OutputItem.ValueType`；`Validate` 与来源选择窗口改按 `ValueType` 判断兼容。（实施：声明了种类的来源仍要求种类相同 —— 图像与区域同为 HObject；按 CLR 类型声明的来源 `Source<T>` 只看可赋值关系）
- [x] `IRoiHost` 拆为 `IInteractionHost` + 模板事件；适配器类实现，`HDisplayUI` 不再实现 `IRoiHost`。HalconAlgo.Tests 的 `FakeRoiHost` 同步改写。
- [x] Shell 内部引入 `ICapabilityEditor`，ROI 页、模板页改成 `RoiEditor`、`TemplateEditor`。（实施：放在 VisionMaster —— 两个编辑器依赖参数页的绘制闸门与编辑会话，同属 Shell）
- [x] `TabPageEnum.FileImage` / `Matching` 不再出现在 Core。

### 阶段 11：插件加载加固与参数迁移（1 周）

- [x] 支持 `plugins\<名称>\` 子目录 + 可选 `plugin.json`；子目录只加载入口程序集，私有依赖靠 `LoadFrom` 自带的同目录探测解析。
- [x] `AlgoCatalog.Load` 返回逐个插件的加载报告；坏插件不影响其它插件（包括共享程序集副本：只拒绝带了副本的那个插件）；信息窗口显示报告。
- [x] `[Algo(ParaVersion = n)]` + 基类虚方法 `MigratePara`；方案记录每个工具的 `ParaVersion`；版本更高时作为 `MissingTool` 保留。
- [x] 补全端到端测试（已有 `CoreContractTests.Plugin_LoadedFromPluginDir_CreatesAndRuns` 覆盖「加载 → 建工具 → 运行」，`MainFormTests.Plugin_AppearsInToolbox_AndItsParamsAreShown` 覆盖工具箱与参数页）：增加子目录布局，以及「保存 → 重新加载 → 参数一致」「删掉插件后重新加载 → `MissingTool` 原样写回」。
- [x] 新建 `DotNet.HalconKit`，迁入 `EdgeMeasurePipeline`、`RobustFitPipeline`，以及按 `IInteractionHost` 改写后的 `RoiEditing`（改为 `public`）；HalconAlgo 改引用；架构测试加 Kit 的引用白名单。

### 阶段 12（可选）：工程与基础类型

- [ ] 目标框架统一升到 .NET Framework 4.8（4.5.2 已停止支持）；升级后可用 `ValueTuple`，polyfill 保留。
- [ ] 上一轮遗留：`DotNet.Drawing` 的纯几何部分拆成不引用 halcondotnet 的项目；拆 `HalconController`。与插件目标无关，优先级最低。

### 阶段依赖

```
阶段 7 ──→ 阶段 8 ──→ 阶段 9      （8、9 都改 SDK，合并成一次对外发布，Core 主版本只升一次）
   │
   └────→ 阶段 10 ──→ 阶段 11（HalconKit 依赖 10 定型的 IInteractionHost）
阶段 12 任意时间
```

---

## 4. 考虑过但不采用的方案

| 方案 | 不采用的原因 |
|---|---|
| 拆成「定义 / 实例 / 结果 / 编辑器」多个类（类似 VisionPro / VM 的 Tool + Run + Edit 三件套） | 违背「一个算法 = 一个类」；同流程多帧并发在当前单相机场景用不上，单执行者约束（§2.4）已能保证正确性 |
| 让插件自带 WinForms 编辑控件 | 插件会依赖 WinForms 和宿主控件库，违反「只依赖 Core」；宿主换 UI 技术时插件全部重写 |
| MEF / MAF（System.AddIn） | 发现部分自写的 `AlgoCatalog` 已够用且更透明；MAF 的管道模型对 HObject 句柄不友好，成本远大于收益 |
| AppDomain 隔离、热卸载 | 跨域调用要求可序列化或 `MarshalByRefObject`，HObject 无法穿越；Halcon 原生运行时本来就是进程级的，隔离不了 |
| 进程外插件（每个插件一个进程，IPC） | 图像要跨进程拷贝，延迟和复杂度不可接受 |
| 数据流节点图引擎（节点 + 连线、并行分支） | 流程目前是线性的；`SourceRef` 已经能表达依赖关系，将来需要分支时在 `FlowSession` 里按依赖拓扑排序即可，不必现在引入图编辑器 |
| DI 容器、Clean Architecture / DDD 全套 | 组合根只有一个窗体，手写装配不到 50 行；分层概念多于业务，收益不足以覆盖成本 |
| 迁移到 .NET 8 + WPF / Avalonia | Halcon 22.11 支持 .NET Core，但 WinForms 界面与 `HWindowControl` 要整体重写，属于另一个项目；本方案的 SDK / Runtime 不依赖 WinForms，正好为将来迁移铺路 |
| `[Param]` 特性反射生成面板 | 条件显示、选项文字、单位换算的表达力不如 lambda 声明（上一轮已论证） |

---

## 5. 验收标准

- [ ] 新增一个算法：只写一个文件（策略类 + 参数类），宿主、Runtime、Designer 零改动——内置和外置都一样。前提是只用现有的能力接口；需要一种**新的交互方式**（点选、标定板…）时，要在 SDK 加能力接口、在 Shell 加编辑器，这是有意的取舍（见 §2.5）。
- [ ] 外置插件只引用 `DotNet.HalconCore`、`DotNet.Drawing`、halcondotnet（可选 `DotNet.HalconKit`、Newtonsoft.Json）；看不到任何 Runtime / Shell 类型。
- [ ] 插件需要新页签、文本 / 文件 / 按钮 / 列表参数、圆 / 文本 / 数组输出时，不改宿主。
- [ ] 一个坏插件不影响其它插件和内置算法；方案里引用它的工具保留原始配置，再次保存不丢。
- [ ] 插件参数类升级后，旧方案通过迁移正确读入；更新的方案不会被旧程序读坏。
- [ ] 连续运行期间界面可拖动、缩放、切换页面；缩放后叠加图形不丢；改参数不用停机，下一帧生效。
- [ ] 算法代码里不再出现线程同步原语（`Interlocked`、`lock`）和显示数据的所有权转移代码。
- [ ] Runtime 可以在没有 WinForms 的测试里跑完整流程（加载方案 → 运行 → 读结果）。
- [ ] 全部测试通过；架构测试与公开 API 快照测试纳入常规测试。

---

## 6. 风险与注意事项

- **Halcon 句柄与线程**：HObject 本身可以跨线程使用，但 `HWindow` 只能在创建它的线程上操作。叠加层只存 HObject 副本和几何数据，真正的 `Disp*` 只在 UI 线程调用；录制适配器里不得触碰 `HWindow`。
- **叠加层内存**：每帧复制区域 / 轮廓会增加内存分配。替换叠加层时必须释放旧的；连续运行时 UI 跟不上就丢弃中间帧（只保留最新一份），不能排队。
- **阶段 8、9 改了 SDK**：`Render` 换参数、`Run` 的第二个参数从 `IHDisplay` 换成 `IOverlay`，都是破坏性变更，Core 主版本号升到 2，外置插件需要重新编译。阶段 8、9 之间不对外发布 SDK，两个阶段合并成一次发布，避免插件作者跟着改两次。
- **输入图像的所有权**：现在流程的初始图像是显示窗口拥有的 `HoImage`，同步执行时没有问题；改到工作线程后，窗口换图会释放它。会话必须先复制输入图像再执行。
- **`ParaForm` 动态化**：Designer 里的固定页删除后，现有 VisionMaster.Tests 里按控件名定位的测试要改为按 `ParamItem` 定位（`PanelOf`、`ParamItems` 已提供入口）。
- **兼容旧方案**：`TabPageEnum` 不进方案文件，字符串化不影响读写；`MergeRegion` 的 `RegionSources` 从数组改成列表，JSON 形状不变，不需要迁移，但要保留旧方案样本做回归测试。
- **页签名是字符串**：拼错（多空格、大小写不同）会悄悄拆出一个新页签。内置名一律用 `Pages.*` 常量；架构测试检查内置算法里不出现「去掉空格 / 统一大小写后相同」的两个页签名。
- **自定义输出类型**：按 `ValueType` 可赋值判断兼容，要求上下游插件引用**同一个程序集**里的那个类型；各自定义一个同名类会被判为不兼容。这一点要写进插件开发指南。
- **不要过度设计**：`ICapabilityEditor` 只是 Shell 内部的扩展点，不对插件开放；`ValueType` 兼容判断只做「可赋值」，不做隐式转换。
