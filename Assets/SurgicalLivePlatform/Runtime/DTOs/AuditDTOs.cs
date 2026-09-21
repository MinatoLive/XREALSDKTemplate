using System;
using System.Collections.Generic;

namespace SurgicalLivePlatform.Runtime.DTOs
{
    #region 响应与模型 DTO (Response DTOs)

    [Serializable]
    public class AuditLog
    {
        public string id;
        public string user_id;
        public string action;
        public string resource;
        public string target_id;
        public string created_at;
    }

    [Serializable]
    public class AuditLogList
    {
        public List<AuditLog> list;
        public int total;
    }

    #endregion

    #region JSON 包装类 (Wrappers)

    [Serializable]
    public class AuditLogListWrapper
    {
        public int code;
        public string message;
        public AuditLogList data;
    }

    #endregion
}
