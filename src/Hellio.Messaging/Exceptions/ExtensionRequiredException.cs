using System.Text.Json;

namespace Hellio.Messaging
{
    /// <summary>
    /// 402 - switching a USSD app to "live" mode requires a purchased extension. Raised
    /// when calling <c>SetModeAsync(id, "live")</c> before renting an extension
    /// (<c>error: "extension_required"</c>). Rent an extension first, then retry.
    /// </summary>
    public class ExtensionRequiredException : HellioException
    {
        public ExtensionRequiredException(string message, int statusCode = 402, JsonElement response = default)
            : base(message, statusCode, response)
        {
        }
    }
}
