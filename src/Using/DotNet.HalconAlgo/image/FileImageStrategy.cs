using HalconDotNet;
using Newtonsoft.Json;
using System;
using DotNet.Drawing;
using DotNet.HalconCore;
using System.Collections.Generic;


namespace DotNet.HalconAlgo
{
    /// <remarks>
    /// <see cref="Index"/> / <see cref="ImagePaths"/> 是「轮播到第几张」的运行态游标，随实例保存、不落盘。
    /// 本类<b>非线程安全</b>：同一实例的 <see cref="Fun_action"/> 只能由流程串行调用（现有宿主即如此），
    /// 并发调用会让两次取像读到同一张或跳过一张。
    /// </remarks>
    public class FileImageStrategy : ParaStrategyBase<FileImage>
    {
        public override AlgoEnum Algorithm => AlgoEnum.FileImage;
        public override string Name { get; set; } = "文件图像";
        public override int RunIndex { get; set; }

        private int Index  = 0;       //图像下标
        // 可空：Init 里目录无效时会主动置 null（见下方 catch），Fun_action 靠判空决定要不要重扫目录。
        private string[] ImagePaths;   //图像路径

        public override void GenTreeNode(ITreeVisualizer tree)
        {
            tree.Branch(Name, branch => branch
                       .Node("图像", OutEnum.Image)
                   );

            ClearResolvers();
            RegisterOutput("图像", () => inPara.Image);

        }
        public override bool Fun_action(IHDisplay display, List<IParaStrategy> strategys)
        {
            // 原先整个方法包在 try { ... } catch { throw; } 里，捕获后原样重抛，等于没包，已去掉。
            if (ImagePaths == null)
            {
                ImagePaths = HalconController.GetPaths(inPara.ImageFolder);
            }

            // 空目录单独报错：否则落到下面的 ImagePaths[0]，得到的是与真实原因无关的 IndexOutOfRangeException
            if (ImagePaths.Length == 0)
            {
                throw new InvalidOperationException($"图像目录中没有图像文件: {inPara.ImageFolder}");
            }
            if (Index >= ImagePaths.Length) Index = 0;

            // 先读到局部变量, 成功后再替换: 原先先 Dispose 再 ReadImage, 读图一旦抛异常(文件损坏/被占用),
            // inPara.Image 就停在已释放的句柄上, 下游经"图像"输出拿到的是个死句柄。
            // 游标在读图之前前移: 否则某张图损坏时 Index 永远停在它身上, 每轮都读同一张坏图,
            // 轮播就此卡死。读失败照样抛出, 下一轮自然跳到下一张。
            int current = Index;
            string path = ImagePaths[current];
            Index = current + 1;
            HOperatorSet.ReadImage(out HObject loaded, path);

            //判断图像是否为空
            if (!loaded.NotNull())
            {
                loaded.Dispose();
                throw new InvalidOperationException($"加载图像失败，读到的图像为空: {path}");
            }

            inPara.Image.Dispose();
            inPara.Image = loaded;

            //旋转
            if (inPara.Rotate != 0)
            {
                double pi = Convert.ToDouble(inPara.Rotate);
                HOperatorSet.RotateImage(inPara.Image, out HObject imgRotated, pi, "constant");
                inPara.Image.Dispose();
                inPara.Image = imgRotated;
            }

            //镜像
            switch (inPara.Mirror)
            {
                case "行镜像":
                    HOperatorSet.MirrorImage(inPara.Image, out HObject imgMirrored1, "row");
                    inPara.Image.Dispose();
                    inPara.Image = imgMirrored1;
                    break;
                case "列镜像":
                    HOperatorSet.MirrorImage(inPara.Image, out HObject imgMirrored2, "column");
                    inPara.Image.Dispose();
                    inPara.Image = imgMirrored2;
                    break;
                case "原点镜像":
                    // 原点镜像 = 关于图像中心的点对称 (行、列各镜像一次, 等价于旋转 180°)。
                    // 原先用的 "diagonal" 是沿主对角线 x=y 反射 (即转置), 非方图时连宽高都会对调。
                    HOperatorSet.MirrorImage(inPara.Image, out HObject imgMirroredRow, "row");
                    inPara.Image.Dispose();
                    inPara.Image = imgMirroredRow;
                    HOperatorSet.MirrorImage(inPara.Image, out HObject imgMirrored3, "column");
                    inPara.Image.Dispose();
                    inPara.Image = imgMirrored3;
                    break;
                default: break;
            }

            display.DispImage(inPara.Image);

            if (inPara.DispText)
            {
                string message = $"{Name} : W:{display.HoWidth} H:{display.HoHeight} 索引:{current}/{ImagePaths.Length}";
                display.DispText(message, new Point2d(inPara.FontX, inPara.FontY), DrawStyle.Of(HColor.Green, inPara.FontSize));
            }

            return true;
        }
        public override void DispPara(IParaUiHost ui)
        {
            ui.ShowTabs(TabPageEnum.FileImage, TabPageEnum.Display);

            ui.ShowComboBoxList("cmb_Rotate", inPara.Rotate.ToString(), new[] { "0", "90", "180", "270" });
            ui.ShowComboBoxList("cmb_Mirror", inPara.Mirror, new[] { "无", "行镜像", "列镜像", "原点镜像" });
            ui.ShowComboBox("cmb_ImageFolder", inPara.ImageFolder, true);

            //------------------------------------------
            ui.ShowCheckBox("ckb_disp0", "显示文本", inPara.DispText);

            ui.ShowComboBoxDropDown("CB_FontX", inPara.FontX.ToString(), new[] { "20", "50" });
            ui.ShowComboBoxDropDown("CB_FontY", inPara.FontY.ToString(), new[] { "20", "50" });
            ui.ShowComboBoxDropDown("CB_FontSize", inPara.FontSize.ToString(), new[] { "15", "30" });
        }
        public override void SavePara(IParaUiHost ui)
        {
            inPara.Rotate = ui.GetInt("cmb_Rotate");
            inPara.Mirror = ui.GetString("cmb_Mirror");
            string folder = ui.GetString("cmb_ImageFolder");
            // 目录一改, 缓存的文件列表与游标就作废: ImagePaths 只在 Init / 为 null 时重扫,
            // 不在这里作废的话, 改了目录后仍会继续轮播旧目录里的图, 界面上毫无提示。
            // null 与 "" 同视为"未设置": 宿主对空下拉返回 null 时不能每次保存都误判为改了目录
            if (!string.Equals(folder ?? string.Empty, inPara.ImageFolder ?? string.Empty, StringComparison.Ordinal))
            {
                ImagePaths = null;
                Index = 0;
            }
            inPara.ImageFolder = folder;

            //------------------------------------------
            inPara.DispText = ui.GetBool("ckb_disp0");

            inPara.FontX = ui.GetInt("CB_FontX");
            inPara.FontY = ui.GetInt("CB_FontY");
            inPara.FontSize = ui.GetInt("CB_FontSize");
        }
        public override void Init(IRoiHost host)
        {
            try
            {
                // Init 可能被重复调用，先释放上一次的句柄再重建，避免累积泄漏
                inPara.Image?.Dispose();
                HOperatorSet.GenEmptyObj(out inPara.Image);
                ImagePaths = HalconController.GetPaths(inPara.ImageFolder);
            }
            catch (Exception ex)
            {
                // Init 在宿主窗体构造期被循环调用, 这里弹窗会卡住整个启动流程;
                // 图像目录无效属于可恢复配置问题, 记录后让其余策略继续初始化.
                ImagePaths = null;
                Log.Error(nameof(FileImageStrategy), $"初始化图像目录失败: {inPara.ImageFolder}", ex);
            }
        }
        public override void Close(IRoiHost host)
        {

        }

    }

