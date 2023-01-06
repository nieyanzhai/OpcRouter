

using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using MessageBox.Avalonia;
using MessageBox.Avalonia.Enums;
using OpcRouter.Models.Entities.DeviceEntity;
using OpcRouter.PageViewModels;
using OpcRouter.Views;
using ReactiveUI;

namespace OpcRouter.Pages;

public partial class Home : ReactiveUserControl<HomeViewModel>
{
    public Home()
    {
        InitializeComponent();

        DataContext = new HomeViewModel();
        
        this.WhenActivated(d =>
        {
            if (ViewModel == null) return;
            d(ViewModel.AddDeviceInteraction.RegisterHandler(ShowAddDeviceDialog));
            d(ViewModel.DeleteDeviceInteraction.RegisterHandler(ShowDeleteDeviceDialog));
        });
    }
    
    private async Task ShowDeleteDeviceDialog(InteractionContext<string, bool> interaction)
    {
        var messageBoxStandardWindow = MessageBoxManager.GetMessageBoxStandardWindow(
            title: "Confirm Delete",
            text: $"Are you sure you want to delete {interaction.Input}",
            ButtonEnum.YesNo,
            MessageBox.Avalonia.Enums.Icon.Warning
        );
        var result = await messageBoxStandardWindow.ShowDialog(App.MainWindow);
        interaction.SetOutput(result == ButtonResult.Yes);
    }

    private async Task ShowAddDeviceDialog(InteractionContext<Unit, Device> obj)
    {
        var addDeviceWindow = new AddDeviceWindow();
        var result = await addDeviceWindow.ShowDialog<Device?>(App.MainWindow);
        obj.SetOutput(result);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}