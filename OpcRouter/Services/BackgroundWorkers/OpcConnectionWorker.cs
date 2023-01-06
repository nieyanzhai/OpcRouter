using System;
using System.Threading;
using System.Threading.Tasks;
using ClassLibrary.CommonServices.OpcUaService;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;
using Polly;
using Polly.Retry;

namespace OpcRouter.Services.BackgroundWorkers;

public class OpcConnectionWorker : BackgroundService
{
    private readonly ILogger<OpcConnectionWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IOpcUaClientService _opcClientService;
    private readonly IMediator _mediator;
    private readonly string _url;
    private readonly AsyncRetryPolicy _reconnectPolicy;


    public OpcConnectionWorker(
        ILogger<OpcConnectionWorker> logger,
        IConfiguration configuration,
        IOpcUaClientService opcClientService,
        IMediator mediator)
    {
        _logger = logger;
        _configuration = configuration;
        _opcClientService = opcClientService;
        _mediator = mediator;

        _url = _configuration["OpcUaServerSettings:url"];
        _opcClientService.KeepAliveNotification += On_OpcClientService_KeepAliveNotification;

        // policy for reconnecting to the server
        _reconnectPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryForeverAsync(
                retryAttempt => TimeSpan.FromSeconds(5),
                (exception, timeSpan) =>
                {
                    var msg = $"Reconnecting to {_url}...";
                    _logger.LogWarning(msg);
                    // WeakReferenceMessenger.Default.Send(new OpcConnectionStatusChangedCommand(false)
                    //     { Message = msg });
                });
    }

    private void On_OpcClientService_KeepAliveNotification(Session session, KeepAliveEventArgs e)
    {
        // _logger.LogInformation("state :" + e.CurrentState);
        if (e.CurrentState != ServerState.Running) _opcClientService.Disconnect();
    }


    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("OpcConnectionWorker is starting.");
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_opcClientService.Session is null || !_opcClientService.Session.Connected)
                await _reconnectPolicy.ExecuteAsync(async () =>
                {
                    if (await ConnectToOpcServer())
                    {
                        var msg = $"Connected to {_url}";
                        _logger.LogInformation(msg);
                        // WeakReferenceMessenger.Default.Send(new OpcConnectionStatusChangedCommand(true)
                        //     { Message = msg });
                    }
                });
            else
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        }
    }


    private async Task<bool> ConnectToOpcServer()
    {
        // await this.serviceControllerHelperAPI.StartAsync();
        // if (this.serviceControllerService.Status != ServiceControllerStatus.Running) return false;

        await _opcClientService.Connect(
            endpointDescription: new EndpointDescription(_url),
            userAuth: false,
            userName: "",
            password: "");

        return _opcClientService.Session is { Connected: true };
    }


    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("OpcConnectionWorker is stopping.");
        _opcClientService?.Disconnect();
        return base.StopAsync(cancellationToken);
    }
}