using NeuroSpectator.Models.Stream;
using NeuroSpectator.PageModels;
using System.Diagnostics;

namespace NeuroSpectator.Pages
{
    public partial class YourDashboardPage : ContentPage
    {
        private readonly YourDashboardPageModel _viewModel;

        public YourDashboardPage()
        {
            InitializeComponent();
        }

        public YourDashboardPage(YourDashboardPageModel viewModel)
        {
            try
            {
                InitializeComponent();
                _viewModel = viewModel;
                BindingContext = viewModel;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing YourDashboardPage: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw; // Rethrow to see the original error
            }
        }

        protected override async void OnAppearing()
        {
            try
            {
                base.OnAppearing();

                // Initialize the page when it appears
                if (_viewModel != null)
                {
                    await _viewModel.OnAppearingAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in YourDashboardPage.OnAppearing: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        protected void OnStreamSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (e.CurrentSelection != null && e.CurrentSelection.Count > 0 && _viewModel != null)
                {
                    // Get the selected StreamInfo
                    var streamInfo = e.CurrentSelection[0] as StreamInfo;

                    // Execute the ViewStreamCommand
                    if (_viewModel.ViewStreamCommand.CanExecute(streamInfo))
                    {
                        _viewModel.ViewStreamCommand.Execute(streamInfo);
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
                Debug.WriteLine($"Error in OnStreamSelectionChanged: {ex.Message}");
            }
        }
    }
}