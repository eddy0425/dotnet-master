using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;


namespace DotNet.HalconAlgo
{
    public class LineRotImageStrategy : ParaStrategyBase<LineRotImage>
    {
        public override AlgoEnum Algorithm => AlgoEnum.LineRotImage;
        public override string Name { get; set; } = "直线图像";
        public override int RunIndex { get; set; }

        public override void GenTreeNode(ITreeVisualizer tree)
        {
            tree.Branch(Name, branch => branch
                       .Node("图像", OutEnum.Image)
                   );

            ClearResolvers();
            RegisterOutput("图像", () => inPara.Image);
        }
        public override bool Fun_action(HObject ho_Image, IHDisplay display)
        {
            display.SetImage(ho_Image);
            // 直接处理传入的图像, 不再转到另一重载按 ImageIn 取图: 单图重载没有上游,
            // ImageIn 一旦不是"默认"就必然解析失败 —— 与 FitLineStrategy 的单图重载同口径。
            // 传空集合而不是 null: Run 内部会对 strategys 做 ResolveFrom, null 会直接 NRE.
            return RunWithReset(() => ho_Image.RequireImage(Name), display, StrategyExtensions.EmptyList());
        }
        public override bool Fun_action(IHDisplay display, List<IParaStrategy> strategys)
        {
            return RunWithReset(() => inPara.ImageIn == "默认"
                    ? display.HoImage.RequireImage(Name)
                    : strategys.ResolveFrom<HObject>(inPara.ImageIn).RequireImage(Name),
                display, strategys);
        }

        /// <summary>
        /// 每轮开头先把输出图像复位成空对象, 与匹配 / 拟合类"先清空再校验"同口径:
        /// 失败都是抛异常退出, 不清的话宿主吞掉异常后, 下游读到的是上一轮的图像, 静默按旧帧继续算。
        /// 旧句柄在 finally 里才释放: "默认"来源下传入的图像可能正是上一轮的输出 (显示窗口不复制时),
        /// 必须等本轮结果算完再放。
        /// </summary>
        private bool RunWithReset(Func<HObject> getImage, IHDisplay display, List<IParaStrategy> strategys)
        {
            HObject previous = inPara.Image;
            HOperatorSet.GenEmptyObj(out inPara.Image);
            try
            {
                return Run(getImage(), display, strategys);
            }
            finally
            {
                previous.Dispose();
            }
        }

        private bool Run(HObject ho_Image, IHDisplay display, List<IParaStrategy> strategys)
        {

            CvLine line = strategys.ResolveFrom<CvLine>(inPara.LineIn);
            // 退化直线是上游拟合结果无效, 属于业务错误而不是"意外的空引用", 不再抛 NullReferenceException
            // (与 HDisplay 审查项 D10 同口径), 否则现场堆栈会被误读成本策略自身的空指针 bug。
            if (line == null || line.IsDegenerate)
                throw new InvalidOperationException($"{Name} : 直线数据为空或退化 ({inPara.LineIn})");

            double dx = line.End.X - line.Start.X;
            double dy = line.End.Y - line.Start.Y;
            double lineAngle = Math.Atan2(dy, dx); // 弧度

            double rotateAngle;
            if (inPara.AlignAxis == "平行Y轴")
                rotateAngle = lineAngle - Math.PI / 2;
            else
                rotateAngle = lineAngle;

            // 归一化到 [-π/2, π/2]，取最小旋转角度（直线无方向性）
            while (rotateAngle > Math.PI / 2) rotateAngle -= Math.PI;
            while (rotateAngle < -Math.PI / 2) rotateAngle += Math.PI;

            HOperatorSet.GetImageSize(ho_Image, out HTuple imgWidth, out HTuple imgHeight);
            double centerRow = imgHeight.D / 2;
            double centerCol = imgWidth.D / 2;

            HOperatorSet.HomMat2dIdentity(out HTuple HomMat2D);
            HOperatorSet.HomMat2dRotate(HomMat2D, rotateAngle, centerRow, centerCol, out HTuple HomMat2DRotate);
            // 先生成新图再替换输出 (此时是 RunWithReset 放进去的空对象)
            HOperatorSet.AffineTransImage(ho_Image, out HObject rotated, HomMat2DRotate, "constant", "false");
            inPara.Image.Dispose();
            inPara.Image = rotated;
            double angleDeg = rotateAngle * 180.0 / Math.PI;

            display.DispImage(inPara.Image);

            if (inPara.DispText)
            {
                string message = $"{Name} : 对齐:{inPara.AlignAxis} 旋转:{angleDeg:F2}°";
                display.DispText(message, new Point2d(inPara.FontX, inPara.FontY), DrawStyle.Of(HColor.Green, inPara.FontSize));
            }

            return true;
        }
        public override void DispPara(IParaUiHost ui)
        {
            ui.ShowTabs(TabPageEnum.Parameter, TabPageEnum.Display);

            ui.ShowLabel("lbl_100", "图像来源");
            ui.ShowComboBox("cmb_100", inPara.ImageIn, false);
            ui.ShowButton("btn_100", true);

            ui.ShowLabel("lbl_101", "直线来源");
            ui.ShowComboBox("cmb_101", inPara.LineIn, false);
            ui.ShowButton("btn_101", true);

            ui.ShowLabel("lbl_102", "对齐方式");
            ui.ShowComboBoxList("cmb_102", inPara.AlignAxis, new[] { "平行X轴", "平行Y轴" });
            ui.ShowButton("btn_102", false);

            //------------------------------------------
            ui.ShowCheckBox("ckb_disp0", "显示文本", inPara.DispText);

            ui.ShowComboBoxDropDown("CB_FontX", inPara.FontX.ToString(), new[] { "20", "50" });
            ui.ShowComboBoxDropDown("CB_FontY", inPara.FontY.ToString(), new[] { "20", "50" });
            ui.ShowComboBoxDropDown("CB_FontSize", inPara.FontSize.ToString(), new[] { "15", "30" });
        }
        public override void SavePara(IParaUiHost ui)
        {
            inPara.ImageIn = ui.GetString("cmb_100");
            inPara.LineIn = ui.GetString("cmb_101");
            inPara.AlignAxis = ui.GetString("cmb_102");

            //------------------------------------------
            inPara.DispText = ui.GetBool("ckb_disp0");

            inPara.FontX = ui.GetInt("CB_FontX");
            inPara.FontY = ui.GetInt("CB_FontY");
            inPara.FontSize = ui.GetInt("CB_FontSize");
        }

    }

    public class LineRotImage : AlgoFont
    {
        public LineRotImage()
        {
            HOperatorSet.GenEmptyObj(out Image);
        }

        /// <summary> 图像来源 </summary>
        public string ImageIn { set; get; } = "默认";

        /// <summary> 直线来源 </summary>
        public string LineIn { set; get; } = "默认";

        /// <summary> 输出图像 </summary>
        /// <remarks>不加 = new HObject() 初始化器：句柄统一由构造函数的 GenEmptyObj 创建，否则初始化器创建的句柄会被覆盖且永不释放。</remarks>
        [JsonIgnore]
        public HObject Image;

        /// <summary> 对齐轴 </summary>
        public string AlignAxis { set; get; } = "平行X轴";
    }
}
