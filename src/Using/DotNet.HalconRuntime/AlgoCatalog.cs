using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DotNet.Drawing;
using DotNet.HalconCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DotNet.HalconRuntime
{
    /// <summary> 一个插件的加载结果 </summary>
    public enum PluginStatus
    {
        Loaded,
        Rejected,
    }

    /// <summary> 一个插件（<c>plugins\</c> 下的一个子目录，或根目录下的一个 dll）的加载结果 </summary>
    public sealed class PluginLoadResult
    {
        internal PluginLoadResult(string name, string path, IReadOnlyList<string> problems, IReadOnlyList<AlgoInfo> algorithms)
        {
            Name = name;
            Path = path;
            Problems = problems;
            Algorithms = algorithms;
        }

        /// <summary> 插件名：子目录名，根目录下的旧式插件取 dll 文件名 </summary>
        public string Name { get; }

        /// <summary> 入口程序集的路径；找不到入口时为 null </summary>
        public string Path { get; }

        public PluginStatus Status => Problems.Count == 0 ? PluginStatus.Loaded : PluginStatus.Rejected;

        /// <summary> 被拒绝的原因（全部列出）；加载成功时为空 </summary>
        public IReadOnlyList<string> Problems { get; }

        /// <summary> 带来的算法；被拒绝时为空 </summary>
        public IReadOnlyList<AlgoInfo> Algorithms { get; }

        public override string ToString()
            => Status == PluginStatus.Loaded ? $"{Name}: 已加载 {Algorithms.Count} 个算法" : $"{Name}: 未加载 ({string.Join("; ", Problems)})";
    }

    /// <summary> 插件目录的加载报告：每个插件一项，宿主在信息窗口里显示 </summary>
    public sealed class CatalogLoadReport
    {
        internal CatalogLoadReport(IReadOnlyList<PluginLoadResult> plugins) => Plugins = plugins;

        public IReadOnlyList<PluginLoadResult> Plugins { get; }

        public IEnumerable<PluginLoadResult> Rejected => Plugins.Where(p => p.Status == PluginStatus.Rejected);

        public bool AllLoaded => Plugins.All(p => p.Status == PluginStatus.Loaded);
    }

    /// <summary>
    /// 算法目录：启动时扫描内置程序集与 <c>plugins\</c> 目录，收集所有带 <see cref="AlgoAttribute"/> 的策略类。
    /// </summary>
    /// <remarks>
    /// 这是加载期插件：发现、校验、按稳定键创建实例。不做热卸载、不做进程隔离 —— 换插件要重启。
    /// <para>
    /// 只扫描显式传入的程序集和插件目录，不扫描整个 AppDomain，避免把测试替身等无关类型注册进来。
    /// 内置程序集有问题属于程序错误：抛出 <see cref="AlgoCatalogException"/>，一次列出<b>全部</b>问题。
    /// 插件逐个隔离：一个插件有问题只拒绝它自己（整个插件，不会只加载一半），原因记进 <see cref="Report"/>，
    /// 方案里引用它的工具照常变成占位工具。
    /// </para>
    /// <para>
    /// 插件目录布局：<c>plugins\&lt;名称&gt;\</c> 一个子目录一个插件，入口程序集是 <c>&lt;名称&gt;.dll</c>，
    /// 或由子目录里的 <c>plugin.json</c>（<c>{ "entry": "xxx.dll" }</c>）指定；子目录里其余 dll 是私有依赖，不扫描，
    /// 由 <c>Assembly.LoadFrom</c> 自带的同目录探测解析。根目录下的 dll 仍按旧规则各算一个插件。
    /// 已知限制：两个插件带了同名、版本不同的未强签名依赖时，先加载的那个生效。
    /// </para>
    /// </remarks>
    public sealed class AlgoCatalog
    {
        /// <summary> 插件不得自带副本的程序集：它们必须与宿主共用同一份 </summary>
        internal static readonly string[] SharedAssemblies = { "halcondotnet", "DotNet.HalconCore", "DotNet.Drawing", "DotNet.HalconRuntime", "DotNet.HalconKit", "Newtonsoft.Json" };

        /// <summary> 契约程序集（DotNet.HalconCore），插件编译时引用的就是它 </summary>
        private static readonly AssemblyName Contract = typeof(IParaStrategy).Assembly.GetName();

        private readonly List<AlgoInfo> _algorithms;
        private readonly Dictionary<string, AlgoInfo> _byKey;

        /// <summary> 子目录插件可选的清单文件 </summary>
        public const string ManifestFileName = "plugin.json";

        private AlgoCatalog(List<AlgoInfo> algorithms, CatalogLoadReport report)
        {
            _algorithms = algorithms;
            _byKey = algorithms.ToDictionary(a => a.Key, StringComparer.Ordinal);
            Report = report;
        }

        /// <summary> 插件目录的加载报告；没有插件目录时为空报告 </summary>
        public CatalogLoadReport Report { get; }

        /// <summary> 全部算法，按 <see cref="AlgoInfo.Order"/>、显示名排序 </summary>
        public IReadOnlyList<AlgoInfo> Algorithms => _algorithms;

        /// <summary> 契约（DotNet.HalconCore，不是本程序集）的主版本号；插件引用的主版本必须与之相同 </summary>
        public static int ContractMajorVersion => Contract.Version.Major;

        /// <summary>
        /// 扫描内置程序集与插件目录。
        /// </summary>
        /// <param name="builtIn">内置算法所在的程序集（例如 HalconAlgo）。</param>
        /// <param name="pluginDir">插件目录；为 null 或不存在时只扫内置程序集。</param>
        /// <exception cref="AlgoCatalogException">内置程序集校验失败；消息里列出全部问题。插件的问题不抛出，见 <see cref="Report"/>。</exception>
        public static AlgoCatalog Load(IEnumerable<Assembly> builtIn, string pluginDir = null)
        {
            var problems = new List<string>();
            var found = new List<AlgoInfo>();
            foreach (var assembly in (builtIn ?? Enumerable.Empty<Assembly>()).Where(a => a != null).Distinct())
                found.AddRange(Scan(assembly, problems));
            foreach (var duplicate in found.GroupBy(a => a.Key, StringComparer.Ordinal).Where(g => g.Count() > 1))
                problems.Add($"算法键 '{duplicate.Key}' 重复: {string.Join(", ", duplicate.Select(a => a.Type.FullName))}");
            if (problems.Count > 0) throw new AlgoCatalogException(problems);

            var results = new List<PluginLoadResult>();
            foreach (var candidate in Discover(pluginDir))
            {
                var result = LoadPlugin(candidate, found);
                results.Add(result);
                found.AddRange(result.Algorithms);
                if (result.Status == PluginStatus.Rejected)
                    Log.Warn(nameof(AlgoCatalog), result.ToString());
            }

            return new AlgoCatalog(found
                .OrderBy(a => a.Order)
                .ThenBy(a => a.DisplayName, StringComparer.CurrentCulture)
                .ToList(), new CatalogLoadReport(results));
        }

        /// <summary> 按键查算法；找不到返回 null（例如方案里引用了已移除的插件） </summary>
        public AlgoInfo Find(string key)
        {
            if (key == null) return null;
            return _byKey.TryGetValue(key, out var info) ? info : null;
        }

        /// <summary>
        /// 新建一个工具实例：名字取 <see cref="AlgoInfo.DisplayName"/>，<see cref="IParaStrategy.Id"/> 取新 Guid。
        /// </summary>
        /// <exception cref="KeyNotFoundException">键未登记。</exception>
        public IParaStrategy Create(string key)
        {
            var info = Find(key) ?? throw new KeyNotFoundException($"未登记的算法键 '{key}'");
            var strategy = (IParaStrategy)Activator.CreateInstance(info.Type);
            strategy.Name = info.DisplayName;
            strategy.Id = Guid.NewGuid();
            return strategy;
        }

        private static bool Validate(Type type, AlgoAttribute attribute, List<string> problems)
        {
            int before = problems.Count;
            string where = type.FullName;
            if (string.IsNullOrWhiteSpace(attribute.Key))
                problems.Add($"{where}: [Algo] 的键为空");
            if (string.IsNullOrWhiteSpace(attribute.DisplayName))
                problems.Add($"{where}: [Algo] 的显示名为空");
            if (type.IsAbstract || type.IsInterface)
                problems.Add($"{where}: [Algo] 不能标在抽象类型上");
            if (!typeof(IParaStrategy).IsAssignableFrom(type))
                problems.Add($"{where}: 没有实现 {nameof(IParaStrategy)}");
            if (type.IsGenericTypeDefinition)
                problems.Add($"{where}: [Algo] 不能标在开放泛型类型上");
            if (!type.IsAbstract && type.GetConstructor(Type.EmptyTypes) == null)
                problems.Add($"{where}: 缺少公开的无参构造函数");
            if (attribute.ParaVersion < 1)
                problems.Add($"{where}: [Algo] 的参数版本必须从 1 开始");
            return problems.Count == before;
        }

        private static IEnumerable<Type> ExportedTypes(Assembly assembly, List<string> problems)
        {
            try
            {
                return assembly.GetExportedTypes();
            }
            catch (Exception ex) when (ex is ReflectionTypeLoadException || ex is FileNotFoundException || ex is FileLoadException || ex is TypeLoadException)
            {
                problems.Add($"{assembly.GetName().Name}: 读取类型失败: {ex.Message}");
                return Type.EmptyTypes;
            }
        }

        /// <summary> 一个程序集里校验通过的算法；问题追加到 <paramref name="problems"/> </summary>
        private static List<AlgoInfo> Scan(Assembly assembly, List<string> problems)
        {
            var found = new List<AlgoInfo>();
            foreach (var type in ExportedTypes(assembly, problems))
            {
                var attribute = AlgoAttribute.Of(type);
                if (attribute == null) continue;
                if (Validate(type, attribute, problems))
                    found.Add(AlgoInfo.From(attribute, type));
            }
            return found;
        }

        /// <summary> 一个候选插件：名字 + 所在目录（根目录下的旧式插件为 null）+ 入口文件（可能不存在） </summary>
        private sealed class Candidate
        {
            public string Name;
            public string Folder;
            public string Entry;
            public string Problem;
        }

        /// <summary> 按目录布局列出候选插件：先子目录、后根目录下的 dll，各自按名字排序 </summary>
        private static IEnumerable<Candidate> Discover(string pluginDir)
        {
            if (string.IsNullOrEmpty(pluginDir) || !Directory.Exists(pluginDir)) yield break;

            foreach (var folder in Directory.GetDirectories(pluginDir).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                var candidate = new Candidate { Name = Path.GetFileName(folder), Folder = folder };
                try
                {
                    candidate.Entry = Path.Combine(folder, EntryName(folder, candidate.Name));
                }
                catch (Exception ex) when (ex is JsonException || ex is IOException || ex is ArgumentException || ex is InvalidDataException)
                {
                    candidate.Problem = $"{ManifestFileName} 无效: {ex.Message}";
                }
                yield return candidate;
            }
            foreach (var file in Directory.GetFiles(pluginDir, "*.dll").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                yield return new Candidate { Name = Path.GetFileNameWithoutExtension(file), Entry = file };
        }

        /// <summary> 子目录插件的入口程序集文件名：<c>plugin.json</c> 的 <c>entry</c>，没有清单时为 <c>&lt;目录名&gt;.dll</c> </summary>
        private static string EntryName(string folder, string name)
        {
            string manifest = Path.Combine(folder, ManifestFileName);
            if (!File.Exists(manifest)) return name + ".dll";
            var entry = JObject.Parse(File.ReadAllText(manifest)).Value<string>("entry");
            if (string.IsNullOrWhiteSpace(entry)) throw new InvalidDataException("缺少 \"entry\"");
            if (Path.GetFileName(entry) != entry) throw new InvalidDataException($"entry 只能是本目录下的文件名: '{entry}'");
            return entry;
        }

        /// <summary> 加载并校验一个插件；任何问题都只拒绝这一个插件，算法一个都不收 </summary>
        private static PluginLoadResult LoadPlugin(Candidate candidate, IReadOnlyList<AlgoInfo> accepted)
        {
            var problems = new List<string>();
            var none = new AlgoInfo[0];
            Func<PluginLoadResult> reject = () => new PluginLoadResult(candidate.Name, candidate.Entry, problems, none);

            if (candidate.Problem != null)
            {
                problems.Add(candidate.Problem);
                return reject();
            }

            // 共享程序集必须与宿主共用同一份: 子目录里带了副本就拒绝这个插件; 根目录下的副本本身算一个被拒绝的项
            var files = candidate.Folder == null
                ? new[] { candidate.Entry }
                : Directory.GetFiles(candidate.Folder, "*.dll");
            foreach (var file in files)
            {
                if (SharedAssemblies.Contains(Path.GetFileNameWithoutExtension(file), StringComparer.OrdinalIgnoreCase))
                    problems.Add($"不得包含 {Path.GetFileName(file)} 的副本: 它必须与宿主共用同一份");
            }
            if (problems.Count > 0) return reject();

            if (!File.Exists(candidate.Entry))
            {
                problems.Add($"找不到入口程序集 {Path.GetFileName(candidate.Entry)}（与目录同名，或在 {ManifestFileName} 里用 entry 指定）");
                return reject();
            }

            AssemblyName name;
            try
            {
                name = AssemblyName.GetAssemblyName(candidate.Entry);
            }
            catch (Exception ex) when (ex is BadImageFormatException || ex is FileLoadException)
            {
                problems.Add($"{Path.GetFileName(candidate.Entry)} 不是有效的 .NET 程序集 ({ex.Message})");
                return reject();
            }

            Assembly assembly;
            try
            {
                assembly = Assembly.LoadFrom(candidate.Entry);
            }
            catch (Exception ex) when (ex is BadImageFormatException || ex is FileLoadException || ex is FileNotFoundException)
            {
                problems.Add($"{name.Name} 加载失败 ({ex.Message})");
                return reject();
            }

            var core = assembly.GetReferencedAssemblies()
                .FirstOrDefault(r => string.Equals(r.Name, Contract.Name, StringComparison.OrdinalIgnoreCase));
            if (core != null && core.Version.Major != ContractMajorVersion)
            {
                problems.Add($"基于契约 {core.Version.Major}.x 编译, 宿主契约为 {ContractMajorVersion}.x");
                return reject();
            }

            var found = Scan(assembly, problems);
            foreach (var info in found)
            {
                var clash = accepted.FirstOrDefault(a => string.Equals(a.Key, info.Key, StringComparison.Ordinal));
                if (clash != null) problems.Add($"算法键 '{info.Key}' 与已加载的 {clash.Type.FullName} 重复");
            }
            foreach (var duplicate in found.GroupBy(a => a.Key, StringComparer.Ordinal).Where(g => g.Count() > 1))
                problems.Add($"算法键 '{duplicate.Key}' 在插件内重复");
            if (problems.Count > 0) return reject();

            return new PluginLoadResult(candidate.Name, candidate.Entry, problems, found);
        }
    }

    /// <summary> 算法目录校验失败；<see cref="Problems"/> 列出全部问题 </summary>
    public sealed class AlgoCatalogException : Exception
    {
        public AlgoCatalogException(IReadOnlyList<string> problems)
            : base("算法目录校验失败:" + Environment.NewLine + string.Join(Environment.NewLine, problems.Select(p => "  - " + p)))
        {
            Problems = problems;
        }

        public IReadOnlyList<string> Problems { get; }
    }
}
