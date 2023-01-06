using System.Reactive;
using System.Threading.Tasks;
using Avalonia.ReactiveUI;
using MessageBox.Avalonia;
using MessageBox.Avalonia.Enums;
using OpcRouter.Models.Entities.DeviceEntity;
using OpcRouter.ViewModels;
using ReactiveUI;

namespace OpcRouter.Views
{
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        public MainWindow()
        {

            this.WhenActivated(d =>
            {
                if (ViewModel == null) return;
                d(ViewModel.AddDeviceInteraction.RegisterHandler(ShowAddDeviceDialog));
                d(ViewModel.DeleteDeviceInteraction.RegisterHandler(ShowDeleteDeviceDialog));
            });
            
            InitializeComponent();
            DataContext = new MainWindowViewModel();
        }

        private async Task ShowDeleteDeviceDialog(InteractionContext<string, bool> interaction)
        {
            var messageBoxStandardWindow = MessageBoxManager.GetMessageBoxStandardWindow(
                title: "Confirm Delete",
                text: $"Are you sure you want to delete {interaction.Input}",
                ButtonEnum.YesNo,
                MessageBox.Avalonia.Enums.Icon.Warning
            );
            var result = await messageBoxStandardWindow.ShowDialog(this);
            interaction.SetOutput(result == ButtonResult.Yes);
        }
        
        private async Task ShowAddDeviceDialog(InteractionContext<Unit, Device> obj)
        {
            var addDeviceWindow = new AddDeviceWindow();
            var result = await addDeviceWindow.ShowDialog<Device?>(this);
            obj.SetOutput(result);
        }
    }
}