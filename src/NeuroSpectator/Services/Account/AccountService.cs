using NeuroSpectator.Models.Account;
using NeuroSpectator.Services.Authentication;
using NeuroSpectator.Services.Storage;

namespace NeuroSpectator.Services.Account
{
    /// <summary>
    /// Service for managing user accounts, coordinating authentication and storage
    /// </summary>
    public class AccountService
    {
        private readonly AuthenticationService authService;
        private readonly UserStorageService storageService;

        private UserModel currentUser;

        /// <summary>
        /// Creates a new instance of the AccountService
        /// </summary>
        public AccountService(AuthenticationService authService, UserStorageService storageService)
        {
            this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
            this.storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        }

        /// <summary>
        /// Gets the currently signed-in user
        /// </summary>
        public UserModel CurrentUser => currentUser;

        /// <summary>
        /// Attempts to sign in a user and load their data
        /// </summary>
        public async Task<bool> SignInAsync()
        {
            try
            {
                var authResult = await authService.SignInAsync();
                var userInfo = await authService.GetUserInfoAsync();

                // Try to get existing user data from storage
                var storedUser = await storageService.GetUserAsync(userInfo.UserId);

                if (storedUser != null)
                {
                    // Update last login time but keep preferences
                    storedUser.LastLogin = DateTime.Now;
                    await storageService.SaveUserAsync(storedUser);
                    currentUser = storedUser;
                }
                else
                {
                    // Create new user record
                    userInfo.LastLogin = DateTime.Now;
                    await storageService.SaveUserAsync(userInfo);
                    currentUser = userInfo;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sign-in error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Signs out the current user
        /// </summary>
        public async Task SignOutAsync()
        {
            try
            {
                await authService.SignOutAsync();
                currentUser = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sign-out error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Checks if a user is currently signed in
        /// </summary>
        public async Task<bool> IsSignedInAsync()
        {
            return await authService.IsUserSignedInAsync();
        }

        /// <summary>
        /// Updates the current user's preferences
        /// </summary>
        public async Task UpdatePreferencesAsync(Dictionary<string, object> preferences)
        {
            if (currentUser == null)
                throw new InvalidOperationException("No user is signed in");

            await storageService.UpdateUserPreferencesAsync(currentUser.UserId, preferences);
            currentUser.Preferences = preferences;
        }
    }
}