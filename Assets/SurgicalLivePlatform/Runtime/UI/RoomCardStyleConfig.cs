using UnityEngine;
using System.Collections.Generic;

namespace SurgicalLivePlatform.Runtime.UI
{
    [CreateAssetMenu(fileName = "RoomCardStyleConfig", menuName = "SurgicalLive/RoomCardStyleConfig")]
    public class RoomCardStyleConfig : ScriptableObject
    {
        [Tooltip("默认缺省样式")]
        public RoomStatusUIStyle defaultStyle = new RoomStatusUIStyle { status = "DEFAULT" };
        
        [Tooltip("各状态样式列表")]
        public List<RoomStatusUIStyle> statusStyles = new List<RoomStatusUIStyle>();
    }
}
