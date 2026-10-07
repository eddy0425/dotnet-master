using HalconDotNet;
using System.Windows.Forms;

namespace DotNet.HalconUI.Draw
{
    /// <summary>轴对齐矩形 ROI：拖拽定对角点，编辑阶段可拖两个角点或中心平移，右键确认。</summary>
    internal sealed class Rect1Shape : DrawShape
    {
        internal double X1, Y1, X2, Y2;
        internal double CX, CY;

        /// <summary>由两个对角点重算中心，<c>Draw*Mod</c> 注入初始几何后也要调一次。</summary>
        internal void SyncCenter()
        {
            CX = (X1 + X2) / 2;
            CY = (Y1 + Y2) / 2;
        }

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
                SyncCenter();
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
                SyncCenter();
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
                    R.Rect1(X1, Y1, e.X, e.Y, "red");
                    break;

                case DrawPhase.Editing:
                    Edit(e);
                    break;
            }
        }

        internal override void RenderStatic()
        {
            R.Cross(X1, Y1, "orange", 50);
            R.Cross(X2, Y2, "orange", 50);
            R.Cross(CX, CY, "orange", 50);
            R.Rect1(X1, Y1, X2, Y2, "red");
        }

        private void Edit(MouseInput e)
        {
            if (Dragging)
            {
                switch (Hover)
                {
                    case DrawHandle.P1:
                        X1 = e.X; Y1 = e.Y;
                        SyncCenter();
                        break;
                    case DrawHandle.P2:
                        X2 = e.X; Y2 = e.Y;
                        SyncCenter();
                        break;
                    case DrawHandle.Center:
                        double dx = e.X - CX, dy = e.Y - CY;
                        X1 += dx; Y1 += dy;
                        X2 += dx; Y2 += dy;
                        CX = e.X; CY = e.Y;
                        break;
                }
            }
            else
            {
                if (IsNear(X1, Y1, e)) Hover = DrawHandle.P1;
                else if (IsNear(X2, Y2, e)) Hover = DrawHandle.P2;
                else if (IsNear(CX, CY, e)) Hover = DrawHandle.Center;
                else Hover = DrawHandle.None;
            }

            R.Cross(X1, Y1, Hover == DrawHandle.P1 ? "green" : "orange", 50);
            R.Cross(X2, Y2, Hover == DrawHandle.P2 ? "green" : "orange", 50);
            R.Cross(CX, CY, Hover == DrawHandle.Center ? "green" : "orange", 50);
            R.Rect1(X1, Y1, X2, Y2, "red");
        }
    }
}
