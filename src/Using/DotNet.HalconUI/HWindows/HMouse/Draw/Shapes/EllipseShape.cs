using HalconDotNet;
using System;
using System.Windows.Forms;

namespace DotNet.HalconUI.Draw
{
    /// <summary>
    /// 椭圆 ROI：左键按下定中心 → 拖拽定主轴方向与长度 → 编辑阶段可拖中心 / 主轴端点 / 副轴端点，右键确认。
    /// </summary>
    internal sealed class EllipseShape : DrawShape
    {
        internal double CX, CY, Phi, R1, R2;

        internal override void OnDown(MouseInput e)
        {
            if (e.Button != MouseButtons.Left) return;

            if (Phase == DrawPhase.Idle)
            {
                CX = e.X; CY = e.Y;
                Phi = 0; R1 = 0; R2 = 0;
                Phase = DrawPhase.Drawing;
            }
            else if (Phase == DrawPhase.Drawing)
            {
                // 处理“按下→释放未越过阈值→再次按下”的情况, 直接落定为可编辑椭圆
                double len = DrawGeometry.Dist(CX, CY, e.X, e.Y);
                R1 = Math.Max(1, len);
                Phi = len > 1 ? Math.Atan2(CY - e.Y, e.X - CX) : 0;
                R2 = R1 / 2;
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
                    R1 = len;
                    Phi = Math.Atan2(CY - e.Y, e.X - CX);
                    R2 = R1 / 2;
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
                        double r1 = DrawGeometry.Dist(CX, CY, e.X, e.Y);
                        if (r1 > 1)
                        {
                            double phi = Math.Atan2(CY - e.Y, e.X - CX);
                            R.Cross(CX, CY, "orange");
                            R.Ellipse(CX, CY, phi, r1, r1 / 2, "red");
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
            DrawGeometry.AxisEnd(CX, CY, Phi, R1, out double a1x, out double a1y);
            DrawGeometry.AxisEndPerp(CX, CY, Phi, R2, out double a2x, out double a2y);
            R.Cross(CX, CY, "orange", 50);
            R.Cross(a1x, a1y, "orange", 30);
            R.Cross(a2x, a2y, "orange", 30);
            R.Ellipse(CX, CY, Phi, R1, R2, "red");
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
                        R1 = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
                        Phi = Math.Atan2(CY - e.Y, e.X - CX);
                        break;
                    case DrawHandle.AxisEnd2:
                        R2 = Math.Max(1, Math.Abs(-dx * Math.Sin(Phi) - dy * Math.Cos(Phi)));
                        break;
                }
            }
            else
            {
                DrawGeometry.AxisEnd(CX, CY, Phi, R1, out double ax1, out double ay1);
                DrawGeometry.AxisEndPerp(CX, CY, Phi, R2, out double ax2, out double ay2);

                if (IsNear(CX, CY, e)) Hover = DrawHandle.Center;
                else if (IsNear(ax1, ay1, e)) Hover = DrawHandle.AxisEnd1;
                else if (IsNear(ax2, ay2, e)) Hover = DrawHandle.AxisEnd2;
                else Hover = DrawHandle.None;
            }

            DrawGeometry.AxisEnd(CX, CY, Phi, R1, out double a1x, out double a1y);
            DrawGeometry.AxisEndPerp(CX, CY, Phi, R2, out double a2x, out double a2y);
            R.Cross(CX, CY, Hover == DrawHandle.Center ? "green" : "orange", 50);
            R.Cross(a1x, a1y, Hover == DrawHandle.AxisEnd1 ? "green" : "orange", 30);
            R.Cross(a2x, a2y, Hover == DrawHandle.AxisEnd2 ? "green" : "orange", 30);
            R.Ellipse(CX, CY, Phi, R1, R2, "red");
        }
    }
}
