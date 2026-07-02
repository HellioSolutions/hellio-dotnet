using System.Text.Json;

namespace Hellio.Messaging
{
    /// <summary>429 - too many requests (120/min per token).</summary>
    public class RateLimitException : HellioException
    {
        public RateLimitException(string message, int statusCode = 429, JsonElement response = default)
            : base(message, statusCode, response)
        {
        }
    }
}
