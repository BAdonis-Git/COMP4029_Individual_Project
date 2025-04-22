using System;
using System.Diagnostics;
using Microsoft.Maui.Controls;
using NeuroSpectator.PageModels;

namespace NeuroSpectator.Pages
{
    public partial class StreamSpectatorPage : ContentPage
    {
        private readonly StreamSpectatorPageModel _viewModel;

        public StreamSpectatorPage()
        {
            InitializeComponent();
        }

        public StreamSpectatorPage(StreamSpectatorPageModel viewModel)
        {
            try
            {
                InitializeComponent();
                _viewModel = viewModel;
                BindingContext = viewModel;

                // Pass the player reference to the view model
                if (_viewModel != null)
                {
                    _viewModel.SetPlayer(streamPlayer);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing StreamSpectatorPage: {ex.Message}");
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
                Debug.WriteLine($"Error in StreamSpectatorPage.OnAppearing: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        protected override async void OnDisappearing()
        {
            try
            {
                base.OnDisappearing();

                // Clean up when the page disappears
                if (_viewModel != null)
                {
                    await _viewModel.OnDisappearingAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in StreamSpectatorPage.OnDisappearing: {ex.Message}");
            }
        }

        protected override bool OnBackButtonPressed()
        {
            // Handle back button press by calling CloseStream
            if (_viewModel != null)
            {
                _viewModel.CloseStreamCommand.Execute(null);
                return true; // We're handling the back button
            }
            return base.OnBackButtonPressed();
        }
    }
}