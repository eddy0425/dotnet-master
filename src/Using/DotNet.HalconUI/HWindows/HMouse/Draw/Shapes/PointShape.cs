using HalconDotNet;
using System.Windows.Forms;

namespace DotNet.HalconUI.Draw
{
    /// <summary>点 ROI：左键点击落点后直接进入编辑，可拖拽十字调整，右键确认。</summary>
    internal sealed class PointShape : DrawShape
    {
        internal double X;
        internal double Y;

        internal override void OnDown(MouseInput e)
        {
            if (e.Button != MouseButtons.Left) return;

            if (Phase == DrawPhase.Idle)
            {
                X = e.X; Y = e.Y;
                Phase = DrawPhase.Editing;
            }
            else if (Phase == DrawPhase.Editing && Hover == DrawHandle.Center)
            {
                Dragging = true;
            }
        }

        internal override void OnUp(MouseInput e) => HandleEditingMouseUp(e);

        internal override void Render(MouseInput e)
        {
            R.RestoreBackground();

            switch (Phase)
            {
                case DrawPhase.Idle:
                    R.Cross(e.X, e.Y, "orange");
                    break;

                case DrawPhase.Editing:
                    Edit(e);
                    break;
            }
        }

        internal override void RenderStatic() => R.Cross(X, Y, "red", 30);

        private void Edit(MouseInput e)
        {
            if (Dragging)
            {
                X = e.X; Y = e.Y;
            }
            else
            {
                Hover = IsNear(X, Y, e) ? DrawHandle.Center : DrawHandle.None;
            }
            R.Cross(X, Y, Hover == DrawHandle.Center ? "green" : "red", 30);
        }
    }
}
