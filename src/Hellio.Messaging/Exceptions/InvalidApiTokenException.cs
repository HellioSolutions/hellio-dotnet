using System.Text.Json;

namespace Hellio.Messaging
{
    /// <summary>401 - missing or invalid Bearer token.</summary>
    public class InvalidApiTokenException : HellioException
    {
        public InvalidApiTokenException(string message, int statusCode = 401, JsonElement response = default)
            : base(message, statusCode, response)
        {
        }
    }
}
