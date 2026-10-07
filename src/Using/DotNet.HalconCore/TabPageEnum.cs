using System;

namespace DotNet.HalconCore
{
    /// <summary>
    /// 参数页的推荐名字。页名是字符串（<see cref="ParamBuilder.Page"/>），插件可以声明自己的页，不用改宿主；
    /// 这里只是内置页的名字，同一页请一律用常量，免得多一个空格就拆出一个新页签。
    /// </summary>
    public static class Pages
    {
        /// <summary> 基本参数（默认页） </summary>
        public const string Parameter = "基本参数";

        /// <summary> 区域设置：实现了 <see cref="IRoiEditable"/> 的工具，宿主在这一页放 ROI 编辑器 </summary>
        public const string Region = "区域设置";

        /// <summary> 模板设置：实现了 <see cref="ITemplateEditable"/> 的工具，宿主在这一页放模板编辑器 </summary>
        public const string Template = "模版设置";

        /// <summary> 显示输出：显示开关、文本坐标 / 字号（基类自动追加到这一页） </summary>
        public const string Display = "显示输出";
    }

    /// <summary> 旧的固定页签；只为兼容 <see cref="ParamBuilder.Tab"/> 保留 </summary>
    [Obsolete("页签改为字符串: 用 ParamBuilder.Page(string) 与 Pages 常量")]
    public enum TabPageEnum
    {
        Parameter,
        Region,
        Display,
    }
}
