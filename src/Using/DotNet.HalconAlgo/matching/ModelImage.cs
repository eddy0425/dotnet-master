using DotNet.Drawing;
using HalconDotNet;
using System;
using System.IO;

namespace DotNet.HalconAlgo
{
    /// <summary>
    /// 模板文件（模板小图、模型文件）的落盘，四种匹配共用。
    /// </summary>
    internal static class ModelImage
    {
        /// <summary> 保存模板小图到 <paramref name="path"/>（先写临时文件，成功后再替换） </summary>
        public static void Save(HObject hImage, HObject imgReduced, string path)
        {
            Stage(path, staged => HalconController.SaveSmallestRectImage(hImage, imgReduced, staged));
        }

        /// <summary>
        /// 先写同目录的临时文件，写成功后再替换目标文件。
        /// </summary>
        /// <remarks>
        /// 目标文件正是仍在使用的旧模板的文件。直接覆盖的话，写到一半失败时旧模板还在用，
        /// 它的文件却已损坏。临时文件名保留原扩展名：write_image 在扩展名与格式不符时会自己再补一个。
        /// </remarks>
        public static void Stage(string path, Action<string> write)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string staged = Path.Combine(dir ?? string.Empty, Path.GetFileNameWithoutExtension(path) + ".tmp" + Path.GetExtension(path));
            try
            {
                write(staged);
                if (File.Exists(path)) File.Replace(staged, path, null);
                else File.Move(staged, path);
            }
            finally
            {
                // 失败时清掉写了一半的临时文件; 清不掉只记日志, 不能盖住真正的异常
                try
                {
                    if (File.Exists(staged)) File.Delete(staged);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    Log.Warn(nameof(ModelImage), $"临时模板文件删除失败: {staged}", ex);
                }
            }
        }
    }
}
