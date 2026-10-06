# src/Using 重构计划

> 范围：`DotNet.Drawing`、`DotNet.HalconCore`、`DotNet.HalconAlgo`、`DotNet.HalconUI`、`DotNet.VisionMaster`
> 现状：.NET Framework 4.5/4.5.2 + WinForms + Halcon 22.11，Core 约 0.85k 行、Algo 约 4.8k 行，测试方法 757 个（Drawing 147、HalconAlgo 249、HalconUI 296、VisionMaster 65；HalconCore 没有测试项目）
>
> **设计前提（必须保留）**：**一个算法 = 一个类**。继承 `ParaStrategyBase<TPara>`，再按需实现能力接口，就能完整地加入一个 Halcon 算法，例如：
>
> ```csharp
> [Algo("fit.arc-midpoint", "圆弧中点", Group = "测量")]
> public class FitArcMidpointStrategy : ParaStrategyBase<FitArcMidpoint>, IRoiEditable
> ```
>
> 本计划的目标是**把这条约定做成真正的插件契约**：
>
> 1. **只依赖 Core**：算法类只引用 `DotNet.HalconCore`（和 `DotNet.Drawing`），不知道宿主窗体里有什么控件。
> 2. **零登记**：除了这个类和它的参数类（放在同一个文件里），宿主端不需要为新算法改任何代码，包括 Designer。
> 3. **可外置**：算法类放在 HalconAlgo 里，或者编译成独立 dll 丢进 `plugins\` 目录，效果一样。

---

## 1. 现状评估

### 1.1 做得对的地方（保留）

- **单类插件模型**：`ParaStrategyBase<TPara>` 加上能力接口（`IRoiEditable`、`ITemplateEditable`、`IParaBinding`、`ITreeNodeProvider`），一个类就能声明算法的执行、参数面板、输出树和 ROI 交互。宿主按能力接口做类型判断（`if (s is IRoiEditable roi)`）。**这是整个架构的核心，保留。**
- 依赖方向正确：`Drawing ← HalconCore ← {HalconAlgo, HalconUI} ← VisionMaster`。Algo 和 UI 互不引用，UI 通过 `IHDisplay`、`IParaUiHost`、`IRoiHost` 等接口反向注入。
- `IParaUiHost` 已经把 WinForms 的 `Control` 挡在算法层之外，方向是对的，只是粒度还停留在“控件”而不是“参数”（见 P3）。
- `EdgeMeasurePipeline`、`RobustFitPipeline` 是纯计算，不碰显示，可以被多个策略共用。
- `FitArcMidpointStrategy` 已经把「纯计算（`ComputeFit`）」和「绘制（`DrawPendingOverlay`）」分成两个方法，是类内分离的样板。
- `CvRegion.HoRegion` 的 setter 会释放旧句柄（`CreateROI.Result` 本身是只读属性，正是依赖这一点），匹配模板的「快照 → 试匹配 → 提交」事务也写得严谨。
- HalconUI 的交互绘制子系统是全库设计最好的一块：Session、Renderer、Shape、Geometry 四个职责分得清楚，每个 Shape 是一个状态机，`DrawGeometry` 是纯函数。**这部分整体保留**，只需要去掉其中的静态状态。
- VsControl 的绑定结构清楚：策略接口 `IVsControlBinding` 加 7 个具体策略，由 `VsControlBindingStrategyFactory` 创建，未知类型回退到空对象 `VsNullBindingStrategy`。阶段 1 的动态面板可以直接复用这套绑定。
- 测试覆盖面不错。

### 1.2 主要问题

| # | 问题 | 证据 | 影响 |
|---|---|---|---|
| P1 | **「一个类」的约定没有闭环**：宿主仍要按算法写分支 | 见 §2.2 的清单：`AlgoEnum` 枚举、`MainForm` 手写 new 和 8 个按钮、`ParaForm` 里 5 处 `switch (strategy.Algorithm)` 和 1 处 `switch (strategy)` 类型匹配 | 新增一个算法至少要改 3 个文件；已经漏注册了 RotateImage、LineRotImage、MergeRegion 三个策略 |
| P2 | 策略类内部职责没有分开 | `Fun_action` 一边计算一边绘制（FitArc 除外）；运行结果（`ArcMidpoint`、`Results`、`Coord`、`HoContour`）放在 `inPara` 里，和配置混在一起；`GenTreeNode` 里树节点和 `RegisterOutput` 要各写一遍同样的路径 | 无法无界面运行；序列化 `inPara` 时会把运行结果和句柄一起带上；树路径和解析路径容易写得不一致 |
| P3 | **控件槽位是算法与宿主 Designer 之间的隐藏契约** | 控件名魔法字符串 `cmb_100`、`ckb_disp0` 出现在全部 11 个策略文件中；`DispPara` 和 `SavePara` 靠同一组槽位字符串对齐，每个参数写两遍；`MergeRegion` 借用 `cmb_110` 当跟随坐标（因为不开 Region 页）；`RotateImage` 的 `cmb_102` 随 `RotateType` 在「角度」和「坐标系」之间切换含义；`SavePara` 每次点运行都会被调用，`MergeRegion` 只好自己比较新旧值来判断「配置是否真的变了」 | 槽位数量和位置由 ParaForm.Designer 决定：参数多一个、来源多一个，就得改 Designer，**插件契约被宿主的窗体布局卡住**；宿主只能靠 `switch` 去猜每个槽位该选什么类型 |
| P4 | 大量复制粘贴 | 4 个匹配策略合计 2037 行，结构几乎一样（Generic 的结果句柄处理和 `SavePara` 稍有不同）；FitLine 和 FitArc 的 ROI 跟随、`DispPara` 重复；Rotate 和 LineRot 的 `RunWithReset` 逐字相同；每个策略都重复一段「显示文本 / 字体」面板代码；ParaForm 里 4 个 async 绘制入口重复 | 修一个 bug 要改 4 处 |
| P5 | 没有流程（Flow）引擎 | 流程就是 `List<IParaStrategy>`；VisionMaster 的运行按钮只跑当前工具（`MainForm.cs:147`）；VisionDemo 里只有简单的顺序全跑循环；`ToolForm` 只有定义，没有任何地方实例化 | 在 VisionMaster 里，上游结果要用户手动逐个运行 |
| P6 | 工具引用是可变显示名拼成的字符串 | `ResolveFrom("形状匹配/坐标系/原点")` 用 foreach 线性查找，取第一个 `Name` 相等的策略；魔法字符串 `"默认"` 表示使用本工具自己的数据；坐标跟随还依赖隐藏的路径约定：`CoordIn.ToTmplPoint()` 把 `xxx/坐标系` 改写成 `xxx/TmplPoint` 再解析一次 | 重名或改名后引用就断了；`TmplPoint` 协议没有任何类型约束，漏注册只在运行时才暴露 |
| P7 | 没有方案持久化 | `AlgoPaths` 定义了 `SchemeDir`、`JobInfo` 等路径，但没有人使用；`FilePaths.cs` 只剩一个空壳静态类，类体全部被注释掉 | 重启后配置丢失 |
| P8 | Halcon 句柄生命周期不完整 | 匹配策略没实现 `IDisposable`，`ModelID`、`HoContour` 等泄漏；`FileImageStrategy` 没有实现 `IDisposable`，它的 `Close` 是空方法，而宿主只对实现了 `IDisposable` 的策略做释放，所以参数类 `FileImage.Image` 永远不会被释放；`Close()` 只有单元测试在调用 | 长时间运行会内存泄漏；而且「要不要实现 `IDisposable`」全靠每个类自己记得 |
| P9 | `DotNet.Drawing` 名不副实，而且被 Halcon 污染 | 纯几何、Halcon 工具、标注为「OpenCV 互操作」的结构体（仓库里其实没有 OpenCV 依赖）、JSON、日志全混在一起；6 个文件用到 Halcon 类型，其中包括 `Rect2d` 的 `HTuple` 构造函数；所有文件的命名空间都是 `DotNet.Drawing`，与文件夹不对应 | 纯几何类型也被迫依赖 halcondotnet 和 x64 原生运行时，无法独立复用 |
| P10 | 可变静态状态 | `AlgoPaths.ProjectDir`、`UIBlock`（宿主会赋值，但生产代码从不读取，只有测试读）、`Prompt.Show`、`SerializeConvert.NewtonsoftJsonFirst`、两套静态 `Log` | 测试互相干扰，行为隐式 |
| P11 | God control | `ParaForm` 573 行代码 + 1591 行 Designer；有向具体策略类型的下转；4 个 `async void` 绘制入口 | 改动风险高 |
| P12 | `HDisplay` 是 god class（968 行），而且会修改领域对象 | 一个类里混了显示、交互绘制、区域生成三种职责；4 个按 `RectEnum` 的 switch；圆环生成有 2 份实现（`GenRing`、`DispRingInternal`），`DrawRingIntoAsync` 又重复了一遍内外半径的归一化；`DispGenRegion`、`GenCoordsRegion` 会修改传入的 `CvRegion` | 改动风险高 |
| P13 | UI 层里有模板编辑和文件 IO | `HModelUI`、`HEditModelUI` 都直接调用 `ReadImage`；`HEditModelUI` 还在窗体里做 `Union2`/`Difference`；`TransObject` 两处逐字相同，`DisplayModel` 两处高度相似；`ModelExtension` 和 `ModelType` 是死代码 | UI 层不是纯视图 |
| P14 | 同一个控件上叠了三套鼠标系统 | `HWindowMouse`、`IMouseHandler`（`DrawEnum` 加 switch）、`DrawSession`；`DrawType` 公开可写，模式切换没有校验 | 事件处理顺序依赖订阅顺序 |
| P15 | VsControl 绑死了宿主窗体的控件名 | `VsControlFactory` 硬编码 `tabControl1`、`tabPage0..4`、`ckb_disp0..4`，靠反射按私有字段名取控件 | HalconUI 反向依赖 VisionMaster 的 Designer |
| P16 | UI 层的静态和全局状态 | `DrawSession.Sessions` 静态列表；`DrawHelper.Timeout`；`SetSystem("autodraw")` 是进程级参数；`HalconAPI.CancelDraw()` 是全局操作 | 两个窗口同时绘制会互相干扰 |
| P17 | 线程假设不明确 | 全部假定在 UI 线程，没有 `InvokeRequired` 保护 | 相机线程调用 `DispImage` 会跨线程修改控件 |
| P18 | 工程配置不一致 | 目标框架混用：Drawing、Core、Algo、UI、Data 是 4.5，VisionMaster、VisionDemo、Logging、Excel、Extension 是 4.5.2；Using 下各项目 Debug 是 x64、Release 是 AnyCPU，而测试项目两种配置都是 x64；HintPath 写死 `C:\DotNet\.dll\...`；VisionMaster 引用了 DotNet.Data 和 DotNet.Excel 却没用 | Debug 和 Release 的行为不一致；Release 的 exe 能以 x64 运行，只是因为没有设置 `Prefer32Bit`；换一台机器就编译不了 |
| P19 | 执行状态只能画在屏幕上 | `Fun_action` 只返回 `bool`，失败原因由各策略自己用红字 `DispText` 画出来（例如 `ShapeModelStrategy.cs:57`、`:82`）；红字是否受「显示文本」开关控制，各策略口径不一（`MergeRegion` 特意不受控） | 流程引擎拿不到失败原因，无法记录日志或汇总；拆出 `Render` 之后，这些信息没有地方放 |

> 关于「Core 里有 UI 抽象」：`IParaUiHost`、`IRoiHost`、`ITreeVisualizer`、`TabPageEnum` 放在 Core，是「一个类就能声明参数面板和 ROI 交互」的必要代价。它们只是接口，不依赖 WinForms，**保留在 Core**。真正的问题是 P3：这些接口的粒度是「控件名」，而不是「参数」。

### 1.3 现存 Bug（不依赖重构，先修）

- [x] **日志没写进文件**：（已修：`Program.cs` 把 `Drawing.Log.Current` 接到 `DotNet.Logging`，见 `DrawingLogBridge`）
  - `DotNet.Drawing.Log` 默认只写到 Trace，而 `Program.cs` 只初始化了 `DotNet.Logging`，生产代码里也没有任何地方给 `Drawing.Log.Current` 赋值。
  - 结果是 HalconUI、HalconAlgo、VisionMaster 中共 24 个文件、71 处 `Log` 调用，日志全部只进了 Trace，没有写进文件。
- [x] **模板图互相覆盖**：（阶段 0 先由 MainForm 按下标赋 `RunIndex`；阶段 1 改为按 `Id`）`RunIndex` 是策略自身的属性（`IParaStrategy.cs:18`），MainForm 从来没有给它赋值（VisionDemo 的 `CreateROIForm.cs:45` 赋值了）。所以 VisionMaster 里 4 个匹配策略的模板图都写到 `JobDir/0/matching.bmp`。
- [x] **三个策略漏注册**：（阶段 1a 由 `AlgoCatalog` 自动生成工具箱后解决）`RotateImage`、`LineRotImage`、`MergeRegion` 没加进 MainForm，但 ParaForm 已经有它们的分支。其中 `MergeRegionStrategy` 在所有生产代码里（包括 VisionDemo）都没有被实例化。
- [x] **死代码**：`MainForm.cs:136-143` 的 `switch (Name) case "ShapeMode"` 永远匹配不到（策略名实际是「形状匹配」），而且这个分支体本身就是空的。
- [x] **参数可编辑但没生效**：（`CoordIn` 已在 4 个匹配的查找里生效：本地查找区域随上游坐标系搬移；`LockCenter`、`AngleEnd` 已删除）匹配策略的 `CoordIn` 只在 `DispPara`、`SavePara` 里出现，两个 `Fun_action` 都没用到。另外 `LockCenter`（4 个匹配参数类都有）和 `AngleEnd`（只在 Generic 里有）声明了但从未被读取。
- [x] **执行结果被忽略**：（阶段 0 先记日志；阶段 2 由 `RunResult` 取代 bool）生产宿主（MainForm、VisionDemo）都丢弃了 `Fun_action` 的 bool 返回值，只有单元测试会检查它。
- [x] **写死的测试路径**：`MainForm.cs:52` 硬编码了 `D:\testImage\FitArcMidpoint`。
- [x] **序列化会带上句柄**：`HTuple ModelID`、`public HObject HoContour/Image` 没加 `[JsonIgnore]`。
- [x] **字体实现与 Halcon 版本不符**：（核实后结论不同：差别在窗口图形栈而不是版本号。`HWindowControl` 是旧图形栈的 `WIN32-Window`，2022 版的 `disp_text` 在上面报 #5123、文本整行消失。改为 `HWindowFonts.Create` 按 `get_window_type` 选择实现）项目引用的是 Halcon 22.11，但 `HDisplay.cs:43` 写死使用 `HWindowFont2018`，`HWindowFont2022` 没有被用到。
- [x] **多余引用**：HalconUI 的 csproj 引用了 Newtonsoft（HintPath 是写死的绝对路径），但没有任何代码使用。

---

## 2. 目标架构：把「单类策略」做成插件契约

### 2.1 结论

保留现有的架构形态：**分层 + 单类策略插件（基类 + 能力接口）**。不另起一套 Tool/Param/Result/Editor 多类模型，也不重命名程序集。

这里的「插件」指的是**加载期插件**：

| 特性 | 采用 | 说明 |
|---|---|---|
| 只依赖 Core 契约 | ✔ | 插件不引用 HalconUI、VisionMaster，也不知道任何控件名 |
| 自动发现 | ✔ | 启动时扫描 HalconAlgo 和 `plugins\*.dll` 中带 `[Algo]` 的类型 |
| 稳定身份 | ✔ | `[Algo]` 上的字符串键，用于方案持久化，与类名、命名空间无关 |
| 启动校验 | ✔ | 键重复、缺无参构造函数、标在抽象类上，一律启动即报错 |
| 热卸载、进程隔离 | ✘ | .NET Framework 下需要 AppDomain，收益不值得；换插件要重启 |

重构做四件事：

1. **宿主通用化**：把宿主里所有「按算法分支」的知识，改为由策略类自己声明。宿主只认识基类和能力接口，不认识任何具体算法。
2. **参数面板声明式化**：策略声明「有哪些参数」，宿主负责生成控件和双向绑定。**算法里不再出现任何控件名**，`DispPara` + `SavePara` 合并成一份声明。
3. **基类承担公共流程**：执行模板、输出声明、状态报告、生命周期都由 `ParaStrategyBase<TPara>` 统一实现，子类只填算法本身。
4. **类内分区**：同一个类里，「配置」「计算」「绘制」「声明」各自待在固定的方法里，不互相渗透。

### 2.2 宿主里现有的「按算法分支」及替代方式

这一节就是「一个类不够用」的全部原因（已对照源码核实）：

| 宿主现在的做法 | 位置 | 改为由策略类自己声明 |
|---|---|---|
| 手写 `new` 8 个策略，8 个按钮按下标切换 | `MainForm.cs:33-40, 68-104` | 策略类打 `[Algo("fit.arc-midpoint", "圆弧中点", Group = "测量")]` 特性；宿主启动时用 `AlgoCatalog` 扫描程序集，自动生成工具箱 |
| 每个策略写 `Algorithm => AlgoEnum.X`，宿主再按枚举 switch | 11 个策略文件 + `ParaForm` | **删除 `AlgoEnum`**。算法的身份是 `[Algo]` 的稳定键，显示名和分组也来自特性 |
| 按算法决定每个「来源」按钮弹出的变量类型（图像、区域、直线、坐标系） | `ParaForm` 的 `btn_100`、`btn_101`、`btn_102..105`、`btn_110`、`btn_setCoordIn` 共 5 个 handler | 策略用 `p.Source("区域来源", ..., OutEnum.Region)` 声明。宿主生成的每个来源控件自带类型，**所有来源按钮共用一个通用实现**，不再有槽位 |
| 每个参数在 `DispPara` 和 `SavePara` 里各写一次，靠槽位名对齐 | 11 个策略文件 | `DeclareParams(ParamBuilder p)` 一份声明，getter/setter 双向绑定 |
| 参数变化的检测由策略自己比较新旧值（`MergeRegion` 的 `SameConfig`） | `MergeRegionStrategy.cs:234-263` | 宿主在值真正变化时才写回并调用 `OnParamsChanged()`，策略在那里清空示教态 |
| 按算法决定新建 ROI 的默认形状（拟合类用 `AffRect`，其余用 `Rectangle`） | `ParaForm.cs:364-373` | `[Algo(..., DefaultRoi = RectEnum.AffRect)]`；只对实现了 `IRoiEditable` 的策略有意义，不进基类 |
| 编辑模板窗、绘制完成事件里，对 4 个匹配类做类型 switch 取 `ModelPath`、`ModeRect`、`HoContour`、`Results` | `ParaForm.cs:498-514, 539-565` | `ITemplateEditable` 增加 `TemplateView GetTemplateView()`，由匹配基类统一实现 |
| 只对实现了 `IDisposable` 的策略做释放 | `MainForm.cs:61` | 基类实现 `IDisposable`，子类覆盖 `Dispose(bool)`。不会再出现「忘了实现就泄漏」 |
| `RunIndex` 由宿主手动赋值（VisionMaster 漏了） | `IParaStrategy.cs:18` | 删除。模板目录由实例的 `Id` 决定；「哪些是上游」由 `FlowRunner` 构造 `RunContext` 时给出 |
| Region 页的几何读数（`cmb_Width`、`cmb_Center` 等）由每个 ROI 策略自己填 | 6 个 ROI 策略的 `DispPara` | 宿主在 `IRoiHost.SetRectPara` 时自己填，策略不再碰这些控件 |

全部替换完成后，`ParaForm` 和 `MainForm` 里不再出现任何具体策略类型、`AlgoEnum` 或 `cmb_1xx` 槽位。

### 2.3 重构后新增一个算法的样子

一个文件，包含策略类和参数类：

```csharp
[Algo("fit.arc-midpoint", "圆弧中点", Group = "测量", Order = 40, DefaultRoi = RectEnum.AffRect)]
public class FitArcMidpointStrategy : ParaStrategyBase<FitArcMidpoint>, IRoiEditable
{
    // ---------- 运行结果：放在策略类上，不放进 inPara ----------
    public Point2d ArcMidpoint { get; private set; }

