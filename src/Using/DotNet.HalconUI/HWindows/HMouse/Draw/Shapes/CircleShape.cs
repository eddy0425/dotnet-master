using HalconDotNet;
using System;
using System.Windows.Forms;

namespace DotNet.HalconUI.Draw
{
    /// <summary>圆 ROI：左键按下定圆心 → 拖拽定半径 → 编辑阶段可拖圆心或半径端点，右键确认。</summary>
    internal sealed class CircleShape : DrawShape
    {
        internal double CX, CY, Radius;

        internal override void OnDown(MouseInput e)
        {
            if (e.Button != MouseButtons.Left) return;

            if (Phase == DrawPhase.Idle)
            {
                CX = e.X; CY = e.Y;
                Radius = 0;
                Phase = DrawPhase.Drawing;
            }
            else if (Phase == DrawPhase.Drawing)
            {
                Radius = Math.Max(1, DrawGeometry.Dist(CX, CY, e.X, e.Y));
                Phase = DrawPhase.Editing;
            }
            else if (Phase == DrawPhase.Editing && Hover != DrawHandle.None)
            {
                Dragging = true;
            }
        }

        internal override void OnUp(MouseInput e)
        {
            if (e.Button == MouseButtons.Left
                && Phase == DrawPhase.Drawing
                && DrawGeometry.Dist(CX, CY, e.X, e.Y) > 2)
            {
                Radius = Math.Max(1, DrawGeometry.Dist(CX, CY, e.X, e.Y));
                Phase = DrawPhase.Editing;
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
                        double r = Math.Max(1, DrawGeometry.Dist(CX, CY, e.X, e.Y));
                        R.Cross(CX, CY, "orange");
                        R.Circle(CX, CY, r, "red");
                    }
                    break;

                case DrawPhase.Editing:
                    Edit(e);
                    break;
            }
        }

        internal override void RenderStatic()
        {
            R.Cross(CX, CY, "orange", 50);
            R.Cross(CX + Radius, CY, "orange", 30);
            R.Circle(CX, CY, Radius, "red");
        }

        private void Edit(MouseInput e)
        {
            double edgeX = CX + Radius, edgeY = CY;

            if (Dragging)
            {
                switch (Hover)
                {
                    case DrawHandle.Center:
                        CX = e.X; CY = e.Y;
                        break;
                    case DrawHandle.AxisEnd1:
                        Radius = Math.Max(1, DrawGeometry.Dist(CX, CY, e.X, e.Y));
                        break;
                }
                edgeX = CX + Radius;
                edgeY = CY;
            }
            else
            {
                if (IsNear(CX, CY, e)) Hover = DrawHandle.Center;
                else if (IsNear(edgeX, edgeY, e)) Hover = DrawHandle.AxisEnd1;
                else Hover = DrawHandle.None;
            }

            R.Cross(CX, CY, Hover == DrawHandle.Center ? "green" : "orange", 50);
            R.Cross(edgeX, edgeY, Hover == DrawHandle.AxisEnd1 ? "green" : "orange", 30);
            R.Circle(CX, CY, Radius, "red");
        }
    }
}
