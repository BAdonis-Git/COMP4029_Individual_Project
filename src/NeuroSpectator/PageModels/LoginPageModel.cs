using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroSpectator.Services.Account;
using System.Diagnostics;
using System.Windows.Input;

namespace NeuroSpectator.PageModels
{
    /// <summary>
    /// View model for the login page
    /// </summary>
    public partial class LoginPageModel : ObservableObject
    {
        private readonly AccountService accountService;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string statusMessage;

        public ICommand SignInCommand { get; }

        /// <summary>
        /// Creates a new instance of the LoginPageModel
        /// </summary>
        public LoginPageModel(AccountService accountService)
        {
            this.accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
            SignInCommand = new AsyncRelayCommand(SignInAsync);
        }

        /// <summary>
        /// Signs in the user
        /// </summary>
        private async Task SignInAsync()
        {
            try
            {
                // Debug logging - confirm the method is being called
                System.Diagnostics.Debug.WriteLine("SignInAsync method called");

                IsLoading = true;
                StatusMessage = "Signing in...";

                System.Diagnostics.Debug.WriteLine("Calling accountService.SignInAsync()");
                var success = await accountService.SignInAsync();
                System.Diagnostics.Debug.WriteLine($"Sign-in result: {success}");

                if (success)
                {
                    System.Diagnostics.Debug.WriteLine("Sign-in successful, creating AuthAppShell");
                    Application.Current.MainPage = new AuthAppShell();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Sign-in failed");
                    StatusMessage = "Sign-in failed. Please try again.";
                }
            }
            catch (Microsoft.Identity.Client.MsalServiceException msalServiceEx)
            {
                // MSAL errors from the service
                Debug.WriteLine($"MSAL SERVICE ERROR: {msalServiceEx.GetType().Name}: {msalServiceEx.Message}");
                Debug.WriteLine($"Error Code: {msalServiceEx.ErrorCode}, HTTP Status: {msalServiceEx.StatusCode}");
                StatusMessage = $"Service error: {msalServiceEx.Message}";
            }
            catch (Microsoft.Identity.Client.MsalException msalEx)
            {
                // General MSAL errors
                Debug.WriteLine($"MSAL ERROR: {msalEx.GetType().Name}: {msalEx.Message}");
                Debug.WriteLine($"Error Code: {msalEx.ErrorCode}");
                StatusMessage = $"MSAL error: {msalEx.Message}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GENERAL ERROR: {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Checks if the user is already logged in and redirects accordingly
        /// </summary>
        public async Task<bool> CheckLoginStatusAsync()
        {
            try
            {
                IsLoading = true;

                var isSignedIn = await accountService.IsSignedInAsync();

                if (isSignedIn)
                {
                    // Auto-login if already signed in
                    await accountService.SignInAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Login status check error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                StatusMessage = $"Error checking login status: {ex.Message}";
                return false;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}