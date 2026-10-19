using System;
using UnityEngine;

namespace SurgicalLivePlatform.Example.Runtime.UI
{
    /// <summary>
    /// 图案密码视图面板控制器 (Pattern Lock Panel View)
    /// 继承自 <see cref="PatternLockUI"/>，遵循 MVP 架构中的 Dumb View 契约规范。
    /// 专用于向场景及外部调用层暴露统一的图形锁视图组件类型，
    /// 提供与 <see cref="UserLoginUIController"/> 协同的自愈寻路、手势密码绘制及面板显隐流转。
    /// </summary>
    public class PatternLockPanelView : PatternLockUI
    {
    }
}
