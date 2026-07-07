using System.Text.Json;

namespace Hellio.Messaging
{
    /// <summary>
    /// 409 - the request conflicts with the current state of the resource. For USSD,
    /// this is raised when renting an extension that is not available
    /// (<c>error: "extension_unavailable"</c>).
    /// </summary>
    public class ConflictException : HellioException
    {
        public ConflictException(string message, int statusCode = 409, JsonElement response = default)
            : base(message, statusCode, response)
        {
        }
    }
}
