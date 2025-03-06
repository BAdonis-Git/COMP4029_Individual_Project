using System;
using System.Diagnostics;
using Microsoft.Maui.Controls;
using NeuroSpectator.PageModels;

namespace NeuroSpectator.Pages
{
    public partial class YourNexusPage : ContentPage
    {
        private readonly YourNexusPageModel _viewModel;

        public YourNexusPage()
        {
            InitializeComponent();
        }

        public YourNexusPage(YourNexusPageModel viewModel)
        {
            try
            {
                InitializeComponent();
                _viewModel = viewModel;
                BindingContext = viewModel;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing YourNexusPage: {ex.Message}");
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
                Debug.WriteLine($"Error in YourNexusPage.OnAppearing: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}