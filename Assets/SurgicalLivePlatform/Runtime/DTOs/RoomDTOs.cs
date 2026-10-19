using System;
using System.Collections.Generic;

namespace SurgicalLivePlatform.Runtime.DTOs
{
    #region 请求 DTO (Request DTOs)

    /// <summary>
    /// 创建房间请求
    /// </summary>
    [Serializable]
    public class CreateRoomRequest
    {
        public string name;
        public string description;
        public string cover_image_url;
        public string hospital_id;
        public string department_id;
        public List<string> members;
    }

    /// <summary>
    /// 更新房间请求 (支持部分更新)
    /// </summary>
    [Serializable]
    public class UpdateRoomRequest
    {
        public string name;
        public string description;
        public string cover_image_url;
        public string status;
    }

    /// <summary>
    /// 邀请用户入会请求
    /// </summary>
    [Serializable]
    public class InviteToRoomRequest
    {
        public List<string> user_ids;
        public List<string> device_ids;
    }

    #endregion

    #region 响应 DTO (Response DTOs)

    /// <summary>
    /// 房间创建者信息
    /// </summary>
    [Serializable]
    public class RoomCreator
    {
        public string id;
        public string username;
        public string display_name;
        public string avatar_url;
    }

    /// <summary>
    /// 房间预览图元数据结构
    /// </summary>
    [Serializable]
    public class RoomPreview
    {
        public string image_url;
        public string direct_url;
        public string preset_key;
        public string preset_title;
        public string preset_subtitle;
    }

    /// <summary>
    /// 手术室预设场景条目
    /// </summary>
    [Serializable]
    public class RoomPresetItem
    {
        public int id;
        public string key;
        public string title;
        public string subtitle;
        public string tag;
        public string badge;
        public string spec;
        public string procedure;
        public string image_url;
    }

    /// <summary>
    /// 房间详细信息条目
    /// </summary>
    [Serializable]
    public class RoomItem
    {
        public string id;
        public string name;
        public string description;
        public string cover_image_url;
        public RoomPreview preview;
        public string creator_id;
        public RoomCreator creator;
        public int participant_count;
        public string hospital_id;
        public string department_id;
        public string status;
        public string stream_type;
        public string started_at;
        public string ended_at;
        public string created_at;
        public string updated_at;
    }

    /// <summary>
    /// 房间列表数据
    /// </summary>
    [Serializable]
    public class RoomList
    {
        public List<RoomItem> list;
        public int total;
    }

    /// <summary>
    /// 加入房间返回的 Token 及配置信息
    /// </summary>
    [Serializable]
    public class JoinRoomResponse
    {
        [Serializable]
        public class RoomPermissions
        {
            public bool can_publish_video;
            public bool can_publish_audio;
            public bool can_subscribe;
        }

        [Serializable]
        public class RoomConfig
        {
            public string room_role;
            public RoomPermissions permissions;
        }

        public string livekit_url;
        public string livekit_token;
        public RoomConfig room_config;
    }

    #endregion

    #region JSON 包装类 (Wrappers)

    [Serializable]
    public class RoomListWrapper
    {
        public int code;
        public string message;
        public RoomList data;
    }

    [Serializable]
    public class RoomItemWrapper
    {
        public int code;
        public string message;
        public RoomItem data;
    }

    [Serializable]
    public class RoomPresetListWrapper
    {
        public int code;
        public string message;
        public List<RoomPresetItem> data;
    }

    [Serializable]
    public class JoinRoomResponseWrapper
    {
        public int code;
        public string message;
        public JoinRoomResponse data;
    }

    [Serializable]
    public class MemberListWrapper
    {
        public int code;
        public string message;
        public List<string> data;
    }

    #endregion
}
