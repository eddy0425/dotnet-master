using System;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace DotNet.VisionMaster.Tests
{
    /// <summary>
    /// 反射读写被测窗体的私有成员。
    /// </summary>
    /// <remarks>
    /// VisionMaster 的窗体逻辑都写在 private 的事件处理方法里（由 Designer 挂到控件事件上），
    /// 这里直接调用它们，绕开窗口消息与真实鼠标。
    /// </remarks>
    internal static class Priv
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        public static T Get<T>(object target, string field) => (T)FindField(target.GetType(), field).GetValue(target);

        public static void Set(object target, string field, object value) => FindField(target.GetType(), field).SetValue(target, value);

        /// <summary>调用私有方法；被调方法抛出的异常原样（保留堆栈）抛出，而不是包在 TargetInvocationException 里。</summary>
        /// <remarks>有重载时（如 ValueForm.GenerateTree）按实参类型挑选；null 实参匹配任意引用类型。</remarks>
        public static object Call(object target, string method, params object[] args)
        {
            var info = target.GetType().GetMethods(Instance)
                .Where(m => m.Name == method)
                .SingleOrDefault(m =>
                {
                    var ps = m.GetParameters();
                    return ps.Length == args.Length && ps.Zip(args, (p, a) =>
                        a == null ? !p.ParameterType.IsValueType : p.ParameterType.IsInstanceOfType(a)).All(ok => ok);
                })
                ?? throw new MissingMethodException(target.GetType().Name, method);
            try { return info.Invoke(target, args); }
            catch (TargetInvocationException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        /// <summary>以 (sender, EventArgs.Empty) 调用一个 Click 类的事件处理方法。</summary>
        public static void Click(object target, string handler, object sender = null) =>
            Call(target, handler, sender, EventArgs.Empty);


        private static FieldInfo FindField(Type type, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var field = t.GetField(name, Instance | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(type.Name, name);
        }
    }
}
