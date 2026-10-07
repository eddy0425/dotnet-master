using DotNet.Drawing;
using HalconDotNet;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Threading.Tasks;
using DotNet.HalconCore;


namespace DotNet.HalconUI
{
    public partial class HEditModelUI : Form
    {
        HObject _srcImage;
        HObject shrErase;
        HObject shrFindMode;
        HObject _shrContour;
        CvCoord _shrCoord;

        /// <summary>
        /// 编辑窗内的交互绘制是否正在进行中。
        /// </summary>
        /// <remarks>
        /// 本窗体是<b>非模态</b>的(<c>ParaForm.but_editModel_Click</c> 用的是 <c>Show()</c>)，
        /// 且 <see cref="HEditForm_FormClosing"/> 只 Hide 不真关、实例长期复用 ——
        /// 绘制挂起期间主窗完全可交互。此时主窗若再调一次 <see cref="DisplayModel"/>，
        /// 它会把 <c>DrawType</c> 改成 <c>DispModel</c>，挂起的会话从此收不到鼠标事件
        /// (见 <c>HDisplayUI.ResolveMouseHandler</c>)，只能干等 5 分钟超时。
        /// 所以主窗开窗前必须先问这一句。只在 UI 线程读写，无需 Interlocked。
        ///
        /// <para>
        /// 这里没有 <c>ParaForm._drawEpoch</c> 那样的轮次护栏，理由相同且更强：
        /// 两个入口都先判 <see cref="_drawBusy"/>，且绘制期间 <see cref="SetRegionButtonsEnabled"/>
        /// 把相关控件一起禁用，第二条协程根本起不来，<c>finally</c> 不存在误清。
        /// 将来若新增不判 <see cref="_drawBusy"/> 的绘制入口，请一并把 epoch 机制照搬过来。
        /// </para>
        /// </remarks>
        public bool IsDrawBusy => _drawBusy;

        bool _drawBusy;

        public HDisplayUI GetDisplay() => display;

        string shrColor => CB_ApplyColor.Text;
        int shrLineWidth => CB_ApplyLineWidth.Text.ExtractNumber();
        DrawEnum _drawType { get => display.DrawType; set => display.DrawType = value; }
        EraseRectMouse eraseRect = new EraseRectMouse();

        public HEditModelUI()
        {
            InitializeComponent();

            HOperatorSet.GenEmptyObj(out _srcImage);
            HOperatorSet.GenEmptyObj(out shrErase);
            HOperatorSet.GenEmptyObj(out shrFindMode);
            HOperatorSet.GenEmptyObj(out _shrContour);

            display.HMouseDown += OnMouseDown;
            display.HMouseUp += OnMouseUp;
            display.HMouseWheel += OnMouseWheel;
            display.HMouseMove += OnMouseMove;
        }

        private void HEditForm_Load(object sender, System.EventArgs e)
        {
            CB_ModifyShape.Items.Clear();
            CB_ModifyShape.Items.Add("矩形"); CB_ModifyShape.Items.Add("仿射矩形"); CB_ModifyShape.Items.Add("圆"); CB_ModifyShape.Items.Add("椭圆"); CB_ModifyShape.Items.Add("多边型");
            CB_ModifyShape.SelectedIndex = 0;

            for (int i = 1; i < 10; i++) CB_ApplyLineWidth.Items.Add($"线宽{i}");
            for (int i = 10; i < 100; i = i + 10) CB_ApplyLineWidth.Items.Add($"线宽{i}");

            CB_ApplyLineWidth.SelectedIndex = 0;
            CB_ApplyColor.SelectedIndex = 0;
        }

        #region Events

        public void OnMouseDown(object sender, HMouseEventArgs e)
        {
            switch (_drawType)
            {
                case DrawEnum.Erase: eraseRect.OnMouseDown(e); SyncEraseResult(); break;
            }
        }

        public void OnMouseUp(object sender, HMouseEventArgs e)
        {
            switch (_drawType)
            {
                case DrawEnum.Erase: eraseRect.OnMouseUp(e); break;
            }
        }

        public void OnMouseWheel(object sender, HMouseEventArgs e)
        {
            switch (_drawType)
            {
                case DrawEnum.Erase: eraseRect.OnMouseWheel(e); break;
            }
        }

