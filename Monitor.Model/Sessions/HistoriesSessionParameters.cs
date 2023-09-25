namespace Monitor.Model.Sessions
{
    public class HistoriesSessionParameters
    {
        /// <summary>
        /// The Histories database fileName to open
        /// </summary>
        public string FileName { get; set; }
        /// <summary>
        /// Gets or sets whether this session should close after the last packet has been received. When disabled, New packets will reset the session state.
        /// </summary>
        public bool CloseAfterCompleted { get; set; } = true;
    }
}