using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ClassLibrary.CommonServices.OpcUaService;
using ClassLibrary.CommonServices.PingService;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;
using OpcRouter.Extensions;
using OpcRouter.Messages.Commands;
using OpcRouter.Models.Entities.DeviceEntity;
using OpcRouter.Models.Entities.Mes;
using OpcRouter.Services.FileService;
using Polly;
using Polly.Retry;
using RestSharp;

namespace OpcRouter.Services.BackgroundWorkers;

public class OpcProcessingWorker : BackgroundService
{
    private readonly ILogger<OpcProcessingWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IOpcUaClientService _opcClientService;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly IOpcFileService _opcFileService;
    private readonly IPingService _pingService;
    private readonly IMediator _mediator;
    private readonly ICommonFileService _commonFileService;

    private static readonly List<Device> Devices = new();
    private static Subscription? _subscription;
    private static int _samplingInterval;
    private readonly object _devicesLock;

    private readonly MesEndpoint _mesEndpoint;
    private readonly RetryPolicy _reconnectPolicy;

    public OpcProcessingWorker(
        ILogger<OpcProcessingWorker> logger,
        IConfiguration configuration,
        IOpcUaClientService opcClientService,
        IHostApplicationLifetime hostApplicationLifetime,
        IOpcFileService opcFileService,
        IPingService pingService,
        IMediator mediator,
        ICommonFileService commonFileService)
    {
        _logger = logger;
        _configuration = configuration;
        _opcClientService = opcClientService;
        _hostApplicationLifetime = hostApplicationLifetime;
        _opcFileService = opcFileService;
        _pingService = pingService;
        _mediator = mediator;
        _commonFileService = commonFileService;

        _samplingInterval = _configuration.GetValue<int>("DefaultSettings:samplingInterval");
        _devicesLock = new object();
        _opcClientService.ItemChangedNotification += On_OpcClientService_ItemChangedNotificationAsync;

        _mesEndpoint = _configuration.GetSection("MesEndpoint").Get<MesEndpoint>();


        // policy for reconnecting to the server
        _reconnectPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetry(new[]
                {
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(3),
                },
                (exception, timeSpan) =>
                {
                    _logger.LogWarning($"Reconnecting to the server. Reason: {exception.Message}");
                });
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("OpcProcessingWorker is starting");
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(_samplingInterval, cancellationToken);

            // if (this.serviceControllerService.Status != ServiceControllerStatus.Running) continue;

            if (_opcClientService.Session is null || !_opcClientService.Session.Connected)
            {
                SubscriptionReset();
                continue;
            }


            lock (_devicesLock)
            {
                try
                {
                    if (!Devices.Any() && !ConvertTagFilesToDevices()) _hostApplicationLifetime.StopApplication();

                    foreach (var device in Devices)
                    {
                        if (device.DeviceInfo?.Ip is { } && !PingDevice(device.DeviceInfo?.Ip))
                        {
                            _logger.LogError(
                                $"can not ping ip({device.DeviceInfo.Ip}), device name: {device.DeviceInfo.DeviceName}");
                            continue;
                        }

                        // if (!ReadOpcServerValues(device)) continue;
                        //
                        // const string topic = "test";
                        // PublishToKafka(topic, device, TimeSpan.FromSeconds(10));
                    }

                    if (_subscription is not null) continue;

                    // Initialize for subscription
                    InitializeSubscription(_samplingInterval);
                    // _isSubscriptionResetting = false;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message, ex);
                }
            }
        }
    }

    private void SubscriptionReset()
    {
        _logger.LogInformation("subscription is resetting...");
        if (_subscription == null) return;
        if (_opcClientService.Session is { Connected: true } &&
            _opcClientService.Session.Subscriptions.Contains(_subscription))
        {
            _opcClientService.RemoveSubscription(_subscription);
            _opcClientService.ItemChangedNotification -= On_OpcClientService_ItemChangedNotificationAsync;
        }

        _subscription = null;
    }

    private void InitializeSubscription(int samplingInterval)
    {
        _logger.LogInformation("subscription is initializing...");
        _subscription = _opcClientService.Subscribe(samplingInterval);

        // Add items to subscription
        foreach (var tag in Devices.Where(device => device.Signals != null).SelectMany(device => device.Signals))
        {
            // MonitoredItems.Add(
            _opcClientService.AddMonitoredItem(
                subscription: _subscription,
                nodeIdString: tag.Id,
                itemName: tag.Id,
                samplingInterval: samplingInterval
            );
        }
    }

    private void On_OpcClientService_ItemChangedNotificationAsync(
        MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
    {
        if (e.NotificationValue is not MonitoredItemNotification notification) return;

        _logger.LogInformation($"\nname: {monitoredItem.DisplayName}\n" +
                               $"value: {notification.Value.Value}\n" +
                               $"timestamp: {notification.Value.SourceTimestamp.ToLocalTime().ToString()}\n" +
                               $"quality: {notification.Value.StatusCode}");

        if (notification.Value.StatusCode.ToString() == "Bad")
        {
            _logger.LogError($"TagName: {monitoredItem.DisplayName}, Quality: Bad");
            return;
        }

        if (notification.Value.Value is null)
        {
            _logger.LogError($"TagName: {monitoredItem.DisplayName} has null value");
            return;
        }

        if (!Convert.ToBoolean(notification.Value.Value)) return;

        // Get Notification Device
        var notificationDevice = GetNotificationDevice(monitoredItem.DisplayName);
        if (notificationDevice is null)
        {
            _logger.LogError($"can not find device with tag name: {monitoredItem.DisplayName}");
            return;
        }

        // Get tag values
        // if (!ReadOpcServerValues(notificationDevice)) return;
        // notificationDevice.Signals.First().Value = notification.Value.Value.ToString();

        // Publish to kafka or Post to mes
        try
        {
            if (notificationDevice.DeviceInfo?.Ip is { } && !PingDevice(notificationDevice.DeviceInfo?.Ip))
            {
                _logger.LogError(
                    $"can not ping ip({notificationDevice.DeviceInfo.Ip}), device name: {notificationDevice.DeviceInfo.DeviceName}");
                return;
            }

            if (!ReadOpcServerValues(notificationDevice)) return;

            // const string topic = "test";
            // PublishToKafka(topic, device, TimeSpan.FromSeconds(10));

            var response = PostToMes(notificationDevice);

            var msg = string.Empty;
            if (response.IsSuccessful)
            {
                msg = $"Post to mes success, device: {notificationDevice.DeviceInfo.DeviceName}";
                _logger.LogInformation("Post to mes success, device: {@Device}", notificationDevice);
                // WeakReferenceMessenger.Default.Send(new CommonMessageCommand(msg) { IsError = false });
                return;
            }

            msg = $"Post to mes failed, device name: {notificationDevice.DeviceInfo.DeviceName}\n" +
                  $"status code: {response.StatusCode}\n" +
                  $"content: {response.Content}\n" +
                  $"error message: {response.ErrorMessage}";
            _logger.LogError(msg);
            // WeakReferenceMessenger.Default.Send(new CommonMessageCommand(msg) { IsError = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            // WeakReferenceMessenger.Default.Send(new CommonMessageCommand(ex.Message) { IsError = true });
        }
    }

    private RestResponse PostToMes(Device notificationDevice)
    {
        // add body
        var transData = ConvertDeviceToMessageBody(notificationDevice);
        const string userId = "SYSTEM";
        var facilityId = "M" + notificationDevice.DeviceInfo.Workshop;
        var body = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + "\n" +
                   "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">" + "\n" +
                   "<soap:Body>" + "\n" +
                   $"<{_mesEndpoint.SoapAction}>" + "\n" +
                   "<facilityId>" + facilityId + "</facilityId>" + "\n" +
                   "<userId>" + userId + "</userId>" + "\n" +
                   "<transData>" + transData + "</transData>" + "\n" +
                   $"</{_mesEndpoint.SoapAction}>" + "\n" +
                   "</soap:Body>" + "\n" +
                   "</soap:Envelope>";


        // client
        var options = new RestClientOptions(_mesEndpoint.Url)
        {
            MaxTimeout = _mesEndpoint.Timeout,
            ThrowOnAnyError = true
        };
        var client = new RestClient(options);
        var request = new RestRequest()
            .AddHeader("Content-Type", "application/xml")
            .AddHeader("soapAction", _mesEndpoint.SoapAction)
            .AddXmlBody(body);

        return _reconnectPolicy.Execute(RestResponse() =>
            client.PostAsync(request).GetAwaiter().GetResult()
        );
    }

    private string ConvertDeviceToMessageBody(Device notificationDevice)
    {
        var values = new JsonArray();
        notificationDevice.Tags.Select(tag => new JsonObject()
        {
            { "name", tag.Name },
            { "value", tag.Value }
        }).ToList().ForEach(values.Add);

        var json = new JsonObject
        {
            { "ts", DateTime.Now.ToTimestamp(TimeZoneInfo.Local) },
            {
                "device", new JsonObject()
                {
                    { "name", notificationDevice.DeviceInfo.DeviceName.Split(".")[0] },
                    { "floor", notificationDevice.DeviceInfo.DeviceName.Split(".")[1] },
                    { "manufacture", notificationDevice.DeviceInfo.Manufacture.ToString() }
                }
            },
            { "values", values }
        };

        return json.ToString();
    }

    // private void NotifyPlcTriggered(Device notificationDevice)
    // {
    //     if (notificationDevice.Signals == null) return;
    //     var plcTriggered = new PlcTriggeredCommand()
    //     {
    //         DeviceName = notificationDevice.DeviceInfo?.DeviceName,
    //         TriggeredTime = DateTime.Now,
    //         TriggeredValue = notificationDevice.Signals.First().Value
    //     };
    //
    //     // notify
    //     Task.Run(async () => await _mediator.Publish(plcTriggered)).Wait(TimeSpan.FromMilliseconds(1000));
    // }

    private Device GetNotificationDevice(string notificationTagId)
    {
        lock (_devicesLock)
        {
            return Devices.Find(d =>
                       d.Signals != null && d.Signals.Any(signal => signal.Id == notificationTagId)) ??
                   throw new InvalidOperationException();
        }
    }

    private void PublishToKafka(string topic, Device device, TimeSpan timeSpan)
    {
        var command = new PublishToKafkaCommand
        {
            Topic = topic,
            Device = device,
            Timeout = timeSpan
        };
        Task.Run(async () => await _mediator.Send(command));
    }

    private bool CheckDeviceEnabled(string tag)
    {
        return bool.Parse(_opcClientService.ReadValue($"ns=2;s={tag}").ToString());
    }

    private bool ConvertTagFilesToDevices()
    {
        if (Devices.Count > 0) Devices.Clear();

        var directoryName = Path.GetDirectoryName(_configuration["Configurations:MainDeviceConfigurationPath"]);
        // var tagFilePaths = _opcFileService.GetFilePathsInDirectory("Configuration", "MainEquipments");
        var tagFilePaths = _opcFileService.GetFilePathsInDirectory(directoryName);
        if (tagFilePaths is null) return false;

        return _opcFileService.UniqueKeysCheck<Device>(tagFilePaths) && tagFilePaths.All(AddToDevices);
    }

    private bool AddToDevices(string filePath)
    {
        var device = _opcFileService.ConvertFileTo<Device>(filePath);
        if (device is null) return false;

        if (Devices.Exists(x => x.DeviceInfo?.DeviceName == device.DeviceInfo?.DeviceName))
        {
            _logger.LogError($"Tags Files With DeviceName({device.DeviceInfo?.DeviceName}) Exist!");
            return false;
        }

        Devices.Add(device);
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
            values = _opcClientService.ReadValues(nodeIdStrings);
        }
        catch (Exception e)
        {
            _logger.LogError($"Get data error! Device Name:{device.DeviceInfo?.DeviceName}");
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

    // private bool getDeviceDatetime(Device device)
    // {
    //     var deviceName = device.DeviceInfo.Ip switch
    //     {
    //         "10.247.81.11" => "FN-03-L1-A",
    //         "10.247.81.12" => "FN-03-L1-B",
    //         "10.247.81.21" => "FN-03-L2-A",
    //         "10.247.81.22" => "FN-03-L2-B",
    //         _ => string.Empty
    //     };
    //
    //     var nodeIdStrings = new List<string>();
    //     var tagIds = new[] { 395, 396, 397, 398, 399, 400 };
    //     foreach (var tagId in tagIds)
    //     {
    //         nodeIdStrings.Add($"ns=2;s=ModbusTcp.{deviceName}.{40000 + tagId}");
    //     }
    //
    //     var values = new List<object>();
    //     try
    //     {
    //         values = _opcClientService.ReadValues(nodeIdStrings);
    //     }
    //     catch (Exception)
    //     {
    //         _logger.LogError($"Get data error! Device Name:{device.DeviceInfo.DeviceName}");
    //         return false;
    //     }
    //
    //     try
    //     {
    //         var year = int.Parse(values[0].ToString());
    //         var month = int.Parse(values[1].ToString());
    //         var day = int.Parse(values[2].ToString());
    //         var hour = int.Parse(values[3].ToString());
    //         var minute = int.Parse(values[4].ToString());
    //         var second = int.Parse(values[5].ToString());
    //         device.DeviceDate = new DateTime(year, month, day, hour, minute, second);
    //         // Console.WriteLine("Device Date: " + device.DeviceDate);
    //     }
    //     catch (Exception)
    //     {
    //         _logger.LogError($"Parse deviceDate error: device-{device.DeviceInfo.DeviceName}, value-{values[0]}_{values[1]}_{values[2]}_{values[3]}_{values[4]}_{values[5]}");
    //         return false;
    //     }
    //
    //     return true;
    // }


    public override Task StopAsync(CancellationToken cancellationToken)
    {
        SubscriptionReset();
        return base.StopAsync(cancellationToken);
    }
}