        public void OnMouseMove(object sender, HMouseEventArgs e)
        {
            switch (_drawType)
            {
                case DrawEnum.Erase:
                    {
                        eraseRect.SetPara(shrColor, shrLineWidth);
                        eraseRect.OnMouseMove(e);
                        SyncEraseResult();
                    }
                    break;
            }
        }

        /// <summary>
        /// 涂抹后把 <see cref="EraseRectMouse"/> 手里的新句柄同步回本窗体。
        /// </summary>
        /// <remarks>
        /// <c>eraseRect</c> 每涂一笔都会生成新的擦除/模板区域并释放旧的——旧的正是 <see cref="but_ApplyRegion_Click"/>
        /// 交给它的 <c>shrErase</c> / <c>shrFindMode</c>。不同步的话这两个字段从第一笔起就指向已释放的句柄，
        /// 之后「添加/删除区域」拿它做 <c>Union2</c> / <c>Difference</c>，擦除结果也随之丢失。
        /// dispModel 里缓存的也是同一个旧句柄，一并同步（与 DrawROIAsync 末尾的 SetModelPara 同理，但这里不能重绘）。
        /// </remarks>
        private void SyncEraseResult()
        {
            shrErase = eraseRect.Erase;
            shrFindMode = eraseRect.FindMode;
            display.UpdateModelFindMode(shrFindMode);
        }

        #endregion

        // DrawROIAsync 要等用户在画面上确认, 因此两个 Click 只能是 async void
        // (事件签名返回 void). 异常仍走 Application.ThreadException, 与改造前一致.
        //
        // await 期间 UI 线程继续泵消息, 按钮还能再点 —— 第二次点击会 CancelDraw 掉第一次的会话,
        // 两条 DrawROIAsync 就会并发读改同一个 shrFindMode(先 Dispose 再赋值), 造成对象被提前释放.
        // 因此绘制期间禁用两个按钮, 结束后在 finally 里恢复.
        private async void but_addRegion_Click(object sender, System.EventArgs e)
        {
            if (_drawBusy) return;
            _drawBusy = true;
            _drawType = DrawEnum.None;
            SetRegionButtonsEnabled(false);
            try { await DrawROIAsync(true); }
            finally { SetRegionButtonsEnabled(true); _drawBusy = false; }
        }

        private async void btn_deleteRegion_Click(object sender, System.EventArgs e)
        {
            if (_drawBusy) return;
            _drawBusy = true;
            _drawType = DrawEnum.None;
            SetRegionButtonsEnabled(false);
            try { await DrawROIAsync(false); }
            finally { SetRegionButtonsEnabled(true); _drawBusy = false; }
        }

        /// <summary>
        /// 绘制期间统一开关交互控件, 防止重入。
        /// </summary>
        /// <remarks>
        /// 除了"添加/删除区域"两个入口, <c>but_ApplyRegion</c> 也必须一起关:
        /// 它把 <c>DrawType</c> 改成 <see cref="DrawEnum.Erase"/>, 而 <c>HDisplayUI.ResolveMouseHandler</c>
        /// 在 Erase 分支返回 null —— 进行中的绘制会话从此收不到鼠标事件, 用户右键也确认不了,
        /// 只能干等 5 分钟超时, 这期间两个区域按钮还一直禁用着。
        /// <c>CB_ModifyShape</c> 一并关掉: 图元类型在 <c>DrawRegionAsync</c> 调用前就已取定, 中途改没有意义。
        /// </remarks>
        private void SetRegionButtonsEnabled(bool enabled)
        {
            but_addRegion.Enabled = enabled;
            btn_deleteRegion.Enabled = enabled;
            but_ApplyRegion.Enabled = enabled;
            CB_ModifyShape.Enabled = enabled;
        }

