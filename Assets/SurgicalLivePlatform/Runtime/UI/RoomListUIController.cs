using SurgicalLivePlatform.Runtime.Auth;
using SurgicalLivePlatform.Runtime.Network;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using UnityEngine.ResourceManagement.AsyncOperations;
using SurgicalLivePlatform.Runtime.DTOs;

namespace SurgicalLivePlatform.Runtime.UI
{
    /// <summary>
    /// 房间大厅 UI 控制器，管理 API 数据拉取和卡片的对象池渲染。
    /// </summary>
    public class RoomListUIController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AssetReferenceGameObject roomCardPrefabRef;
        [SerializeField] private Transform contentContainer;
        

        // 对象池与状态
        private ObjectPool<RoomCardUI> _cardPool;
        private List<RoomCardUI> _activeCards = new List<RoomCardUI>();
        private GameObject _loadedPrefab;
        private bool _isRefreshing = false;

        /// <summary>
        /// 依赖注入方法，由外部容器或启动器调用
        /// </summary>

        private void InitPool()
        {
            _cardPool = new ObjectPool<RoomCardUI>(
                createFunc: CreateCardInstance,
                actionOnGet: OnGetCard,
                actionOnRelease: OnReleaseCard,
                actionOnDestroy: OnDestroyCard,
                collectionCheck: false,
                defaultCapacity: 10,
                maxSize: 50
            );
        }

        private RoomCardUI CreateCardInstance()
        {
            if (_loadedPrefab == null)
            {
                Debug.LogError("[RoomListUIController] Prefab not loaded yet!");
                return null;
            }
            GameObject go = Instantiate(_loadedPrefab, contentContainer);
            RoomCardUI card = go.GetComponent<RoomCardUI>();
            return card;
        }

        private void OnGetCard(RoomCardUI card)
        {
            if (card != null)
            {
                card.gameObject.SetActive(true);
            }
        }

        private void OnReleaseCard(RoomCardUI card)
        {
            if (card != null)
            {
                card.Clear();
                card.gameObject.SetActive(false);
            }
        }

        private void OnDestroyCard(RoomCardUI card)
        {
            if (card != null)
            {
                Destroy(card.gameObject);
            }
        }

        public async Task RefreshRoomList(RoomList roomInfo)
        {
            if (_isRefreshing) return;
            _isRefreshing = true;
            if (_cardPool == null) InitPool();
            try
            {
                // 如果预制体还未加载，通过 Addressables 异步加载
                if (_loadedPrefab == null)
                {
#if UNITY_EDITOR
                    string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(roomCardPrefabRef.AssetGUID);
                    _loadedPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
#else
                    var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<UnityEngine.Object>(roomCardPrefabRef.RuntimeKey);
                    await handle.Task;
                    if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                    {
                        _loadedPrefab = handle.Result as GameObject;
                    }
                    else
                    {
                        Debug.LogError("[RoomListUIController] Failed to load RoomCard Prefab via Addressables.");
                        return;
                    }
#endif
                }

                // 归还所有当前活跃的卡片到对象池
                foreach (var card in _activeCards)
                {
                    _cardPool.Release(card);
                }
                _activeCards.Clear();

                // 根据数据重新从对象池获取并渲染
                if (roomInfo != null && roomInfo.list != null)
                {
                    foreach (var item in roomInfo.list)
                    {
                        RoomCardUI newCard = _cardPool.Get();
                        if (newCard != null)
                        {
                            newCard.BindData(item);
                            _activeCards.Add(newCard);
                            // 保证 UI 排序按照数据顺序
                            newCard.transform.SetAsLastSibling();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RoomListUIController] Failed to refresh room list: {ex.Message}");
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private void OnDestroy()
        {
            _cardPool?.Dispose();
            if (roomCardPrefabRef != null && roomCardPrefabRef.IsValid())
            {
                roomCardPrefabRef.ReleaseAsset();
            }
        }
    }
}
