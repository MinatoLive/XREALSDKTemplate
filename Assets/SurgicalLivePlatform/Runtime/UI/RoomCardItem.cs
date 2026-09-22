using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SurgicalLivePlatform.Runtime.DTOs;

namespace SurgicalLivePlatform.Runtime.UI
{
    public class RoomCardItem : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private RawImage coverImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text participantCountText;
        
        [Header("Status")]
        [SerializeField] private Image statusBackground;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Color colorCreated = Color.gray;
        [SerializeField] private Color colorLive = Color.green;
        [SerializeField] private Color colorEnded = Color.black;

        [Header("Creator")]
        [SerializeField] private RawImage creatorAvatar;
        [SerializeField] private TMP_Text creatorNameText;
        [SerializeField] private Texture2D defaultAvatar;
        [SerializeField] private Texture2D defaultCoverFallback;

        private RoomItem currentData;

        public void BindData(RoomItem data)
        {
            currentData = data;
            nameText.text = data.name;
            descriptionText.text = data.description;
            
            // Description CSS equivalent text-overflow: ellipsis is handled by TMP automatically
            // if configured in the inspector (Overflow: Ellipsis).

            participantCountText.text = $"<b>🔥 {data.participant_count} 人在会</b>";

            BindStatus(data.status);
            BindCreator(data.creator);
            LoadCoverImage(data.cover_image_url);
        }

        private void BindStatus(string status)
        {
            switch (status)
            {
                case "CREATED":
                    statusText.text = "未开始";
                    statusBackground.color = colorCreated;
                    break;
                case "LIVE":
                    statusText.text = "直播中";
                    statusBackground.color = colorLive;
                    break;
                case "ENDED":
                    statusText.text = "已结束";
                    statusBackground.color = colorEnded;
                    break;
            }
        }

        private void BindCreator(RoomCreator creator)
        {
            if (creator == null)
            {
                creatorNameText.text = "系统发起";
                creatorAvatar.texture = defaultAvatar;
                return;
            }

            creatorNameText.text = string.IsNullOrEmpty(creator.display_name) 
                ? creator.username 
                : creator.display_name;

            if (string.IsNullOrEmpty(creator.avatar_url))
            {
                creatorAvatar.texture = defaultAvatar;
            }
            else
            {
                // In production: LoadImageAsync(creator.avatar_url, creatorAvatar, defaultAvatar);
            }
        }

        public void UpdateCreatorInfo(UserInfo userInfo)
        {
            if (userInfo == null) return;
            
            creatorNameText.text = string.IsNullOrEmpty(userInfo.name) 
                ? userInfo.username 
                : userInfo.name;

            if (!string.IsNullOrEmpty(userInfo.avatar_url))
            {
                // In production: LoadImageAsync(userInfo.avatar_url, creatorAvatar, defaultAvatar);
            }
        }

        private void LoadCoverImage(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                coverImage.texture = defaultCoverFallback;
                return;
            }

            // In production: LoadImageAsync(url, coverImage, defaultCoverFallback);
        }

        public void OnHoverEnter()
        {
            // TooltipManager.Show(currentData.description);
        }
        
        public void OnHoverExit()
        {
            // TooltipManager.Hide();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (currentData != null)
            {
                if (RoomJoinController.Instance != null)
                {
                    RoomJoinController.Instance.JoinRoom(currentData.id);
                }
                else
                {
                    Debug.LogError("[RoomCardItem] 找不到 RoomJoinController 实例，无法发起入会请求。");
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            OnHoverEnter();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            OnHoverExit();
        }
    }
}
