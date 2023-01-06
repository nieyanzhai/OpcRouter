using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using OpcRouter.Models.Common;
using OpcRouter.Models.Entities.DeviceEntity;
using OpcRouter.Services.BackgroundWorkers;
using OpcRouter.ViewModels;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace OpcRouter.PageViewModels;

public class HomeViewModel:ViewModelBase
{
    private Dictionary<string, DeviceAccessService> _deviceAccessServices = new();
    public ObservableCollection<DeviceInfo> DeviceInfos { get; } = new();
    [Reactive] public DeviceInfo SelectedDevice { get; set; }

    public ReactiveCommand<Unit, Unit> StartCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<Unit, Unit> AddDeviceCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteDeviceCommand { get; }
    public Interaction<Unit, Device> AddDeviceInteraction { get; } = new();
    public Interaction<string, bool> DeleteDeviceInteraction { get; } = new();

    public HomeViewModel()
    {
        DeleteDeviceCommand = ReactiveCommand.CreateFromTask(DeleteDevice);

        AddDeviceCommand = ReactiveCommand.CreateFromTask(AddDevice);

        StartCommand = ReactiveCommand.Create(Start);
        StopCommand = ReactiveCommand.Create(Stop);
    }

    private async Task DeleteDevice()
    {
        if (SelectedDevice is null) return;

        var result = await DeleteDeviceInteraction.Handle(SelectedDevice.DeviceName);

        if (result)
        {
            _deviceAccessServices[SelectedDevice.DeviceName].Stop();
            _deviceAccessServices.Remove(SelectedDevice.DeviceName);
            DeviceInfos.Remove(DeviceInfos.First(d => d.DeviceName == SelectedDevice.DeviceName));
        }
    }

    private async Task AddDevice()
    {
        var result = await AddDeviceInteraction.Handle(Unit.Default);
        if (result is not null)
        {
            _deviceAccessServices.Add(result.DeviceInfo.DeviceName, new DeviceAccessService(result.Rate, result));
            DeviceInfos.Add(result.DeviceInfo);
        }
    }


    private void Start()
    {
        if (!_deviceAccessServices.ContainsKey(SelectedDevice.DeviceName)) return;
        var das = _deviceAccessServices[SelectedDevice.DeviceName];
        Task.Run(async () => await das.Start());
    }

    private void Stop()
    {
        if (!_deviceAccessServices.ContainsKey(SelectedDevice.DeviceName)) return;
        var das = _deviceAccessServices[SelectedDevice.DeviceName];
        das.Stop();
    }
}