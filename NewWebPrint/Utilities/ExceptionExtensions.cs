using System;

namespace NewWebPrint.Utilities
{
    public static class ExceptionExtensions
    {
        /// <summary>
        /// 获取最底层异常（等价于 GetBaseException）
        /// </summary>
        public static Exception Root(this Exception ex)
        {
            if (ex == null) throw new ArgumentNullException(nameof(ex));

            var root = ex;
            while (root.InnerException != null) root = root.InnerException;
            return root;
        }

        /// <summary>
        /// 获取最底层异常消息（方便直接写日志/返回）
        /// </summary>
        public static string RootMessage(this Exception ex)
        {
            return ex.Root().Message;
        }
    }
}