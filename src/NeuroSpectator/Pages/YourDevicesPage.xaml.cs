using NeuroSpectator.PageModels;

namespace NeuroSpectator.Pages;

public partial class YourDevicesPage : ContentPage
{
    private readonly YourDevicesPageModel _viewModel;

    public YourDevicesPage(YourDevicesPageModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }
}