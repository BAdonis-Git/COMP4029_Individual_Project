using System;
using System.Diagnostics;
using Microsoft.Maui.Controls;
using NeuroSpectator.PageModels;

namespace NeuroSpectator.Pages
{
    public partial class YourAccountPage : ContentPage
    {
        private readonly YourAccountPageModel _viewModel;

        public YourAccountPage()
        {
            InitializeComponent();
        }

        public YourAccountPage(YourAccountPageModel viewModel)
        {
            try
            {
                InitializeComponent();
                _viewModel = viewModel;
                BindingContext = viewModel;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing YourAccountPage: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw; // Rethrow to see the original error
            }
        }

        protected override async void OnAppearing()
        {
            try
            {
                base.OnAppearing();

                // Initialize the account page when it appears
                if (_viewModel != null)
                {
                    await _viewModel.InitializeAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in YourAccountPage.OnAppearing: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}