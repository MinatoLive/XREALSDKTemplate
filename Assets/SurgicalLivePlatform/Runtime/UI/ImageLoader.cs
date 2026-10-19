using System;
using System.Threading.Tasks;
using UnityEngine;

namespace SurgicalLivePlatform.Runtime.UI
{
    /// <summary>
    /// 轻量级的网络图片异步加载工具类 (已迁移重构)。
    /// 核心功能已整合并沉淀至 <see cref="SurgicalLivePlatform.Runtime.SurgicalLiveSDK"/>。
    /// 建议直接使用 SurgicalLiveSDK.LoadImageAsync 与 SurgicalLiveSDK.LoadImageWithFallbackAsync。
    /// </summary>
    public static class ImageLoader
    {
        /// <summary>
        /// 后端业务 API 服务器基地址
        /// </summary>
        public static string BaseServerUrl
        {
            get => SurgicalLiveSDK.BaseServerUrl;
            set => SurgicalLiveSDK.BaseServerUrl = value;
        }

        /// <summary>
        /// 前端静态资源服务器基地址
        /// </summary>
        public static string BaseStaticServerUrl
        {
            get => SurgicalLiveSDK.BaseStaticServerUrl;
            set => SurgicalLiveSDK.BaseStaticServerUrl = value;
        }

        /// <summary>
        /// 智能解析完整的图片网络访问地址。
        /// </summary>
        /// <param name="url">输入的图片原始路径或协议字符串</param>
        /// <returns>返回规范化后的完整图片 URL</returns>
        public static string ResolveUrl(string url)
        {
            return SurgicalLiveSDK.ResolveImageUrl(url);
        }

        /// <summary>
        /// 异步加载指定 URL 的图片并返回 <see cref="Texture2D"/>。
        /// <para>若图片缓存中已存在，则直接从内存返回以节省网络 IO。</para>
        /// </summary>
        /// <param name="url">目标图片路径或 URL</param>
        /// <param name="timeoutSeconds">网络超时上限时间 (秒)</param>
        /// <returns>返回解密解码后的 Texture2D 实例，加载失败返回 null</returns>
        public static Task<Texture2D> LoadImageAsync(string url, int timeoutSeconds = 5)
        {
            return SurgicalLiveSDK.LoadImageAsync(url, timeoutSeconds);
        }

        /// <summary>
        /// 多级降级容灾异步加载图片。
        /// <para>优先尝试请求 primaryUrl；若网络异常或 404，则自动降级请求 secondaryUrl；若均失败返回 null。</para>
        /// </summary>
        /// <param name="primaryUrl">首选优先级图片 URL (通常为预设高清大图)</param>
        /// <param name="secondaryUrl">次选降级图片 URL (通常为房间快照直连接口)</param>
        /// <param name="timeoutSeconds">单次尝试请求超时时间 (秒)</param>
        /// <returns>成功加载的 Texture2D 实例，全部失败返回 null</returns>
        public static Task<Texture2D> LoadImageWithFallbackAsync(string primaryUrl, string secondaryUrl, int timeoutSeconds = 3)
        {
            return SurgicalLiveSDK.LoadImageWithFallbackAsync(primaryUrl, secondaryUrl, timeoutSeconds);
        }

        /// <summary>
        /// 清理图片内存缓存，释放所有已下载的 Texture2D 显存与内存资源
        /// </summary>
        public static void ClearCache()
        {
            SurgicalLiveSDK.ClearImageCache();
        }
    }
}
