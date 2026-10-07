using HalconDotNet;
using System;
using DotNet.Drawing;
using DotNet.HalconCore;


namespace DotNet.HalconAlgo
{
    /// <summary>
    /// 从文件夹轮播读图。
    /// </summary>
    /// <remarks>
    /// 文件列表与"轮播到第几张"的游标是运行态，不落盘；目录一改就重扫、游标归零。
    /// 本类<b>非线程安全</b>：同一实例只能由流程串行执行，并发执行会让两次取像读到同一张或跳过一张。
    /// </remarks>
    [Algo("image.file", "文件图像", Group = "图像", Order = 10)]
    public class FileImageStrategy : ParaStrategyBase<FileImage>, IImageProducer
    {
        private string[] _paths;          // 已扫描的文件列表; null 表示需要重扫
        private string _scannedFolder;    // _paths 对应的目录: 与配置不同即作废
        private int _next;                // 下一张的下标
        private int _current;             // 本轮读的那张, 仅用于结果文本

        public FileImageStrategy()
        {
            HOperatorSet.GenEmptyObj(out HObject empty);
            Image = empty;
        }

        /// <summary> 本轮读到并做完旋转 / 镜像的图像；失败时为空对象 </summary>
        public HObject Image { get; private set; }

        protected override void DeclareParams(ParamBuilder p)
        {
            p.Tab(TabPageEnum.FileImage)
             .Folder("图片路径", () => inPara.ImageFolder, v => inPara.ImageFolder = v)
             .Group("图片处理")
             .Choice("旋转", () => inPara.Rotate, v => inPara.Rotate = v,
                     Option.Of(0, "0"), Option.Of(90, "90"), Option.Of(180, "180"), Option.Of(270, "270"))
             .Choice("镜像", () => inPara.Mirror, v => inPara.Mirror = v,
                     Option.Of(MirrorMode.None, "无"), Option.Of(MirrorMode.Row, "行镜像"),
                     Option.Of(MirrorMode.Column, "列镜像"), Option.Of(MirrorMode.Origin, "原点镜像"));
        }

        protected override void DeclareOutputs(OutputBuilder o)
        {
            o.Image("图像", () => Image);
        }

        protected override void ResetOutputs()
        {
            // 本工具不读别人的图, 旧输出可以立即释放
            HOperatorSet.GenEmptyObj(out HObject empty);
            var old = Image;
            Image = empty;
            old?.Dispose();
        }

        protected override RunResult Execute(RunContext context)
        {
            string folder = inPara.ImageFolder ?? string.Empty;
            if (_paths == null || !string.Equals(_scannedFolder, folder, StringComparison.Ordinal))
            {
                _paths = HalconController.GetPaths(folder);
                _scannedFolder = folder;
                _next = 0;
            }

            // 空目录单独报错：否则落到下面的 _paths[0]，得到的是与真实原因无关的 IndexOutOfRangeException
            if (_paths.Length == 0)
                throw new InvalidOperationException($"图像目录中没有图像文件: {folder}");
            if (_next >= _paths.Length) _next = 0;

            // 游标在读图之前前移: 否则某张图损坏时游标永远停在它身上, 每轮都读同一张坏图, 轮播就此卡死。
            _current = _next;
            string path = _paths[_current];
            _next = _current + 1;

            HOperatorSet.ReadImage(out HObject image, path);
            try
            {
                if (!image.NotNull() || image.CountObj() == 0)
                    throw new InvalidOperationException($"加载图像失败，读到的图像为空: {path}");

                if (inPara.Rotate != 0)
                    image = Replace(image, img => { HOperatorSet.RotateImage(img, out HObject r, inPara.Rotate, "constant"); return r; });

                switch (inPara.Mirror)
                {
                    case MirrorMode.Row:
                        image = Replace(image, img => Mirror(img, "row"));
                        break;
                    case MirrorMode.Column:
                        image = Replace(image, img => Mirror(img, "column"));
                        break;
                    case MirrorMode.Origin:
                        // 原点镜像 = 关于图像中心的点对称 (行、列各镜像一次, 等价于旋转 180°)。
                        // "diagonal" 是沿主对角线反射 (即转置), 非方图时连宽高都会对调, 不能用。
                        image = Replace(image, img => Mirror(img, "row"));
                        image = Replace(image, img => Mirror(img, "column"));
                        break;
                }

                // 所有权转交输出: 先换上新图再释放旧的 (ResetOutputs 放进去的空对象)
                var old = Image;
                Image = image;
                image = null;
                old.Dispose();
            }
            finally
            {
                image?.Dispose();
            }

            HOperatorSet.GetImageSize(Image, out HTuple width, out HTuple height);
            return RunResult.Ok($"W:{width.I} H:{height.I} 索引:{_current}/{_paths.Length}");
        }

        private static HObject Mirror(HObject image, string mode)
        {
            HOperatorSet.MirrorImage(image, out HObject mirrored, mode);
            return mirrored;
        }

        /// <summary> 用变换结果替换图像并释放旧图；变换抛异常时旧图仍由调用方释放 </summary>
        private static HObject Replace(HObject image, Func<HObject, HObject> transform)
        {
            var next = transform(image);
            image.Dispose();
            return next;
        }

        /// <summary> 预扫图像目录。目录无效属于可恢复的配置问题：只记日志，执行时再报错 </summary>
        public override void Init(IRoiHost host)
        {
            try
            {
                _paths = HalconController.GetPaths(inPara.ImageFolder ?? string.Empty);
                _scannedFolder = inPara.ImageFolder ?? string.Empty;
                _next = 0;
            }
            catch (Exception ex)
            {
                _paths = null;
                Log.Error(nameof(FileImageStrategy), $"初始化图像目录失败: {inPara.ImageFolder}", ex);
            }
        }

        protected override void Dispose(bool disposing)
        {
            Image?.Dispose();
            base.Dispose(disposing);
        }
    }

    public class FileImage : DisplayOptions
    {
        /// <summary> 图像文件夹 </summary>
        /// <remarks>未配置时为空串而不是 null：这个值直接交给 Directory.GetFiles，空串得到的是一条明确的路径异常。</remarks>
        public string ImageFolder { get; set; } = string.Empty;

        /// <summary> 旋转角度（度）：0 / 90 / 180 / 270 </summary>
        public int Rotate { get; set; } = 0;

        /// <summary> 镜像 </summary>
        public MirrorMode Mirror { get; set; } = MirrorMode.None;
    }
}
