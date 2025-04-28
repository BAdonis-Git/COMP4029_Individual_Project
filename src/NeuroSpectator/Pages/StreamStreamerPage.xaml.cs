using NeuroSpectator.PageModels;
using System.Diagnostics;

namespace NeuroSpectator.Pages
{
    public partial class StreamStreamerPage : ContentPage
    {
        private readonly StreamStreamerPageModel _viewModel;

        public StreamStreamerPage()
        {
            InitializeComponent();
        }

        public StreamStreamerPage(StreamStreamerPageModel viewModel)
        {
            try
            {
                InitializeComponent();
                _viewModel = viewModel;
                BindingContext = viewModel;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing StreamStreamerPage: {ex.Message}");
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
                Debug.WriteLine($"Error in StreamStreamerPage.OnAppearing: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        protected override async void OnDisappearing()
        {
            try
            {
                base.OnDisappearing();

                // Clean up resources when the page disappears
                // This helps ensure resources are properly released
                // even if the page is closed without confirmation
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in StreamStreamerPage.OnDisappearing: {ex.Message}");
            }
        }

        protected override bool OnBackButtonPressed()
        {
            // Ask for confirmation before closing the stream window
            if (_viewModel != null)
            {
                // Call the confirmation method in the view model
                _viewModel.ConfirmExitAsync.Execute(null);
                return true; // Handling the back button
            }
            return base.OnBackButtonPressed();
        }
    }
}