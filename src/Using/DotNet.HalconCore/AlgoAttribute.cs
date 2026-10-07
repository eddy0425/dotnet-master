using System;
using System.Reflection;
using DotNet.Drawing;

namespace DotNet.HalconCore
{
    /// <summary>
    /// 把一个策略类登记为算法：宿主启动时由算法目录（<c>DotNet.VisionRuntime.AlgoCatalog</c>）扫描带本特性的类型，自动生成工具箱。
    /// </summary>
    /// <remarks>
    /// 取代原来的 <c>AlgoEnum</c> 与宿主里手写的 <c>new</c>。新增一个算法只需要写一个类并打上本特性，
    /// 宿主（包括 Designer）不需要任何改动。
    /// <para>
    /// <see cref="Key"/> 是算法的稳定身份，写进方案文件，与类名、命名空间无关；<b>一经发布不再修改</b>。
    /// 约定写成 <c>分组.算法</c>，小写、连字符，例如 <c>fit.arc-midpoint</c>。
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class AlgoAttribute : Attribute
    {
        public AlgoAttribute(string key, string displayName)
        {
            Key = key;
            DisplayName = displayName;
        }

        /// <summary> 稳定身份，写进方案文件 </summary>
        public string Key { get; }

        /// <summary> 工具箱里显示的名字，也是新建工具的默认名 </summary>
        public string DisplayName { get; }

        /// <summary> 工具箱分组 </summary>
        public string Group { get; set; } = "其它";

        /// <summary>
        /// 排序，小的在前。工具箱分组按组内最小值的先后排列，宿主的默认流程也按它排 ——
        /// 内置算法按 图像(10..) → 定位(100..) → 区域(200..) → 测量(300..) 分段取值。
        /// </summary>
        public int Order { get; set; }

        /// <summary> 新建 ROI 的默认形状；只对实现了 <see cref="IRoiEditable"/> 的策略有意义 </summary>
        public RectEnum DefaultRoi { get; set; } = RectEnum.Rectangle;

        /// <summary>
        /// 参数类的版本，写进方案文件；从 1 开始。参数类改名、改单位、拆字段、改语义时加一，
        /// 并覆盖 <c>ParaStrategyBase.MigratePara</c> 把旧版本的 JSON 改成新形状。
        /// 只是新增有默认值的字段、改容器类型（数组 ↔ List）时 JSON 形状不变，不必加。
        /// </summary>
        public int ParaVersion { get; set; } = 1;

        /// <summary> 读取类型上的特性；没有标注时返回 null </summary>
        public static AlgoAttribute Of(Type type)
        {
            return type == null ? null : type.GetCustomAttribute<AlgoAttribute>(false);
        }
    }

    /// <summary>
    /// 一个已登记算法的元数据（<see cref="AlgoAttribute"/> 的只读快照 + 实现类型）。
    /// </summary>
    public sealed class AlgoInfo
    {
        private AlgoInfo(AlgoAttribute attribute, Type type)
        {
            Key = attribute.Key;
            DisplayName = attribute.DisplayName;
            Group = string.IsNullOrWhiteSpace(attribute.Group) ? "其它" : attribute.Group;
            Order = attribute.Order;
            DefaultRoi = attribute.DefaultRoi;
            ParaVersion = attribute.ParaVersion;
            Type = type;
        }

        public string Key { get; }
        public string DisplayName { get; }
        public string Group { get; }
        public int Order { get; }
        public RectEnum DefaultRoi { get; }

        /// <summary> 参数类的当前版本（见 <see cref="AlgoAttribute.ParaVersion"/>） </summary>
        public int ParaVersion { get; }

        public Type Type { get; }

        /// <summary> 由特性与实现类型生成元数据；宿主扫描算法目录时用 </summary>
        public static AlgoInfo From(AlgoAttribute attribute, Type type)
        {
            if (attribute == null) throw new ArgumentNullException(nameof(attribute));
            if (type == null) throw new ArgumentNullException(nameof(type));
            return new AlgoInfo(attribute, type);
        }

        /// <summary> 按策略实例取元数据；类型上没有 <see cref="AlgoAttribute"/> 时返回 null </summary>
        public static AlgoInfo Of(object strategy)
        {
            var type = strategy?.GetType();
            var attribute = AlgoAttribute.Of(type);
            return attribute == null ? null : new AlgoInfo(attribute, type);
        }

        public override string ToString() => $"{Key} ({DisplayName}, {Type.FullName})";
    }
}
