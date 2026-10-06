using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DotNet.Drawing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DotNet.HalconCore
{
    /// <summary>
    /// 方案的读写：一个方案是一个目录，里面是 <c>scheme.json</c>（工具列表与各自的参数）
    /// 和每个工具一个以 <see cref="IAlgoStrategy.Id"/> 命名的数据子目录（模板图、模型文件…）。
    /// </summary>
    /// <remarks>
    /// 每个工具存 <c>{ AlgoKey, Id, Name, Para }</c>。加载时按 <see cref="AlgoCatalog"/> 的稳定键创建实例；
    /// 找不到键（插件缺失）或参数读不出来时，用 <see cref="MissingTool"/> 占位并保留原始 JSON，
    /// 再次保存时原样写回 —— 不丢配置，也不打乱其余工具的顺序与引用。
    /// </remarks>
    public static class FlowScheme
    {
        public const string FileName = "scheme.json";
        public const int FormatVersion = 1;

        private static JsonSerializer Serializer() => JsonSerializer.Create(new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Include,
        });

        /// <summary>
        /// 把流程保存到 <paramref name="schemeDir"/>。各工具的数据目录随之迁到方案目录下（必要时复制文件），
        /// 保存后 <see cref="IParaStrategy.DataDir"/> 指向新位置。
        /// </summary>
        public static void Save(string schemeDir, IEnumerable<IParaStrategy> tools)
        {
            if (string.IsNullOrEmpty(schemeDir)) throw new ArgumentNullException(nameof(schemeDir));
            if (tools == null) throw new ArgumentNullException(nameof(tools));
            Directory.CreateDirectory(schemeDir);

            var serializer = Serializer();
            var records = new JArray();
            foreach (var tool in tools)
            {
                string dataDir = ToolDir(schemeDir, tool.Id);
                MoveData(tool, dataDir);

                if (tool is MissingTool missing)
                {
                    records.Add(missing.Record.DeepClone());
                    continue;
                }

                var info = AlgoInfo.Of(tool) ?? throw new InvalidOperationException($"工具 '{tool.Name}' 的类型 {tool.GetType().FullName} 没有标注 [Algo], 无法保存");
                records.Add(new JObject
                {
                    ["AlgoKey"] = info.Key,
                    ["Id"] = tool.Id,
                    ["Name"] = tool.Name,
                    ["Para"] = JToken.FromObject(tool.Para, serializer),
                });
            }

            var root = new JObject { ["Version"] = FormatVersion, ["Tools"] = records };
            string path = Path.Combine(schemeDir, FileName);
            string staged = path + ".tmp";
            File.WriteAllText(staged, root.ToString(Formatting.Indented), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(staged, path, null);
            else File.Move(staged, path);
        }

        /// <summary>
        /// 读取方案。返回的工具已设置好 Id / 名字 / 参数 / 数据目录，宿主还需逐个 <see cref="IParaStrategy.Init"/>。
        /// </summary>
        public static List<IParaStrategy> Load(string schemeDir, AlgoCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            string path = Path.Combine(schemeDir, FileName);
            var root = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
            int version = root.Value<int?>("Version") ?? 0;
            if (version > FormatVersion)
                throw new InvalidDataException($"方案格式版本 {version} 高于本程序支持的 {FormatVersion}");

            var serializer = Serializer();
            var tools = new List<IParaStrategy>();
            foreach (var record in root["Tools"] as JArray ?? new JArray())
            {
                var obj = (JObject)record;
                IParaStrategy tool = TryCreate(obj, catalog, serializer, out string problem)
                    ?? new MissingTool(obj, problem);
                tool.DataDir = ToolDir(schemeDir, tool.Id);
                tools.Add(tool);
            }
            return tools;
        }

        public static string ToolDir(string schemeDir, Guid id) => Path.Combine(schemeDir, id.ToString("N"));

        private static IParaStrategy TryCreate(JObject record, AlgoCatalog catalog, JsonSerializer serializer, out string problem)
        {
            string key = record.Value<string>("AlgoKey");
            if (catalog.Find(key) == null)
            {
                problem = $"未找到算法 '{key}'（插件缺失？）";
                return null;
            }

            IParaStrategy tool = catalog.Create(key);
            try
            {
                tool.Id = record["Id"]?.ToObject<Guid>() ?? Guid.NewGuid();
                tool.Name = record.Value<string>("Name") ?? tool.Name;
                if (record["Para"] is JObject para)
                {
                    // 先释放默认参数里的句柄 (例如空 ROI), 再换成读进来的配置
                    object loaded = para.ToObject(tool.Para.GetType(), serializer);
                    (tool.Para as IDisposable)?.Dispose();
                    tool.Para = loaded;
                }
                problem = null;
                return tool;
            }
            catch (Exception ex) when (ex is JsonException || ex is ArgumentException || ex is FormatException || ex is InvalidCastException)
            {
                tool.Dispose();
                problem = $"参数读取失败: {ex.Message}";
                Log.Warn(nameof(FlowScheme), $"工具 '{record.Value<string>("Name")}' ({key}) 参数读取失败, 以占位工具保留原始配置.", ex);
                return null;
            }
        }

        private static void MoveData(IParaStrategy tool, string target)
        {
            string from = tool.DataDir;
            if (!string.IsNullOrEmpty(from) && Directory.Exists(from) &&
                !string.Equals(Path.GetFullPath(from).TrimEnd('\\', '/'), Path.GetFullPath(target).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(target);
                foreach (var file in Directory.GetFiles(from))
                    File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            }
            tool.DataDir = target;
        }
    }

    /// <summary>
    /// 占位工具：方案里的算法找不到（插件缺失）或参数读不出来时用它代替。
    /// 原始 JSON 原样保留，保存时原样写回；执行一律失败并说明原因。
    /// </summary>
    public sealed class MissingTool : IParaStrategy
    {
        private static readonly IReadOnlyList<OutputItem> NoOutputs = new OutputItem[0];

        internal MissingTool(JObject record, string problem)
        {
            Record = (JObject)record.DeepClone();
            Problem = problem;
            AlgoKey = record.Value<string>("AlgoKey");
            Id = record["Id"]?.ToObject<Guid>() ?? Guid.NewGuid();
            Name = record.Value<string>("Name") ?? AlgoKey;
        }

        /// <summary> 原始记录（含参数），保存时写回 </summary>
        internal JObject Record { get; }

        public string AlgoKey { get; }

        /// <summary> 为什么没能创建真正的工具 </summary>
        public string Problem { get; }

        public Guid Id { get; set; }

        public string Name
        {
            get => Record.Value<string>("Name");
            set => Record["Name"] = value;
        }

        public RunResult LastResult { get; private set; }

        public object Para
        {
            get => Record["Para"];
            set => throw new NotSupportedException("占位工具的参数只读");
        }

        public string DataDir { get; set; }

        public IReadOnlyList<OutputItem> Outputs => NoOutputs;

        public OutputItem FindOutput(string path) => null;

        public RunResult Run(RunContext context, IHDisplay display)
        {
            LastResult = RunResult.Fail(Problem);
            display?.DispText($"{Name} : {Problem}", new Point2d(50, 50), DrawStyle.Of(HColor.Red));
            return LastResult;
        }

        public void Init(IRoiHost host) { }

        public void Close(IRoiHost host) { }

        public void Dispose() { }

        public override string ToString() => $"{Name} (缺失: {AlgoKey})";
    }
}
