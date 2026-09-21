using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace SurgicalLivePlatform.Runtime.UI
{
    /// <summary>
    /// 轻量级的网络图片异步加载工具类，带基础的内存缓存功能。
    /// </summary>
    public static class ImageLoader
    {
        private static readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>();

        /// <summary>
        /// 异步加载图片并返回 Texture2D。如果缓存中已存在，则直接返回。
        /// </summary>
        /// <param name="url">图片的绝对或相对网络地址</param>
        /// <returns>加载成功的 Texture2D，若加载失败则返回 null</returns>
        public static async Task<Texture2D> LoadImageAsync(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return null;
            }

            if (_textureCache.TryGetValue(url, out var cachedTexture))
            {
                if (cachedTexture != null)
                {
                    return cachedTexture;
                }
                else
                {
                    _textureCache.Remove(url);
                }
            }

            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
            {
                var asyncOp = request.SendWebRequest();

                while (!asyncOp.isDone)
                {
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.ConnectionError ||
                    request.result == UnityWebRequest.Result.DataProcessingError ||
                    request.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogWarning($"[ImageLoader] Failed to load image from {url}: {request.error}");
                    return null;
                }

                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                if (texture != null)
                {
                    _textureCache[url] = texture;
                }
                return texture;
            }
        }

        /// <summary>
        /// 清理缓存，释放所有 Texture2D 资源
        /// </summary>
        public static void ClearCache()
        {
            foreach (var kvp in _textureCache)
            {
                if (kvp.Value != null)
                {
                    Object.Destroy(kvp.Value);
                }
            }
            _textureCache.Clear();
        }
    }
}
