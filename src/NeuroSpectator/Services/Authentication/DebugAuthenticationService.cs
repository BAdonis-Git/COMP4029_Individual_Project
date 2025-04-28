using Microsoft.Identity.Client;
using NeuroSpectator.Models.Account;

namespace NeuroSpectator.Services.Authentication
{
    /// <summary>
    /// Debug version of the authentication service that doesn't require actual Azure login
    /// </summary>
    public class DebugAuthenticationService : AuthenticationService
    {
        private bool isSignedIn = false;
        private readonly UserModel debugUser;

        /// <summary>
        /// Creates a new instance of the DebugAuthenticationService
        /// </summary>
        public DebugAuthenticationService() : base()
        {
            // Create a debug user for testing
            debugUser = new UserModel
            {
                UserId = "debug-user-123",
                DisplayName = "Debug Test User",
                Email = "debug@example.com",
                LastLogin = DateTime.Now,
                Preferences = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "DarkMode", true },
                    { "StreamNotifications", true },
                    { "Language", "English" }
                }
            };
        }

        /// <summary>
        /// Debug version that simulates sign-in without requiring Microsoft authentication
        /// </summary>
        public override async Task<AuthenticationResult> SignInAsync()
        {
            // Simulate network delay
            await Task.Delay(1000);

            isSignedIn = true;


            // The actual AccountService will handle this
            return null;
        }

        /// <summary>
        /// Signs out the debug user
        /// </summary>
        public override async Task SignOutAsync()
        {
            // Simulate network delay
            await Task.Delay(500);

            isSignedIn = false;
        }

        /// <summary>
        /// Checks if a user is currently signed in
        /// </summary>
        public override async Task<bool> IsUserSignedInAsync()
        {
            return isSignedIn;
        }

        /// <summary>
        /// Gets the debug user's information
        /// </summary>
        public override async Task<UserModel> GetUserInfoAsync()
        {
            if (!isSignedIn)
            {
                await SignInAsync();
            }

            return debugUser;
        }
    }
}