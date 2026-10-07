namespace DotNet.Drawing
{
    public static class AngleExtension
    {
        /// <summary>
        /// 将弧度转换为角度
        /// </summary>
        /// <param name="radian">弧度值</param>
        /// <returns>角度值</returns>
        public static double ToDegrees(this double radian)
            => MathHelper.ToDegrees(radian);

        /// <summary>
        /// 将角度转换为弧度
        /// </summary>
        /// <param name="angle">角度值</param>
        /// <returns>弧度值</returns>
        public static double ToRadians(this double angle)
            => MathHelper.ToRadians(angle);
    }
}
