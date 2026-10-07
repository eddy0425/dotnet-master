using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DotNet.HalconCore
{
    /// <summary>
    /// 算法目录：启动时扫描内置程序集与 <c>plugins\</c> 目录，收集所有带 <see cref="AlgoAttribute"/> 的策略类。
    /// </summary>
    /// <remarks>
    /// 这是加载期插件：发现、校验、按稳定键创建实例。不做热卸载、不做进程隔离 —— 换插件要重启。
    /// <para>
    /// 只扫描显式传入的程序集和插件目录，不扫描整个 AppDomain，避免把测试替身等无关类型注册进来。
    /// 任何一项校验失败都会抛出 <see cref="AlgoCatalogException"/>，并一次列出<b>全部</b>问题，
    /// 而不是修一个报一个。
    /// </para>
    /// </remarks>
    public sealed class AlgoCatalog
    {
        /// <summary> 插件不得自带副本的程序集：它们必须与宿主共用同一份 </summary>
        internal static readonly string[] SharedAssemblies = { "halcondotnet", "DotNet.HalconCore", "DotNet.Drawing" };

        private readonly List<AlgoInfo> _algorithms;
        private readonly Dictionary<string, AlgoInfo> _byKey;

        private AlgoCatalog(List<AlgoInfo> algorithms)
        {
            _algorithms = algorithms;
            _byKey = algorithms.ToDictionary(a => a.Key, StringComparer.Ordinal);
        }

        /// <summary> 全部算法，按 <see cref="AlgoInfo.Order"/>、显示名排序 </summary>
        public IReadOnlyList<AlgoInfo> Algorithms => _algorithms;

        /// <summary> 契约（本程序集）的主版本号；插件引用的主版本必须与之相同 </summary>
        public static int ContractMajorVersion => typeof(AlgoCatalog).Assembly.GetName().Version.Major;

        /// <summary>
        /// 扫描内置程序集与插件目录。
        /// </summary>
        /// <param name="builtIn">内置算法所在的程序集（例如 HalconAlgo）。</param>
        /// <param name="pluginDir">插件目录；为 null 或不存在时只扫内置程序集。</param>
        /// <exception cref="AlgoCatalogException">任一校验失败；消息里列出全部问题。</exception>
        public static AlgoCatalog Load(IEnumerable<Assembly> builtIn, string pluginDir = null)
        {
            var problems = new List<string>();
            var assemblies = new List<Assembly>();
            if (builtIn != null) assemblies.AddRange(builtIn.Where(a => a != null).Distinct());
            assemblies.AddRange(LoadPlugins(pluginDir, problems));

            var found = new List<AlgoInfo>();
            foreach (var assembly in assemblies)
            {
                foreach (var type in ExportedTypes(assembly, problems))
                {
                    var attribute = AlgoAttribute.Of(type);
                    if (attribute == null) continue;
                    if (Validate(type, attribute, problems))
                        found.Add(new AlgoInfo(attribute, type));
                }
            }

            foreach (var duplicate in found.GroupBy(a => a.Key, StringComparer.Ordinal).Where(g => g.Count() > 1))
            {
                problems.Add($"算法键 '{duplicate.Key}' 重复: {string.Join(", ", duplicate.Select(a => a.Type.FullName))}");
            }

            if (problems.Count > 0) throw new AlgoCatalogException(problems);

            return new AlgoCatalog(found
                .OrderBy(a => a.Order)
                .ThenBy(a => a.DisplayName, StringComparer.CurrentCulture)
                .ToList());
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

        private static IEnumerable<Assembly> LoadPlugins(string pluginDir, List<string> problems)
        {
            var loaded = new List<Assembly>();
            if (string.IsNullOrEmpty(pluginDir) || !Directory.Exists(pluginDir)) return loaded;

            var files = Directory.GetFiles(pluginDir, "*.dll").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var file in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                if (SharedAssemblies.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                {
                    problems.Add($"插件目录不得包含 {Path.GetFileName(file)} 的副本: 它必须与宿主共用同一份");
                    continue;
                }
            }
            if (problems.Count > 0) return loaded;

            int contract = ContractMajorVersion;
            foreach (var file in files)
            {
                AssemblyName name;
                try
                {
                    name = AssemblyName.GetAssemblyName(file);
                }
                catch (Exception ex) when (ex is BadImageFormatException || ex is FileLoadException)
                {
                    problems.Add($"{Path.GetFileName(file)}: 不是有效的 .NET 程序集 ({ex.Message})");
                    continue;
                }

                Assembly assembly;
                try
                {
                    assembly = Assembly.LoadFrom(file);
                }
                catch (Exception ex) when (ex is BadImageFormatException || ex is FileLoadException || ex is FileNotFoundException)
                {
                    problems.Add($"{name.Name}: 加载失败 ({ex.Message})");
                    continue;
                }

                var core = assembly.GetReferencedAssemblies()
                    .FirstOrDefault(r => string.Equals(r.Name, typeof(AlgoCatalog).Assembly.GetName().Name, StringComparison.OrdinalIgnoreCase));
                if (core != null && core.Version.Major != contract)
                {
                    problems.Add($"{name.Name}: 基于契约 {core.Version.Major}.x 编译, 宿主契约为 {contract}.x, 拒绝加载");
                    continue;
                }
                loaded.Add(assembly);
            }
            return loaded;
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