        private RectEnum GetModifyShape()
        {
            RectEnum drawForm = RectEnum.Rectangle;
            if (CB_ModifyShape.Text == "矩形") drawForm = RectEnum.Rectangle;
            else if (CB_ModifyShape.Text == "仿射矩形") drawForm = RectEnum.AffRect;
            else if (CB_ModifyShape.Text == "圆") drawForm = RectEnum.Circle;
            else if (CB_ModifyShape.Text == "椭圆") drawForm = RectEnum.Ellipse;
            else if (CB_ModifyShape.Text == "多边型") drawForm = RectEnum.Polygon;
            return drawForm;
        }
        /// <remarks>
        /// <para>
        /// 用户取消 / 超时时 <c>DrawRegionAsync</c> 返回的是<b>空对象元组</b>
        /// (<c>gen_empty_obj</c>, <c>count_obj == 0</c>), 而不是"空区域"(<c>count_obj == 1</c>)。
        /// <c>union2</c> / <c>difference</c> 只有对后者才是无操作; 喂 0 长度的元组会报错或得到空结果,
        /// 模板区域会被整片清掉。所以这里必须先 <c>CountObj</c> 判空并短路返回。
        /// </para>
        /// <para>
        /// <b>不接受 findMode 参数</b>: <c>await</c> 期间消息泵照跑, <c>shrFindMode</c> 可能已被别的
        /// 路径(重新载入模板等)换成新对象、旧的被 <c>Dispose</c>。参数会把 await 之前的旧引用一路
        /// 带到之后, 拿它做 <c>Union2</c>/<c>Difference</c> 就是在用已释放的句柄, 紧随其后的
        /// <c>Dispose</c> + 赋值还会把新对象一并丢掉。所以 await 之后一律重新读字段。
        /// </para>
        /// </remarks>
        private async Task DrawROIAsync(bool IsAdd)
        {
            HObject drawRegion = null;
            HObject regionResult = null;
            try
            {
                display.Reset();
                display.ReDispImage();
                display.Display.Disp(shrFindMode, DrawStyle.Of(HColor.Blue));
                var drawType = GetModifyShape();

                drawRegion = await display.DrawRegionAsync(drawType);

                // await 之后重新读字段, 绝不能沿用 await 之前的快照(见方法注释)
                var findMode = shrFindMode;

                // 取消 / 超时: 按进入时的颜色把原模板区域重新画回去, 不做任何集合运算。
                // 这里必须用 Blue(与上面进入时一致), 不能用成功分支的 Green ——
                // 用户什么都没改, 颜色却跳一下, 看上去像是"改成功了"。
                HOperatorSet.CountObj(drawRegion, out HTuple drawCount);
                if (drawCount.Length == 0 || drawCount.I == 0)
                {
                    display.ReDispImage();
                    display.Display.Disp(findMode, DrawStyle.Of(HColor.Blue));
                    return;
                }

                regionResult = TemplatePreview.Combine(findMode, drawRegion, IsAdd);

                findMode.Dispose();
                shrFindMode = regionResult;
                regionResult = null;

                // 旧句柄已经释放, 但 dispModel 里还缓存着它(见 DisplayModel 末尾那次 SetModelPara)。
                // 当前 _drawType 已归零、够不着那条派发路径, 可"字段换了下游缓存没换"是很脆的不变式,
                // 这里顺手同步掉, 别留悬挂引用。
                // SetModelPara 内部会 Reset() + ReDispImage() 并把 DrawType 置成 DispModel,
                // 所以紧接着改回 None; 重绘也已经由它做完, 下面直接画绿色区域即可。
                display.SetModelPara(shrFindMode, _shrContour, _shrCoord);
                _drawType = DrawEnum.None;

                display.Display.Disp(shrFindMode, DrawStyle.Of(HColor.Green));
            }
            finally
            {
                drawRegion?.Dispose();
                regionResult?.Dispose();
            }
        }

        private void but_ApplyRegion_Click(object sender, System.EventArgs e)
        {
            // Enabled 只挡鼠标点击; 快捷键 / 代码调用仍能进来, 这里再判一次真实状态。
            // 判 _drawBusy 而不是 but_addRegion.Enabled: 后者是 UI 状态, 将来若因别的原因
            // (未载入模板 / 只读模式)被禁用, 本方法就会静默变成空操作, 用户点了没反应也没提示。
            // 绘制期间切到 Erase 会让进行中的会话彻底收不到鼠标事件(见 SetRegionButtonsEnabled)。
            if (_drawBusy) return;

            eraseRect.SetUp(display.Display, shrErase, shrFindMode, shrColor, shrLineWidth);
            _drawType = DrawEnum.Erase;
        }

