using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hellio.Messaging
{
    /// <summary>
    /// USSD endpoints. Reached through <c>client.Ussd</c>. Covers short-code pricing and
    /// availability, applications (<c>client.Ussd.Apps</c>), extension rentals
    /// (<c>client.Ussd.Extensions</c>), sessions (<c>client.Ussd.Sessions</c>), and the
    /// callback simulator. Requires a token with the <c>ussd</c> ability.
    /// </summary>
    public sealed class UssdService
    {
        private readonly HellioClient _client;

        internal UssdService(HellioClient client)
        {
            _client = client;
            Apps = new UssdAppsService(client);
            Extensions = new UssdExtensionsService(client);
            Sessions = new UssdSessionsService(client);
        }

        /// <summary>USSD applications: list, create, update, delete.</summary>
        public UssdAppsService Apps { get; }

        /// <summary>Rented USSD extensions: list, rent, release.</summary>
        public UssdExtensionsService Extensions { get; }

        /// <summary>USSD sessions: list and fetch.</summary>
        public UssdSessionsService Sessions { get; }

        /// <summary>GET <c>ussd/pricing</c>. Session and extension pricing for your short code.</summary>
        public async Task<UssdPricing> PricingAsync(CancellationToken cancellationToken = default)
        {
            var body = await _client.GetAsync("ussd/pricing", null, cancellationToken).ConfigureAwait(false);
            return UssdJson.Data<UssdPricing>(body);
        }

        /// <summary>GET <c>ussd/pricing/availability</c>. Whether a specific extension code can be rented.</summary>
        public async Task<UssdAvailability> AvailabilityAsync(string code, CancellationToken cancellationToken = default)
        {
            var query = new Dictionary<string, string> { ["code"] = code };
            var body = await _client.GetAsync("ussd/pricing/availability", query, cancellationToken).ConfigureAwait(false);
            return UssdJson.Data<UssdAvailability>(body);
        }

        /// <summary>
        /// POST <c>ussd/simulate</c>. Drives your app's callback URL as Hellio would, so you can
        /// test a flow without a live dial. Always runs in the sandbox (test mode). Set
        /// <paramref name="newSession"/> for the first step and pass the returned session reference
        /// back as <paramref name="sessionId"/> on later steps. Leave <paramref name="serviceCode"/>
        /// null to use the shared short code. An app you do not own returns
        /// <see cref="ValidationException"/> (422, <c>error: "unknown_app"</c>).
        /// </summary>
        public async Task<UssdSimulateResult> SimulateAsync(
            string appId,
            string msisdn,
            string? input = null,
            string? sessionId = null,
            bool newSession = false,
            string? serviceCode = null,
            CancellationToken cancellationToken = default)
        {
            var request = HellioClient.Compact(new Dictionary<string, object?>
            {
                ["app_id"] = appId,
                ["session_id"] = sessionId,
                ["msisdn"] = msisdn,
                ["service_code"] = serviceCode,
                ["input"] = input,
                ["new_session"] = newSession,
            });

            var body = await _client.PostAsync("ussd/simulate", request, cancellationToken).ConfigureAwait(false);
            return UssdJson.Data<UssdSimulateResult>(body);
        }
    }

    /// <summary>USSD applications resource. Reached through <c>client.Ussd.Apps</c>.</summary>
    public sealed class UssdAppsService
    {
        private readonly HellioClient _client;

        internal UssdAppsService(HellioClient client) => _client = client;

        /// <summary>GET <c>ussd/apps</c>. Your USSD applications. Pass <paramref name="cursor"/> to page.</summary>
        public async Task<IReadOnlyList<UssdApp>> ListAsync(string? cursor = null, CancellationToken cancellationToken = default)
        {
            var body = await _client.GetAsync("ussd/apps", UssdQuery.Cursor(cursor), cancellationToken).ConfigureAwait(false);
            return UssdJson.Data<List<UssdApp>>(body);
        }

        /// <summary>
        /// POST <c>ussd/apps</c>. Register an application. The response carries both signing
        /// secrets (<c>test_secret</c> and <c>live_secret</c>); new apps start in "test" mode.
        /// </summary>
        public async Task<UssdApp> CreateAsync(string name, string callbackUrl, CancellationToken cancellationToken = default)
        {
            var request = new Dictionary<string, object?>
            {
                ["name"] = name,
                ["callback_url"] = callbackUrl,
            };

            var body = await _client.PostAsync("ussd/apps", request, cancellationToken).ConfigureAwait(false);
            return UssdJson.Data<UssdApp>(body);
        }

        /// <summary>PUT <c>ussd/apps/{id}</c>. Update the name, callback URL, or active flag.</summary>
        public async Task<UssdApp> UpdateAsync(string id, string name, string callbackUrl, bool active, CancellationToken cancellationToken = default)
        {
            var request = new Dictionary<string, object?>
            {
                ["name"] = name,
                ["callback_url"] = callbackUrl,
                ["active"] = active,
            };

            var body = await _client.PutAsync("ussd/apps/" + id, request, cancellationToken).ConfigureAwait(false);
            return UssdJson.Data<UssdApp>(body);
        }

        /// <summary>
        /// POST <c>ussd/apps/{id}/mode</c>. Switch the app between "test" and "live". Switching to
        /// "live" before an extension is purchased throws <see cref="ExtensionRequiredException"/>
        /// (402, <c>error: "extension_required"</c>). Returns the updated app.
        /// </summary>
        public async Task<UssdApp> SetModeAsync(string id, string mode, CancellationToken cancellationToken = default)
        {
            var request = new Dictionary<string, object?> { ["mode"] = mode };
            var body = await _client.PostAsync("ussd/apps/" + id + "/mode", request, cancellationToken).ConfigureAwait(false);
            return UssdJson.Data<UssdApp>(body);
        }

        /// <summary>
        /// POST <c>ussd/apps/{id}/rotate-secret</c>. Generate a fresh signing secret for the given
        /// <paramref name="mode"/> ("test" or "live"). Returns the app carrying the rotated secret.
        /// </summary>
        public async Task<UssdApp> RotateSecretAsync(string id, string mode, CancellationToken cancellationToken = default)
        {
            var request = new Dictionary<string, object?> { ["mode"] = mode };
            var body = await _client.PostAsync("ussd/apps/" + id + "/rotate-secret", request, cancellationToken).ConfigureAwait(false);
            return UssdJson.Data<UssdApp>(body);
        }

        /// <summary>DELETE <c>ussd/apps/{id}</c>. Remove an application.</summary>
        public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
            => _client.DeleteAsync("ussd/apps/" + id, cancellationToken);
    }

    /// <summary>USSD extensions resource. Reached through <c>client.Ussd.Extensions</c>.</summary>
    public sealed class UssdExtensionsService
    {
        private readonly HellioClient _client;

        internal UssdExtensionsService(HellioClient client) => _client = client;

        /// <summary>GET <c>ussd/extensions</c>. Your rented extensions. Pass <paramref name="cursor"/> to page.</summary>
        public async Task<IReadOnlyList<UssdExtension>> ListAsync(string? cursor = null, CancellationToken cancellationToken = default)
        {
            var body = await _client.GetAsync("ussd/extensions", UssdQuery.Cursor(cursor), cancellationToken).ConfigureAwait(false);
            return UssdJson.Data<List<UssdExtension>>(body);
        }

        /// <summary>
        /// POST <c>ussd/extensions</c>. Rent an extension code, optionally binding it to an app.
        /// The rental is drawn from your dedicated USSD balance (separate from SMS credit and the
        /// main wallet). Throws <see cref="ConflictException"/> (409) when the code is taken and
        /// <see cref="InsufficientBalanceException"/> (402, <c>error: "insufficient_ussd_balance"</c>)
        /// when the USSD balance cannot cover the rental.
        /// </summary>
        public async Task<UssdExtension> RentAsync(string code, string? appId = null, CancellationToken cancellationToken = default)
        {
            var request = HellioClient.Compact(new Dictionary<string, object?>
            {
                ["code"] = code,
                ["app_id"] = appId,
            });

            var body = await _client.PostAsync("ussd/extensions", request, cancellationToken).ConfigureAwait(false);
            return UssdJson.Data<UssdExtension>(body);
        }

        /// <summary>DELETE <c>ussd/extensions/{id}</c>. Release a rented extension.</summary>
        public Task ReleaseAsync(string id, CancellationToken cancellationToken = default)
            => _client.DeleteAsync("ussd/extensions/" + id, cancellationToken);
    }

    /// <summary>USSD sessions resource. Reached through <c>client.Ussd.Sessions</c>.</summary>
    public sealed class UssdSessionsService
    {
        private readonly HellioClient _client;

        internal UssdSessionsService(HellioClient client) => _client = client;

        /// <summary>
        /// GET <c>ussd/sessions</c>. Recent sessions, newest first. Filter with
        /// <paramref name="status"/> (e.g. <c>ended</c>) and page with <paramref name="cursor"/>.
        /// </summary>
        public async Task<IReadOnlyList<UssdSession>> ListAsync(string? status = null, string? cursor = null, CancellationToken cancellationToken = default)
        {
            var query = UssdQuery.Cursor(cursor);
            if (!string.IsNullOrEmpty(status))
            {
                query ??= new Dictionary<string, string>();
                query["status"] = status!;
            }

            var body = await _client.GetAsync("ussd/sessions", query, cancellationToken).ConfigureAwait(false);
            return UssdJson.Data<List<UssdSession>>(body);
        }

        /// <summary>GET <c>ussd/sessions/{id}</c>. A single session with its step count and charge.</summary>
        public async Task<UssdSession> GetAsync(string id, CancellationToken cancellationToken = default)
        {
            var body = await _client.GetAsync("ussd/sessions/" + id, null, cancellationToken).ConfigureAwait(false);
            return UssdJson.Data<UssdSession>(body);
        }
    }

    internal static class UssdQuery
    {
        internal static IDictionary<string, string>? Cursor(string? cursor)
            => string.IsNullOrEmpty(cursor)
                ? null
                : new Dictionary<string, string> { ["cursor"] = cursor! };
    }
}
