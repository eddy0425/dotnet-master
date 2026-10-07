using HalconDotNet;
using System.Collections.Generic;
using System.Windows.Forms;

namespace DotNet.HalconUI.Draw
{
    /// <summary>
    /// 多边形区域 ROI：左键逐点添加顶点 → 右键闭合(至少 3 点)进入编辑 → 拖拽顶点 → 右键确认。
    /// </summary>
    internal sealed class RegionShape : DrawShape
    {
        private readonly List<double> _cols = new List<double>();
        private readonly List<double> _rows = new List<double>();
        private int _editIdx = -1;

        /// <summary>多边形顶点的列坐标 (x)。与 <see cref="Rows"/> 一一对应。</summary>
        internal IReadOnlyList<double> Cols => _cols;

        /// <summary>多边形顶点的行坐标 (y)。与 <see cref="Cols"/> 一一对应。</summary>
        internal IReadOnlyList<double> Rows => _rows;

        internal override void OnDown(MouseInput e)
        {
            if (e.Button != MouseButtons.Left) return;

            if (Phase == DrawPhase.Idle || Phase == DrawPhase.Drawing)
            {
                _cols.Add(e.X);
                _rows.Add(e.Y);
                if (Phase == DrawPhase.Idle) Phase = DrawPhase.Drawing;
            }
            else if (Phase == DrawPhase.Editing && Hover == DrawHandle.P1)
            {
                Dragging = true;
            }
        }

        internal override void OnUp(MouseInput e)
        {
            if (e.Button == MouseButtons.Left && Phase == DrawPhase.Editing)
            {
                EndDrag();
            }
            else if (e.Button == MouseButtons.Right)
            {
                // 与其它图元不同: 第一次右键是“闭合多边形”，第二次右键才是确认
                if (Phase == DrawPhase.Drawing && _rows.Count >= 3)
                    Phase = DrawPhase.Editing;
                else if (Phase == DrawPhase.Editing)
                    Confirm();
            }
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
                    DispPolyLines("orange");
                    R.Cross(e.X, e.Y, "red");
                    if (_cols.Count > 0)
                    {
                        int last = _cols.Count - 1;
                        R.Line(_cols[last], _rows[last], e.X, e.Y, "red");
                    }
                    break;

                case DrawPhase.Editing:
                    Edit(e);
                    break;
            }
        }

        /// <remarks>
        /// 拖拽时先更新顶点再画：原先先画后改，画出的多边形总落后鼠标一帧，松手那一帧的位置永远不显示。
        /// </remarks>
        private void Edit(MouseInput e)
        {
            bool dragging = Dragging && _editIdx >= 0 && _editIdx < _cols.Count;
            if (dragging)
            {
                _cols[_editIdx] = e.X;
                _rows[_editIdx] = e.Y;
            }

            DispPolyLines("red");
            for (int i = 0; i < _cols.Count; i++)
                R.Cross(_cols[i], _rows[i], "green", 10);

            if (!dragging)
            {
                Hover = DrawHandle.None;
                for (int i = 0; i < _cols.Count; i++)
                {
                    if (IsNear(_cols[i], _rows[i], e))
                    {
                        R.Cross(_cols[i], _rows[i], "red", 15);
                        _editIdx = i;
                        Hover = DrawHandle.P1;
                        break;
                    }
                }
            }
        }

        private void DispPolyLines(string color)
        {
            for (int i = 0; i < _cols.Count - 1; i++)
                R.Line(_cols[i], _rows[i], _cols[i + 1], _rows[i + 1], color);
            if (_cols.Count > 2)
                R.Line(_cols[_cols.Count - 1], _rows[_rows.Count - 1], _cols[0], _rows[0], color);
        }
    }
}
