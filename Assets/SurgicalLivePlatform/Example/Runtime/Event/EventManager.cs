using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurgicalLivePlatform.Example.Runtime.Event
{
    /// <summary>
    /// 全局事件管理器，使用 C# Action 委托实现发布-订阅模式。
    /// 统一管理 UI 与系统事件的生命周期。
    /// </summary>
    public static class EventManager
    {
        private static readonly Dictionary<EventId, Action> _eventDict = new Dictionary<EventId, Action>();

        /// <summary>
        /// 注册无参数事件监听
        /// </summary>
        public static void AddListener(EventId eventId, Action listener)
        {
            if (!_eventDict.ContainsKey(eventId))
            {
                _eventDict[eventId] = null;
            }
            _eventDict[eventId] += listener;
        }

        /// <summary>
        /// 注销无参数事件监听
        /// </summary>
        public static void RemoveListener(EventId eventId, Action listener)
        {
            if (_eventDict.ContainsKey(eventId))
            {
                _eventDict[eventId] -= listener;
                if (_eventDict[eventId] == null)
                {
                    _eventDict.Remove(eventId);
                }
            }
        }

        /// <summary>
        /// 广播无参数事件
        /// </summary>
        public static void Broadcast(EventId eventId)
        {
            if (_eventDict.TryGetValue(eventId, out Action callback))
            {
                if (callback != null)
                {
                    callback.Invoke();
                }
            }
            else
            {
                Debug.LogWarning($"[EventManager] Broadcast Event {eventId} but no listener found.");
            }
        }
        
        /// <summary>
        /// 清理所有事件（例如场景切换时）
        /// </summary>
        public static void Clear()
        {
            _eventDict.Clear();
        }
    }
}
