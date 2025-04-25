namespace NeuroSpectator.Models.Account
{
    /// <summary>
    /// Represents a user account in the application
    /// </summary>
    public class UserModel
    {
        /// <summary>
        /// Unique identifier for the user (from Microsoft authentication)
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Display name of the user
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Email address of the user
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// User's application preferences
        /// </summary>
        public Dictionary<string, object> Preferences { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// When the user last logged in
        /// </summary>
        public DateTime LastLogin { get; set; }

        /// <summary>
        /// URL to the user's profile image (optional)
        /// </summary>
        public string ProfileImageUrl { get; set; }
    }
}