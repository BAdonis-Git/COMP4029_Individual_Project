using NeuroSpectator.PageModels;
using System.Diagnostics;

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

    // Handle device selection to ensure connection is triggered
    private void OnDeviceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection != null && e.CurrentSelection.Count > 0 && _viewModel != null)
            {
                // Execute the ConnectCommand
                if (_viewModel.ConnectCommand.CanExecute(e.CurrentSelection[0]))
                {
                    _viewModel.ConnectCommand.Execute(e.CurrentSelection[0]);
                }

                // Clear selection to allow re-selecting the same item
                if (sender is CollectionView collectionView)
                {
                    collectionView.SelectedItem = null;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in OnDeviceSelectionChanged: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}