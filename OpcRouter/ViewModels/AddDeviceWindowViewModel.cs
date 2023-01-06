using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using OpcRouter.Models.Common;
using OpcRouter.Models.Entities.DeviceEntity;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace OpcRouter.ViewModels;

public class AddDeviceWindowViewModel:ViewModelBase
{
    [Reactive] public string Manufacture { get; set; }
    [Reactive] public string Ip { get; set; }
    [Reactive] public string Factory { get; set; }
    [Reactive] public string Workshop { get; set; }
    [Reactive] public string Line { get; set; }
    [Reactive] public string DeviceName { get; set; }
    [Reactive] public int Rate { get; set; }
    [Reactive] public string Tags { get; set; }
    public ReactiveCommand<Unit, Device> SaveCommand { get; }

    public AddDeviceWindowViewModel()
    {
        SaveCommand = ReactiveCommand.Create(() =>
        {
            var tags = Tags?.Split("\n").Select(tag => new Tag() {Id = tag}).ToList();
            
            var device = new Device
            {
                DeviceInfo = new()
                {
                    Manufacture = (Manufacture)Enum.Parse(typeof(Manufacture), Manufacture),
                    // Manufacture = Manufacture,
                    Ip = Ip,
                    Factory = Factory,
                    Workshop = Workshop,
                    Line = Line,
                    DeviceName = DeviceName,
                },
                Tags = tags,
                Rate = Rate
            };
            return device;
        });
    }
}