    // ---------- 参数：一份声明，同时生成面板、回存、来源类型、输入依赖 ----------
    protected override void DeclareParams(ParamBuilder p)
    {
        p.Tab(TabPageEnum.Parameter)
         .Source("图像来源", () => inPara.ImageIn,  v => inPara.ImageIn  = v, OutEnum.Image)
         .Source("区域来源", () => inPara.RegionIn, v => inPara.RegionIn = v, OutEnum.Region)
         .Source("跟随坐标", () => inPara.CoordIn,  v => inPara.CoordIn  = v, OutEnum.Coord)
         .Choice("过渡方向", () => inPara.Transition, v => inPara.Transition = v,
                 (Transition.Positive, "由黑到白"), (Transition.Negative, "由白到黑"), (Transition.All, "全部"))
         .Int   ("阈值",     () => inPara.Threshold, v => inPara.Threshold = v, presets: new[] { 30, 50 })
         .Double("粗滤阈值", () => inPara.CoarseGate, v => inPara.CoarseGate = v, min: 0);
        p.Tab(TabPageEnum.Display)
         .Flag("拟合点", () => inPara.DispFixPoint, v => inPara.DispFixPoint = v);
        // 「显示文本 / 字体」三项由基类自动追加，Region 页由宿主根据 IRoiEditable 自动打开
    }

