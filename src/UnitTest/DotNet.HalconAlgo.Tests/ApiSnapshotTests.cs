using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DotNet.HalconCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconAlgo.Tests
{
    /// <summary>
    /// 契约（DotNet.HalconCore）公开 API 快照：插件看得见的每一个类型与成员都在基线文件里。
    /// </summary>
    /// <remarks>
    /// 任何公开面变化（加、删、改签名、改默认值、改枚举值）都会让本测试失败，必须显式更新基线：
    /// 失败时实际结果写在基线旁边的 <c>*.received.txt</c>，确认无误后覆盖基线并一起提交。
    /// 破坏性变化（删除 / 改签名）还要同时升 Core 的主版本号。
    /// </remarks>
    [TestClass]
    public class ApiSnapshotTests
    {
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        [TestMethod]
        public void HalconCore_PublicApi_MatchesBaseline()
        {
            string baseline = BaselinePath("DotNet.HalconCore.api.txt");
            string received = Path.ChangeExtension(baseline, ".received.txt");
            string actual = Describe(typeof(IParaStrategy).Assembly);

            string expected = File.Exists(baseline) ? File.ReadAllText(baseline, Encoding.UTF8).Replace("\r\n", "\n") : null;
            if (expected == actual)
            {
                if (File.Exists(received)) File.Delete(received);
                return;
            }

            File.WriteAllText(received, actual, new UTF8Encoding(false));
            if (expected == null) Assert.Fail($"基线不存在，已生成 {received}；确认后改名为 {Path.GetFileName(baseline)} 并提交");

            var expectedLines = expected.Split('\n');
            var actualLines = actual.Split('\n');
            var removed = expectedLines.Except(actualLines).Take(20).Select(l => "  - " + l);
            var added = actualLines.Except(expectedLines).Take(20).Select(l => "  + " + l);
            Assert.Fail($"契约公开 API 与基线不一致（实际结果: {received}）:\n" + string.Join("\n", removed.Concat(added)));
        }

        /// <summary> 基线放在源码目录，而不是输出目录：更新基线就是改一个受版本控制的文件 </summary>
        private static string BaselinePath(string name)
        {
            string dir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Baseline"));
            Assert.IsTrue(Directory.Exists(dir), "前提：基线目录存在 " + dir);
            return Path.Combine(dir, name);
        }

        internal static string Describe(Assembly assembly)
        {
            var sb = new StringBuilder();
            foreach (var type in assembly.GetExportedTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                sb.Append(TypeHeader(type)).Append('\n');
                foreach (var member in Members(type).OrderBy(m => m, StringComparer.Ordinal))
                    sb.Append("    ").Append(member).Append('\n');
            }
            return sb.ToString();
        }

        private static string TypeHeader(Type type)
        {
            string kind = type.IsEnum ? "enum" : type.IsInterface ? "interface" : type.IsValueType ? "struct"
                : typeof(Delegate).IsAssignableFrom(type) ? "delegate" : "class";
            string modifiers = kind != "class" ? string.Empty
                : type.IsAbstract && type.IsSealed ? "static "
                : type.IsAbstract ? "abstract "
                : type.IsSealed ? "sealed " : string.Empty;

            var bases = new List<string>();
            if (kind == "class" && type.BaseType != null && type.BaseType != typeof(object)) bases.Add(Name(type.BaseType));
            if (type.IsEnum) bases.Add(Name(Enum.GetUnderlyingType(type)));
            if (!type.IsEnum)
            {
                var inherited = new HashSet<Type>((type.BaseType?.GetInterfaces() ?? Type.EmptyTypes)
                    .Concat(type.GetInterfaces().SelectMany(i => i.GetInterfaces())));
                bases.AddRange(type.GetInterfaces().Where(i => !inherited.Contains(i)).Select(Name).OrderBy(n => n, StringComparer.Ordinal));
            }
            return $"{modifiers}{kind} {Name(type)}" + (bases.Count == 0 ? string.Empty : " : " + string.Join(", ", bases));
        }

        private static IEnumerable<string> Members(Type type)
        {
            if (type.IsEnum)
            {
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
                    yield return $"{field.Name} = {Convert.ToInt64(field.GetRawConstantValue())}";
                yield break;
            }

            foreach (var ctor in type.GetConstructors(Declared).Where(c => Visible(c)))
                yield return $"{Access(ctor)}.ctor({Parameters(ctor)})";

            foreach (var method in type.GetMethods(Declared).Where(m => Visible(m) && (!m.IsSpecialName || m.Name.StartsWith("op_", StringComparison.Ordinal))))
            {
                string generic = method.IsGenericMethodDefinition ? "<" + string.Join(", ", method.GetGenericArguments().Select(Name)) + ">" : string.Empty;
                yield return $"{Access(method)}{Modifiers(method)}{Name(method.ReturnType)} {method.Name}{generic}({Parameters(method)})";
            }

            foreach (var prop in type.GetProperties(Declared))
            {
                var accessors = new[] { prop.GetMethod, prop.SetMethod }.Where(a => a != null && Visible(a)).ToList();
                if (accessors.Count == 0) continue;
                var index = prop.GetIndexParameters();
                string name = index.Length == 0 ? prop.Name : $"this[{string.Join(", ", index.Select(p => Name(p.ParameterType) + " " + p.Name))}]";
                var first = accessors[0];
                string parts = string.Join(" ", accessors.Select(a => (Access(a) == Access(first) ? string.Empty : Access(a)) + (a == prop.GetMethod ? "get;" : "set;")));
                yield return $"{Access(first)}{Modifiers(first)}{Name(prop.PropertyType)} {name} {{ {parts} }}";
            }

            foreach (var field in type.GetFields(Declared).Where(f => f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly))
            {
                string access = field.IsPublic ? "public " : "protected ";
                string modifiers = field.IsLiteral ? "const " : (field.IsStatic ? "static " : string.Empty) + (field.IsInitOnly ? "readonly " : string.Empty);
                string value = field.IsLiteral ? " = " + Literal(field.GetRawConstantValue()) : string.Empty;
                yield return $"{access}{modifiers}{Name(field.FieldType)} {field.Name}{value}";
            }

            foreach (var evt in type.GetEvents(Declared).Where(e => e.AddMethod != null && Visible(e.AddMethod)))
                yield return $"{Access(evt.AddMethod)}event {Name(evt.EventHandlerType)} {evt.Name}";

            foreach (var nested in type.GetNestedTypes(BindingFlags.Public))
                yield return "nested " + Name(nested);
        }

        private static bool Visible(MethodBase m) => m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly;

        private static string Access(MethodBase m) => m.IsPublic ? "public " : "protected ";

        private static string Modifiers(MethodInfo m)
        {
            if (m.IsStatic) return "static ";
            if (m.DeclaringType.IsInterface) return string.Empty;
            if (m.IsAbstract) return "abstract ";
            if (m.GetBaseDefinition() != m) return m.IsFinal ? "sealed override " : "override ";
            if (m.IsVirtual && !m.IsFinal) return "virtual ";
            return string.Empty;
        }

        private static string Parameters(MethodBase m) => string.Join(", ", m.GetParameters().Select(p =>
        {
            string prefix = p.ParameterType.IsByRef ? (p.IsOut ? "out " : "ref ") : p.IsDefined(typeof(ParamArrayAttribute), false) ? "params " : string.Empty;
            string type = Name(p.ParameterType.IsByRef ? p.ParameterType.GetElementType() : p.ParameterType);
            string value = p.HasDefaultValue ? " = " + Literal(p.DefaultValue) : string.Empty;
            return $"{prefix}{type} {p.Name}{value}";
        }));

        private static string Literal(object value)
        {
            switch (value)
            {
                case null: return "null";
                case string s: return "\"" + s + "\"";
                case bool b: return b ? "true" : "false";
                case IFormattable f: return f.ToString(null, System.Globalization.CultureInfo.InvariantCulture);
                default: return value.ToString();
            }
        }

        /// <summary> C# 风格的类型名：带命名空间、泛型实参、嵌套类型；与反射内部名（`1、+）无关 </summary>
        private static string Name(Type type)
        {
            if (type.IsGenericParameter) return type.Name;
            if (type.IsArray) return Name(type.GetElementType()) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
            if (type.IsByRef) return Name(type.GetElementType()) + "&";
            if (type == typeof(void)) return "void";

            string name = type.Name;
            int tick = name.IndexOf('`');
            if (tick >= 0) name = name.Substring(0, tick);
            string prefix = type.IsNested ? Name(type.DeclaringType) + "." : string.IsNullOrEmpty(type.Namespace) ? string.Empty : type.Namespace + ".";

            if (!type.IsGenericType) return prefix + name;
            var args = type.GetGenericArguments();
            // 嵌套在泛型类型里时，外层的泛型实参已经由 DeclaringType 写过
            int own = type.IsNested ? args.Length - type.DeclaringType.GetGenericArguments().Length : args.Length;
            if (own <= 0) return prefix + name;
            return prefix + name + "<" + string.Join(", ", args.Skip(args.Length - own).Select(Name)) + ">";
        }
    }
}