        /// <remarks>
        /// <para>
        /// 平移目标取<b>新读入图像</b>的中心，而不是 <c>display.Display.HoCentre</c>：后者是窗口里
        /// 当前那张图的尺寸，首次打开时还是 <see cref="ZoomImage"/> 的默认值，换了尺寸不同的模板图时
        /// 又是上一张图的——两种情况模板都会被平移到错误位置(常常在画面外)。
        /// </para>
        /// <para>
        /// 所有新对象生成成功后才换入字段：读图或变换失败时，当前模板保持原样而不是停在已释放的句柄上。
        /// 每次打开都从模板重新生成 <see cref="shrFindMode"/>，上一轮的擦除笔迹一并清空，
        /// 否则再次涂抹时它们会叠画在完好的新模板上。
        /// </para>
        /// </remarks>
        public void DisplayModel(string modelPath, HObject ho_ModeRect, HObject ho_Contour, ModelResult result)
        {
            display.Reset();

            using (var preview = TemplatePreview.Load(modelPath, ho_ModeRect, ho_Contour, result))
            {
                HOperatorSet.GenEmptyObj(out HObject erase);
                _shrCoord = preview.Coord;
                preview.Detach(out HObject srcImage, out HObject findMode, out HObject contour);
                HModelUI.Replace(ref _srcImage, ref srcImage);
                HModelUI.Replace(ref shrFindMode, ref findMode);
                HModelUI.Replace(ref _shrContour, ref contour);
                HModelUI.Replace(ref shrErase, ref erase);
            }

            // 先换图再 SetModelPara：后者内部 ReDispImage 重绘的是窗口里的当前图
            display.DispImage(_srcImage);
            display.SetModelPara(shrFindMode, _shrContour, _shrCoord);
            _drawType = DrawEnum.DispModel;

            display.Display.Disp(shrFindMode, DrawStyle.Of(HColor.Blue));
            display.Display.Disp(_shrContour, DrawStyle.Of(HColor.Green));
            display.Display.Disp(_shrCoord, DrawStyle.Of(HColor.Red));
        }

        /// <summary>
        /// 释放本窗体持有的 HALCON 句柄，由 <see cref="Dispose(bool)"/> 的 disposing 分支调用。
        /// </summary>
        /// <remarks>
        /// 与 <see cref="HModelUI"/> 对齐：这四个字段原先从未释放，<c>ParaForm</c> 每次销毁
        /// 都会留下一整份模板图像与区域的非托管句柄。<c>eraseRect</c> 手里的是同一批对象
        /// （见 <see cref="SyncEraseResult"/>），不需要另行释放。
        /// </remarks>
        private void ReleaseModelResources()
        {
            try
            {
                _srcImage?.Dispose();
                shrErase?.Dispose();
                shrFindMode?.Dispose();
                _shrContour?.Dispose();
            }
            catch (System.Exception ex) { Log.Error(nameof(HEditModelUI), "释放模板资源失败.", ex); }
        }

        /// <summary>
        /// 关窗即视为取消绘制：只隐藏、不真关，但必须先把挂起的会话终止掉。
        /// </summary>
        /// <remarks>
        /// 不终止会话的话：用户画到一半点 × 关窗，图被隐藏后他既右键确认不了也取消不了，
        /// <see cref="IsDrawBusy"/> 会一路挂到 5 分钟超时，期间主窗的"编辑模板"
        /// (<c>ParaForm.but_editModel_Click</c>) 一直被这道闸门挡在门外 —— 用户彻底没有出路。
        /// </remarks>
        private void HEditForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 先立"不真关"的旗子: 下面的 CancelDraw 会就地内联跑完挂起会话的续体
            // (见 DrawSession.Finish —— TrySetResult 在当前调用栈上直接执行续体)。
            // 万一续体抛异常而 e.Cancel 还没置位, 窗体就真被销毁了 ——
            // 本实例是 ParaForm 长期复用的, 之后再 Show() 就是 ObjectDisposedException。
            e.Cancel = true;

            // CancelDraw 让续体走 DrawROIAsync 的取消分支(重画原区域、不做集合运算),
            // 并在 Click 的 finally 里恢复按钮、清掉 _drawBusy。
            if (_drawBusy)
            {
                try
                {
                    // 必须先判空再调用: CancelDraw(null) 在 DrawSession.CancelAll 里是"取消所有窗口",
                    // 拿不到自己的窗口就误伤了其他 HDisplayUI 上正在进行的绘制。宁可不取消。
                    var window = display?.HoWindow;
                    if (window != null) DrawHelper.CancelDraw(window);
                    else Log.Warn(nameof(HEditModelUI), "关窗时取不到 HalconWindow, 跳过取消绘制会话.");
                }
                catch (System.Exception ex) { Log.Warn(nameof(HEditModelUI), "关窗时取消绘制会话失败.", ex); }
            }

            this.Hide();
        }

    }
}