    // ---------- 输出：一次声明，同时生成变量树和解析器 ----------
    protected override void DeclareOutputs(OutputBuilder o)
    {
        o.Point("中点", () => ArcMidpoint);          // 自动带出「中点/行」「中点/列」
    }

    // ---------- 计算：纯计算，不碰显示 ----------
    protected override void ResetOutputs() => ArcMidpoint = default;

    protected override RunResult Execute(RunContext ctx)
    {
        HObject image  = ctx.ResolveImage(inPara.ImageIn);              // Local → 当前图像
        HObject region = ctx.ResolveRegion(inPara.RegionIn, inPara.HoRect);
        CoordFollow follow = ctx.ResolveCoord(inPara.CoordIn);          // Local → 恒等跟随
        ...                                                             // 只写算法本身
        return RunResult.Ok($"中点:({ArcMidpoint.X:F2},{ArcMidpoint.Y:F2})");
    }

    // ---------- 绘制：只读运行结果去画；状态文本由基类统一画 ----------
    protected override void Render(IHDisplay display, RunResult result) { ... }

    // ---------- ROI 交互（可选能力） ----------
    public Task DrawROIAsync(IRoiHost host, RectEnum type, bool newROI) { ... }
    public void DispROI(IRoiHost host) { ... }

    // ---------- 生命周期 ----------
    protected override void Dispose(bool disposing) { inPara.HoRect?.Dispose(); base.Dispose(disposing); }
}

