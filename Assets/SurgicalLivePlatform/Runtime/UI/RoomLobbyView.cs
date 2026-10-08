using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using SurgicalLivePlatform.Runtime;
using SurgicalLivePlatform.Runtime.DTOs;

namespace SurgicalLivePlatform.Runtime.UI
{
    public class RoomLobbyView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentPanel;
        [SerializeField] private GameObject roomCardPrefab;
        [SerializeField] private Button immediateLiveButton;
        [SerializeField] private Button scheduledLiveButton;
       // [SerializeField] private GameObject loadingIndicator;
        [SerializeField] private RoomModalView roomModalView;

        [Header("Settings")]
        [SerializeField] private float loadMoreThreshold = 0.2f; // 80% scroll (1 - 0.8)
        [Tooltip("开启后将跳过鉴权，强制显示创建房间按钮（仅供快速测试）")]
        [SerializeField] private bool quickTestMode = false;

        private List<RoomCardItem> cardPool = new List<RoomCardItem>();
        private List<RoomCardItem> activeCards = new List<RoomCardItem>();

        private int currentPage = 1;
        private bool isLoading = false;
        private bool hasMoreData = true;
        private const int PAGE_SIZE = 20;

        private void Start()
        {
            // SDK Initialization is expected to be done earlier in the app flow (e.g. login)
            scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
            
            if (immediateLiveButton != null)
            {
                immediateLiveButton.onClick.AddListener(OnImmediateLiveClicked);
            }
            if (scheduledLiveButton != null)
            {
                scheduledLiveButton.onClick.AddListener(OnScheduledLiveClicked);
            }
            
            CheckCreatePermission();

            //RefreshRooms();
        }

        private async void CheckCreatePermission()
        {
            bool hasPermission = false;
            
            if (quickTestMode)
            {
                hasPermission = true;
                Debug.Log("[RoomLobbyView] 快速测试模式已开启，跳过 'room.create' 鉴权。");
            }
            else
            {
                try
                {
                    if (SurgicalLiveSDK.Auth == null) return;
                    string userId = SurgicalLiveSDK.Auth.CurrentUserId;
                    if (!string.IsNullOrEmpty(userId))
                    {
                        var userData = await SurgicalLiveSDK.Storage.GetUserAsync(userId);
                        if (userData != null && userData.user != null && userData.user.permissions != null)
                        {
                            var permissions = new List<string>(userData.user.permissions);
                            if (permissions.Contains("room.create"))
                            {
                                hasPermission = true;
                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[RoomLobbyView] Failed to check permissions: {e.Message}");
                }
            }
            
            if (immediateLiveButton != null) immediateLiveButton.gameObject.SetActive(hasPermission);
            if (scheduledLiveButton != null) scheduledLiveButton.gameObject.SetActive(hasPermission);
        }
        
        private void EnsureRoomModalView()
        {
            if (roomModalView == null)
            {
                roomModalView = Resources.FindObjectsOfTypeAll<RoomModalView>().FirstOrDefault();
                if (roomModalView == null)
                {
                    Debug.LogError("[RoomLobbyView] 找不到 RoomModalView 组件！");
                }
            }
        }

        private void OnImmediateLiveClicked()
        {
            EnsureRoomModalView();
            if (roomModalView == null) return;
            try { roomModalView.Open(RoomCreateMode.Immediate); }
            catch (System.Exception e) { Debug.LogError($"[RoomLobbyView] 打开立即直播面板异常: {e.Message}"); }
        }

        private void OnScheduledLiveClicked()
        {
            EnsureRoomModalView();
            if (roomModalView == null) return;
            try { roomModalView.Open(RoomCreateMode.Scheduled); }
            catch (System.Exception e) { Debug.LogError($"[RoomLobbyView] 打开预约直播面板异常: {e.Message}"); }
        }

        public async void RefreshRooms()
        {
            if (isLoading) return;

            currentPage = 1;
            hasMoreData = true;
            ClearActiveCards();
            
          //  loadingIndicator.SetActive(true);
            isLoading = true;

            try
            {
                var response = await SurgicalLiveSDK.Auth.GetRoomInfosAsync(currentPage, PAGE_SIZE, null, null, null);
                RenderRooms(response.list);
                
                if (response.list.Count < PAGE_SIZE)
                    hasMoreData = false;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Refresh failed: {e.Message}");
            }
            finally
            {
                isLoading = false;
              //  loadingIndicator.SetActive(false);
            }
        }

        public async void LoadNextPage()
        {
            if (isLoading || !hasMoreData) return;

            currentPage++;
            isLoading = true;
          //  loadingIndicator.SetActive(true);

            try
            {
                var response = await SurgicalLiveSDK.Auth.GetRoomInfosAsync(currentPage, PAGE_SIZE, null, null, null);
                RenderRooms(response.list);

                if (response.list.Count < PAGE_SIZE)
                    hasMoreData = false;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Load next page failed: {e.Message}");
                currentPage--; // Revert page on failure
            }
            finally
            {
                isLoading = false;
             //   loadingIndicator.SetActive(false);
            }
        }

        private void OnScrollValueChanged(Vector2 pos)
        {
            if (pos.y <= loadMoreThreshold && !isLoading && hasMoreData)
            {
                LoadNextPage();
            }
        }

        private void RenderRooms(List<RoomItem> rooms)
        {
            if (rooms == null) return;
            foreach (var room in rooms)
            {
                var card = GetCardFromPool();
                card.BindData(room);
                card.gameObject.SetActive(true);
                activeCards.Add(card);

                if (!string.IsNullOrEmpty(room.creator_id))
                {
                    FetchAndUpdateCreatorInfo(card, room.creator_id);
                }
            }
        }

        private async void FetchAndUpdateCreatorInfo(RoomCardItem card, string creatorId)
        {
           // Debug.Log($"[FetchAndUpdateCreatorInfo] Calling GetUserByIdAsync for creatorId: {creatorId}");
            try
            {
                var creatorInfo = await SurgicalLiveSDK.Auth.GetUserByIdAsync(creatorId);
                if (creatorInfo != null)
                {
                    Debug.Log($"[Room Creator Info] ID: {creatorInfo.id}, Name: {creatorInfo.name}, Username: {creatorInfo.username}, Phone: {creatorInfo.phone}, Hospital ID: {creatorInfo.hospital_id}, Department ID: {creatorInfo.department_id}");
                    card.UpdateCreatorInfo(creatorInfo);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to fetch creator info for {creatorId}: {e.Message}");
            }
        }

        private RoomCardItem GetCardFromPool()
        {
            foreach (var card in cardPool)
            {
                if (!card.gameObject.activeSelf)
                {
                    card.transform.SetAsLastSibling();
                    return card;
                }
            }

            var newObj = Instantiate(roomCardPrefab, contentPanel);
            var newCard = newObj.GetComponent<RoomCardItem>();
            cardPool.Add(newCard);
            return newCard;
        }

        private void ClearActiveCards()
        {
            foreach (var card in activeCards)
            {
                card.gameObject.SetActive(false);
            }
            activeCards.Clear();
        }
    }
}
