using HalconDotNet;
using System.Windows.Forms;

namespace DotNet.HalconUI.Draw
{
    /// <summary>线段 ROI：左键按下→拖拽→释放定两端点，编辑阶段可拖端点或中点平移，右键确认。</summary>
    internal sealed class LineShape : DrawShape
    {
        internal double X1, Y1, X2, Y2;

        internal override void OnDown(MouseInput e)
        {
            if (e.Button != MouseButtons.Left) return;

            if (Phase == DrawPhase.Idle)
            {
                X1 = e.X; Y1 = e.Y;
                Phase = DrawPhase.Drawing;
            }
            else if (Phase == DrawPhase.Drawing)
            {
                X2 = e.X; Y2 = e.Y;
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
                && DrawGeometry.Dist(X1, Y1, e.X, e.Y) > 2)
            {
                X2 = e.X; Y2 = e.Y;
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
                    R.Cross(X1, Y1, "orange");
                    R.Cross(e.X, e.Y, "orange");
                    R.Line(X1, Y1, e.X, e.Y, "red");
                    break;

                case DrawPhase.Editing:
                    Edit(e);
                    break;
            }
        }

        internal override void RenderStatic()
        {
            double midX = (X1 + X2) / 2;
            double midY = (Y1 + Y2) / 2;
            R.Cross(X1, Y1, "orange", 50);
            R.Cross(X2, Y2, "orange", 50);
            R.Cross(midX, midY, "orange", 30);
            R.Line(X1, Y1, X2, Y2, "red");
        }

        private void Edit(MouseInput e)
        {
            double midX = (X1 + X2) / 2;
            double midY = (Y1 + Y2) / 2;

            if (Dragging)
            {
                switch (Hover)
                {
                    case DrawHandle.P1:
                        X1 = e.X; Y1 = e.Y;
                        break;
                    case DrawHandle.P2:
                        X2 = e.X; Y2 = e.Y;
                        break;
                    case DrawHandle.Center:
                        double dx = e.X - midX, dy = e.Y - midY;
                        X1 += dx; Y1 += dy;
                        X2 += dx; Y2 += dy;
                        break;
                }
                midX = (X1 + X2) / 2;
                midY = (Y1 + Y2) / 2;
            }
            else
            {
                if (IsNear(X1, Y1, e)) Hover = DrawHandle.P1;
                else if (IsNear(X2, Y2, e)) Hover = DrawHandle.P2;
                else if (IsNear(midX, midY, e)) Hover = DrawHandle.Center;
                else Hover = DrawHandle.None;
            }

            R.Cross(X1, Y1, Hover == DrawHandle.P1 ? "green" : "orange", 50);
            R.Cross(X2, Y2, Hover == DrawHandle.P2 ? "green" : "orange", 50);
            R.Cross(midX, midY, Hover == DrawHandle.Center ? "green" : "orange", 30);
            R.Line(X1, Y1, X2, Y2, "red");
        }
    }
}
