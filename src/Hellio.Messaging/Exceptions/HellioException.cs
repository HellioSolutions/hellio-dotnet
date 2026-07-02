using System;
using System.Text.Json;

namespace Hellio.Messaging
{
    /// <summary>
    /// Base for every error returned by the Hellio API. Carries the HTTP status
    /// code and the parsed JSON response body so callers can inspect details.
    /// </summary>
    public class HellioException : Exception
    {
        /// <summary>HTTP status code that triggered the error, or 0 when unknown.</summary>
        public int StatusCode { get; }

        /// <summary>The parsed JSON response body (may be an undefined element).</summary>
        public JsonElement Response { get; }

        public HellioException(string message, int statusCode = 0, JsonElement response = default)
            : base(message)
        {
            StatusCode = statusCode;
            Response = response;
        }

        /// <summary>
        /// Field-level validation errors under the body's <c>errors</c> key, when present.
        /// Returns an undefined element otherwise.
        /// </summary>
        public JsonElement Errors
        {
            get
            {
                if (Response.ValueKind == JsonValueKind.Object &&
                    Response.TryGetProperty("errors", out var errors))
                {
                    return errors;
                }

                return default;
            }
        }
    }
}
