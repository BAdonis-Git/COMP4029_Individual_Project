using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroSpectator.Models.Stream;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;

namespace NeuroSpectator.PageModels
{
    /// <summary>
    /// View model for the BrowsePage
    /// </summary>
    public partial class BrowsePageModel : ObservableObject
    {
        [ObservableProperty]
        private bool isInitialized;

        [ObservableProperty]
        private string searchQuery;

        [ObservableProperty]
        private string selectedCategory = "All";

        [ObservableProperty]
        private ObservableCollection<StreamInfo> availableStreams = new ObservableCollection<StreamInfo>();

        // Commands
        public ICommand SearchCommand { get; }
        public ICommand SelectCategoryCommand { get; }
        public ICommand WatchStreamCommand { get; }

        /// <summary>
        /// Creates a new instance of the BrowsePageModel class
        /// </summary>
        public BrowsePageModel()
        {
            // Initialize commands
            SearchCommand = new AsyncRelayCommand(SearchStreamsAsync);
            SelectCategoryCommand = new AsyncRelayCommand<string>(SelectCategoryAsync);
            WatchStreamCommand = new AsyncRelayCommand<string>(WatchStreamAsync);

            // Load placeholder data for demonstration
            LoadPlaceholderData();
        }

        /// <summary>
        /// Loads placeholder data for demonstration purposes
        /// </summary>
        private void LoadPlaceholderData()
        {
            // In a real implementation, this would load from a service
            availableStreams.Add(new StreamInfo
            {
                Id = "Stream1",
                StreamerName = "Streamer1",
                Title = "CS:GO Tournament",
                Game = "Counter-Strike: Global Offensive",
                ViewerCount = 1245,
                IsLive = true,
                BrainMetrics = new Dictionary<string, string>
                {
                    { "Focus", "89%" },
                    { "Alpha", "High" }
                }
            });

            availableStreams.Add(new StreamInfo
            {
                Id = "Stream2",
                StreamerName = "Streamer2",
                Title = "League of Legends",
                Game = "League of Legends",
                ViewerCount = 983,
                IsLive = true,
                BrainMetrics = new Dictionary<string, string>
                {
                    { "Focus", "92%" },
                    { "Alpha", "Medium" }
                }
            });

            availableStreams.Add(new StreamInfo
            {
                Id = "Stream3",
                StreamerName = "Streamer3",
                Title = "Apex Legends",
                Game = "Apex Legends",
                ViewerCount = 756,
                IsLive = true,
                BrainMetrics = new Dictionary<string, string>
                {
                    { "Focus", "85%" },
                    { "Beta", "High" }
                }
            });

            availableStreams.Add(new StreamInfo
            {
                Id = "Stream4",
                StreamerName = "Streamer4",
                Title = "Minecraft",
                Game = "Minecraft",
                ViewerCount = 645,
                IsLive = true,
                BrainMetrics = new Dictionary<string, string>
                {
                    { "Focus", "78%" },
                    { "Alpha", "Medium" }
                }
            });

            availableStreams.Add(new StreamInfo
            {
                Id = "Stream5",
                StreamerName = "Streamer5",
                Title = "Valorant",
                Game = "Valorant",
                ViewerCount = 512,
                IsLive = true,
                BrainMetrics = new Dictionary<string, string>
                {
                    { "Focus", "91%" },
                    { "Beta", "Medium" }
                }
            });

            availableStreams.Add(new StreamInfo
            {
                Id = "Stream6",
                StreamerName = "Streamer6",
                Title = "Rocket League",
                Game = "Rocket League",
                ViewerCount = 394,
                IsLive = true,
                BrainMetrics = new Dictionary<string, string>
                {
                    { "Focus", "82%" },
                    { "Gamma", "High" }
                }
            });
        }

        /// <summary>
        /// Initialize when page appears
        /// </summary>
        public async Task OnAppearingAsync()
        {
            try
            {
                if (!IsInitialized)
                {
                    // In a real implementation, this would fetch streams from a service
                    // For now, we're using placeholder data loaded in the constructor

                    IsInitialized = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnAppearingAsync: {ex.Message}");
            }
        }

        /// <summary>
        /// Searches for streams based on the search query
        /// </summary>
        private async Task SearchStreamsAsync()
        {
            try
            {
                // In a real implementation, this would search for streams based on the search query
                // For now, just log the search query
                Debug.WriteLine($"Searching for streams with query: {SearchQuery}");

                // Here you would filter the AvailableStreams collection based on the search query
                // For now, just display a message
                await Shell.Current.DisplayAlert("Search",
                    $"Searching for: {SearchQuery}",
                    "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error searching for streams: {ex.Message}");
            }
        }

        /// <summary>
        /// Selects a category to filter streams
        /// </summary>
        private async Task SelectCategoryAsync(string category)
        {
            if (string.IsNullOrEmpty(category)) return;

            SelectedCategory = category;

            // In a real implementation, this would filter the streams based on the selected category
            // For now, just log the selected category
            Debug.WriteLine($"Selected category: {category}");

            // Clear search query when changing category
            SearchQuery = string.Empty;
        }

        /// <summary>
        /// Watches a stream
        /// </summary>
        private async Task WatchStreamAsync(string streamId)
        {
            if (string.IsNullOrEmpty(streamId)) return;

            // Find the stream info
            var stream = AvailableStreams.FirstOrDefault(s => s.Id == streamId);
            if (stream == null) return;

            // In a real implementation, this would navigate to the stream viewer
            // For now, just show a placeholder alert
            await Shell.Current.DisplayAlert("Watch Stream",
                $"Opening stream: {stream.Title} by {stream.StreamerName}\n" +
                $"Game: {stream.Game}\n" +
                $"Viewers: {stream.ViewerCount}",
                "OK");

            // For simulating the actual navigation in the prototype:
            // var parameters = new Dictionary<string, object>
            // {
            //     { "StreamId", streamId }
            // };
            // await Shell.Current.GoToAsync("//StreamSpectatorPage", parameters);
        }
    }
}