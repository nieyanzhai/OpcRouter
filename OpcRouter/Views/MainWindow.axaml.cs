using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using Material.Styles.Themes;
using Material.Styles.Themes.Base;
using OpcRouter.ViewModels;

namespace OpcRouter.Views
{
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        
        private void MaterialIcon_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var materialTheme = Application.Current.LocateMaterialTheme<MaterialTheme>();
            materialTheme.BaseTheme = materialTheme.BaseTheme == BaseThemeMode.Light
                ? BaseThemeMode.Dark
                : BaseThemeMode.Light;
        }

        private void DrawerList_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (sender is not ListBox listBox)
                return;

            if (listBox is {IsFocused: false, IsKeyboardFocusWithin: false})
                return;
            try
            {
                PageCarousel.SelectedIndex = listBox.SelectedIndex;
                mainScroller.Offset = Vector.Zero;
                mainScroller.VerticalScrollBarVisibility =
                    listBox.SelectedIndex == 5 ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
            }
            catch
            {
                // ignored
            }

            LeftDrawer.OptionalCloseLeftDrawer();
        }
    }
}