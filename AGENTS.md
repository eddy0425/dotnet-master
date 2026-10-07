# AGENTS.md

## 项目概览

机器视觉系统，.NET Framework 4.5/4.5.2 + WinForms + Halcon 22.11。当前重构范围是 `src/Using/`：

| 项目 | 职责 |
| --- | --- |
| `DotNet.Drawing` | 绘图基础 |
| `DotNet.HalconCore` | 算法契约（SDK：基类、特性、能力接口），插件只看到这一层 |
| `DotNet.VisionRuntime` | 无界面宿主内核：算法目录与插件加载、流程引擎、方案读写 |
| `DotNet.HalconAlgo` | 内置算法实现 |
| `DotNet.HalconUI` | Halcon 显示与交互控件 |
| `DotNet.VisionMaster` | 宿主程序 |

测试在 `src/UnitTest/`（MSTest 2.2.10），`DotNet.SamplePlugin` 是外置插件示例。

## 构建与测试

- 解决方案：`src/dotnet-master.sln`
- 第三方 dll 不进仓库，按优先级查找：`/p:DotNetDllDir=...` > 环境变量 `DOTNET_DLL_DIR` > 仓库根 `lib\` > `C:\DotNet\.dll`（见 `src/Directory.Build.props`）
- 构建：`msbuild src/dotnet-master.sln /restore`
- 测试：`vstest.console.exe src/UnitTest/<项目>/bin/Debug/<项目>.dll`
- 改动后跑受影响项目的测试，全部通过后再提交

## 算法插件契约（必须遵守）

**一个算法 = 一个类**：继承 `ParaStrategyBase<TPara>`，按需实现能力接口。

```csharp
[Algo("fit.arc-midpoint", "圆弧中点", Group = "测量")]
public class FitArcMidpointStrategy : ParaStrategyBase<FitArcMidpoint>, IRoiEditable
```

1. **只依赖 Core**：算法类只引用 `DotNet.HalconCore` 和 `DotNet.Drawing`，不能引用宿主控件。
2. **零登记**：算法类和参数类放在同一个文件里；新增算法不改宿主端任何代码（包括 Designer）。
3. **可外置**：放在 `DotNet.HalconAlgo` 中，或编译成独立 dll 放进 `plugins\`，行为一致。

## 代码风格

- 匹配周边代码的命名、注释密度和写法
- 注释、文档使用中文

## 提交规范

Conventional Commits，描述用中文，例如 `refactor: 阶段 5（一）—— HDisplay 拆分`。
类型：`feat` `fix` `refactor` `perf` `test` `docs` `style` `build` `chore`。