public class FitArcMidpoint : DisplayOptions   // 只放可序列化的配置
{
    public SourceRef ImageIn  { get; set; } = SourceRef.Local;
    public SourceRef RegionIn { get; set; } = SourceRef.Local;
    public SourceRef CoordIn  { get; set; } = SourceRef.Local;
    public Transition Transition { get; set; } = Transition.Positive;   // 存枚举，不存界面文字
    ...
}
```

**新增算法不需要改的东西**：`MainForm`、`ParaForm`（含 Designer）、`ValueForm`、任何其他文件。

### 2.4 Core 契约（草案）

```csharp
// ---- 注册：代替 AlgoEnum 和 MainForm 的手写 new ----
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AlgoAttribute : Attribute
{
    public AlgoAttribute(string key, string displayName) { Key = key; DisplayName = displayName; }
    public string Key { get; }                  // 稳定身份，写进方案文件；一经发布不再修改
    public string DisplayName { get; }
    public string Group { get; set; }
    public int Order { get; set; }
    public RectEnum DefaultRoi { get; set; } = RectEnum.Rectangle;   // 仅 IRoiEditable 有意义
}

public sealed class AlgoInfo { string Key; string DisplayName; string Group; int Order; RectEnum DefaultRoi; Type Type; }

public sealed class AlgoCatalog
{
    // 扫描显式传入的程序集 + 插件目录；任何校验失败都抛 AlgoCatalogException，列出全部问题
    public static AlgoCatalog Load(IEnumerable<Assembly> builtIn, string pluginDir);
    public IReadOnlyList<AlgoInfo> Algorithms { get; }
    public AlgoInfo Find(string key);
    public IParaStrategy Create(string key);    // 同时设置 Name = DisplayName、Id = Guid.NewGuid()
}

// ---- 引用：代替 "默认" 和 "工具名/路径" 字符串 ----
public struct SourceRef : IEquatable<SourceRef>
{
    public Guid ToolId { get; }                 // Guid.Empty 表示本地（原来的 "默认"）
    public string Output { get; }               // 工具内的输出路径，例如 "坐标系/原点"
    public static readonly SourceRef Local;
    public bool IsLocal { get; }
}

// ---- 执行状态：代替 bool + 各策略自己画红字 ----
public enum RunStatus { Ok, Warning, Error }
public sealed class RunResult
{
    public RunStatus Status { get; }
    public string Message { get; }
    public TimeSpan Elapsed { get; internal set; }   // 由基类计时
    public static RunResult Ok(string message = null);
    public static RunResult Warn(string message);    // 例如 MergeRegion 有无效来源但仍出结果
    public static RunResult Fail(string message);
}

// ---- 执行上下文：把「本地 / 上游」的解析收进一处 ----
public sealed class RunContext
{
    public HObject CurrentImage { get; }                         // 单图验证时就是传入的图
    public IReadOnlyList<IParaStrategy> Upstream { get; }        // 只含当前工具之前的工具
    public CancellationToken Cancellation { get; }

    public HObject ResolveImage(SourceRef source);                // Local → CurrentImage
    public HObject ResolveRegion(SourceRef source, CvRegion local); // Local → 本地 ROI，带 IsUsableRegion 检查
    public CoordFollow ResolveCoord(SourceRef source);            // Local → 恒等；否则返回 { Current, Template }
    public T Resolve<T>(SourceRef source);
    public bool TryResolve<T>(SourceRef source, out T value);
}

// ---- 显示选项：从 HalconAlgo.AlgoFont 下沉到 Core，基类要用它统一画状态文本 ----
public class DisplayOptions
{
    public bool DispText { get; set; } = true;
    public int FontX { get; set; } = 50;
    public int FontY { get; set; } = 50;
    public int FontSize { get; set; } = 15;
}

