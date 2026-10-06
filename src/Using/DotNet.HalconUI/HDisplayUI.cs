using DotNet.Drawing;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Threading.Tasks;
using DotNet.HalconCore;


namespace DotNet.HalconUI
{
    /// <summary>
    /// 承载 HALCON 显示窗口的用户控件。
    /// </summary>
    /// <remarks>
    /// 本类不再实现 <see cref="IHDisplay"/>：原先它把 46 个 <c>Disp*</c> 重载
    /// 逐个转发给内部的 <c>HDisplay</c>，一个绘制方法要在接口、HDisplay、HDisplayUI
    /// 三处各写一遍签名，改一次动三处。现在改为组合——绘制走 <see cref="Display"/>，
    /// 本类只保留「控件才有」的职责：鼠标交互模式、刷新通知、按钮状态。
    /// </remarks>
    public partial class HDisplayUI : UserControl, IRoiHost
    {
        readonly HDisplay display;
        readonly HWindowMouse mouse;

        /// <summary> 绘制接口。所有 <c>Disp*</c> 调用都经由它，不再由本控件转发。 </summary>
        public IHDisplay Display => display;
        public event HMouseEventHandler HMouseUp { add => hWindowControl.HMouseUp += value; remove => hWindowControl.HMouseUp -= value; }
        public event HMouseEventHandler HMouseMove { add => hWindowControl.HMouseMove += value; remove => hWindowControl.HMouseMove -= value; }
        public event HMouseEventHandler HMouseDown { add => hWindowControl.HMouseDown += value; remove => hWindowControl.HMouseDown -= value; }
        public event HMouseEventHandler HMouseWheel { add => hWindowControl.HMouseWheel += value; remove => hWindowControl.HMouseWheel -= value; }

        public delegate void ShowDelegate();
        /// <summary>
        /// 图像刷新完成后触发。
        /// </summary>
        /// <remarks>
        /// 原为公开委托字段：外部可以直接整体覆盖（丢掉别人的订阅），也可以替本控件触发。
        /// 改成 event 后外部只能 += / -=，触发权保留在本类内部。
        /// </remarks>
        public event ShowDelegate OnShow;
        public event EventHandler<DrawModelUIArgs> DrawDoneEvent;
        public void DrawDone(string modelPath, HObject ho_ModeRect, HObject ho_Contour, ModelResult result)
        {
            DrawDoneEvent?.Invoke(this, new DrawModelUIArgs(modelPath, ho_ModeRect, ho_Contour, result));
        }

        #region 属性

        // 宽/高/尺寸/中心/图像/颜色/字号等一律经 Display 访问，本控件不再镜像一份。
        public HWindow HoWindow => hWindowControl.HalconWindow;  //窗体句柄

        #endregion

        private DrawEnum _drawType = DrawEnum.None;

        /// <summary>
        /// 当前鼠标交互模式。
        /// </summary>
        /// <remarks>原为公开可变字段，改成属性以便后续加入校验/通知，外部读写语义不变。</remarks>
        public DrawEnum DrawType
        {
            get { return _drawType; }
            set { _drawType = value; }
        }

        // 在构造函数里创建: 需要 InitializeComponent() 之后才拿得到 hWindowControl.HalconWindow,
        // 而字段初始化器先于构造函数体执行。
        readonly NoneMouse dispNone;
        readonly DispRectMouse dispRect = new DispRectMouse();
        readonly DispModelMouse dispModel = new DispModelMouse();

        public HDisplayUI()
        {
            InitializeComponent();
            this.Dock = DockStyle.Fill;

            // 直接组合 HDisplay + HWindowMouse：原先夹在中间的 HDisplayCore 除了这两个字段的
            // 转发外没有任何行为，却让每个新增的绘制方法都要改三处签名。
            display = new HDisplay(hWindowControl);
            mouse = new HWindowMouse(hWindowControl, display);
            mouse.RefreshUI += Display_RefreshUI;

            // 绑定本控件的窗口: 绘制会话按窗口对象注册, 多个 HDisplayUI 并存时事件不会串台
            dispNone = new NoneMouse(hWindowControl.HalconWindow);

            HMouseDown  += OnMouseDown;
            HMouseUp    += OnMouseUp;
            HMouseWheel += OnMouseWheel;
            HMouseMove  += OnMouseMove;
            // 资源释放走 Dispose(bool)（见 HDisplayUI.Designer.cs），不要用 HandleDestroyed：
            // 更换 Parent / 修改 Dock / TabPage 切换等场景会重建句柄并触发 HandleDestroyed，
            // 此时控件并未销毁，提前释放 display 会让显示功能永久失效。
        }

        private void Display_RefreshUI(HTuple Row, HTuple Column, HTuple egray)
        {
            lbl_result.Text = string.Format("坐标[X:{0} Y:{1}]  灰度:{2} ", Column.D.ToString("F2"), Row.D.ToString("F2"), egray.D.ToString());
        }

        private void btn_ReSetPart_Click(object sender, EventArgs e)
        {
            display.Adaptive = !display.Adaptive;
        }

