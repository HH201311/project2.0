
namespace SUNWODA_SEVB.Core.Events
{
    /// <summary>
    /// 用户状态变更事件（登录/登出）
    /// </summary>
    public static class UserStateChangedEvent
    {
        /// <summary>
        /// 用户状态变更时触发
        /// </summary>
        public static event Action<string>? OnUserStateChanged;

        /// <summary>
        /// 触发用户状态变更通知
        /// </summary>
        /// <param name="userAccount">当前用户账号</param>
        public static void Raise(string userAccount)
        {
            OnUserStateChanged?.Invoke(userAccount);
        }
    }
}
