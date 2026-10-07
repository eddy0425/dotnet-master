using System;

namespace DotNet.Drawing
{
    /// <summary>
    /// HALCON 颜色。以 <c>set_color</c> 接受的颜色名或十六进制 RGB 字符串为唯一状态的不可变值类型。
    /// </summary>
    /// <remarks>
    /// 原先是一组 <c>public const string</c>：颜色在类型系统里就是普通字符串，
    /// 任何拼错的字面量都要等到 HALCON 运行期才报错，编译器帮不上忙。
    /// <para>
    /// 改成 <c>readonly struct</c> 后，绘制 API 的形参写成 <see cref="HColor"/>，
    /// 传错类型编译期即拒绝；同时保留与 <see cref="string"/> 的双向隐式转换，
    /// 既有的 <c>DispXxx(..., "red")</c> 调用与把颜色存成字符串的配置/序列化代码都不用改。
    /// </para>
    /// <para>
    /// 传给 HALCON 时请用 <see cref="Name"/>：C# 不做连续隐式转换，
    /// <c>HColor -&gt; string -&gt; HTuple</c> 不会自动发生。
    /// </para>
    /// </remarks>
    public readonly struct HColor : IEquatable<HColor>
    {
        private readonly string _name;

        /// <summary>用颜色名或十六进制 RGB/RGBA 字符串构造。值由 HALCON 解释，本类型不做白名单校验。</summary>
        public HColor(string name)
        {
            _name = name;
        }

        /// <summary>颜色名。未赋值的 <c>default(HColor)</c> 返回空字符串而非 null。</summary>
        public string Name => _name ?? string.Empty;

        /// <summary>是否为未指定颜色 (<c>default(HColor)</c> 或空名)。</summary>
        public bool IsEmpty => string.IsNullOrEmpty(_name);

        public static implicit operator HColor(string name) => new HColor(name);

        public static implicit operator string(HColor color) => color.Name;

        public bool Equals(HColor other) => string.Equals(Name, other.Name, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is HColor other && Equals(other);

        public override int GetHashCode() => Name.GetHashCode();

        public static bool operator ==(HColor left, HColor right) => left.Equals(right);

        public static bool operator !=(HColor left, HColor right) => !left.Equals(right);

        public override string ToString() => Name;

        // HALCON 22.11 set_color 文档明确列出的颜色保留原名称；其余预设使用
        // set_color 同样支持的 #rrggbb，避免将 CSS/X11 名称误当作 HALCON 内置颜色名。
        // https://www.mvtec.com/doc/halcon/2211/en/set_color.html
        #region Basic Colors

        /// <summary>绿色</summary>
        public static readonly HColor Green = new HColor("green");

        /// <summary>红色</summary>
        public static readonly HColor Red = new HColor("red");

        /// <summary>蓝色</summary>
        public static readonly HColor Blue = new HColor("blue");

        /// <summary>橙色</summary>
        public static readonly HColor Orange = new HColor("orange");

        /// <summary>粉色</summary>
        public static readonly HColor Pink = new HColor("pink");

        /// <summary>黄色</summary>
        public static readonly HColor Yellow = new HColor("yellow");

        /// <summary>青色</summary>
        public static readonly HColor Cyan = new HColor("cyan");

        /// <summary>品红</summary>
        public static readonly HColor Magenta = new HColor("magenta");

        /// <summary>珊瑚色</summary>
        public static readonly HColor Coral = new HColor("coral");

        #endregion

        #region Grayscale Colors

        /// <summary>黑色</summary>
        public static readonly HColor Black = new HColor("black");

        /// <summary>白色</summary>
        public static readonly HColor White = new HColor("white");

        /// <summary>灰色</summary>
        public static readonly HColor Gray = new HColor("gray");

        /// <summary>暗灰色</summary>
        public static readonly HColor DimGray = new HColor("dim gray");

        /// <summary>浅灰色</summary>
        public static readonly HColor LightGray = new HColor("light gray");

        /// <summary>深灰色</summary>
        public static readonly HColor DarkGray = new HColor("#a9a9a9");

        /// <summary>银色</summary>
        public static readonly HColor Silver = new HColor("#c0c0c0");

        #endregion

        #region Blue Variants

        /// <summary>军蓝色</summary>
        public static readonly HColor CadetBlue = new HColor("cadet blue");

        /// <summary>中灰蓝色</summary>
        public static readonly HColor MediumSlateBlue = new HColor("medium slate blue");

        /// <summary>灰蓝色</summary>
        public static readonly HColor SlateBlue = new HColor("slate blue");

        /// <summary>天蓝色</summary>
        public static readonly HColor SkyBlue = new HColor("#87ceeb");

        /// <summary>淡蓝色</summary>
        public static readonly HColor LightBlue = new HColor("#add8e6");

        /// <summary>深蓝色</summary>
        public static readonly HColor DarkBlue = new HColor("#00008b");

        /// <summary>海军蓝</summary>
        public static readonly HColor Navy = new HColor("#000080");

        /// <summary>宝蓝色</summary>
        public static readonly HColor RoyalBlue = new HColor("#4169e1");

        /// <summary>钢蓝色</summary>
        public static readonly HColor SteelBlue = new HColor("#4682b4");

        /// <summary>道奇蓝</summary>
        public static readonly HColor DodgerBlue = new HColor("#1e90ff");

        #endregion

        #region Green Variants

        /// <summary>春绿色</summary>
        public static readonly HColor SpringGreen = new HColor("spring green");

        /// <summary>暗橄榄绿</summary>
        public static readonly HColor DarkOliveGreen = new HColor("dark olive green");

        /// <summary>森林绿</summary>
        public static readonly HColor ForestGreen = new HColor("#228b22");

        /// <summary>淡绿色</summary>
        public static readonly HColor LightGreen = new HColor("#90ee90");

        /// <summary>深绿色</summary>
        public static readonly HColor DarkGreen = new HColor("#006400");

        /// <summary>草绿色</summary>
        public static readonly HColor LawnGreen = new HColor("#7cfc00");

        /// <summary>酸橙绿</summary>
        public static readonly HColor LimeGreen = new HColor("#32cd32");

        /// <summary>海绿色</summary>
        public static readonly HColor SeaGreen = new HColor("#2e8b57");

        /// <summary>橄榄色</summary>
        public static readonly HColor Olive = new HColor("#808000");

        /// <summary>青绿色</summary>
        public static readonly HColor Teal = new HColor("#008080");

        #endregion

        #region Red Variants

        /// <summary>橙红色</summary>
        public static readonly HColor OrangeRed = new HColor("orange red");

        /// <summary>深红色</summary>
        public static readonly HColor DarkRed = new HColor("#8b0000");

        /// <summary>深红</summary>
        public static readonly HColor Crimson = new HColor("#dc143c");

        /// <summary>火砖红</summary>
        public static readonly HColor Firebrick = new HColor("#b22222");

        /// <summary>印度红</summary>
        public static readonly HColor IndianRed = new HColor("#cd5c5c");

        /// <summary>褐红色</summary>
        public static readonly HColor Maroon = new HColor("#800000");

        /// <summary>番茄红</summary>
        public static readonly HColor Tomato = new HColor("#ff6347");

        #endregion

        #region Yellow/Orange Variants

        /// <summary>金色</summary>
        public static readonly HColor Gold = new HColor("#ffd700");

        /// <summary>浅黄色</summary>
        public static readonly HColor LightYellow = new HColor("#ffffe0");

        /// <summary>柠檬绸色</summary>
        public static readonly HColor LemonChiffon = new HColor("#fffacd");

        /// <summary>卡其色</summary>
        public static readonly HColor Khaki = new HColor("#f0e68c");

        /// <summary>深橙色</summary>
        public static readonly HColor DarkOrange = new HColor("#ff8c00");

        /// <summary>沙棕色</summary>
        public static readonly HColor SandyBrown = new HColor("#f4a460");

        /// <summary>桃色</summary>
        public static readonly HColor PeachPuff = new HColor("#ffdab9");

        #endregion

        #region Purple/Violet Variants

        /// <summary>紫色</summary>
        public static readonly HColor Purple = new HColor("#800080");

        /// <summary>紫罗兰</summary>
        public static readonly HColor Violet = new HColor("#ee82ee");

        /// <summary>兰花紫</summary>
        public static readonly HColor Orchid = new HColor("#da70d6");

        /// <summary>深紫色</summary>
        public static readonly HColor DarkViolet = new HColor("#9400d3");

        /// <summary>蓝紫色</summary>
        public static readonly HColor BlueViolet = new HColor("#8a2be2");

        /// <summary>靛蓝</summary>
        public static readonly HColor Indigo = new HColor("#4b0082");

        /// <summary>梅红色</summary>
        public static readonly HColor Plum = new HColor("#dda0dd");

        /// <summary>淡紫色</summary>
        public static readonly HColor Lavender = new HColor("#e6e6fa");

        #endregion

    }
}
