using System;

namespace OpcRouter.Models.Common;

public abstract class EntityBase
{
    public DateTimeOffset CreatedDate { get; set; } // 更新日期时间
    public DateTimeOffset? DeviceDate { get; set; } // 设备时间

    protected EntityBase()
    {
        CreatedDate = DateTimeOffset.Now;
    }
}