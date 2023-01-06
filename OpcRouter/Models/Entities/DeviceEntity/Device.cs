using System.Collections.Generic;
using OpcRouter.Models.Common;

namespace OpcRouter.Models.Entities.DeviceEntity;

public class Device : EntityBase
{
    public int Rate { get; set; }
    public DeviceInfo? DeviceInfo { get; set; }
    public List<Tag>? Signals { get; set; }
    public List<Tag>? Tags { get; set; }
}