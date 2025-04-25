using NeuroSpectator.PageModels;
using NeuroSpectator.Services.Account;
using System.Diagnostics;

namespace NeuroSpectator.Pages
{
    public partial class LoginPage : ContentPage
    {
        private readonly LoginPageModel _viewModel;
        private readonly AccountService _accountService;

        public LoginPage()
        {
            InitializeComponent();

            // For design time only
            if (Application.Current?.Handler == null)
            {
                Debug.WriteLine("Skipping initialization - design time detected");
                return;
            }

            // Get services when created directly from App.xaml.cs
            if (MauiProgram.Services != null)
            {
                Debug.WriteLine("MauiProgram.Services is available");

                _accountService = MauiProgram.Services.GetService<AccountService>();
                if (_accountService == null)
                    Debug.WriteLine("ERROR: AccountService could not be resolved");

                var viewModel = MauiProgram.Services.GetService<LoginPageModel>();
                if (viewModel != null)
                {
                    Debug.WriteLine("LoginPageModel successfully resolved");
                    _viewModel = viewModel;
                    BindingContext = viewModel;
                    Debug.WriteLine("ViewModel binding complete");
                }
                else
                {
                    Debug.WriteLine("ERROR: LoginPageModel could not be resolved");
                }
            }
            else
            {
                Debug.WriteLine("ERROR: MauiProgram.Services is null");
            }
        }

        public LoginPage(LoginPageModel viewModel, AccountService accountService)
        {
            try
            {
                InitializeComponent();
                _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
                _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
                BindingContext = viewModel;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing LoginPage: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw; // Rethrow to see the original error
            }
        }

        protected override async void OnAppearing()
        {
            try
            {
                base.OnAppearing();

                // Check if user is already logged in
                if (_viewModel != null)
                {
                    var isSignedIn = await _viewModel.CheckLoginStatusAsync();

                    if (isSignedIn)
                    {
                        // Switch to the authenticated shell
                        Application.Current.MainPage = new AuthAppShell();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in LoginPage.OnAppearing: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        private void TestButton_Clicked(object sender, EventArgs e)
        {
            DisplayAlert("Test", "Button click works!", "OK");

            // More detailed ViewModel check
            if (BindingContext == null)
            {
                DisplayAlert("Error", "BindingContext is null", "OK");

                // Try to recover
                if (MauiProgram.Services != null)
                {
                    var viewModel = MauiProgram.Services.GetService<LoginPageModel>();
                    if (viewModel != null)
                    {
                        BindingContext = viewModel;
                        DisplayAlert("Recovery", "Attempted to set ViewModel", "OK");
                    }
                }
            }
            else if (BindingContext is LoginPageModel)
            {
                DisplayAlert("ViewModel", "ViewModel is bound correctly", "OK");

                // Check if SignInCommand exists
                var vm = (LoginPageModel)BindingContext;
                if (vm.SignInCommand != null)
                {
                    DisplayAlert("Command", "SignInCommand exists", "OK");
                }
                else
                {
                    DisplayAlert("Error", "SignInCommand is null", "OK");
                }
            }
            else
            {
                DisplayAlert("Error", $"BindingContext is of type {BindingContext.GetType().Name}, not LoginPageModel", "OK");
            }
        }
    }
}