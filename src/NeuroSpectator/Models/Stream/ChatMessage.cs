namespace NeuroSpectator.Models.Stream
{
    /// <summary>
    /// Represents a chat message in the streaming system that can come from
    /// streamers, viewers, or the system itself.
    /// </summary>
    public class ChatMessage
    {
        /// <summary>
        /// Gets or sets the username of the person who sent the message.
        /// For system messages, this should be "System".
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the content of the message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the message was sent.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets a value indicating whether this message was sent by a streamer.
        /// </summary>
        public bool IsFromStreamer { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this message was sent by a viewer.
        /// </summary>
        public bool IsFromViewer { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is a system message.
        /// </summary>
        public bool IsSystemMessage { get; set; }

        /// <summary>
        /// Creates a streamer message.
        /// </summary>
        public static ChatMessage CreateStreamerMessage(string username, string message)
        {
            return new ChatMessage
            {
                Username = username,
                Message = message,
                IsFromStreamer = true,
                IsFromViewer = false,
                IsSystemMessage = false
            };
        }

        /// <summary>
        /// Creates a viewer message.
        /// </summary>
        public static ChatMessage CreateViewerMessage(string username, string message)
        {
            return new ChatMessage
            {
                Username = username,
                Message = message,
                IsFromStreamer = false,
                IsFromViewer = true,
                IsSystemMessage = false
            };
        }

        /// <summary>
        /// Creates a system message.
        /// </summary>
        public static ChatMessage CreateSystemMessage(string message)
        {
            return new ChatMessage
            {
                Username = "System",
                Message = message,
                IsFromStreamer = false,
                IsFromViewer = false,
                IsSystemMessage = true
            };
        }
    }
}