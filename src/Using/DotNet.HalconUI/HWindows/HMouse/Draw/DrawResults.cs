using HalconDotNet;

namespace DotNet.HalconUI.Draw
{
    /// <summary>
    /// 交互绘制的返回值。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 原来的 <c>Draw*</c> 用一串 <c>out HTuple</c> 输出几何，而 <c>async</c> 方法不能有 <c>out</c> 参数，
    /// 所以每种图元各配一个只读结构体。字段一律是 <see cref="double"/>：HALCON 的 <see cref="HTuple"/>
    /// 有从 double 的隐式转换，调用点写法不变。
    /// </para>
    /// <para>
    /// <b><see cref="DrawPointResult.Completed"/> 的意义</b>：false 表示用户没有右键确认
    /// (取消 / 超时 / 被新的绘制顶掉)，此时几何字段是图元的中间状态，<b>不应</b>写回配置。
    /// 旧的阻塞实现不区分这一点，新建 ROI 时取消会把 ROI 写成全零，这次一并修掉。
    /// </para>
    /// </remarks>
    public readonly struct DrawPointResult
    {
        internal DrawPointResult(bool completed, double row, double column)
        {
            Completed = completed; Row = row; Column = column;
        }

        /// <summary>用户是否右键确认。false 时几何字段不可信，调用方应原样保留旧值。</summary>
        public bool Completed { get; }

        public double Row { get; }
        public double Column { get; }
    }

    /// <summary>线段 ROI 的绘制结果。语义见 <see cref="DrawPointResult"/>。</summary>
    public readonly struct DrawLineResult
    {
        internal DrawLineResult(bool completed, double row1, double column1, double row2, double column2)
        {
            Completed = completed;
            Row1 = row1; Column1 = column1; Row2 = row2; Column2 = column2;
        }

        public bool Completed { get; }

        public double Row1 { get; }
        public double Column1 { get; }
        public double Row2 { get; }
        public double Column2 { get; }
    }

    /// <summary>轴对齐矩形 ROI 的绘制结果，已归一化为左上/右下。语义见 <see cref="DrawPointResult"/>。</summary>
    public readonly struct DrawRectangle1Result
    {
        internal DrawRectangle1Result(bool completed, double row1, double column1, double row2, double column2)
        {
            Completed = completed;
            Row1 = row1; Column1 = column1; Row2 = row2; Column2 = column2;
        }

        public bool Completed { get; }

        public double Row1 { get; }
        public double Column1 { get; }
        public double Row2 { get; }
        public double Column2 { get; }
    }

    /// <summary>可旋转矩形 ROI 的绘制结果。<see cref="Phi"/> 为弧度。语义见 <see cref="DrawPointResult"/>。</summary>
    public readonly struct DrawRectangle2Result
    {
        internal DrawRectangle2Result(bool completed,
            double row, double column, double phi, double length1, double length2)
        {
            Completed = completed;
            Row = row; Column = column; Phi = phi; Length1 = length1; Length2 = length2;
        }

        public bool Completed { get; }

        public double Row { get; }
        public double Column { get; }

        /// <summary>主轴方向，弧度。</summary>
        public double Phi { get; }

        /// <summary>主轴半长。</summary>
        public double Length1 { get; }

        /// <summary>副轴半长。</summary>
        public double Length2 { get; }
    }

    /// <summary>圆 ROI 的绘制结果。语义见 <see cref="DrawPointResult"/>。</summary>
    public readonly struct DrawCircleResult
    {
        internal DrawCircleResult(bool completed, double row, double column, double radius)
        {
            Completed = completed; Row = row; Column = column; Radius = radius;
        }

        public bool Completed { get; }

        public double Row { get; }
        public double Column { get; }
        public double Radius { get; }
    }

    /// <summary>
    /// 椭圆 ROI 的绘制结果。已按 HALCON 约定归一化：
    /// <see cref="Radius1"/> 恒 &gt;= <see cref="Radius2"/>，<see cref="Phi"/> 为长轴方向(弧度)。
    /// </summary>
    public readonly struct DrawEllipseResult
    {
        internal DrawEllipseResult(bool completed,
            double row, double column, double phi, double radius1, double radius2)
        {
            Completed = completed;
            Row = row; Column = column; Phi = phi; Radius1 = radius1; Radius2 = radius2;
        }

        public bool Completed { get; }

        public double Row { get; }
        public double Column { get; }

        /// <summary>长轴方向，弧度。</summary>
        public double Phi { get; }

        /// <summary>长半轴。</summary>
        public double Radius1 { get; }

        /// <summary>短半轴。</summary>
        public double Radius2 { get; }
    }

    /// <summary>
    /// 多边形 ROI 的绘制结果。
    /// </summary>
    /// <remarks>
    /// <b>所有权</b>：<see cref="Region"/> 归调用方，用完必须 <c>Dispose</c>。
    /// 即使 <see cref="Completed"/> 为 false，<c>DrawHelper</c> 返回的也是一个合法的空区域而非 null。
    /// 但本类型是 <c>struct</c>，<c>default(DrawRegionResult)</c> 的 <see cref="Region"/> 仍是 null，
    /// 编译期挡不住，所以调用方释放时请一律用 <c>?.Dispose()</c>。
    /// </remarks>
    public readonly struct DrawRegionResult
    {
        internal DrawRegionResult(bool completed, HObject region)
        {
            Completed = completed; Region = region;
        }

        public bool Completed { get; }

        /// <summary>绘制出的区域，所有权归调用方。未确认时为空区域。</summary>
        public HObject Region { get; }
    }
}
