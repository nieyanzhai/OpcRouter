using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpcRouter.Models.Common;

namespace OpcRouter.Services.BackgroundWorkers;

public class TaskManagerWorker : BackgroundService
{
    private readonly IHost _host;
    private readonly ILogger<TaskManagerWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly OpcConnectionWorker _opcConnectionWorker;
    private readonly OpcProcessingWorker _opcProcessingWorker;
    private readonly SecsWorker _secsWorker;
    private readonly Protocol _protocol;

    public TaskManagerWorker(
        IHost host,
        ILogger<TaskManagerWorker> logger,
        IConfiguration configuration,
        OpcConnectionWorker opcConnectionWorker,
        OpcProcessingWorker opcProcessingWorker,
        SecsWorker secsWorker)
    {
        _host = host;
        _logger = logger;
        _configuration = configuration;
        _opcConnectionWorker = opcConnectionWorker;
        _opcProcessingWorker = opcProcessingWorker;
        _secsWorker = secsWorker;

        _protocol = (Protocol)Enum.Parse(typeof(Protocol), _configuration["DefaultSettings:Protocol"]);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        switch (_protocol)
        {
            case Protocol.OpcUa:
                await _opcConnectionWorker.StartAsync(cancellationToken);
                await _opcProcessingWorker.StartAsync(cancellationToken);
                break;
            case Protocol.SecsGem:
                await _secsWorker.StartAsync(cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        switch (_protocol)
        {
            case Protocol.OpcUa:
                await _opcConnectionWorker.StopAsync(cancellationToken);
                await _opcProcessingWorker.StopAsync(cancellationToken);
                break;
            case Protocol.SecsGem:
                await _secsWorker.StopAsync(cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        await base.StopAsync(cancellationToken);
    }
}