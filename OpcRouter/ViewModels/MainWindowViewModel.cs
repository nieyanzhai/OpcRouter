using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using OpcRouter.Models.Entities.DeviceEntity;
using OpcRouter.Services.BackgroundWorkers;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace OpcRouter.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private Dictionary<string, DeviceAccessService> _deviceAccessServices = new();

    public ObservableCollection<string> Devices { get; set; } = new();
    [Reactive] public string SelectedDevice { get; set; }

    public ReactiveCommand<Unit, Unit> StartCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<Unit, Unit> AddDeviceCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteDeviceCommand { get; }

    public Interaction<Unit, Device> AddDeviceInteraction { get; } = new();
    public Interaction<string, bool> DeleteDeviceInteraction { get; } = new();

    public MainWindowViewModel()
    {
        DeleteDeviceCommand = ReactiveCommand.CreateFromTask(DeleteDevice);

        AddDeviceCommand = ReactiveCommand.CreateFromTask(AddDevice);

        StartCommand = ReactiveCommand.Create(Start);
        StopCommand = ReactiveCommand.Create(Stop);
    }

    private async Task DeleteDevice()
    {
        if (string.IsNullOrWhiteSpace(SelectedDevice)) return;

        var result = await DeleteDeviceInteraction.Handle(SelectedDevice);

        if (result)
        {
            _deviceAccessServices[SelectedDevice].Stop();
            _deviceAccessServices.Remove(SelectedDevice);
            Devices.Remove(SelectedDevice);
        }
    }

    private async Task AddDevice()
    {
        var result = await AddDeviceInteraction.Handle(Unit.Default);
        if (result is not null)
        {
            _deviceAccessServices.Add(result.DeviceInfo.DeviceName, new DeviceAccessService(result.Rate, result));
            Devices.Add(result.DeviceInfo.DeviceName);
        }
    }


    private void Start()
    {
        if (!_deviceAccessServices.ContainsKey(SelectedDevice)) return;
        var das = _deviceAccessServices[SelectedDevice];
        Task.Run(async () => await das.Start());
    }

    private void Stop()
    {
        if (!_deviceAccessServices.ContainsKey(SelectedDevice)) return;
        var das = _deviceAccessServices[SelectedDevice];
        das.Stop();
    }
}