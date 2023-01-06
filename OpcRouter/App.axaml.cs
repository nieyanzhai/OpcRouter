using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ClassLibrary.CommonServices;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpcRouter.Models.Common;
using OpcRouter.Services.BackgroundWorkers;
using OpcRouter.Services.FileService;
using OpcRouter.ViewModels;
using OpcRouter.Views;
using Serilog;
using Serilog.Events;

namespace OpcRouter;

public class App : Application
{
    private Protocol _protocol;
    public static IHost Host;
    private Mutex _mutex;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Create the logger
        ConfigureLogger();

        // Create the host
        Host = ConfigureServices();

        // Mutex check
        _mutex = new Mutex(true, "Opc Router", out var createdNew);
        if (createdNew) return;
        Log.Error("Another instance of the program is already running");
        Environment.Exit(0);
    }
     
    
    private IHost ConfigureServices()
    {
        return Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddCommonServices(context.Configuration);
                services.AddMediatR(Assembly.GetExecutingAssembly());
                services.AddSingleton<ICommonFileService, CommonFileService>();
                services.AddSingleton<IOpcFileService, OpcFileService>();

                // services.AddSingleton<MainWindowViewModel>();
                // services.AddSingleton<MainWindow>();

                _protocol = (Protocol)Enum.Parse(typeof(Protocol),
                    context.Configuration["DefaultSettings:Protocol"]);
                switch (_protocol)
                {
                    case Protocol.OpcUa:
                        services.AddSingleton<OpcConnectionWorker>();
                        // services.AddSingleton<OpcProcessingWorker>();
                        break;
                    case Protocol.SecsGem:
                        services.AddSingleton<SecsWorker>();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                // services.AddHostedService<TaskManagerWorker>();
            })
            .UseSerilog()
            .Build();
    }
    
    private static void ConfigureLogger()
    {
        // Create the log path
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Log.txt");
        if (!Directory.Exists(Path.GetDirectoryName(logPath)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
        }

        // Configure the logger
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                path: logPath,
                fileSizeLimitBytes: 100000000,
                rollOnFileSizeLimit: true,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 90)
            .CreateLogger();
    }
    
    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            // Show the main window
            Log.Information("Showing main window");
            // var mainWindow = Host.Services.GetRequiredService<MainWindow>();
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.MainWindow = new MainWindow()
            {
                DataContext = new MainWindowViewModel()
            };
            // mainWindow.Show();
            Log.Information("Main window shown");

            // Start the host
            Log.Information("Starting host");
            StartHost(_cancellationTokenSource.Token);
            Log.Information("Host started");

            // Run the application
            Log.Information("Running application");
            base.OnFrameworkInitializationCompleted();
            Log.Information("Application running");
        }
        catch (Exception ex)
        {
            // Log the exception
            Log.Fatal(ex, "Host terminated unexpectedly");
            _cancellationTokenSource.Cancel();
            StopHost(_cancellationTokenSource.Token);
        }

    }
    
    private void StopHost(CancellationToken cancellationToken)
    {
        Host.StopAsync(cancellationToken).GetAwaiter().GetResult();
        switch (_protocol)
        {
            case Protocol.OpcUa:
                // var opcProcessingWorker = Host.Services.GetRequiredService<OpcProcessingWorker>();
                var opcConnectionWorker = Host.Services.GetRequiredService<OpcConnectionWorker>();
                // opcProcessingWorker.StopAsync(cancellationToken).GetAwaiter().GetResult();
                opcConnectionWorker.StopAsync(cancellationToken).GetAwaiter().GetResult();
                break;
            case Protocol.SecsGem:
                var secsWorker = Host.Services.GetRequiredService<SecsWorker>();
                secsWorker.StopAsync(cancellationToken).GetAwaiter().GetResult();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        Host.Dispose();
    }
    
    private void StartHost(CancellationToken cancellationToken)
    {
        Host.Start();

        switch (_protocol)
        {
            case Protocol.OpcUa:
                var opcConnectionWorker = Host.Services.GetRequiredService<OpcConnectionWorker>();
                // var opcProcessingWorker = Host.Services.GetRequiredService<OpcProcessingWorker>();
                opcConnectionWorker.StartAsync(cancellationToken).GetAwaiter().GetResult();
                // opcProcessingWorker.StartAsync(cancellationToken).GetAwaiter().GetResult();
                break;
            case Protocol.SecsGem:
                var secsWorker = Host.Services.GetRequiredService<SecsWorker>();
                secsWorker.StartAsync(cancellationToken).GetAwaiter().GetResult();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    
}