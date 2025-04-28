using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroSpectator.Models.Account;
using NeuroSpectator.Pages;
using NeuroSpectator.Services.Account;
using System.Windows.Input;

namespace NeuroSpectator.PageModels
{
    /// <summary>
    /// View model for the account management page
    /// </summary>
    public partial class YourAccountPageModel : ObservableObject
    {
        private readonly AccountService accountService;

        [ObservableProperty]
        private UserModel userInfo;

        [ObservableProperty]
        private string statusMessage;

        [ObservableProperty]
        private bool isDarkMode;

        [ObservableProperty]
        private bool isStreamNotificationsEnabled;

        [ObservableProperty]
        private string preferredLanguage = "English";

        public ICommand SignOutCommand { get; }
        public ICommand SavePreferencesCommand { get; }
        public ICommand DeleteAccountCommand { get; }

        /// <summary>
        /// Creates a new instance of the YourAccountPageModel
        /// </summary>
        public YourAccountPageModel(AccountService accountService)
        {
            this.accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
            SignOutCommand = new AsyncRelayCommand(SignOutAsync);
            SavePreferencesCommand = new AsyncRelayCommand(SavePreferencesAsync);
            DeleteAccountCommand = new AsyncRelayCommand(DeleteAccountAsync);
        }

        /// <summary>
        /// Initializes the view model with user data
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                UserInfo = accountService.CurrentUser;

                if (UserInfo?.Preferences != null)
                {
                    // Load user preferences
                    if (UserInfo.Preferences.TryGetValue("DarkMode", out var darkMode) && darkMode is bool dm)
                        IsDarkMode = dm;

                    if (UserInfo.Preferences.TryGetValue("StreamNotifications", out var notifications) && notifications is bool n)
                        IsStreamNotificationsEnabled = n;

                    if (UserInfo.Preferences.TryGetValue("Language", out var language) && language is string l)
                        PreferredLanguage = l;
                }

                StatusMessage = string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading user information: {ex.Message}";
            }
        }

        /// <summary>
        /// Saves user preferences
        /// </summary>
        private async Task SavePreferencesAsync()
        {
            try
            {
                var preferences = new Dictionary<string, object>
                {
                    { "DarkMode", IsDarkMode },
                    { "StreamNotifications", IsStreamNotificationsEnabled },
                    { "Language", PreferredLanguage }
                };

                await accountService.UpdatePreferencesAsync(preferences);
                StatusMessage = "Preferences saved successfully";

                // Short delay to show success message
                await Task.Delay(2000);
                StatusMessage = string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving preferences: {ex.Message}";
            }
        }

        /// <summary>
        /// Signs out the current user
        /// </summary>
        private async Task SignOutAsync()
        {
            try
            {
                var result = await Shell.Current.DisplayAlert(
                    "Sign Out",
                    "Are you sure you want to sign out?",
                    "Yes",
                    "No");

                if (result)
                {
                    await accountService.SignOutAsync();

                    // Switch back to login shell
                    Application.Current.MainPage = new LoginPage();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error signing out: {ex.Message}";
            }
        }

        /// <summary>
        /// Deletes the current user's account
        /// </summary>
        private async Task DeleteAccountAsync()
        {
            try
            {
                var result = await Shell.Current.DisplayAlert(
                    "Delete Account",
                    "Are you sure you want to delete your account? This action cannot be undone.",
                    "Delete",
                    "Cancel");

                if (result)
                {
                    // Here delete the user's data from storage
                    // Then sign them out
                    await accountService.SignOutAsync();
                    await Shell.Current.GoToAsync("//LoginPage");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting account: {ex.Message}";
            }
        }
    }
}