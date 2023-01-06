using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClassLibrary.CommonServices.SecsGemService;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpcRouter.Messages.Commands;
using OpcRouter.Models.Common;
using OpcRouter.Models.Entities.DeviceEntity;
using OpcRouter.Services.FileService;
using Polly;
using Polly.Retry;
using Secs4Net;

namespace OpcRouter.Services.BackgroundWorkers;

public class SecsWorker : BackgroundService
{
    private readonly ILogger<SecsWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly ICommonFileService _commonFileService;
    private readonly ISecsGemService _secsGemService;
    private readonly IMediator _mediator;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly Device _device;
    private static int _samplingInterval;
    private readonly RetryPolicy _reconnectPolicy;
    private bool _isSelected;
    private bool _isCommunicationEstablished;

    public SecsWorker(
        ILogger<SecsWorker> logger,
        IConfiguration configuration,
        ICommonFileService commonFileService,
        ISecsGemService secsGemService,
        IMediator mediator,
        IHostApplicationLifetime hostApplicationLifetime
    )
    {
        _logger = logger;
        _configuration = configuration;
        _commonFileService = commonFileService;
        _secsGemService = secsGemService;
        _mediator = mediator;
        _hostApplicationLifetime = hostApplicationLifetime;

        _samplingInterval = Convert.ToInt32(_configuration["DefaultSettings:SamplingInterval"]);
        _device = _commonFileService.LoadJsonFile<Device>(_configuration["Configurations:MainDeviceConfigurationPath"]);

        _secsGemService.ConnectionStateChanged += OnConnectionStateChanged;
        _secsGemService.AlarmReported += OnAlarmReported;
        _secsGemService.EventReported += OnEventReported;

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


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SecsWorker is starting.");
        
        // For Processing received messages
        await (_secsGemService as SecsGemService)?.StartAsync(stoppingToken);

        var secsGemServiceDataTypes = new List<DataType>
        {
            DataType.A,
            DataType.U2,
            DataType.U4,
            DataType.F4
        };
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_samplingInterval, stoppingToken);
            if (!_isSelected) continue;
            if (!_isCommunicationEstablished)
                _isCommunicationEstablished = await _secsGemService.EstablishCommunicationAsync();
            if (!_isCommunicationEstablished) continue;

            try
            {
                // Get Device Date and Time
                var deviceDate = await GetDeviceDateTime(_device.DeviceInfo.Manufacture);
                if (deviceDate is not null)
                    _device.DeviceDate = DateTime.ParseExact(deviceDate, "yyyyMMddHHmmss", new CultureInfo("zh-CN"));

                // Update Device Data
                await UpdateDeviceData(_device, secsGemServiceDataTypes);

                // Notify Device Data Updated
                await _reconnectPolicy.Execute(async () => await _mediator.Send(new PublishToKafkaCommand
                {
                    Topic = "test",
                    Device = _device,
                    Timeout = TimeSpan.FromSeconds(10)
                }, stoppingToken));
            }
            catch (Exception e)
            {
                var msg = "Error occurred while updating device data";
                _logger.LogError(e, msg);
                // WeakReferenceMessenger.Default.Send(new CommonMessageCommand(msg) { IsError = true });
            }
        }
    }

    


    private async Task UpdateDeviceData(Device device, List<DataType> secsGemServiceDataTypes)
    {
        foreach (var dataType in secsGemServiceDataTypes)
        {
            var svids = GetSvids(device, dataType);
            if (svids.Count == 0) continue;

            switch (dataType)
            {
                case DataType.A:
                    var valuesOfA = await _secsGemService.GetStringListAsync(svids);
                    UpdateSvidValuesInDevice(device, valuesOfA, dataType);
                    break;
                case DataType.U2:
                    var valuesOfU2 = await _secsGemService.GetU2ListAsync(svids);
                    UpdateSvidValuesInDevice(device, valuesOfU2, dataType);
                    break;
                case DataType.U4:
                    var valuesOfU4 = await _secsGemService.GetU4ListAsync(svids);
                    UpdateSvidValuesInDevice(device, valuesOfU4, dataType);
                    break;
                case DataType.F4:
                    var valuesOfF4 = await _secsGemService.GetF4ListAsync(svids);
                    UpdateSvidValuesInDevice(device, valuesOfF4, dataType);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private void UpdateSvidValuesInDevice<T>(Device device, IReadOnlyList<T> values, DataType dataType)
    {
        var valuesIndex = 0;
        var dt = dataType.ToString();

        for (var i = 0; i < device.Tags.Count(); i += 1)
        {
            if (device.Tags[i].DataType != dt) continue;

            device.Tags[i].Value = values[valuesIndex].ToString();
            valuesIndex += 1;
        }

        device.CreatedDate = DateTimeOffset.Now;
    }

    private List<uint> GetSvids(Device device, DataType dataType)
    {
        var dt = dataType.ToString();
        return (from tag in device.Tags where tag.DataType == dt select uint.Parse(tag.Id)).ToList();
    }

    private async Task<string?> GetDeviceDateTime(Manufacture deviceInfoManufacture)
    {
        var deviceDate = deviceInfoManufacture switch
        {
            // 北方华创 - 标准secs协议读取时间，s2f17
            Manufacture.BeiFangHuaChuang =>
                await _secsGemService.GetDeviceDateTimeAsync(),

            // 捷佳伟创 - 设备日期时间地址2004
            Manufacture.JieJiaWeiChuang => await _secsGemService.GetStringAsync(2004),

            // 红太阳 - 设备日期时间地址17
            Manufacture.HongTaiYang => await _secsGemService.GetStringAsync(17),

            _ => throw new ArgumentOutOfRangeException(nameof(deviceInfoManufacture), deviceInfoManufacture, null)
        };
        return deviceDate?.Trim();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping SecsWorker");

        // await (_secsGemService as SecsGemService)?.StopAsync(cancellationToken);

        await base.StopAsync(cancellationToken);
    }

    private void OnEventReported(object sender, EventReportedEventArgs e)
    {
        _logger.LogInformation("Alarm reported: {SecsMessage}", e.SecsMessage);
        // WeakReferenceMessenger.Default.Send(new SecsReceivePrimaryMessageCommand { Message = e.SecsMessage });
    }

    private void OnAlarmReported(object sender, AlarmReportedEventArgs e)
    {
        _logger.LogInformation("Alarm reported: {SecsMessage}", e.SecsMessage);
        // WeakReferenceMessenger.Default.Send(new SecsReceivePrimaryMessageCommand { Message = e.SecsMessage });
    }

    private void OnConnectionStateChanged(object sender, ConnectionStateChangedEventArgs e)
    {
        _isCommunicationEstablished = false;

        var msg = $"Connection state changed to {e.State}";
        _isSelected = e.State == ConnectionState.Selected;
        _logger.LogInformation(msg);
        // WeakReferenceMessenger.Default.Send(new SecsConnectionStatusChangedCommand(_isSelected) { Message = msg });
    }
}