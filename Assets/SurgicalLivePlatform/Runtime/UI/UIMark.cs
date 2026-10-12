using UnityEngine;

namespace SurgicalLivePlatform.Runtime.UI
{
    /// <summary>
    /// UI 节点标记组件，用于代码解耦查找
    /// 挂载此脚本并填入 markName 后，代码中即可通过 GetComponentInChildren 收集它
    /// </summary>
    public class UIMark : MonoBehaviour
    {
        [Tooltip("该节点的唯一标记名称，供代码做字典键值匹配")]
        public string markName;
    }
}
