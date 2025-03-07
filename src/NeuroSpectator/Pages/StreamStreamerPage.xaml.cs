using System;
using System.Diagnostics;
using Microsoft.Maui.Controls;
using NeuroSpectator.PageModels;

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

        protected override bool OnBackButtonPressed()
        {
            // Ask for confirmation before closing the stream window
            if (_viewModel != null)
            {
                _viewModel.ConfirmExitAsync();
                return true; // We're handling the back button
            }
            return base.OnBackButtonPressed();
        }
    }
}