    public class FileImage : AlgoFont, IDisposable
    {
        public FileImage()
        {
            HOperatorSet.GenEmptyObj(out Image);
        }

        /// <summary> 释放 <see cref="Image"/>。幂等：HObject.Dispose 本身可重复调用。 </summary>
        public void Dispose()
        {
            Image?.Dispose();
        }

        /// <summary> 图像 </summary>
        /// <remarks>不加 = new HObject() 初始化器：句柄统一由构造函数的 GenEmptyObj 创建，否则初始化器创建的句柄会被覆盖且永不释放。
        /// 原先句柄只由 <see cref="FileImageStrategy.Init"/> 创建，Init 之前（或 Init 抛异常时）字段为 null，
        /// 与另外四个匹配参数类的约定也不一致；改由构造函数建空句柄，Init 里的 <c>Image?.Dispose()</c> 会正常回收它。</remarks>
        [JsonIgnore]
        public HObject Image;

        /// <summary> 旋转 </summary>
        public int Rotate { get; set; } = 0;

        /// <summary> 镜像 </summary>
        public string Mirror { get; set; } = "无";

        /// <summary> 图像文件夹 </summary>
        /// <remarks>未配置时为空串而不是 null：这个值直接交给 Directory.GetFiles，空串得到的是一条明确的路径异常（Init 会记录并继续），null 得到的是 NRE。</remarks>
        public string ImageFolder { get; set; } = string.Empty;

    }
}
