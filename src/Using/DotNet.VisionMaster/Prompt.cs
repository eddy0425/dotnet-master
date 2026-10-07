using System;
using System.Windows.Forms;

namespace DotNet.VisionMaster
{
    /// <summary>
    /// 窗体给用户的提示框统一从这里走。
    /// </summary>
    /// <remarks>
    /// <c>MessageBox.Show</c> 是模态的，无人值守的测试点不掉它；测试把 <see cref="Show"/> 换成记录器，
    /// 就能验证"闸门拦下时有没有给出反馈、给的是什么"，而不必绕开这些分支。
    /// </remarks>
    internal static class Prompt
    {
        internal static Action<string> Show = text => MessageBox.Show(text);
    }
}
