using System;
using System.Collections.Generic;
using System.Linq;
using DotNet.HalconAlgo;
using DotNet.HalconCore;
using DotNet.HalconUI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.VisionMaster.Tests
{
    /// <summary>
    /// <see cref="MainForm"/>：装配 8 个算法策略，按钮切换工具时与 <see cref="ParaForm"/> 保持同一个索引。
    /// </summary>
    /// <remarks>
    /// 绘制进行中切换工具的那道闸门会弹 <c>MessageBox</c>，无人值守的测试里没法点掉，这里不覆盖；
    /// 闸门的判据 <see cref="ParaForm.IsDrawBusy"/> 由 <see cref="ParaFormTests"/> 覆盖。
    /// </remarks>
    [TestClass]
    public class MainFormTests : HalconTestBase
    {
        private static void Run(Action<MainForm> body) =>
            Sta.Run(() =>
            {
                using (var form = new MainForm())
                {
                    WindowHost.ShowOffscreen(form);
                    body(form);
                }
            });

        private static List<IParaStrategy> Strategies(MainForm form) => Priv.Get<List<IParaStrategy>>(form, "_strategys");

        [TestMethod]
        public void Constructor_RegistersStrategiesInButtonOrder()
        {
            Run(form =>
            {
                CollectionAssert.AreEqual(new[]
                {
                    typeof(FileImageStrategy),
                    typeof(CreateROIStrategy),
                    typeof(ShapeModelStrategy),
                    typeof(FitLineStrategy),
                    typeof(FitArcMidpointStrategy),
                    typeof(NccModelStrategy),
                    typeof(ScaledModelStrategy),
                    typeof(GenericModelStrategy),
                }, Strategies(form).Select(s => s.GetType()).ToArray());
            });
        }

        /// <summary>模板图目录按 RunIndex 区分：不赋值时 4 个匹配工具的模板图会写进同一个目录互相覆盖。</summary>
        [TestMethod]
        public void Constructor_AssignsDistinctRunIndex()
        {
            Run(form =>
            {
                var strategies = Strategies(form);
                for (int i = 0; i < strategies.Count; i++)
                    Assert.AreEqual(i, strategies[i].RunIndex, strategies[i].Name);
            });
        }

        /// <summary>不再写死开发机上的测试目录。</summary>
        [TestMethod]
        public void Constructor_DoesNotPresetImageFolder()
        {
            Run(form => Assert.AreEqual(string.Empty, ((FileImageStrategy)Strategies(form)[0]).inPara.ImageFolder));
        }

        [TestMethod]
        public void ToolButtons_SwitchToMatchingStrategy_AndSyncParaForm()
        {
            Run(form =>
            {
                var para = Priv.Get<ParaForm>(form, "_formPara");

                for (int i = 0; i < 8; i++)
                {
                    Priv.Click(form, "button" + (i + 1) + "_Click");

                    Assert.AreEqual(i, Priv.Get<int>(form, "_index"), $"button{i + 1}");
                    Assert.AreEqual(i, Priv.Get<int>(para, "_index"), $"button{i + 1}: ParaForm 的索引必须跟宿主一致");
                    Assert.AreSame(Strategies(form), Priv.Get<List<IParaStrategy>>(para, "_strategys"));
                }
            });
        }

        [TestMethod]
        public void SwitchStrategy_ClearsPreviousBindings()
        {
            Run(form =>
            {
                var bindings = Priv.Get<Dictionary<string, VsControlModel>>(form, "_vsControls");

                Priv.Click(form, "button3_Click");
                var previous = bindings.Values.ToList();
                Assert.IsTrue(previous.Count > 0, "前提：形状匹配的参数页应建立控件绑定");

                Priv.Click(form, "button4_Click");

                Assert.IsFalse(previous.Any(bindings.ContainsValue), "切换工具前必须清掉上一个工具的控件绑定");
            });
        }

        /// <summary>
        /// 绘制进行中切换工具：提示用户，宿主与参数页都停在原工具上。
        /// </summary>
        [TestMethod]
        public void SwitchStrategy_WhileDrawing_PromptsAndStaysOnCurrentTool()
        {
            Run(form =>
            {
                var para = Priv.Get<ParaForm>(form, "_formPara");
                Priv.Click(form, "button3_Click");

                using (var prompts = new PromptLog())
                {
                    Priv.Set(para, "_drawBusy", true);
                    try { Priv.Click(form, "button5_Click"); }
                    finally { Priv.Set(para, "_drawBusy", false); }

                    Assert.AreEqual(1, prompts.Messages.Count);
                    StringAssert.Contains(prompts.Messages[0], "正在绘制");
                }
                Assert.AreEqual(2, Priv.Get<int>(form, "_index"));
                Assert.AreEqual(2, Priv.Get<int>(para, "_index"));
            });
        }

        [TestMethod]
        public void Run_Failure_PromptsReason()
        {
            Run(form =>
            {
                Strategies(form)[0] = new FakeStrategy(AlgoEnum.FileImage) { RunError = new InvalidOperationException("运行失败原因") };

                using (var prompts = new PromptLog())
                {
                    Priv.Click(form, "but_Run_Click");

                    CollectionAssert.AreEqual(new[] { "运行失败原因" }, prompts.Messages);
                }
            });
        }

        [TestMethod]
        public void Run_ExecutesCurrentStrategyWithAllStrategies()
        {
            Run(form =>
            {
                var fake = new FakeStrategy(AlgoEnum.CreateROI);
                Strategies(form)[1] = fake;
                Priv.Click(form, "button2_Click");

                using (var prompts = new PromptLog())
                {
                    fake.RunError = new InvalidOperationException("被执行了");
                    Priv.Click(form, "but_Run_Click");

                    CollectionAssert.AreEqual(new[] { "被执行了" }, prompts.Messages, "运行按钮必须执行当前选中的工具");
                }
            });
        }

        /// <summary>
        /// 主窗体销毁时释放持有 HALCON 句柄的策略。
        /// </summary>
        [TestMethod]
        public void Dispose_DisposesStrategies()
        {
            Sta.Run(() =>
            {
                List<IParaStrategy> strategies;
                using (var form = new MainForm())
                {
                    WindowHost.ShowOffscreen(form);
                    strategies = Strategies(form).ToList();
                }

                var disposables = strategies.OfType<IDisposable>().ToList();
                Assert.IsTrue(disposables.Count > 0, "前提：至少有一个策略持有需要释放的资源");
                foreach (var s in disposables)
                    Assert.IsTrue(Priv.Get<bool>(s, "_disposed"), s.GetType().Name + " 没被释放");
            });
        }

        /// <summary>某个策略释放失败时，其余策略照样释放，窗体销毁不被打断。</summary>
        [TestMethod]
        public void Dispose_OneStrategyThrows_OthersStillDisposed()
        {
            Sta.Run(() =>
            {
                List<IParaStrategy> strategies;
                using (var form = new MainForm())
                {
                    WindowHost.ShowOffscreen(form);
                    Strategies(form).Insert(0, new ThrowOnDisposeStrategy());
                    strategies = Strategies(form).ToList();
                }

                Assert.IsTrue(((ThrowOnDisposeStrategy)strategies[0]).DisposeCalled);
                foreach (var s in strategies.Skip(1).OfType<IDisposable>())
                    Assert.IsTrue(Priv.Get<bool>(s, "_disposed"), s.GetType().Name + " 没被释放");
            });
        }

        private sealed class ThrowOnDisposeStrategy : IParaStrategy, IDisposable
        {
            public bool DisposeCalled;

            public void Dispose()
            {
                DisposeCalled = true;
                throw new InvalidOperationException("释放失败");
            }

            public AlgoEnum Algorithm => AlgoEnum.Undefined;
            public string Name { get; set; }
            public int RunIndex { get; set; }
            public void Init(IRoiHost host) { }
            public void Close(IRoiHost host) { }
            public bool Fun_action(IHDisplay display, List<IParaStrategy> strategys) => true;
            public bool Fun_action(HalconDotNet.HObject ho_Image, IHDisplay display) => true;
            public object ResolveOutput(string[] path) => null;
            public T ResolveOutput<T>(string[] path) => default(T);
            public bool TryResolveOutput<T>(string[] path, out T value) { value = default(T); return false; }
        }
    }
}
