using System;

namespace DotNet.HalconCore
{
    /// <summary>
    /// 输出变量树的构建契约。
    /// </summary>
    /// <remarks>
    /// 原 <c>TreeVisualizer</c> 直接持有 WinForms 的 <c>TreeView</c>，导致算法层的
    /// <c>GenTreeNode</c> 被迫依赖 System.Windows.Forms。实际上算法层只用到
    /// 「加分支 / 加节点」这几个动作，与控件本身无关，故抽成接口下沉到契约层，
    /// WinForms 实现留在 UI 层（<c>DotNet.HalconUI.TreeVisualizer</c>）。
    /// </remarks>
    public interface ITreeVisualizer
    {
        /// <summary> 添加一个分支节点（含子节点配置） </summary>
        ITreeVisualizer Branch(string text, Action<ITreeBranch> config);

        /// <summary> 添加多个空节点分支 </summary>
        ITreeVisualizer Branches(params string[] texts);
    }

    /// <summary>
    /// 树分支构建器，支持链式调用。
    /// </summary>
    public interface ITreeBranch
    {
        /// <summary> 添加一个子节点 </summary>
        ITreeBranch Node(string text, OutEnum type, Action<ITreeBranch> config = null);

        /// <summary> 添加一个嵌套分支节点 </summary>
        ITreeBranch Branch(string text, Action<ITreeBranch> config);

        /// <summary> 复用点结构（行 / 列） </summary>
        ITreeBranch ReusePointStructure(string pointName);

        /// <summary> 添加通用节点（结果、文本显示） </summary>
        ITreeBranch CommonNodes();
    }
}
