namespace QuantConnect.Brokerages
{
    /// <summary>
    /// Defines a message received at a web socket
    /// </summary>
    public class WebSocketMessageBytes
    {
        /// <summary>
        /// Gets the raw message data as text
        /// </summary>
        public byte[] Message { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocketMessage"/> class
        /// </summary>
        /// <param name="message">The message</param>
        public WebSocketMessageBytes(byte[] message)
        {
            Message = message;
        }
    }
}