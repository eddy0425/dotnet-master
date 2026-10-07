using System;
using System.IO;
using System.Linq;
using System.Text;
using DotNet.Drawing;
using DotNet.HalconAlgo;
using DotNet.HalconAlgo.Tests;
using DotNet.HalconCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace DotNet.VisionRuntime.Tests
{
    /// <summary>
    /// 端到端：外置插件放进 <c>plugins\&lt;名称&gt;\</c> → 建工具 → 存方案 → 重新加载；参数版本迁移；插件缺失 / 版本更新时原样保留。
    /// </summary>
    /// <remarks>
    /// 样例插件的参数类是 2 版：1 版把最小面积存成 <c>Min</c>，2 版改名为 <c>MinArea</c>（见 RegionAreaStrategy.MigratePara）。
    /// </remarks>
    [TestClass]
    public class PluginSchemeTests : HalconTestBase
    {
        private const string Key = "sample.region-area";
        private string _root;
        private string _plugins;
        private string _scheme;

        [TestInitialize]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "DotNet.VisionRuntime.Tests", Guid.NewGuid().ToString("N"));
            _plugins = Path.Combine(_root, "plugins");
            _scheme = Path.Combine(_root, "方案");
            string dir = Path.Combine(_plugins, "DotNet.SamplePlugin");
            Directory.CreateDirectory(dir);
            File.Copy(AlgoCatalogTests.SamplePluginDll(), Path.Combine(dir, "DotNet.SamplePlugin.dll"));
        }

        [TestCleanup]
        public void TearDown()
        {
            try { Directory.Delete(_root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        private AlgoCatalog Catalog(bool withPlugin = true)
            => AlgoCatalog.Load(new[] { typeof(FileImageStrategy).Assembly }, withPlugin ? _plugins : null);

        private JObject SchemeJson() => JObject.Parse(File.ReadAllText(Path.Combine(_scheme, FlowScheme.FileName), Encoding.UTF8));

        private static JObject Record(JObject scheme, string key) => scheme["Tools"].Cast<JObject>().Single(t => t.Value<string>("AlgoKey") == key);

        /// <summary> 手写一份方案：一个创建ROI 工具 + 一个插件工具，插件工具的记录由调用方给 </summary>
        private void WriteScheme(JObject pluginRecord)
        {
            Directory.CreateDirectory(_scheme);
            var root = new JObject { ["Version"] = FlowScheme.FormatVersion, ["Tools"] = new JArray(pluginRecord) };
            File.WriteAllText(Path.Combine(_scheme, FlowScheme.FileName), root.ToString(), new UTF8Encoding(false));
        }

        private static JObject PluginRecord(Guid id, int? paraVersion, JObject para)
        {
            var record = new JObject { ["AlgoKey"] = Key, ["Id"] = id, ["Name"] = "面积" };
            if (paraVersion.HasValue) record["ParaVersion"] = paraVersion.Value;
            record["Para"] = para;
            // 经一次文本往返: Id 等值的 JSON 类型与读回来的一致, 才能逐项比较
            return JObject.Parse(record.ToString());
        }

        private static void DisposeAll(System.Collections.Generic.IEnumerable<IParaStrategy> tools)
        {
            foreach (var tool in tools) tool.Dispose();
        }

        /// <summary> 子目录插件：保存 → 重新加载，参数（含跨工具引用）一致，方案里记下参数版本 </summary>
        [TestMethod]
        public void SubdirPlugin_SaveReload_ParamsRoundTrip()
        {
            var catalog = Catalog();
            Assert.IsTrue(catalog.Report.AllLoaded, string.Join("; ", catalog.Report.Plugins));
            var roi = (CreateROIStrategy)catalog.Create("region.create-roi");
            var area = catalog.Create(Key);
            var binding = (IParaBinding)area;
            Assert.IsTrue(binding.DescribeParams().Single(p => p.Label == "区域来源").TrySetValue(SourceRef.To(roi, "区域")));
            Assert.IsTrue(binding.DescribeParams().Single(p => p.Label == "最小面积").TrySetValue(50));
            var tools = new IParaStrategy[] { roi, area };
            try
            {
                FlowScheme.Save(_scheme, tools);
                Assert.AreEqual(2, Record(SchemeJson(), Key).Value<int>("ParaVersion"));

                var loaded = FlowScheme.Load(_scheme, Catalog());
                try
                {
                    var area2 = loaded[1];
                    Assert.AreEqual(area.Id, area2.Id);
                    var params2 = ((IParaBinding)area2).DescribeParams();
                    Assert.AreEqual(50, params2.Single(p => p.Label == "最小面积").GetValue());
                    Assert.AreEqual(SourceRef.To(roi, "区域"), params2.Single(p => p.Label == "区域来源").GetValue());
                    Assert.AreEqual(0, new FlowRunner(loaded).Validate().Count, "引用按 Id 还原");
                }
                finally { DisposeAll(loaded); }
            }
            finally { DisposeAll(tools); }
        }

        /// <summary> 1 版方案（没有存版本、字段叫 Min）：读入时迁移，不会被静默读成默认值；再存就是 2 版 </summary>
        [TestMethod]
        public void OldParaVersion_MigratedOnLoad_SavedAsCurrent()
        {
            var id = Guid.NewGuid();
            WriteScheme(PluginRecord(id, null, new JObject { ["Min"] = 77 }));

            var loaded = FlowScheme.Load(_scheme, Catalog());
            try
            {
                Assert.IsNotInstanceOfType(loaded[0], typeof(MissingTool));
                Assert.AreEqual(77, ((IParaBinding)loaded[0]).DescribeParams().Single(p => p.Label == "最小面积").GetValue());

                FlowScheme.Save(_scheme, loaded);
                var record = Record(SchemeJson(), Key);
                Assert.AreEqual(2, record.Value<int>("ParaVersion"));
                Assert.AreEqual(77, record["Para"].Value<int>("MinArea"));
                Assert.IsNull(record["Para"]["Min"]);
            }
            finally { DisposeAll(loaded); }
        }

        /// <summary> 迁移后仍读不出来：作为占位工具保留原始配置（迁移在副本上做，原文不被改动） </summary>
        [TestMethod]
        public void MigrationFails_KeptAsMissingTool_WithOriginalRecord()
        {
            var original = PluginRecord(Guid.NewGuid(), 1, new JObject { ["Min"] = "不是数" });
            WriteScheme(original);

            var loaded = FlowScheme.Load(_scheme, Catalog());
            try
            {
                var missing = (MissingTool)loaded.Single();
                StringAssert.Contains(missing.Problem, "参数读取失败");
                FlowScheme.Save(_scheme, loaded);
                Assert.IsTrue(JToken.DeepEquals(original, Record(SchemeJson(), Key)), "原样写回: " + Record(SchemeJson(), Key));
            }
            finally { DisposeAll(loaded); }
        }

        /// <summary> 方案由更新的程序保存（参数版本更高）：不尝试读取，占位保留原文，免得旧程序读坏后又写回 </summary>
        [TestMethod]
        public void NewerParaVersion_KeptAsMissingTool_WrittenBackUnchanged()
        {
            var original = PluginRecord(Guid.NewGuid(), 3, new JObject { ["MinArea"] = 5, ["未来的字段"] = "x" });
            WriteScheme(original);

            var loaded = FlowScheme.Load(_scheme, Catalog());
            try
            {
                var missing = (MissingTool)loaded.Single();
                StringAssert.Contains(missing.Problem, "高于插件支持的 2");
                FlowScheme.Save(_scheme, loaded);
                Assert.IsTrue(JToken.DeepEquals(original, Record(SchemeJson(), Key)));
            }
            finally { DisposeAll(loaded); }
        }

        /// <summary> 删掉插件后重新加载：引用它的工具变成占位工具，再次保存时原样写回，其余工具不受影响 </summary>
        [TestMethod]
        public void PluginRemoved_ReloadKeepsRecord_SaveWritesItBack()
        {
            var catalog = Catalog();
            var roi = catalog.Create("region.create-roi");
            var area = catalog.Create(Key);
            ((IParaBinding)area).DescribeParams().Single(p => p.Label == "最小面积").TrySetValue(9);
            var tools = new IParaStrategy[] { roi, area };
            JObject saved;
            try
            {
                FlowScheme.Save(_scheme, tools);
                saved = (JObject)Record(SchemeJson(), Key).DeepClone();
            }
            finally { DisposeAll(tools); }

            var loaded = FlowScheme.Load(_scheme, Catalog(withPlugin: false));
            try
            {
                Assert.IsInstanceOfType(loaded[0], typeof(CreateROIStrategy));
                var missing = (MissingTool)loaded[1];
                StringAssert.Contains(missing.Problem, "插件缺失");
                Assert.AreEqual(area.Id, missing.Id);

                FlowScheme.Save(_scheme, loaded);
                Assert.IsTrue(JToken.DeepEquals(saved, Record(SchemeJson(), Key)), "占位工具原样写回");
            }
            finally { DisposeAll(loaded); }

            var back = FlowScheme.Load(_scheme, Catalog());
            try
            {
                Assert.AreEqual(9, ((IParaBinding)back[1]).DescribeParams().Single(p => p.Label == "最小面积").GetValue(), "插件装回来后配置还在");
            }
            finally { DisposeAll(back); }
        }

        [TestMethod]
        public void Catalog_ExposesParaVersion_BuiltInsDefaultToOne()
        {
            var catalog = Catalog();
            Assert.AreEqual(2, catalog.Find(Key).ParaVersion);
            Assert.IsTrue(catalog.Algorithms.Where(a => a.Key != Key).All(a => a.ParaVersion == 1));
        }
    }
}
