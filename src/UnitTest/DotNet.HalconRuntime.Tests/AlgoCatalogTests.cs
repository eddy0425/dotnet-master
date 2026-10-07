using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DotNet.HalconAlgo;
using DotNet.HalconAlgo.Tests;
using DotNet.HalconCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconRuntime.Tests
{
    /// <summary>
    /// 插件契约：目录扫描、启动校验、稳定键、外置插件。
    /// </summary>
    [TestClass]
    public class AlgoCatalogTests : HalconTestBase
    {
        private static readonly Assembly BuiltIn = typeof(FileImageStrategy).Assembly;
        private string _dir;

        [TestInitialize]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "DotNet.HalconRuntime.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TestCleanup]
        public void TearDown()
        {
            try { Directory.Delete(_dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        /// <summary> 样例插件的编译输出（只引用 Drawing + HalconCore） </summary>
        internal static string SamplePluginDll()
        {
            string config = Path.GetFileName(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'));
            string path = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "DotNet.SamplePlugin", "bin", config, "DotNet.SamplePlugin.dll"));
            Assert.IsTrue(File.Exists(path), "前提：样例插件已编译 " + path);
            return path;
        }

        [TestMethod]
        public void BuiltIn_AllElevenAlgorithms_WithStableKeys()
        {
            var catalog = AlgoCatalog.Load(new[] { BuiltIn });

            CollectionAssert.AreEquivalent(new[]
            {
                "image.file", "image.rotate", "image.line-rotate",
                "region.create-roi", "region.merge",
                "fit.line", "fit.arc-midpoint",
                "match.shape", "match.ncc", "match.scaled", "match.generic",
            }, catalog.Algorithms.Select(a => a.Key).ToArray());
            Assert.AreEqual(typeof(FitArcMidpointStrategy), catalog.Find("fit.arc-midpoint").Type);
            Assert.AreEqual("测量", catalog.Find("fit.arc-midpoint").Group);
            Assert.IsNull(catalog.Find("不存在"));
        }

        [TestMethod]
        public void Create_SetsDisplayNameAndFreshId()
        {
            var catalog = AlgoCatalog.Load(new[] { BuiltIn });

            using (var a = catalog.Create("fit.line"))
            using (var b = catalog.Create("fit.line"))
            {
                Assert.IsInstanceOfType(a, typeof(FitLineStrategy));
                Assert.AreEqual("拟合直线", a.Name);
                Assert.AreNotEqual(Guid.Empty, a.Id);
                Assert.AreNotEqual(a.Id, b.Id, "每个实例一个 Id");
            }
            Assert.ThrowsException<KeyNotFoundException>(() => catalog.Create("不存在"));
        }

        /// <summary> 校验失败一次列出全部问题，而不是修一个报一个 </summary>
        [TestMethod]
        public void Load_InvalidTypes_ReportsAllProblems()
        {
            var ex = Assert.ThrowsException<AlgoCatalogException>(() => AlgoCatalog.Load(new[] { typeof(BadAlgos.Duplicate1).Assembly }));

            string all = string.Join("\n", ex.Problems);
            StringAssert.Contains(all, "bad.dup");
            StringAssert.Contains(all, "重复");
            StringAssert.Contains(all, nameof(BadAlgos.AbstractAlgo));
            StringAssert.Contains(all, nameof(BadAlgos.NoDefaultCtor));
            StringAssert.Contains(all, nameof(BadAlgos.NotAStrategy));
            StringAssert.Contains(all, "键为空");
            Assert.IsTrue(ex.Problems.Count >= 5);
        }

        [TestMethod]
        public void Plugin_LoadedFromPluginDir_CreatesAndRuns()
        {
            File.Copy(SamplePluginDll(), Path.Combine(_dir, "DotNet.SamplePlugin.dll"));

            var catalog = AlgoCatalog.Load(new[] { BuiltIn }, _dir);

            var info = catalog.Find("sample.region-area");
            Assert.IsNotNull(info, "插件目录里的算法出现在目录里");
            Assert.AreEqual("区域面积", info.DisplayName);
            Assert.AreEqual(12, catalog.Algorithms.Count);

            using (var region = Rectangle1(0, 0, 9, 9))
            using (var tool = catalog.Create("sample.region-area"))
            {
                var upstream = new StubStrategy("上游").Region("区域", () => region);
                var source = (SourceParam)((IParaBinding)tool).DescribeParams().Single(p => p.Label == "区域来源");
                Assert.IsTrue(source.TrySetValue(upstream.Ref("区域")));

                var result = tool.Run(new RunContext(null, new IParaStrategy[] { upstream }), null);

                Assert.IsTrue(result.IsOk, result.Message);
                Assert.AreEqual(100.0, tool.FindOutput("面积").GetValue());
            }
        }

        /// <summary> 在插件目录下建一个子目录插件，放进样例插件（可改名）与额外文件 </summary>
        private string SubdirPlugin(string folder, string entryName = "DotNet.SamplePlugin.dll")
        {
            string dir = Path.Combine(_dir, folder);
            Directory.CreateDirectory(dir);
            File.Copy(SamplePluginDll(), Path.Combine(dir, entryName));
            return dir;
        }

        private static PluginLoadResult Plugin(AlgoCatalog catalog, string name)
            => catalog.Report.Plugins.Single(p => p.Name == name);

        /// <summary> 根目录下的共享程序集副本只拒绝它自己，不再连累整个插件目录 </summary>
        [TestMethod]
        public void Plugin_SharedCopyInRoot_RejectedAlone_OthersStillLoad()
        {
            // 契约程序集是 Core（插件引用的那一个），不是 Runtime
            File.Copy(typeof(IParaStrategy).Assembly.Location, Path.Combine(_dir, "DotNet.HalconCore.dll"));
            File.Copy(typeof(AlgoCatalog).Assembly.Location, Path.Combine(_dir, "DotNet.HalconRuntime.dll"));
            SubdirPlugin("DotNet.SamplePlugin");

            var catalog = AlgoCatalog.Load(new[] { BuiltIn }, _dir);

            Assert.IsNotNull(catalog.Find("sample.region-area"), "好插件照常加载");
            Assert.AreEqual(12, catalog.Algorithms.Count);
            StringAssert.Contains(string.Join(";", Plugin(catalog, "DotNet.HalconCore").Problems), "副本");
            StringAssert.Contains(string.Join(";", Plugin(catalog, "DotNet.HalconRuntime").Problems), "DotNet.HalconRuntime.dll");
            Assert.AreEqual(2, catalog.Report.Rejected.Count());
            Assert.IsFalse(catalog.Report.AllLoaded);
        }

        /// <summary> 子目录里带了共享程序集副本：只拒绝这一个插件 </summary>
        [TestMethod]
        public void Plugin_SharedCopyInSubdir_RejectsThatPluginOnly()
        {
            string dir = SubdirPlugin("DotNet.SamplePlugin");
            File.Copy(typeof(IParaStrategy).Assembly.Location, Path.Combine(dir, "DotNet.HalconCore.dll"));

            var catalog = AlgoCatalog.Load(new[] { BuiltIn }, _dir);

            var plugin = Plugin(catalog, "DotNet.SamplePlugin");
            Assert.AreEqual(PluginStatus.Rejected, plugin.Status);
            StringAssert.Contains(plugin.Problems.Single(), "DotNet.HalconCore.dll");
            Assert.AreEqual(0, plugin.Algorithms.Count);
            Assert.AreEqual(11, catalog.Algorithms.Count, "内置算法不受影响");
        }

        [TestMethod]
        public void Plugin_NotADotNetAssembly_RejectedAlone()
        {
            File.WriteAllBytes(Path.Combine(_dir, "native.dll"), new byte[] { 0x4D, 0x5A, 0, 0 });
            SubdirPlugin("DotNet.SamplePlugin");

            var catalog = AlgoCatalog.Load(new[] { BuiltIn }, _dir);

            StringAssert.Contains(Plugin(catalog, "native").Problems.Single(), "native.dll");
            Assert.IsNotNull(catalog.Find("sample.region-area"));
        }

        /// <summary> 子目录插件：入口与目录同名；目录里其余 dll 是私有依赖，不扫描（原生 dll 不会被报成坏插件） </summary>
        [TestMethod]
        public void Plugin_Subdir_EntryByFolderName_PrivateDependenciesNotScanned()
        {
            string dir = SubdirPlugin("DotNet.SamplePlugin");
            File.WriteAllBytes(Path.Combine(dir, "native_dep.dll"), new byte[] { 0x4D, 0x5A, 0, 0 });

            var catalog = AlgoCatalog.Load(new[] { BuiltIn }, _dir);

            var plugin = Plugin(catalog, "DotNet.SamplePlugin");
            Assert.AreEqual(PluginStatus.Loaded, plugin.Status, string.Join(";", plugin.Problems));
            Assert.AreEqual("sample.region-area", plugin.Algorithms.Single().Key);
            Assert.AreEqual(1, catalog.Report.Plugins.Count, "私有依赖不是候选插件");
            Assert.IsTrue(catalog.Report.AllLoaded);
        }

        [TestMethod]
        public void Plugin_Subdir_EntryFromManifest()
        {
            string dir = SubdirPlugin("样例");
            File.WriteAllText(Path.Combine(dir, AlgoCatalog.ManifestFileName), "{ \"entry\": \"DotNet.SamplePlugin.dll\", \"version\": \"1.0\" }");

            var catalog = AlgoCatalog.Load(new[] { BuiltIn }, _dir);

            Assert.AreEqual(PluginStatus.Loaded, Plugin(catalog, "样例").Status);
            Assert.IsNotNull(catalog.Find("sample.region-area"));
        }

        [DataTestMethod]
        [DataRow(null, "找不到入口程序集")]
        [DataRow("{ 坏了", "plugin.json 无效")]
        [DataRow("{ \"entry\": \"..\\\\x.dll\" }", "本目录下的文件名")]
        [DataRow("{ }", "entry")]
        public void Plugin_Subdir_BadLayout_Rejected(string manifest, string expected)
        {
            string dir = SubdirPlugin("样例", "其它名字.dll");
            if (manifest != null) File.WriteAllText(Path.Combine(dir, AlgoCatalog.ManifestFileName), manifest);

            var catalog = AlgoCatalog.Load(new[] { BuiltIn }, _dir);

            StringAssert.Contains(Plugin(catalog, "样例").Problems.Single(), expected);
            Assert.AreEqual(11, catalog.Algorithms.Count);
        }

        /// <summary> 键与已加载的算法重复：后来的整个插件被拒绝，先来的不受影响 </summary>
        [TestMethod]
        public void Plugin_DuplicateKey_LaterPluginRejected()
        {
            SubdirPlugin("A");
            File.WriteAllText(Path.Combine(_dir, "A", AlgoCatalog.ManifestFileName), "{ \"entry\": \"DotNet.SamplePlugin.dll\" }");
            SubdirPlugin("B");
            File.WriteAllText(Path.Combine(_dir, "B", AlgoCatalog.ManifestFileName), "{ \"entry\": \"DotNet.SamplePlugin.dll\" }");

            var catalog = AlgoCatalog.Load(new[] { BuiltIn }, _dir);

            Assert.AreEqual(PluginStatus.Loaded, Plugin(catalog, "A").Status);
            StringAssert.Contains(Plugin(catalog, "B").Problems.Single(), "重复");
            Assert.AreEqual(12, catalog.Algorithms.Count);
        }

        [TestMethod]
        public void Plugin_MissingDir_OnlyBuiltIn()
        {
            var catalog = AlgoCatalog.Load(new[] { BuiltIn }, Path.Combine(_dir, "none"));
            Assert.AreEqual(11, catalog.Algorithms.Count);
        }

        [TestMethod]
        public void ContractMajorVersion_ReadsCoreAssembly()
        {
            Assert.AreEqual(typeof(IParaStrategy).Assembly.GetName().Version.Major, AlgoCatalog.ContractMajorVersion);
        }
    }
}

namespace DotNet.HalconRuntime.Tests.BadAlgos
{
    // 只在 Load_InvalidTypes_ReportsAllProblems 里被显式扫描: 目录不扫整个 AppDomain, 平时不会被登记

    public sealed class BadPara : DisplayOptions { }

    public abstract class BadBase : ParaStrategyBase<BadPara>
    {
        protected override void DeclareParams(ParamBuilder p) { }
        protected override void DeclareOutputs(OutputBuilder o) { }
        protected override void ResetOutputs() { }
        protected override RunResult Execute(RunContext context) => RunResult.Ok();
    }

    [Algo("bad.dup", "重复1")]
    public sealed class Duplicate1 : BadBase { }

    [Algo("bad.dup", "重复2")]
    public sealed class Duplicate2 : BadBase { }

    [Algo("bad.abstract", "抽象")]
    public abstract class AbstractAlgo : BadBase { }

    [Algo("bad.ctor", "无参构造缺失")]
    public sealed class NoDefaultCtor : BadBase
    {
        public NoDefaultCtor(int x) { }
    }

    [Algo("bad.not-strategy", "不是策略")]
    public sealed class NotAStrategy { }

    [Algo(" ", "空键")]
    public sealed class EmptyKey : BadBase { }
}
