using System.Text.Json;

namespace Hellio.Messaging
{
    /// <summary>422 - invalid request. Field errors are exposed via <see cref="HellioException.Errors"/>.</summary>
    public class ValidationException : HellioException
    {
        public ValidationException(string message, int statusCode = 422, JsonElement response = default)
            : base(message, statusCode, response)
        {
        }
    }
}
