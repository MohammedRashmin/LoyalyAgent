namespace loyalityAgent2._0.Models
{
    public class WorkflowProgress
    {
        public string Step { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Status { get; set; } = "processing"; // processing, completed, error
        public object? Data { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
