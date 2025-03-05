using System.Diagnostics;
using NeuroSpectator.PageModels;

namespace NeuroSpectator.Pages;

public partial class YourDevicesPage : ContentPage
{
    private readonly YourDevicesPageModel _viewModel;

    public YourDevicesPage(YourDevicesPageModel viewModel)
    {
        try
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error initializing YourDevicesPage: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            throw; // Rethrow to see the original error
        }
    }

    protected override async void OnAppearing()
    {
        try
        {
            base.OnAppearing();

            // Initialize and start scanning when page appears
            if (_viewModel != null)
            {
                await _viewModel.OnAppearingAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in YourDevicesPage.OnAppearing: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}