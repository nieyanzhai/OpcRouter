using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading.Tasks;
using ClassLibrary.CommonServices.OpcUaService;
using ClassLibrary.CommonServices.PingService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpcRouter.Models.Entities.DeviceEntity;

namespace OpcRouter.Services.BackgroundWorkers;

public class DeviceAccessService
{
    private bool _cancel = false;
    private readonly ILogger<DeviceAccessService> _logger;
    private readonly IOpcUaClientService _opcUaClientService;
    private readonly IPingService _pingService;
    public int Rate { get; set; }
    public Device Device { get; set; }

    

    public DeviceAccessService(int rate, Device device)
    {
        _logger = App.Host.Services.GetRequiredService<ILogger<DeviceAccessService>>();
        _opcUaClientService = App.Host.Services.GetRequiredService<IOpcUaClientService>();
        _pingService = App.Host.Services.GetRequiredService<IPingService>();
        Rate = rate;
        Device = device;
    }


    public async Task Start()
    {
        while (!_cancel)
        {
            await Task.Delay(Rate);
            StartDA();
        }

        _cancel = false;
    }

    private void StartDA()
    {
        if (_opcUaClientService.Session is null || !_opcUaClientService.Session.Connected) return;

        if (Device.DeviceInfo?.Ip is { } && !PingDevice(Device.DeviceInfo?.Ip))
        {
            _logger.LogError("can not ping ip({Ip}), device name: {DeviceName}", Device.DeviceInfo.Ip,
                Device.DeviceInfo.DeviceName);
            return;
        }

        if (!ReadOpcServerValues(Device)) return;

        // todo: publish to mes or sql server
        _logger.LogInformation(JsonSerializer.Serialize(Device));
    }

    private bool ReadOpcServerValues(Device device)
    {
        if (device.Tags == null) return false;

        var nodeIdStrings = new List<string>();
        var descriptions = new List<string>();


        nodeIdStrings.AddRange(device.Tags.Select(tag => tag.Id));

        // Read signal values
        List<object> values;
        try
        {
            values = _opcUaClientService.ReadValues(nodeIdStrings);
        }
        catch (Exception e)
        {
            _logger.LogError("Get data error! Device Name:{DeviceName}", device.DeviceInfo?.DeviceName);
            _logger.LogError(e.Message);
            return false;
        }

        for (var i = 0; i < values.Count; i += 1)
        {
            device.Tags[i].Value = values[i].ToString();
        }


        device.CreatedDate = DateTimeOffset.Now;

        // 扩散炉-红太阳
        // if (device.DeviceInfo.Ip == "10.247.81.11" ||
        //     device.DeviceInfo.Ip == "10.247.81.12" ||
        //     device.DeviceInfo.Ip == "10.247.81.21" ||
        //     device.DeviceInfo.Ip == "10.247.81.22")
        // {
        //     if (!getDeviceDatetime(device)) return false;
        // }
        // else device.DeviceDate = DateTimeOffset.MinValue;

        return true;
    }
    
    
    private bool PingDevice(string ip)
    {
        if (string.IsNullOrEmpty(ip)) return false;

        var numberStrings = ip.Trim().Split(".");
        foreach (var numberString in numberStrings)
        {
            try
            {
                var number = int.Parse(numberString);
                if (number is < 0 or > 255) return false;
            }
            catch (Exception)
            {
                _logger.LogError($"Invalid Device Ip({ip})");
            }
        }

        return _pingService.Send(ip).Status == IPStatus.Success;
    }
    

    public void Stop() => _cancel = true;
}