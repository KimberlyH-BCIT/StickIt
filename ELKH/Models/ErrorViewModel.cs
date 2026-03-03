namespace ELKH.Models
{
    /// <summary>
    /// View model for displaying error information to the user.
    /// </summary>
    public class ErrorViewModel
    {
        /// <summary>
        /// Unique request identifier for tracing errors.
        /// </summary>
        public string? RequestId { get; set; }

        /// <summary>
        /// Whether to show the request ID in the error view.
        /// </summary>
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
