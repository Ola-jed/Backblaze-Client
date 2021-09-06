namespace Backblaze_Client
{
    /// <summary>
    /// Handling config for Backblaze api client configuration
    /// </summary>
    public record BackblazeConfig
    {
        public string KeyId { get; init; }
        public string AppKey { get; init;}
        public string ApiBase { get; init;}
        public string BucketId { get; init;}
    }
}