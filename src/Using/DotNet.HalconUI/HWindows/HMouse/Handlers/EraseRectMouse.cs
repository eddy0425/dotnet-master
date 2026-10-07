using DotNet.Drawing;
using HalconDotNet;
using System;
using System.Windows.Forms;
using DotNet.HalconCore;


namespace DotNet.HalconUI
{
    /// <summary>
    /// 擦除矩形处理器
    /// 通过左键拖动以圆形画笔擦除区域
    /// </summary>
    public class EraseRectMouse : IMouseHandler
    {
        // 两段式初始化：字段在 SetUp 里赋值而不是构造函数里。
        // HEditModelUI 只在 but_ApplyRegion_Click 里 SetUp 后才切到 DrawEnum.Erase，
        // 但 DrawType 是公开可写的，外部可能不经 SetUp 直接切过来：此时 _display 为 null，什么都不做。
        private bool _editing;
        private HObject _erase = null;    //擦除区域 ShrErase
        private HObject _findMode = null; //查找模版区域 ShrFindMode
        private HColor _color;
        private int _lineWidth;
        private IHDisplay _display = null;

        /// <summary> 当前累计的擦除区域 </summary>
        /// <remarks>
        /// <see cref="SetUp"/> 传入的句柄所有权随之转给本类：每次涂抹都会生成新对象并释放旧对象，
        /// 调用方原先持有的引用在第一次涂抹后即失效，必须改读本属性（见 <c>HEditModelUI.SyncEraseResult</c>）。
        /// </remarks>
        public HObject Erase => _erase;

        /// <summary> 扣除擦除区域后的模板区域，所有权约定同 <see cref="Erase"/> </summary>
        public HObject FindMode => _findMode;

        public void SetUp(IHDisplay display, HObject shrErase, HObject shrFindMode, HColor color, int lineWidth)
        {
            //display.Reset();
            //display.ReDispImage();
            _display = display;
            _erase = shrErase;
            _findMode = shrFindMode;
            _color = color;
            _lineWidth = lineWidth;
        }

        public void SetPara(HColor color, int lineWidth)
        {
            _color = color;
            _lineWidth = lineWidth;
        }

        public void OnMouseDown(HMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            _editing = true;
            EraseAt(e.Y, e.X);
        }

        public void OnMouseUp(HMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) _editing = false;
        }

        public void OnMouseWheel(HMouseEventArgs e)
        {
            if (_display == null) return;
            try
            {
                DispEraseRegion();
            }
            catch (Exception ex)
            {
                Log.Warn(nameof(EraseRectMouse), "显示擦除区域失败.", ex);
            }
        }

        public void OnMouseMove(HMouseEventArgs e)
        {
            if (_editing) EraseAt(e.Y, e.X);
        }

        /// <remarks>
        /// 运行在鼠标回调里，异常不能冒到 HWindowControl 的事件总线上（会打断整个涂抹交互），
        /// 与 DispRectMouse / DispModelMouse 一样只记日志。
        /// </remarks>
        private void EraseAt(HTuple row, HTuple column)
        {
            if (_display == null) return;
            try
            {
                DrawCircle(row, column);
                DispEraseRegion();
            }
            catch (Exception ex)
            {
                // 结束本次涂抹：失败多半是持续性的（窗口已销毁、句柄为空），
                // 否则拖动中每次移动都会重复失败、刷一条带堆栈的告警
                _editing = false;
                Log.Warn(nameof(EraseRectMouse), "擦除区域失败.", ex);
            }
        }

        private void DrawCircle(HTuple row, HTuple column)
        {
            // 不预先 GenEmptyObj：紧接着的 GenCircle(out …) 会覆盖掉它，占位句柄随之泄漏。
            HObject subRegion = null;
            try
            {
                _display.SetDraw("fill");
                HOperatorSet.GenCircle(out subRegion, row, column, _lineWidth);

                if (_erase.CountObj() > 0)
                {
                    HOperatorSet.Union2(_erase, subRegion, out HObject regionUnion);
                    _erase.Dispose();
                    _erase = regionUnion;
                }
                else
                {
                    _erase.Dispose();
                    HOperatorSet.CopyObj(subRegion, out _erase, 1, -1);
                }

                // 只扣本次笔刷：_erase 是跨多次 SetUp 累计的显示用区域，
                // 用它做差会把用户之后重新添加回来的部分再扣掉一次。
                if (_findMode.CountObj() > 0)
                {
                    HOperatorSet.Difference(_findMode, subRegion, out HObject regionDifference);
                    _findMode.Dispose();
                    _findMode = regionDifference;
                }
            }
            finally
            {
                _display.SetDraw("margin");
                subRegion?.Dispose();
            }
        }

        private void DispEraseRegion()
        {
            if (!_erase.NotNull()) return;

            // 颜色与填充模式一并由 DrawStyle 描述，省掉一次单独的 SetDraw；
            // 显示失败时同样恢复为 margin，否则之后所有区域都按填充绘制
            try
            {
                _display.Disp(_erase, new DrawStyle { Color = _color, DrawMode = "fill" });
            }
            finally
            {
                _display.SetDraw("margin");
            }
        }

    }
}
