using NeuroSpectator.Models.Account;

namespace NeuroSpectator.Services.Storage
{
    /// <summary>
    /// In-memory implementation of UserStorageService for development and testing
    /// </summary>
    public class DebugUserStorageService : UserStorageService
    {
        private readonly Dictionary<string, UserModel> userStore = new Dictionary<string, UserModel>();

        /// <summary>
        /// Creates a new instance of the DebugUserStorageService
        /// </summary>
        public DebugUserStorageService() : base(string.Empty)
        {
            // Create a mock user for testing
            var debugUser = new UserModel
            {
                UserId = "debug-user-123",
                DisplayName = "Debug Test User",
                Email = "debug@example.com",
                LastLogin = DateTime.Now,
                Preferences = new Dictionary<string, object>
                {
                    { "DarkMode", true },
                    { "StreamNotifications", true },
                    { "Language", "English" }
                }
            };

            userStore[debugUser.UserId] = debugUser;
        }

        /// <summary>
        /// Gets a user from the in-memory store
        /// </summary>
        public override async Task<UserModel> GetUserAsync(string userId)
        {
            await Task.Delay(200); // Simulate network delay

            if (userStore.TryGetValue(userId, out var user))
            {
                return user;
            }

            return null;
        }

        /// <summary>
        /// Saves a user to the in-memory store
        /// </summary>
        public override async Task SaveUserAsync(UserModel user)
        {
            await Task.Delay(200); // Simulate network delay

            if (user != null && !string.IsNullOrEmpty(user.UserId))
            {
                userStore[user.UserId] = user;
            }
        }

        /// <summary>
        /// Updates a user's preferences in the in-memory store
        /// </summary>
        public override async Task UpdateUserPreferencesAsync(string userId, Dictionary<string, object> preferences)
        {
            await Task.Delay(200); // Simulate network delay

            if (userStore.TryGetValue(userId, out var user))
            {
                user.Preferences = preferences;
            }
            else
            {
                throw new Exception($"User {userId} not found");
            }
        }

        /// <summary>
        /// Deletes a user from the in-memory store
        /// </summary>
        public override async Task DeleteUserAsync(string userId)
        {
            await Task.Delay(200); // Simulate network delay

            userStore.Remove(userId);
        }
    }
}