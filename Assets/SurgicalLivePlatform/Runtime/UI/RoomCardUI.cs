using SurgicalLivePlatform.Runtime.Network;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;

namespace SurgicalLivePlatform.Runtime.UI
{
    /// <summary>
    /// 房间卡片 UI 视图层。负责将 API 返回的房间数据绑定到 UGUI 和 TMP 组件上。
    /// </summary>
    public class RoomCardUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text roomNameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text participantCountText;
        [SerializeField] private TMP_Text creatorNameText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private RawImage coverImage;
        
        [Header("Fallback Assets")]
        [SerializeField] private AssetReferenceTexture2D fallbackCoverRef;

        private UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<UnityEngine.Object> _fallbackLoadHandle;
        
        /// <summary>
        /// 绑定数据到 UI
        /// </summary>
        /// <param name="roomData">房间数据对象</param>
        public async void BindData(SurgicalLivePlatform.Runtime.DTOs.RoomItem roomData)
        {
            if (roomData == null)
            {
                Debug.LogWarning("[RoomCardUI] BindData called with null data.");
                return;
            }

            // 基础信息绑定
            if (roomNameText != null) roomNameText.text = string.IsNullOrEmpty(roomData.name) ? "未知房间" : roomData.name;
            if (descriptionText != null) descriptionText.text = string.IsNullOrEmpty(roomData.description) ? "暂无描述" : roomData.description;
            if (participantCountText != null) participantCountText.text = $"🔥 {roomData.participant_count} 人在会";
            
            // 状态标签渲染
            if (statusText != null)
            {
                string statusString = roomData.status;
                if (statusString == "CREATED") statusText.text = "未开始";
                else if (statusString == "LIVE") statusText.text = "直播中";
                else if (statusString == "ENDED") statusText.text = "已结束";
                else statusText.text = statusString;
            }

            // 发起者信息兜底处理
            if (creatorNameText != null)
            {
                if (roomData.creator != null)
                {
                    creatorNameText.text = !string.IsNullOrEmpty(roomData.creator.display_name) 
                        ? roomData.creator.display_name 
                        : (string.IsNullOrEmpty(roomData.creator.username) ? "未知医生" : roomData.creator.username);
                }
                else
                {
                    creatorNameText.text = "系统发起";
                }
            }
            Debug.Log($"异步加载封面图并处理失败回退:{roomData.cover_image_url}");
            // 异步加载封面图并处理失败回退
            await LoadCoverImageAsync(roomData.cover_image_url);
        }

        private async Task LoadCoverImageAsync(string url)
        {
            if (coverImage == null) return;
            
            // 默认清空图片
            coverImage.texture = null;
            coverImage.color = Color.clear;

            Texture2D tex = null;
            if (!string.IsNullOrEmpty(url))
            {
                tex = await ImageLoader.LoadImageAsync(url);
            }

            if (tex != null)
            {
                coverImage.texture = tex;
                coverImage.color = Color.white;
            }
            else
            {
                // 加载失败或 URL 为空，加载默认 fallback 图片
                LoadFallbackCoverImage();
            }
        }

        private void LoadFallbackCoverImage()
        {
            if (fallbackCoverRef == null || !fallbackCoverRef.RuntimeKeyIsValid())
            {
                Debug.LogWarning("[RoomCardUI] Fallback cover reference is missing or invalid.");
                return;
            }

            if (!_fallbackLoadHandle.IsValid())
            {
#if UNITY_EDITOR
                string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(fallbackCoverRef.AssetGUID);
                Texture2D tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (tex != null && coverImage != null)
                {
                    coverImage.texture = tex;
                    coverImage.color = Color.white;
                }
#else
                _fallbackLoadHandle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<UnityEngine.Object>(fallbackCoverRef.RuntimeKey);
                _fallbackLoadHandle.Completed += (handle) =>
                {
                    if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                    {
                        if (coverImage != null)
                        {
                            coverImage.texture = handle.Result as Texture2D;
                            coverImage.color = Color.white;
                        }
                    }
                };
#endif
            }
            else if (_fallbackLoadHandle.IsDone && _fallbackLoadHandle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
            {
                coverImage.texture = _fallbackLoadHandle.Result as Texture2D;
                coverImage.color = Color.white;
            }
        }

        /// <summary>
        /// 对象池回收时的清理工作
        /// </summary>
        public void Clear()
        {
            if (roomNameText != null) roomNameText.text = string.Empty;
            if (descriptionText != null) descriptionText.text = string.Empty;
            if (participantCountText != null) participantCountText.text = string.Empty;
            if (creatorNameText != null) creatorNameText.text = string.Empty;
            if (statusText != null) statusText.text = string.Empty;
            
            if (coverImage != null)
            {
                coverImage.texture = null;
                coverImage.color = Color.clear;
            }
        }

        private void OnDestroy()
        {
            if (_fallbackLoadHandle.IsValid())
            {
                Addressables.Release(_fallbackLoadHandle);
            }
        }
    }
}
