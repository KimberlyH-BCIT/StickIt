namespace ELKH.Services
{
    /// <summary>
    /// Result of a rating create, edit, or delete operation.
    /// </summary>
    public class RatingOperationResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        /// <summary>Product ID for controller redirect after the operation.</summary>
        public int ProductId { get; init; }
    }
}
