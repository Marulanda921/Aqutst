namespace TCP_AQUTEST.Models.Entity
{
    /// <summary>
    /// Represents a message received from a TCP connection.
    /// </summary>
    public class TcpMessage
    {
        public string Data { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
