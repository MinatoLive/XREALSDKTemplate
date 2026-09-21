using System;
using System.Collections.Generic;

namespace SurgicalLivePlatform.Runtime.DTOs
{
    #region 请求 DTO (Request DTOs)

    /// <summary>
    /// 修改成员权限请求
    /// </summary>
    [Serializable]
    public class UpdatePermissionRequest
    {
        public bool can_publish_audio;
        public bool can_annotate;
    }

    /// <summary>
    /// 踢出成员请求
    /// </summary>
    [Serializable]
    public class KickMemberRequest
    {
        public string user_id;
    }

    /// <summary>
    /// 发送聊天消息请求
    /// </summary>
    [Serializable]
    public class SendMessageRequest
    {
        public string content;
    }

    /// <summary>
    /// 发送标注数据请求
    /// </summary>
    [Serializable]
    public class SendAnnotationRequest
    {
        public string annotation_data; 
    }

    #endregion

    #region 响应与模型 DTO (Response DTOs)

    [Serializable]
    public class ChatMessage
    {
        public string id;
        public string user_id;
        public string content;
        public string created_at;
    }

    [Serializable]
    public class AnnotationData
    {
        public string id;
        public string user_id;
        public string annotation_data;
        public string created_at;
    }

    #endregion

    #region JSON 包装类 (Wrappers)

    [Serializable]
    public class ChatMessageListWrapper
    {
        public int code;
        public string message;
        public List<ChatMessage> data;
    }

    [Serializable]
    public class AnnotationDataListWrapper
    {
        public int code;
        public string message;
        public List<AnnotationData> data;
    }

    #endregion
}
