using System.Text.Json;

namespace Hellio.Messaging
{
    /// <summary>402 - the wallet cannot cover the request.</summary>
    public class InsufficientBalanceException : HellioException
    {
        public InsufficientBalanceException(string message, int statusCode = 402, JsonElement response = default)
            : base(message, statusCode, response)
        {
        }
    }
}
