namespace BizfreeApp.Models.DTOs
{
    public class ApiResponse<T>
    {
        public string Message { get; set; }
        public string Status { get; set; }
        public int StatusCode { get; set; }
        public T? Data { get; set; }
        public object? ErrorDetails { get; set; } // Added ErrorDetails property

        public ApiResponse(string message, string status, int statusCode, T? data = default, object? errorDetails = null)
        {
            Message = message;
            Status = status;
            StatusCode = statusCode;
            Data = data;
            ErrorDetails = errorDetails; // Initialize ErrorDetails
        }
    }
}
