using Azure.Storage.Blobs;
using NeuroSpectator.Models.Account;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace NeuroSpectator.Services.Storage
{
    /// <summary>
    /// Service for storing and retrieving user data from Azure Blob Storage
    /// </summary>
    public class UserStorageService
    {
        private readonly BlobServiceClient blobServiceClient;
        private readonly BlobContainerClient usersContainer;
        private const string CONTAINER_NAME = "users";

        /// <summary>
        /// Creates a new instance of the UserStorageService with the specified connection string
        /// </summary>
        public UserStorageService(string connectionString)
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                try
                {
                    blobServiceClient = new BlobServiceClient(connectionString);
                    usersContainer = blobServiceClient.GetBlobContainerClient(CONTAINER_NAME);

                    // Create container if it doesn't exist
                    usersContainer.CreateIfNotExists();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error initializing Azure Storage: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            }
        }

        /// <summary>
        /// Tests the connection to Azure Storage
        /// </summary>
        public virtual async Task<bool> TestConnectionAsync()
        {
            try
            {
                if (usersContainer == null)
                    return false;

                // Try to list all blobs in the users container
                var blobs = new List<string>();
                await foreach (var blobItem in usersContainer.GetBlobsAsync())
                {
                    blobs.Add(blobItem.Name);
                }

                System.Diagnostics.Debug.WriteLine($"Successfully connected to Azure Storage. Found {blobs.Count} user blobs.");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error connecting to Azure Storage: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Gets a user from storage by their ID
        /// </summary>
        public virtual async Task<UserModel> GetUserAsync(string userId)
        {
            try
            {
                if (usersContainer == null)
                    throw new InvalidOperationException("Storage not initialized");

                var blobClient = usersContainer.GetBlobClient($"{userId}.json");

                if (await blobClient.ExistsAsync())
                {
                    var response = await blobClient.DownloadAsync();
                    using var streamReader = new StreamReader(response.Value.Content);
                    var json = await streamReader.ReadToEndAsync();
                    return JsonSerializer.Deserialize<UserModel>(json);
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting user: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Saves a user to storage
        /// </summary>
        public virtual async Task SaveUserAsync(UserModel user)
        {
            try
            {
                if (usersContainer == null)
                    throw new InvalidOperationException("Storage not initialized");

                var blobClient = usersContainer.GetBlobClient($"{user.UserId}.json");
                var json = JsonSerializer.Serialize(user);

                using var ms = new MemoryStream();
                using var writer = new StreamWriter(ms);

                await writer.WriteAsync(json);
                await writer.FlushAsync();

                ms.Position = 0;
                await blobClient.UploadAsync(ms, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving user: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Updates a user's preferences
        /// </summary>
        public virtual async Task UpdateUserPreferencesAsync(string userId, Dictionary<string, object> preferences)
        {
            var user = await GetUserAsync(userId);

            if (user != null)
            {
                user.Preferences = preferences;
                await SaveUserAsync(user);
            }
            else
            {
                throw new Exception($"User {userId} not found");
            }
        }

        /// <summary>
        /// Deletes a user from storage
        /// </summary>
        public virtual async Task DeleteUserAsync(string userId)
        {
            try
            {
                if (usersContainer == null)
                    throw new InvalidOperationException("Storage not initialized");

                var blobClient = usersContainer.GetBlobClient($"{userId}.json");
                await blobClient.DeleteIfExistsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting user: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}