// ---- 基类：公共流程收进来，子类只填空 ----
public abstract class ParaStrategyBase<TPara> : IParaStrategy, IParaBinding, ITreeNodeProvider, IDisposable
    where TPara : DisplayOptions, new()
{
    public TPara inPara { get; set; } = new TPara();
    public object Para => inPara;                       // 宿主做通用序列化用
    public Guid Id { get; set; }                        // 稳定实例标识，代替按 Name 查找
    public string Name { get; set; }                    // 默认取 [Algo] 的 DisplayName，用户可改
    public RunResult LastResult { get; private set; }

    // 执行入口由基类实现，顺序固定：
    //   ResetOutputs() → 计时 Execute(ctx)（异常转成 Fail，并再次 ResetOutputs）→ Render(display, result) → 画状态文本
    public RunResult Run(RunContext ctx, IHDisplay display);

    protected abstract void ResetOutputs();
    protected abstract RunResult Execute(RunContext ctx);          // 纯计算，不碰显示
    protected virtual void Render(IHDisplay display, RunResult result) { }
    // 状态文本规则：Error / Warning 始终显示（红字）；Ok 只在 DispText 为 true 时显示（绿字）

    protected abstract void DeclareParams(ParamBuilder p);         // 代替 DispPara + SavePara
    protected virtual void OnParamsChanged() { }                   // 只在值真正变化时调用
    protected abstract void DeclareOutputs(OutputBuilder o);       // 代替 GenTreeNode + RegisterOutput

    public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
    protected virtual void Dispose(bool disposing) { }
}
```

**参数声明 `ParamBuilder`**（Core，不依赖 WinForms）：

```csharp
public sealed class ParamBuilder
{
    public ParamBuilder Tab(TabPageEnum tab);
    public ParamBuilder Source(string label, Func<SourceRef> get, Action<SourceRef> set, OutEnum type);
    public ParamBuilder Choice<T>(string label, Func<T> get, Action<T> set, params (T value, string text)[] items);
    public ParamBuilder Int(string label, Func<int> get, Action<int> set, int[] presets = null, int? min = null, int? max = null);
    public ParamBuilder Double(string label, Func<double> get, Action<double> set, double[] presets = null, double? min = null, double? max = null);
    public ParamBuilder Flag(string label, Func<bool> get, Action<bool> set);
    public ParamBuilder Folder(string label, Func<string> get, Action<string> set);
    public ParamBuilder When(Func<bool> visible);     // 作用于上一项；任一参数变化后宿主重新求值
    public IReadOnlyList<ParamItem> Items { get; }     // 宿主读取，按 Tab 分组生成控件
}
```

- `When` 解决 RotateImage 那种「同一个位置随模式切换含义」的情况：声明两项，各带一个 `When`，而不是让一个槽位身兼两职。
- 每个 `Source` 项同时就是一条**输入依赖**：`FlowRunner` 和 `ValueForm` 都可以据此校验「来源必须是排在前面的工具，且类型匹配」。
- 宿主在控件值通过校验、且与 getter 的结果不同时才调用 setter，最后统一调用一次 `OnParamsChanged()`。「点运行」不再隐含回存，MergeRegion 的 `SameConfig` 比较可以删掉。
- 逃生口：确实无法用以上几种项表达的面板，策略可以额外实现 `ICustomParamPanel`（Core 里只有接口，返回宿主无关的描述，或者由 UI 层按键查找注册好的自定义控件）。**目前 11 个策略都不需要它**，先只定义接口，不实现。

**输出声明 `OutputBuilder`**：

```csharp
public sealed class OutputBuilder
{
    public OutputBuilder Image(string name, Func<HObject> get);
    public OutputBuilder Region(string name, Func<HObject> get);
    public OutputBuilder Point(string name, Func<Point2d> get);                      // 自动带出 行 / 列
    public OutputBuilder Line(string name, Func<CvLine> get);                        // 自动带出 起点 / 终点
    public OutputBuilder Coord(string name, Func<CvCoord> current, Func<Point2d?> template); // 自动带出 原点 / 角度，并登记跟随所需的模板点
    public OutputBuilder Number(string name, Func<double> get);
}
```

`Coord` 把「当前坐标系 + 示教模板点」作为一个整体登记，`RunContext.ResolveCoord` 一次拿到两者。原来的 `ToTmplPoint()` 路径改写约定和单独注册的 `TmplPoint` 输出一起删除。

说明：

- 现有的能力接口（`IRoiEditable`、`ITemplateEditable`）保留，`ITemplateEditable` 增加 `GetTemplateView()`。`IParaBinding`、`ITreeNodeProvider` 改由基类实现，宿主调用方式不变。
- 目标框架是 .NET Framework，**不能用接口默认实现**。有默认值的元数据放在 `[Algo]` 特性上，有默认行为的方法放在基类上。
- 子类类声明上写不写 `IDisposable` 都可以。基类已经实现了它，子类只需要覆盖 `Dispose(bool)`。
- 现有的 `FitArcMidpointRenderData` 那种「把渲染数据单独做成一个类」的做法，在需要跨线程绘制时仍然可以用，但不是必需的。默认做法是 `Render` 直接读策略上的结果属性。
- **Core 就是插件的 ABI**。Core 的公开成员一旦发布，只增不改；需要破坏性修改时提升 Core 的主版本号，`AlgoCatalog` 加载插件时检查它引用的 Core 主版本，不一致就拒绝加载并说明原因。

### 2.5 同族算法：用中间基类，不用复制

仍然是「一个算法一个类」，只是继承链多一层：

| 中间基类 | 子类 | 公共部分 | 子类只写 |
|---|---|---|---|
| `MatchStrategyBase<TPara> : ParaStrategyBase<TPara>, IRoiEditable, ITemplateEditable` | Shape、Ncc、Scaled、Generic | 模板事务（快照 → 试匹配 → 提交）、结果循环、公共参数声明、输出声明（坐标系 + 模板点）、`GetTemplateView`、`ModelID` 的释放、模板文件路径 | `CreateModel`、`FindModel`、`ClearModel` 三个钩子，加上各自特有参数的声明（在 `base.DeclareParams(p)` 之后追加） |
| `MatchParaBase : DisplayOptions`（参数基类） | 4 个匹配参数类 | `ModelPath`、`ModeRect`、`MinScore`、`NumMatches`、角度范围等公共字段（`HTuple` 改成 `double`、`int`） | 特有字段 |
| `RotateStrategyBase<TPara>` | RotateImage、LineRotImage | `RunWithReset` 等公共流程 | 旋转角度的来源 |
| `RunContext.ResolveCoord`（不是基类，是辅助方法） | FitLine、FitArc、CreateROI、MergeRegion 等 | 坐标系跟随 | 无 |

预计 4 个匹配策略能从 2037 行降到约 700 行；参数面板声明化后，每个策略再少 40～80 行。

### 2.6 程序集划分

保持现有的 5 个项目，不重命名：

```
DotNet.Drawing     ← HalconCore（插件契约：基类、能力接口、[Algo]、AlgoCatalog、ParamBuilder、OutputBuilder、
                   │             RunContext、RunResult、SourceRef、FlowRunner、DisplayOptions）
                   ← HalconAlgo（内置算法，每个算法一个文件；本身就是一个「内置插件」）
                   ← HalconUI（显示和交互，实现 IHDisplay、IRoiHost；根据 ParamItem 生成控件）
                   ← VisionMaster（宿主：只认识 Core）