        private void but_IsCross_Click(object sender, EventArgs e)
        {
            display.IsCross = !display.IsCross;
        }

        public void Reset()
        {
            hWindowControl.Focus();
        }

        #region Mouse Events

        /// <summary>
        /// 按当前模式取出应处理鼠标事件的处理器；返回 null 表示本控件不处理该模式。
        /// </summary>
        /// <remarks>
        /// 原来四个事件各写一份三分支 switch，既重复又漏掉了 <see cref="DrawEnum"/> 的另外两个取值，
        /// 且没有 default 兜底——枚举将来再加成员时会静默无响应。这里收敛成一处解析：
        /// <para>
        /// Erase 的处理器不由 HDisplayUI 持有：它由 <c>HEditModelUI</c> 自行订阅
        /// HMouseXxx 事件驱动 <c>EraseRectMouse</c>。
        /// 因此这里显式返回 null，保持“本控件不插手”的既有行为，而不是回落到 <c>dispNone</c>
        /// ——后者会把事件转发给本窗口当前的 <c>DrawHelper</c> 绘制会话，属于行为变更。
        /// </para>
        /// </remarks>
        private IMouseHandler ResolveMouseHandler()
        {
            switch (_drawType)
            {
                case DrawEnum.None:
                    return dispNone;
                case DrawEnum.DispRect:
                    return dispRect;
                case DrawEnum.DispModel:
                    return dispModel;
                case DrawEnum.Erase:
                    return null;
                default:
                    Log.Warn(nameof(HDisplayUI), $"未处理的鼠标交互模式: {_drawType}，本次鼠标事件被忽略。");
                    return null;
            }
        }

        private void OnMouseDown(object sender, HMouseEventArgs e)
        {
            ReDispImage();
            ResolveMouseHandler()?.OnMouseDown(e);
        }

        private void OnMouseUp(object sender, HMouseEventArgs e)
        {
            ReDispImage();
            ResolveMouseHandler()?.OnMouseUp(e);
        }

        private void OnMouseWheel(object sender, HMouseEventArgs e)
        {
            ReDispImage();
            ResolveMouseHandler()?.OnMouseWheel(e);
        }

        private void OnMouseMove(object sender, HMouseEventArgs e)
        {
            // 移动事件不做整幅重绘：拖拽期间每个像素都刷一次图像会明显掉帧，
            // 由各 handler 自行决定要不要重绘（DispRectMouse / EraseRectMouse 都是只重画自己的区域）。
            ResolveMouseHandler()?.OnMouseMove(e);
        }

        /// <summary>
        /// 控件是否正在释放显示资源。
        /// </summary>
        /// <remarks>
        /// 不能用 <see cref="System.Windows.Forms.Control.Disposing"/> 代替：
        /// Designer 里 <c>Dispose(bool)</c> 是先调 <see cref="ReleaseDisplayResources"/>、
        /// 后调 <c>base.Dispose(disposing)</c>，而 <c>STATE_DISPOSING</c> 是在后者里才置位的 ——
        /// 被 <c>CancelDraw</c> 内联唤醒的续体正好跑在这个窗口期里，那时 <c>Disposing</c> 还是 false。
        /// 调用方(如 <c>ParaForm</c> 的绘制 handler)靠它判断"现在不能弹模态框"。
        /// </remarks>
        public bool IsReleasing { get; private set; }

        /// <summary>
        /// 释放显示相关的托管资源，由 <see cref="Dispose(bool)"/> 的 disposing 分支调用。
        /// </summary>
        private void ReleaseDisplayResources()
        {
            IsReleasing = true;

            // 0) 先解绑自身订阅：一是确保后续即便 display 内部触发事件也不再回到当前实例；
            //    二是下一步的 CancelDraw 会**就地内联**跑完调用方 await 之后的整段代码
            //    （见 DrawSession.Finish —— TrySetResult 在当前调用栈上直接执行续体），
            //    续体里的 SetRectPara / SetModelPara 会回头重新配置 dispRect / dispModel 并改 DrawType。
            //    先摘掉订阅，这些配置就影响不到已销毁控件的事件派发了。
            HMouseDown -= OnMouseDown;
            HMouseUp -= OnMouseUp;
            HMouseWheel -= OnMouseWheel;
            HMouseMove -= OnMouseMove;

            // 1) 再终止挂起的绘制会话：否则它会一直持有本控件的 HWindow
            //    直到 5 分钟超时，续体恢复时还会去操作已销毁的窗口。
            try
            {
                // 必须先判空再调用: CancelDraw(null) 在 DrawSession.CancelAll 里是"取消所有窗口",
                // 拿不到自己的窗口就误伤了其他 HDisplayUI 上正在进行的绘制。宁可不取消。
                var window = hWindowControl?.HalconWindow;
                if (window != null) DrawHelper.CancelDraw(window);
                else Log.Warn(nameof(HDisplayUI), "释放时取不到 HalconWindow, 跳过取消绘制会话.");
            }
            catch (Exception ex) { Log.Warn(nameof(HDisplayUI), "取消绘制会话失败.", ex); }

            // 2) 先释放鼠标交互（解除它对 hWindowControl 的事件订阅），再释放 HDisplay 持有的 HObject。
            //    两者的 Dispose 自身都具备幂等性；这里不把字段置 null，
            //    避免外部在控件销毁后仍访问属性时引发 NullReferenceException——
            //    Disposed 后属性会通过各自内部的 _disposed 保护返回安全默认值。
            try { mouse?.Dispose(); }
            catch (Exception ex) { Log.Error(nameof(HDisplayUI), "释放鼠标交互资源失败.", ex); }

            try { display?.Dispose(); }
            catch (Exception ex) { Log.Error(nameof(HDisplayUI), "释放显示资源失败.", ex); }
        }

