using HalconDotNet;
using System;
using System.Windows.Forms;

namespace DotNet.HalconUI.Draw
{
    /// <summary>
    /// 可旋转矩形 ROI。交互流程 (与 Rect1/Circle 一致)：
    /// Idle → 左键按下设置中心 → Drawing → 拖拽定义主轴方向(phi)与长度(halfLen1) → 释放 → Editing
    /// → 拖拽控制点(中心 / 主轴端点 / 短轴端点) → 右键确认。
    /// </summary>
    internal sealed class Rect2Shape : DrawShape
    {
        internal double CX, CY, Phi, HalfLen1, HalfLen2;

        internal override void OnDown(MouseInput e)
        {
            if (e.Button != MouseButtons.Left) return;

            if (Phase == DrawPhase.Idle)
            {
                CX = e.X; CY = e.Y;
                Phi = 0; HalfLen1 = 0; HalfLen2 = 0;
                Phase = DrawPhase.Drawing;
            }
            else if (Phase == DrawPhase.Drawing)
            {
                // 处理“按下→释放未越过阈值→再次按下”的情况, 直接落定为可编辑矩形
                double len = DrawGeometry.Dist(CX, CY, e.X, e.Y);
                HalfLen1 = Math.Max(1, len);
                Phi = len > 1 ? Math.Atan2(CY - e.Y, e.X - CX) : 0;
                // 与 Drawing 阶段预览的短轴宽度一致(Render 中使用 len / 5), 避免释放瞬间矩形突然变宽
                HalfLen2 = Math.Max(1, HalfLen1 / 5);
                Phase = DrawPhase.Editing;
            }
            else if (Phase == DrawPhase.Editing && Hover != DrawHandle.None)
            {
                Dragging = true;
            }
        }

        internal override void OnUp(MouseInput e)
        {
            if (e.Button == MouseButtons.Left && Phase == DrawPhase.Drawing)
            {
                double len = DrawGeometry.Dist(CX, CY, e.X, e.Y);
                if (len > 2)
                {
                    HalfLen1 = len;
                    Phi = Math.Atan2(CY - e.Y, e.X - CX);
                    // 与 Drawing 阶段预览的短轴宽度一致, 避免释放瞬间矩形突然变宽
                    HalfLen2 = Math.Max(1, HalfLen1 / 5);
                    Phase = DrawPhase.Editing;
                }
                return;
            }

            HandleEditingMouseUp(e);
        }

        internal override void Render(MouseInput e)
        {
            R.RestoreBackground();

            switch (Phase)
            {
                case DrawPhase.Idle:
                    R.Cross(e.X, e.Y, "orange");
                    break;

                case DrawPhase.Drawing:
                    {
                        double len = DrawGeometry.Dist(CX, CY, e.X, e.Y);
                        R.Cross(CX, CY, "orange");
                        if (len > 1)
                        {
                            double phi = Math.Atan2(CY - e.Y, e.X - CX);
                            R.Line(CX, CY, e.X, e.Y, "orange");
                            R.Rect2Arrow(CX, CY, phi, len, Math.Max(1, len / 5), "red");
                        }
                    }
                    break;

                case DrawPhase.Editing:
                    Edit(e);
                    break;
            }
        }

        internal override void RenderStatic()
        {
            DrawGeometry.AxisEnd(CX, CY, Phi, HalfLen1, out double a1x, out double a1y);
            DrawGeometry.AxisEndPerp(CX, CY, Phi, HalfLen2, out double a2x, out double a2y);
            DrawGeometry.AxisEndPerp(CX, CY, Phi, -HalfLen2, out double a2nx, out double a2ny);
            R.Cross(CX, CY, "orange", 50);
            R.Cross(a1x, a1y, "orange", 30);
            R.Cross(a2x, a2y, "orange", 30);
            R.Cross(a2nx, a2ny, "orange", 30);
            R.Rect2Arrow(CX, CY, Phi, HalfLen1, HalfLen2, "red");
        }

        private void Edit(MouseInput e)
        {
            if (Dragging)
            {
                double dx = e.X - CX, dy = e.Y - CY;
                switch (Hover)
                {
                    case DrawHandle.Center:
                        CX = e.X; CY = e.Y;
                        break;
                    case DrawHandle.AxisEnd1:
                        HalfLen1 = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
                        Phi = Math.Atan2(CY - e.Y, e.X - CX);
                        break;
                    case DrawHandle.AxisEnd2:
                    case DrawHandle.AxisEnd2Neg:
                        HalfLen2 = Math.Max(1, Math.Abs(-dx * Math.Sin(Phi) - dy * Math.Cos(Phi)));
                        break;
                }
            }
            else
            {
                DrawGeometry.AxisEnd(CX, CY, Phi, HalfLen1, out double ax1, out double ay1);
                DrawGeometry.AxisEndPerp(CX, CY, Phi, HalfLen2, out double ax2, out double ay2);
                DrawGeometry.AxisEndPerp(CX, CY, Phi, -HalfLen2, out double ax2n, out double ay2n);

                if (IsNear(CX, CY, e)) Hover = DrawHandle.Center;
                else if (IsNear(ax1, ay1, e)) Hover = DrawHandle.AxisEnd1;
                else if (IsNear(ax2, ay2, e)) Hover = DrawHandle.AxisEnd2;
                else if (IsNear(ax2n, ay2n, e)) Hover = DrawHandle.AxisEnd2Neg;
                else Hover = DrawHandle.None;
            }

            DrawGeometry.AxisEnd(CX, CY, Phi, HalfLen1, out double a1x, out double a1y);
            DrawGeometry.AxisEndPerp(CX, CY, Phi, HalfLen2, out double a2x, out double a2y);
            DrawGeometry.AxisEndPerp(CX, CY, Phi, -HalfLen2, out double a2nx, out double a2ny);
            R.Cross(CX, CY, Hover == DrawHandle.Center ? "green" : "orange", 50);
            R.Cross(a1x, a1y, Hover == DrawHandle.AxisEnd1 ? "green" : "orange", 30);
            R.Cross(a2x, a2y, Hover == DrawHandle.AxisEnd2 ? "green" : "orange", 30);
            R.Cross(a2nx, a2ny, Hover == DrawHandle.AxisEnd2Neg ? "green" : "orange", 30);
            R.Rect2Arrow(CX, CY, Phi, HalfLen1, HalfLen2, "red");
        }
    }
}
