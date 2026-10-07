using System;
using HalconDotNet;
using Newtonsoft.Json;

namespace DotNet.Drawing
{
    /// <summary>
    /// 自定义序列化转换器
    /// </summary>
    public class JsonConvertHObject : JsonConverter
    {
        /// <summary>
        /// 仅处理 <see cref="HObject"/> 及其派生类型.
        /// </summary>
        /// <remarks>
        /// 只允许通过 <c>[JsonConverter(typeof(JsonConvertHObject))]</c> 挂在成员上使用
        /// (目前唯一用法是 <c>CvRegion.HoRegion</c>), 该路径下 Newtonsoft 不会调用本方法.
        /// <para>
        /// 不要注册进 <c>JsonSerializerSettings.Converters</c>: ReadJson/WriteJson 内部用同一个
        /// serializer 处理 HObject, 全局注册后会再次选中本转换器, 自我递归直至 StackOverflow.
        /// </para>
        /// </remarks>
        public override bool CanConvert(Type objectType)
        {
            return objectType != null && typeof(HObject).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            HObject hObject = new HObject();
            try
            {
                if ((reader.Value as string) != "Destroyed")
                {
                    // Deserialize 返回 null 时（JSON 里是 null 字面量）保留上面的空 HObject,
                    // 与 "Destroyed" 分支同样语义, 不让 null 流到调用方.
                    hObject = serializer.Deserialize(reader, objectType) as HObject ?? hObject;
                }
                return hObject;
            }
            catch (Exception ex)
            {
                // 反序列化失败退化为空 HObject: 这是库层代码, 不能弹窗阻塞调用线程.
                Log.Warn(nameof(JsonConvertHObject), "反序列化 HObject 失败, 返回空对象.", ex);
                return hObject;
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            try
            {
                HObject hObject = value as HObject;
                if (hObject != null && hObject.NotNull())
                {
                    serializer.Serialize(writer, value);
                }
                else
                {
                    serializer.Serialize(writer, "Destroyed");
                }
            }
            catch (Exception ex)
            {
                // 序列化失败不能静默丢数据: 记录后抛出, 由上层决定提示还是重试.
                Log.Error(nameof(JsonConvertHObject), "序列化 HObject 失败.", ex);
                throw;
            }
        }
    }
}
