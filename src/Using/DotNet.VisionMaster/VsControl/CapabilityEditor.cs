using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using DotNet.HalconCore;
using DotNet.HalconUI;

namespace DotNet.VisionMaster
{
    /// <summary>
    /// 能力编辑器：某种能力接口（<see cref="IRoiEditable"/>、<see cref="ITemplateEditable"/>…）在参数页上的交互界面。
    /// </summary>
    /// <remarks>
    /// 取代 Designer 里写死的 ROI 页 / 模板页：参数页按能力挑编辑器，与具体算法无关。
    /// 以后新增一种交互能力，是在 SDK 加一个能力接口、在这里加一个编辑器；插件只实现能力接口。
    /// <para>宿主内部的扩展点，不对插件开放 —— 插件自带 WinForms 控件就依赖了宿主的界面技术。</para>
    /// </remarks>
    internal interface ICapabilityEditor
    {
        /// <summary> 这个工具要不要本编辑器 </summary>
        bool Supports(IParaStrategy tool);

        /// <summary> 放在哪一页（与参数共用页名，见 <see cref="Pages"/>） </summary>
        string Page { get; }

        /// <summary> 建出编辑器界面；参数页只建一次，换工具时调 <see cref="ICapabilityView.Bind"/> </summary>
        Control Create(EditorContext context);
    }

    /// <summary> 编辑器界面要响应的参数页事件 </summary>
    internal interface ICapabilityView
    {
        /// <summary> 显示一个工具（参数页已经把它的 ROI 交给显示窗口之后调用）；<paramref name="tool"/> 可能为 null </summary>
        void Bind(IParaStrategy tool);

        /// <summary> 闸门（正在绘制 / 宿主忙）变了：刷新按钮可用状态 </summary>
        void UpdateState();
    }

    /// <summary> 参数页交给编辑器的能力：显示窗口、交互宿主、当前工具，以及统一的绘制闸门 </summary>
    internal sealed class EditorContext
    {
        private readonly Func<IParaStrategy> _tool;
        private readonly Func<bool> _drawBusy;
        private readonly Func<bool> _hostBusy;
        private readonly Action<Func<IParaStrategy, Task>, bool> _runDraw;

        public EditorContext(HDisplayUI display, DisplayInteractionHost host, Func<IParaStrategy> tool,
            Func<bool> drawBusy, Func<bool> hostBusy, Action<Func<IParaStrategy, Task>, bool> runDraw)
        {
            Display = display;
            Host = host;
            _tool = tool;
            _drawBusy = drawBusy;
            _hostBusy = hostBusy;
            _runDraw = runDraw;
        }

        public HDisplayUI Display { get; }

        public DisplayInteractionHost Host { get; }

        public IParaStrategy Tool => _tool();

        /// <summary> 参数页上有一条绘制正在进行 </summary>
        public bool IsDrawBusy => _drawBusy();

        /// <summary> 宿主正在运行（执行会话忙）：不能做会改动 HObject 的交互 </summary>
        public bool IsHostBusy => _hostBusy();

        /// <summary>
        /// 发起一次绘制：闸门 → 清屏 → 交给策略 → 异常提示 → 只清自己那一轮的闸门。
        /// <paramref name="commitsData"/> 为 true 表示绘制会写盘（模板），结束后以当前状态为新的编辑起点。
        /// </summary>
        public void RunDraw(Func<IParaStrategy, Task> draw, bool commitsData = false) => _runDraw(draw, commitsData);
    }

    /// <summary> 宿主内置的能力编辑器 </summary>
    internal static class CapabilityEditors
    {
        public static IReadOnlyList<ICapabilityEditor> BuiltIn { get; } = new ICapabilityEditor[]
        {
            new Editor<IRoiEditable>(Pages.Region, ctx => new RoiEditor(ctx)),
            new Editor<ITemplateEditable>(Pages.Template, ctx => new TemplateEditor(ctx)),
        };

        /// <summary> "实现了能力接口 <typeparamref name="TCapability"/> 就放这个编辑器" </summary>
        private sealed class Editor<TCapability> : ICapabilityEditor
        {
            private readonly Func<EditorContext, Control> _create;

            public Editor(string page, Func<EditorContext, Control> create)
            {
                Page = page;
                _create = create;
            }

            public string Page { get; }

            public bool Supports(IParaStrategy tool) => tool is TCapability;

            public Control Create(EditorContext context) => _create(context);
        }
    }
}
