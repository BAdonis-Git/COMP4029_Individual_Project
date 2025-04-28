using Microsoft.Identity.Client;
using NeuroSpectator.Models.Account;

namespace NeuroSpectator.Services.Authentication
{
    /// <summary>
    /// Service for handling Microsoft Identity authentication
    /// </summary>
    public class AuthenticationService
    {
        private readonly IPublicClientApplication msalClient;
        private readonly string[] scopes = { "User.Read" };

        private readonly string clientId = "8148bc5a-c57b-491a-97fd-30ae8e61f960";
        private string redirectUri;

        /// <summary>
        /// Creates a new instance of the AuthenticationService
        /// </summary>
        public AuthenticationService()
        {
            try
            {
#if WINDOWS
                redirectUri = "http://localhost";
#elif ANDROID
                redirectUri = "msal8148bc5a-c57b-491a-97fd-30ae8e61f960://auth";
#else
                redirectUri = "msal://callback";
#endif

                msalClient = PublicClientApplicationBuilder
                    .Create(clientId)
                    .WithRedirectUri(redirectUri)
                    .WithAuthority("https://login.microsoftonline.com/common")
                    .Build();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing MSAL client: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Attempts to sign in the user, first silently then interactively if needed
        /// </summary>
        public virtual async Task<AuthenticationResult> SignInAsync()
        {
            try
            {
                // Try to get accounts from cache first
                var accounts = await msalClient.GetAccountsAsync();
                if (accounts.Any())
                {
                    // Try silent authentication if have accounts
                    return await msalClient.AcquireTokenSilent(scopes, accounts.FirstOrDefault())
                        .ExecuteAsync();
                }

                // If no accounts or token expired, do interactive login
                return await msalClient.AcquireTokenInteractive(scopes)
                    .ExecuteAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Authentication error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Signs out the current user
        /// </summary>
        public virtual async Task SignOutAsync()
        {
            try
            {
                var accounts = await msalClient.GetAccountsAsync();

                if (accounts.Any())
                {
                    await msalClient.RemoveAsync(accounts.FirstOrDefault());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sign out error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Checks if a user is currently signed in
        /// </summary>
        public virtual async Task<bool> IsUserSignedInAsync()
        {
            try
            {
                var accounts = await msalClient.GetAccountsAsync();
                return accounts.Any();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IsUserSignedIn error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Gets the current user's information
        /// </summary>
        public virtual async Task<UserModel> GetUserInfoAsync()
        {
            try
            {
                var authResult = await SignInAsync();

                var user = new UserModel
                {
                    UserId = authResult.UniqueId,
                    DisplayName = authResult.Account.Username,
                    Email = authResult.Account.Username,
                    LastLogin = DateTime.Now
                };

                return user;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetUserInfo error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}