plugins\*.dll      ← 第三方算法，只引用 Drawing + HalconCore
```

- HalconAlgo **不能**引用 HalconUI，也不应依赖任何 Core 以外的宿主约定，这样它和外置插件走的是同一条路。可以加一个架构测试：HalconAlgo 的引用列表里只允许 Drawing、HalconCore、halcondotnet 和 BCL。
- 可选：把 `DotNet.Drawing` 里的纯几何类型拆成一个不依赖 halcondotnet 的 AnyCPU 项目（P9）。这与插件目标无关，优先级放到最后。

### 2.7 考虑过但不采用的方案

| 方案 | 不采用的原因 |
|---|---|
| 拆成 Tool + Param + Result + Editor 多个类 | 违背「一个类完成一个算法」的约定，新增算法的成本反而更高 |
| 只给 `ShowSource` 加类型、保留槽位（原计划阶段 1） | 11 个策略要先改一遍，去槽位时再改一遍；槽位数仍由 Designer 决定，插件契约不成立 |
| 用 `[Param("阈值")]` 特性标注参数类、全自动反射生成面板 | 条件显示、枚举文字、单位换算都要塞进特性参数，表达力不如 lambda 声明；调试困难 |
| MEF（`System.ComponentModel.Composition`） | 能做发现，但校验、稳定键、显示元数据仍要自己写；自写 `AlgoCatalog` 约 100 行，更透明 |
| AppDomain 隔离、热卸载 | 跨域调用要求可序列化 / `MarshalByRefObject`，HObject 句柄无法穿越；收益远小于成本 |
| 数据流图（节点 + 连线）引擎 | 当前流程是线性的；`SourceRef` + 声明的输入依赖已经能表达 DAG，将来需要时可以在 `FlowRunner` 上演进，不必现在引入 |
| Clean Architecture / DDD 全套、迁移到 WPF、引入 DI 容器 | 收益不足以覆盖成本 |

---

## 3. 分阶段实施计划

渐进迁移，每个阶段都能独立编译、测试并合入。**新旧写法在迁移期并存**：基类新增的成员都提供默认实现，没迁移的策略照样能跑。

### 阶段 0：修 Bug 和工程卫生（1～2 天，低风险）

- [x] 修复 §1.3 中的全部 Bug。
- [x] 删除 `DotNet.Drawing.Log`，把调用改到 `DotNet.Logging`。过渡期也可以先在 `Program.cs` 里把 `Drawing.Log.Current` 桥接到 DotNet.Logging。（采用桥接并保留 `Drawing.Log`：它是插件可见的日志抽象，插件契约不应依赖 DotNet.Logging）
- [x] 统一目标框架（按决定**暂不升 4.8**，全部统一到 4.5.2；因此 `Choice<T>` 不能用 ValueTuple，改用 `Option.Of(value, text)`），建议直接升到 **.NET Framework 4.8**，因为 4.5 和 4.5.2 都已停止支持。注意：升级后**现有的 polyfill 都还要保留**。4.8 里仍然没有 `System.HashCode`（对应 `Internal/HashCode.cs`）和 `IsExternalInit`。4.7 起才有 `System.ValueTuple`，`Choice<T>` 的 `(T, string)[]` 参数依赖它。
- [x] 统一平台为 x64，Release 也一样。原因是 HALCON 的原生 `halcon.dll` 只有 x64 版本，所以宿主进程必须是 64 位的。`halcondotnet.dll` 本身是 AnyCPU。
- [x] HintPath 改为仓库内的相对路径，或者改用 NuGet。（改为 `src/Directory.Build.props` 里的 `$(DotNetDllDir)`：可用 `/p:`、环境变量 `DOTNET_DLL_DIR` 或仓库 `lib\` 覆盖，默认仍是 `C:\DotNet\.dll`）
- [x] 删除 VisionMaster 中未使用的 DotNet.Data、DotNet.Excel 引用。
- [x] 删除死代码：`ToolForm`、`FilePaths.cs`（空壳类）、`ValueForm.GenerateTree(TreeView)` 重载（实际被调用的是另一个重载）、`AlgoPaths.UIBlock`（同时删除 `MainFormTests` 里对它的断言）、未使用的 `LockCenter`、`AngleEnd`。

### 阶段 1：插件注册 + 声明式参数面板，删除 `AlgoEnum` 和槽位（1.5～2 周）★ 核心阶段

目标：完成后，新增算法只需要写一个类，宿主（含 Designer）零改动。

**1a. 注册与身份**

- [x] 新增 `AlgoAttribute`、`AlgoInfo`、`AlgoCatalog`（含 `plugins\` 目录扫描和启动校验），给现有 11 个策略打上特性，确定稳定键。（稳定键见各策略的 `[Algo]`；插件目录里带共享程序集副本、非 .NET dll、Core 主版本不符都在启动时报错）
- [x] MainForm 改为由 `AlgoCatalog` 生成工具箱，删除手写 `new` 和 8 个 `buttonN_Click`。顺带解决了漏注册的问题。
- [x] 基类增加 `Id`、`Name` 默认实现；删除 `RunIndex`，模板目录暂时改为按 `Id` 计算。（数据目录为 `DataDir`，默认按 `Id`，方案保存时迁到 `Scheme/<方案>/<Id>/`）

**1b. 声明式参数面板**

- [x] Core 新增 `ParamBuilder`、`ParamItem`；基类新增 `DeclareParams`、`OnParamsChanged`，并基于它们实现 `IParaBinding.DispPara/SavePara`（迁移期：子类没覆盖 `DeclareParams` 时，仍走旧的 `DispPara/SavePara`）。（未保留迁移期双路径：11 个策略一次迁完，`IParaBinding` 直接改为 `DescribeParams()` + `ParamsChanged(changed)`；`OnParamsChanged` 带上真正变了的项，MergeRegion 据此只在来源变化时清示教点）
- [x] HalconUI 新增 `ParamPanel`：按 `ParamItem` 在 `TableLayoutPanel` 里生成控件，复用 VsControl 的绑定；`Source` 项统一弹出 `ValueForm` 并按 `OutEnum` 过滤；`When` 在任一值变化后重新求值。（没有复用 VsControl：VsControl 靠私有字段名反射查找控件，动态生成的控件用不上；ParamPanel 自己持有控件并直接绑定。VsControl、`IParaUiHost`、`WinFormsParaUiHost` 已删除）
- [x] ParaForm 的参数页、显示页改为承载 `ParamPanel`；Region 页、Matching 页仍是固定布局，由宿主根据 `IRoiEditable` / `ITemplateEditable` 决定是否显示，几何读数由宿主在 `SetRectPara` 时自己填。（五个页都承载 ParamPanel；Region / Matching 页的固定工具栏保留，几何读数由宿主订阅 `HDisplayUI.RoiShown` 填写）
- [x] 逐个策略把 `DispPara` + `SavePara` 改写为 `DeclareParams`。顺序：FileImage → CreateROI → LineRot → Rotate（验证 `When`）→ MergeRegion（验证 `OnParamsChanged`，删除 `SameConfig`）→ FitLine → FitArc → 4 个匹配。
- [x] 删除 ParaForm.Designer 里的 `lbl/cmb/btn_100..115`、`ckb_disp0..4`、`CB_Font*` 以及对应的 5 个来源 handler。（Designer 重写，1591 行 → 约 500 行）

**1c. 其余宿主分支**

- [x] ROI 默认形状改读 `AlgoInfo.DefaultRoi`；删除 `ParaForm.cs:364-373` 的 switch。
- [x] `ITemplateEditable` 增加 `GetTemplateView()`，4 个匹配类实现它；删除 `ParaForm` 里的两处类型 switch 和具体类型下转。
- [x] 基类实现 `IDisposable`（`Dispose(bool)` 模式），宿主改为对所有策略统一释放。`FileImage` 和 4 个匹配类补上各自的释放逻辑。（`IParaStrategy : IDisposable`；配置 ROI 也随工具一起释放，宿主先保存再释放）
- [x] 上面全部完成后，**删除 `AlgoEnum` 和所有 `Algorithm` 属性**。

**验收**

- [x] `ParaForm`、`MainForm` 里搜不到任何具体策略类型名、`AlgoEnum`、`cmb_1xx`。（ParaFormTests.NoSlotControls 守住）
- [x] 在测试项目里写一个只引用 HalconCore 的假算法，编译成 dll 放进 `plugins\`，启动后出现在工具箱里，参数面板、来源选择、运行都能用。（`UnitTest/DotNet.SamplePlugin`，测试把它复制进临时 plugins 目录加载）

### 阶段 2：基类承担执行流程（1 周，逐个策略迁移）

- [x] Core 新增 `RunContext`、`RunResult`、`SourceRef`；基类新增 `ResetOutputs`、`Execute(RunContext)`、`Render(IHDisplay, RunResult)` 模板方法和统一的状态文本绘制。迁移期两个 `Fun_action` 仍是 virtual，由基类适配到新的 `Run`；没迁移的策略照常覆盖它们。（与阶段 1 合并实施，没有保留 `Fun_action` 的迁移期适配；取消 (`OperationCanceledException`) 会向外传播，其余异常一律转成 Fail）
- [x] 基类新增 `DeclareOutputs(OutputBuilder)`，由它同时生成变量树和解析器；迁移期 `GenTreeNode` 仍是 virtual。（另外由基类追加"结果 / 文本显示"两个公共输出，原来的 `CommonNodes` 只有树节点没有值）
- [x] `AlgoFont` 下沉到 Core，改名 `DisplayOptions`；「显示文本 / 字体」三项由基类自动追加到参数声明里。
- [x] 逐个策略迁移：
  - 把运行结果从 `inPara` 挪到策略类的属性上；
  - 把计算和绘制拆成 `Execute`、`Render` 两个方法，删除各策略里自己画的红字；
  - 参数类里的 `HTuple` 改为 `double`、`int`，界面文字（「由黑到白」「是 / 否」）改为枚举或 `bool`，文字只出现在 `Choice` 声明里；
  - 来源字段从 `string` 改为 `SourceRef`（此时 `SourceRef` 先按「工具名 + 路径」解析，阶段 4 再切到 `Id`）。
- [x] 每迁移一个策略，补齐无界面运行的单元测试：直接调用 `Execute`，不需要显示窗口；并断言 `Execute` 失败后所有输出都是默认值。
- [x] 全部迁移完成后，删除两个 `Fun_action` 和 `GenTreeNode` 的 virtual 入口，`IAlgoStrategy` 只保留 `Run`。

### 阶段 3：同族去重（1 周）

- [x] `MatchStrategyBase<TPara>` 和 `MatchParaBase`：把 4 个匹配类的公共部分收进来。模板事务保留现有的严谨实现，只搬到基类里。（另外：模型句柄不再放进参数，模型文件随模板一起写进数据目录、`Init` 时读回；匹配 0 个结果改为 Fail）
- [x] `RotateStrategyBase<TPara>`：消除 `RunWithReset` 的重复。（另加 `EdgeFitStrategyBase<TPara>` 收拢 FitLine / FitArc 的参数、ROI 与测量前端）
- [x] `OutputBuilder.Coord` + `RunContext.ResolveCoord`：消除 FitLine、FitArc、MergeRegion 里复制的坐标系跟随逻辑，删除 `ToTmplPoint()` 和单独的 `TmplPoint` 输出。
- [x] 模板图路径改为由策略实例的 `Id` 或方案目录决定，不再读静态 `AlgoPaths`。（`AlgoPaths` 已删除）

### 阶段 4：流程运行和方案持久化（1 周）

这些都写在宿主或 Core 里，**算法类不需要为此增加任何代码**。

- [x] `FlowRunner`：按顺序执行整个流程，支持单步执行、从某个工具开始执行；为每个工具构造只含上游的 `RunContext`；收集每个工具的 `RunResult`（含耗时），失败时按策略决定停止或继续。（"当前图像"随 `IImageProducer` 前进，取代原来读显示窗口里的图）
- [x] `SourceRef` 切换为按 `ToolId` 解析；界面显示的「工具名/路径」在运行时拼出来，不存储。重命名工具不会断开引用。（与阶段 1 合并：没有经过"工具名 + 路径"的过渡形态）
- [x] 运行前校验：用参数声明里的 `Source` 项检查每个引用的工具存在、排在前面、输出类型匹配；不满足的工具在工具树上标红。（工具列表里标红，并在状态栏给出第一条问题）
- [x] 方案序列化：每个工具存 `{ AlgoKey, Id, Name, Para }`。加载时用 `AlgoCatalog.Create(AlgoKey)` 创建实例；找不到键（插件缺失）时保留原始 JSON 并在界面上标出，再次保存时原样写回，不丢配置。模板图和模型文件存放在 `Scheme/<方案>/<Id>/`。（`FlowScheme` + `MissingTool` 占位）
- [x] 实现工具树（目前 `ToolForm` 是空壳）：支持增删、排序、重命名。可添加的工具列表来自 `AlgoCatalog`，按 `Group`、`Order` 排列。（流程是线性的，做成主窗右侧的工具列表 + "添加"菜单，没有另开窗体）
- [x] 补集成测试：多工具串联执行；保存后重新加载，结果一致。

### 阶段 5：UI 层内部整理（1～2 周，可以和阶段 2～4 并行）

这部分不影响算法类的写法：

- [x] 拆分 `HDisplay`：（交互绘制拆到 `RoiInteraction`；圆环生成三份实现收拢为 `DotNet.Drawing.RegionShapes.GenRing`；字体经构造函数可注入；删除会改写传入 CvRegion 的 `DispGenRegion` / `GenCoordsRegion`）
  - 显示和画笔状态。
  - 交互绘制（`RoiInteraction`）。
  - 区域生成逻辑移到 Drawing 或 Core。
  - `IHWindowFont` 改为注入，并按 Halcon 版本选择实现。
- [x] 把 `HModelUI`、`HEditModelUI` 中的模板编辑（仿射变换、Union、擦除累积）和读图抽成服务，合并两份重复的 `TransObject`、`DisplayModel`。（`TemplatePreview`：读图、平移到小图中心、并集 / 差集）
- [x] 鼠标模式改成显式的状态机：同一时刻只有一个活动模式，平移和缩放作为默认模式。`DrawType` 不再公开可写。（`HDisplayUI.Dispatch` 固定顺序：视图导航 → 重绘 → 当前模式；`DrawType` 外部只读、设置时校验取值）
- [x] 去掉绘制子系统中的静态状态：`DrawSession` 注册表改为每个窗口一个实例；`Timeout` 改为参数传入；`autodraw` 的保存和还原按窗口隔离。（`Timeout` 改为各入口参数；删除进程级 `HalconAPI.CancelDraw()`；**autodraw 的保存 / 还原直接删除**：HALCON 22.11 没有这个系统参数，get/set 都报 #1301，原代码每次会话都静默失败。会话注册表本来就按窗口区分，保留）
- [x] 让 Shape 状态机可以脱离 Halcon 测试：`DrawRenderer` 抽出 `IDrawCanvas` 接口，Shape 改用自有的 `MouseInput` 结构，代替 `HMouseEventArgs`。
- [x] VsControl 改为显式注册控件，取代按私有字段名反射（阶段 1 的 `ParamPanel` 生成的控件已经是显式注册的，这里处理剩下的 Region、Matching 页）。（VsControl 已整体删除：参数页由 ParamPanel 生成，Region / Matching 页的固定控件由 ParaForm 直接持有，不再有按名字反射的地方）
- [x] ParaForm 里 4 个重复的 async 绘制入口合并为一个。（`RunDraw`）
- [x] 显示入口统一切回 UI 线程，为将来接入相机做准备。（`UiThreadDisplay` 装饰器，`HDisplayUI.Display` 返回它）
- [x] 删除死代码 `ModelExtension`、`ModelType`；`ZoomImage` 构造函数的默认分辨率 1248x2200 不再写死（`HWindowImage` 的 `zoomInfo` 已改为 0x0，只有 `getInfo` 还在沿用这个默认值）。（`ZoomImage` 默认改为 0×0）

### 阶段 6（可选）：拆分 DotNet.Drawing

- [ ] 纯几何类型拆到一个不引用 halcondotnet 的 AnyCPU 项目；`Rect2d(HTuple)` 构造函数改为 Halcon 侧的扩展方法。
- [ ] 拆 `HalconController`：目录排序、仿射变换、存图分别归位。
- [ ] `StringExtension.ToTmplPoint` 在阶段 3 后已无调用方，直接删除。`ExtractNumber` 与 `DotNet.Extension` 里的版本合并成一份。注意 Drawing 版修复了「⑫ 这类大于 9 的带圈数字被截成首位」的 bug，Library 版（`DotNet.Extension/StringExtension.cs:154`）还没修，所以**要以 Drawing 版为准**。
- [ ] `Rect`、`Point2f`、`Rect2f`、`Size2f`、`TransExpV2`、`SerializeConvert` 只有测试在用，决定删除还是保留。

---

## 4. 验收标准

- [x] **新增一个算法 = 新增一个 `.cs` 文件**（策略类 + 参数类），打上 `[Algo]`，继承 `ParaStrategyBase<TPara>`（或同族中间基类），按需实现能力接口。**不修改任何已有文件，包括 Designer。**（样例插件即是一个文件）
- [x] **外置插件可用**：只引用 Drawing + HalconCore 编译出的 dll 放进 `plugins\`，重启后可添加、配置、运行、保存、重新加载。
- [x] `MainForm`、`ParaForm` 中不出现任何具体策略类型名、`AlgoEnum` 或 `cmb_1xx` 槽位；算法类中不出现任何控件名字符串。
- [x] 每个参数只在 `DeclareParams` 里出现一次；每个输出只在 `DeclareOutputs` 里出现一次。
- [x] 每个策略的 `Execute` 可以在单元测试里无界面运行，失败原因通过 `RunResult` 返回，而不是只画在屏幕上。
- [x] 每个策略都会被释放（基类实现 `IDisposable`），长时间循环运行时内存平稳，不泄漏 HObject 或 HTuple 句柄。（`SoakTests`：真实流程跑 2000 轮，私有内存增长 < 8 MB）
- [x] `inPara` 只包含可序列化的配置：没有运行结果、没有 Halcon 句柄、没有界面文字。
- [x] 方案保存后重新打开，参数、ROI、模板、工具之间的引用全部还原；重命名工具不会断开引用；插件缺失时配置不丢。
- [x] HalconAlgo 只引用 Drawing、HalconCore、halcondotnet 和 BCL（架构测试守住）。（`AlgoCatalogTests.HalconAlgo_ReferencesOnlyContractAndHalcon`）
- [x] Core 和 Algo 中没有可写的 public static 字段。（`AlgoCatalogTests.CoreAndAlgo_HaveNoWritablePublicStatics`）
- [x] 现有测试全部通过，或者已迁移到新的写法。

## 5. 风险与注意事项

- **测试依赖私有实现**：VisionMaster 的测试通过 `Infrastructure/Priv.cs` 反射私有成员。阶段 1 删除槽位控件后，依赖 `cmb_1xx` 的测试会大面积失效，需要改为针对 `ParamItem` 列表（纯数据，无需窗体）断言，反而更好测。
- **动态面板的观感**：自动布局在像素级上不会和现在的 Designer 一模一样。先定好统一的行高、标签宽度和每页最大行数（超出时滚动），在 FitArc（参数最多的策略）上确认观感后再批量迁移。
- **参数写回时机变化**：现在是「点运行 → `SavePara`」，改为控件值变化即写回。要确认没有策略依赖「运行前才回存」的副作用（目前已知只有 MergeRegion 的示教态清空，阶段 1 用 `OnParamsChanged` 替代）。
- **反射扫描的范围**：`AlgoCatalog` 只扫描显式传入的程序集和 `plugins\` 目录，不要扫描整个 AppDomain，避免把测试替身也注册进来。
- **插件的依赖冲突**：所有插件与宿主共用一个 AppDomain，共用同一份 halcondotnet 和 Core。插件不得自带这两个 dll 的副本；`AlgoCatalog` 检测到插件目录里有它们时直接报错。
- **Core 的兼容性**：Core 是插件的 ABI，阶段 1～3 期间还会频繁改动。在阶段 3 结束之前，不对外承诺插件兼容性；阶段 3 结束后冻结 Core 公开成员，此后只增不改。
- **稳定键一经发布不能改**：方案文件靠 `[Algo]` 的键找类型。命名约定建议用 `分组.算法`（小写、连字符），在代码评审时检查。
- **旧方案兼容**：目前没有持久化，所以没有旧数据需要兼容，这是调整 `inPara` 结构（`SourceRef`、枚举化、移出运行结果）的最好时机。**阶段 4 之前必须完成这些调整。**
- **Halcon 线程**：`FlowRunner` 如果放到后台线程执行，`Render` 必须切回 UI 线程；HWindow 只能在 UI 线程操作。`Execute` 和 `Render` 分开以后，这一点很好实现。
