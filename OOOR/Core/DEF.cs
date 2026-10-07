using System.Drawing;

namespace ooor.Core
{
    internal class DEF
    {
        // 自动维护，不可手动维护
        public const string ver = "0.0046";

        public static Icon Icon { get; internal set; }

        /// <summary>
        /// 设备唯一指纹：物理硬盘序列号经 SHA1 得到的 40 位 hex（启动心跳上报用）。
        /// 由 ClientHeartbeat 在后台线程计算并赋值；心跳完成前可能为 null。
        /// </summary>
        public static string UUID { get; internal set; }
    }
}
