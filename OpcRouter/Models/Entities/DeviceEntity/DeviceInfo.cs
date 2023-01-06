using OpcRouter.Models.Common;

namespace OpcRouter.Models.Entities.DeviceEntity;

public class DeviceInfo
{
    public Manufacture Manufacture { get; set; } // Manufacturer
    public string? Ip { get; set; }
    public string? Factory { get; set; } // 工厂
    public string? Workshop { get; set; } // 车间
    public string? Line { get; set; } // 产线
    public string? DeviceName { get; set; } // 设备名称
}