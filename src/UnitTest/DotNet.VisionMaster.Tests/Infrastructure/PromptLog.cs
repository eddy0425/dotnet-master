using System;
using System.Collections.Generic;

namespace DotNet.VisionMaster.Tests
{
    /// <summary>
    /// 把 <see cref="Prompt.Show"/> 换成记录器，<see cref="Dispose"/> 时还原。
    /// </summary>
    /// <remarks>被测代码一弹 <c>MessageBox</c> 测试就卡死，凡是可能走到提示框的测试都要先装上它。</remarks>
    internal sealed class PromptLog : IDisposable
    {
        private readonly Action<string> _saved = Prompt.Show;

        public readonly List<string> Messages = new List<string>();

        public PromptLog()
        {
            Prompt.Show = Messages.Add;
        }

        public void Dispose() => Prompt.Show = _saved;
    }
}