        public void SetNonePara()
        {
            Reset();
            ReDispImage();
            DrawType = DrawEnum.None;
        }

        /// <summary>
        /// ROI 被交给本控件显示 / 编辑之后触发（<see cref="SetRectPara"/>）。
        /// 宿主据此刷新自己的几何读数，策略不再碰这些控件。
        /// </summary>
        public event EventHandler<CvRegion> RoiShown;

        public void SetRectPara(CvRegion shrRegion)
        {
            Reset();
            ReDispImage();
            DrawType = DrawEnum.DispRect;
            dispRect.SetUp(Display, shrRegion);
            RoiShown?.Invoke(this, shrRegion);
        }

        public void SetModelPara(HObject shrFindMode, HObject shrContour, CvCoord shrCoord)
        {
            Reset();
            ReDispImage();
            DrawType = DrawEnum.DispModel;
            dispModel.SetUp(Display, shrFindMode, shrContour, shrCoord);
        }

        /// <summary>
        /// 模板区域句柄被换掉（旧的已释放）时同步 dispModel 的缓存；与 <see cref="SetModelPara"/> 不同，不重绘也不改 DrawType。
        /// </summary>
        internal void UpdateModelFindMode(HObject shrFindMode)
        {
            dispModel.UpdateFindMode(shrFindMode);
        }

        #endregion

        #region DispImage

        // 这三个方法之所以留在控件上而没有交给 Display：DispImage 需要在刷新后触发 OnShow，
        // ReDispImage 是鼠标事件处理的内部依赖。其余图像操作（SetImage / ClearWinDisp）
        // 是纯转发，已删除，请改用 Display。

        /// <summary> 重新显示图片 </summary>
        public void ReDispImage()
        {
            display?.ReDispImage();
        }

        /// <summary> 显示图片 </summary>
        public void DispImage(HObject image)
        {
            display?.DispImage(image);
            OnShow?.Invoke();
        }

        /// <summary> 显示图片 </summary>
        public void DispImage(HObject image, bool isSetPart)
        {
            display?.DispImage(image, isSetPart);
            OnShow?.Invoke();
        }

        #endregion

        #region Draw Region

        // 三个入口都只是"先把控件自身的交互状态清干净，再把活交给 display"。
        // 绘制要等用户在画面上确认，所以全部返回 Task —— 调用方必须在 UI 线程 await，
        // 绝不能 .Wait()/.Result：等待期间 UI 线程要继续泵消息才能收到鼠标事件，阻塞即死锁。
        //
        // 两个 CvRegion 重载的返回值必须一路透传到算法层: 取消 / 超时时 HDisplay 不回写几何,
        // 调用方若不看返回值就继续"按新 ROI 重建模板", 会拿旧几何做出一份不是用户想要的模板。

        /// <summary>
        /// 绘制（创建）橡皮筋区域
        /// </summary>
        /// <returns>
        /// 用户右键确认返回 true；取消 / 超时 / 绘制失败返回 false，此时 <paramref name="hRegion"/> 未被改动。
        /// </returns>
        public Task<bool> DrawRegionAsync(CvRegion hRegion)
        {
            DrawType = DrawEnum.None;
            Reset();
            return display.DrawRegionAsync(hRegion);
        }

        /// <summary>
        /// 绘制（修改）橡皮筋区域
        /// </summary>
        /// <returns>语义同 <see cref="DrawRegionAsync(CvRegion)"/>。</returns>
        public Task<bool> DrawRegionModAsync(CvRegion hRegion)
        {
            DrawType = DrawEnum.None;
            Reset();
            return display.DrawRegionModAsync(hRegion);
        }

        /// <summary> 绘制区域；返回的对象所有权归调用方 </summary>
        /// <returns>
        /// 取消 / 超时返回<b>空对象元组</b>（<c>count_obj == 0</c>，不是 null，也不是空区域），
        /// 调用方必须先用 <c>CountObj</c> 判空再喂给 <c>Union2</c> / <c>Difference</c>。
        /// </returns>
        public Task<HObject> DrawRegionAsync(RectEnum type)
        {
            DrawType = DrawEnum.None;
            Reset();
            return display.DrawRegionAsync(type);
        }

        #endregion

    }
}
