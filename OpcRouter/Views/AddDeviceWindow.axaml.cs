using System;
using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using OpcRouter.ViewModels;
using ReactiveUI;

namespace OpcRouter.Views;

public partial class AddDeviceWindow : ReactiveWindow<AddDeviceWindowViewModel>
{
    public AddDeviceWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
    }

    private void InitializeComponent()
    {
        this.WhenActivated(d => d(ViewModel?.SaveCommand.Subscribe(Close)));
        AvaloniaXamlLoader.Load(this);
        DataContext = new AddDeviceWindowViewModel();
    }
}