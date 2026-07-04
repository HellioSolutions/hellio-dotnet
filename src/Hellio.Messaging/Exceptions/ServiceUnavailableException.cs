using System.Text.Json;

namespace Hellio.Messaging
{
    /// <summary>
    /// 503 - the service is temporarily unavailable, because an admin switched it off
    /// (SMS, OTP, voice, WhatsApp, lookup, email) or the API is paused. Transient: retry later.
    /// </summary>
    public class ServiceUnavailableException : HellioException
    {
        public ServiceUnavailableException(string message, int statusCode = 503, JsonElement response = default)
            : base(message, statusCode, response)
        {
        }
    }
}
