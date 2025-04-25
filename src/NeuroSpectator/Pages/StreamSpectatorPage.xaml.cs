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

                // Handle query parameters here instead of in constructor
                if (_viewModel != null)
                {
                    // Parse the query string to extract streamId
                    var uri = Shell.Current.CurrentState?.Location;
                    if (uri?.Query != null)
                    {
                        var queryString = uri.Query;
                        if (queryString.Contains("streamId="))
                        {
                            int start = queryString.IndexOf("streamId=") + "streamId=".Length;
                            int end = queryString.IndexOf('&', start);
                            string streamId = end > start ?
                                queryString.Substring(start, end - start) :
                                queryString.Substring(start);

                            // Decode and set the stream ID
                            streamId = Uri.UnescapeDataString(streamId);
                            Debug.WriteLine($"OnAppearing: Found stream ID in query: {streamId}");
                            _viewModel.StreamId = streamId;
                        }
                    }

                    // Now initialize the view model
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