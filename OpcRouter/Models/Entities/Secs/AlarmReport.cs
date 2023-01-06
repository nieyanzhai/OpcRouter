using System;
using System.Collections.Generic;

namespace OpcRouter.Models.Entities.Secs;

public class AlarmReport
{
    private List<AlarmItem> AlarmItems { get; set; }
    
    public byte ALCD => GetDetails().alcd;
    public UInt32 ALID => GetDetails().alid;
    public string ALTX => GetDetails().altx;
    
    private (byte alcd, UInt32 alid, string altx) GetDetails()
    {
        byte binary = default;
        UInt32 u4 = default;
        string ascll = default;

        foreach (var alarmItem in AlarmItems)
        {
            if (alarmItem.Binary is not null)
            {
                binary = alarmItem.Binary[0];
                continue;
            }
            if (alarmItem.U4 is not null)
            {
                u4 = alarmItem.U4[0];
                continue;
            }
            if (alarmItem.ASCII is not null)
            {
                ascll = alarmItem.ASCII;
                continue;
            }
        }

        return (binary, u4, ascll);
    }
}

public class AlarmItem
{
    public List<byte> Binary { get; set; }
    public List<UInt32> U4 { get; set; }
    public string ASCII { get; set; }
}