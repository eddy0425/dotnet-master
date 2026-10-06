using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;


namespace DotNet.HalconAlgo
{
    public class RotateImageStrategy : ParaStrategyBase<RotateImage>
    {
        public override AlgoEnum Algorithm => AlgoEnum.RotateImage;
        public override string Name { get; set; } = "旋转图像";
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

            string message;
            // 本轮结果先落在局部变量, 成功后再替换 inPara.Image (此时是 RunWithReset 放进去的空对象)
            HObject result;

            if (inPara.RotateType == "图像中心")
            {
                if (inPara.RotateAngle != 0)
                {
                    // HALCON rotate_image: Phi 单位为度
                    HOperatorSet.RotateImage(ho_Image, out result, inPara.RotateAngle, "constant");
                }
                else
                {
                    HOperatorSet.CopyObj(ho_Image, out result, 1, 1);
                }
                message = $"{Name} : 方式:{inPara.RotateType} 角度:{inPara.RotateAngle:F2}°";
            }
            else
            {
                CvCoord hCoord = strategys.ResolveFrom<CvCoord>(inPara.CoordIn);

                double baseRow = hCoord.Y;
                double baseCol = hCoord.X;
                // 先用 Angle.Normalized 归一化到 [-π, π)，再换算成度数做逻辑处理,
                // 不再手写 %360 三段式 (原写法在 ±180 处的取舍不明确)
                double baseAglDeg = hCoord.Angle.Normalized.Degrees;

                switch (inPara.RotateType)
                {
                    case "坐标系":
                    case "坐标系X轴":
                        baseAglDeg = -baseAglDeg;
                        break;
                    case "坐标系Y轴":
                        // 目标是把坐标系 Y 轴摆正, 所需旋转量为 (±90 - baseAglDeg).
                        // baseAglDeg == 0 时 +90 与 -90 在几何上等价(相差 180°, 都能让 Y 轴竖直),
                        // 这里明确归入 ">= 0" 分支取 +90, 与 baseAglDeg → 0⁺ 的极限保持连续;
                        // 原实现把 0 漏在两个分支之外, 结果退化成 "不旋转", 与两侧极限都不连续.
                        baseAglDeg = baseAglDeg >= 0 ? 90 - baseAglDeg : -90 - baseAglDeg;
                        break;
                    default:
                        // 未知旋转方式: 保持原角度不做换算, 但记录下来便于发现配置写错.
                        Log.Warn(nameof(RotateImageStrategy),
                                 $"未知的旋转方式 '{inPara.RotateType}', 按原角度处理.");
                        break;
                }

                HOperatorSet.HomMat2dIdentity(out HTuple HomMat2D);
                HOperatorSet.HomMat2dRotate(HomMat2D, baseAglDeg.ToRadians(), baseRow, baseCol, out HTuple HomMat2DRotate);
                HOperatorSet.AffineTransImage(ho_Image, out result, HomMat2DRotate, "constant", "false");

                message = $"{Name} : 方式:{inPara.RotateType} 坐标:({baseCol:F2},{baseRow:F2}) 角度:{baseAglDeg:F2}°";
            }

            inPara.Image.Dispose();
            inPara.Image = result;

            display.DispImage(inPara.Image);

            if (inPara.DispText)
            {
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

            ui.ShowLabel("lbl_101", "选择方式");
            ui.ShowComboBoxList("cmb_101", inPara.RotateType, new[] { "图像中心", "坐标系", "坐标系X轴", "坐标系Y轴" });
            ui.ShowButton("btn_101", false);

            if (inPara.RotateType == "图像中心")
            {
                ui.ShowLabel("lbl_102", "旋转角度");
                ui.ShowComboBoxDropDown("cmb_102", inPara.RotateAngle.ToString(), new[] { "0", "90", "180", "270" });
                ui.ShowButton("btn_102", false);
            }
            else
            {
                ui.ShowLabel("lbl_102", "坐标系");
                ui.ShowComboBox("cmb_102", inPara.CoordIn, false);
                ui.ShowButton("btn_102", true);
            }

            //------------------------------------------
            ui.ShowCheckBox("ckb_disp0", "显示文本", inPara.DispText);

            ui.ShowComboBoxDropDown("CB_FontX", inPara.FontX.ToString(), new[] { "20", "50" });
            ui.ShowComboBoxDropDown("CB_FontY", inPara.FontY.ToString(), new[] { "20", "50" });
            ui.ShowComboBoxDropDown("CB_FontSize", inPara.FontSize.ToString(), new[] { "15", "30" });
        }
        public override void SavePara(IParaUiHost ui)
        {
            inPara.ImageIn = ui.GetString("cmb_100");

            // cmb_102 的含义由 DispPara 时的旋转方式决定 (角度 / 坐标系路径), 必须按"旧"方式解读:
            // 界面切换 cmb_101 不会重新 DispPara, 按新方式读会把角度文本 "90" 写进 CoordIn, 丢掉已配置的坐标系。
            bool slot102IsAngle = inPara.RotateType == "图像中心";
            inPara.RotateType = ui.GetString("cmb_101");

            if (slot102IsAngle)
            {
                // 用户可能输入非数字, 这里保留 TryParse 的容错: 解析失败时不覆盖原值.
                if (float.TryParse(ui.GetString("cmb_102"), out float angle))
                    inPara.RotateAngle = angle;
            }
            else
            {
                inPara.CoordIn = ui.GetString("cmb_102");
            }

            //------------------------------------------
            inPara.DispText = ui.GetBool("ckb_disp0");

            inPara.FontX = ui.GetInt("CB_FontX");
            inPara.FontY = ui.GetInt("CB_FontY");
            inPara.FontSize = ui.GetInt("CB_FontSize");
        }

    }

    public class RotateImage : AlgoFont
    {
        public RotateImage()
        {
            HOperatorSet.GenEmptyObj(out Image);
        }

        /// <summary> 图像来源 </summary>
        public string ImageIn { set; get; } = "默认";

        /// <summary> 跟随坐标 </summary>
        public string CoordIn { set; get; } = "默认";

        /// <summary> 输出图像 </summary>
        /// <remarks>不加 = new HObject() 初始化器：句柄统一由构造函数的 GenEmptyObj 创建，否则初始化器创建的句柄会被覆盖且永不释放。</remarks>
        [JsonIgnore]
        public HObject Image;

        /// <summary> 旋转方式 </summary>
        public string RotateType { set; get; } = "图像中心";

        /// <summary> 旋转角度（度） </summary>
        public float RotateAngle { set; get; } = 0;
    }
}
