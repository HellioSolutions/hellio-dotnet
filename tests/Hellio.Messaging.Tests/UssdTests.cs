using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Hellio.Messaging.Tests
{
    public class UssdTests
    {
        private static HellioClient Client(MockHttpMessageHandler handler)
            => new HellioClient(
                token: "test-token",
                baseUrl: "https://api.helliomessaging.com/v1",
                httpClient: new HttpClient(handler));

        // ------------------------------------------------------------- Pricing

        [Fact]
        public async Task Pricing_GetsPricingAndMapsFields()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK,
                "{\"data\":{\"short_code\":\"*920#\",\"currency\":\"GHS\"," +
                "\"session_prices\":[{\"network\":\"MTN\",\"slug\":\"mtn\",\"session_price\":\"0.0300\"}]," +
                "\"extension_prices\":[{\"length\":3,\"monthly_price\":\"50.00\"}]}}");
            var client = Client(handler);

            var pricing = await client.Ussd.PricingAsync();

            Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
            Assert.Equal("https://api.helliomessaging.com/v1/ussd/pricing",
                handler.LastRequest!.RequestUri!.ToString());
            Assert.Equal("*920#", pricing.ShortCode);
            Assert.Equal("GHS", pricing.Currency);
            Assert.Single(pricing.SessionPrices);
            Assert.Equal("MTN", pricing.SessionPrices[0].Network);
            Assert.Equal(0.03m, pricing.SessionPrices[0].SessionPrice);
            Assert.Single(pricing.ExtensionPrices);
            Assert.Equal(3, pricing.ExtensionPrices[0].Length);
            Assert.Equal(50.00m, pricing.ExtensionPrices[0].MonthlyPrice);
        }

        [Fact]
        public async Task Availability_AddsCodeQuery()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK,
                "{\"data\":{\"code\":\"100\",\"valid\":true,\"available\":false,\"monthly_price\":null}}");
            var client = Client(handler);

            var availability = await client.Ussd.AvailabilityAsync("100");

            Assert.Equal("https://api.helliomessaging.com/v1/ussd/pricing/availability?code=100",
                handler.LastRequest!.RequestUri!.ToString());
            Assert.Equal("100", availability.Code);
            Assert.True(availability.Valid);
            Assert.False(availability.Available);
            Assert.Null(availability.MonthlyPrice);
        }

        // ---------------------------------------------------------------- Apps

        [Fact]
        public async Task Apps_List_ReturnsTypedList()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK,
                "{\"data\":[{\"id\":1,\"name\":\"Airtime\",\"callback_url\":\"https://a.com/cb\"," +
                "\"secret\":\"shh\",\"active\":true,\"created_at\":\"2026-01-01T00:00:00Z\"}]}");
            var client = Client(handler);

            var apps = await client.Ussd.Apps.ListAsync();

            Assert.Equal("https://api.helliomessaging.com/v1/ussd/apps",
                handler.LastRequest!.RequestUri!.ToString());
            Assert.Single(apps);
            Assert.Equal(1, apps[0].Id);
            Assert.Equal("Airtime", apps[0].Name);
            Assert.True(apps[0].Active);
        }

        [Fact]
        public async Task Apps_List_AddsCursorWhenGiven()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"data\":[]}");
            var client = Client(handler);

            await client.Ussd.Apps.ListAsync(cursor: "abc123");

            Assert.Equal("https://api.helliomessaging.com/v1/ussd/apps?cursor=abc123",
                handler.LastRequest!.RequestUri!.ToString());
        }

        [Fact]
        public async Task Apps_Create_PostsNameAndCallback()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.Created,
                "{\"data\":{\"id\":7,\"name\":\"Airtime\",\"callback_url\":\"https://a.com/cb\",\"secret\":\"whsec_x\",\"active\":true}}");
            var client = Client(handler);

            var app = await client.Ussd.Apps.CreateAsync("Airtime", "https://a.com/cb");

            Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
            Assert.Equal("https://api.helliomessaging.com/v1/ussd/apps",
                handler.LastRequest!.RequestUri!.ToString());
            using var body = JsonDocument.Parse(handler.LastRequestBody!);
            Assert.Equal("Airtime", body.RootElement.GetProperty("name").GetString());
            Assert.Equal("https://a.com/cb", body.RootElement.GetProperty("callback_url").GetString());
            Assert.Equal(7, app.Id);
            Assert.Equal("whsec_x", app.Secret);
        }

        [Fact]
        public async Task Apps_Update_UsesPutWithActiveFlag()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK,
                "{\"data\":{\"id\":7,\"name\":\"New\",\"callback_url\":\"https://a.com/cb2\",\"active\":false}}");
            var client = Client(handler);

            var app = await client.Ussd.Apps.UpdateAsync(7, "New", "https://a.com/cb2", active: false);

            Assert.Equal(HttpMethod.Put, handler.LastRequest!.Method);
            Assert.Equal("https://api.helliomessaging.com/v1/ussd/apps/7",
                handler.LastRequest!.RequestUri!.ToString());
            using var body = JsonDocument.Parse(handler.LastRequestBody!);
            Assert.Equal("New", body.RootElement.GetProperty("name").GetString());
            Assert.False(body.RootElement.GetProperty("active").GetBoolean());
            Assert.False(app.Active);
        }

        [Fact]
        public async Task Apps_Delete_UsesDeleteMethod()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.NoContent, "");
            var client = Client(handler);

            await client.Ussd.Apps.DeleteAsync(7);

            Assert.Equal(HttpMethod.Delete, handler.LastRequest!.Method);
            Assert.Equal("https://api.helliomessaging.com/v1/ussd/apps/7",
                handler.LastRequest!.RequestUri!.ToString());
        }

        // ---------------------------------------------------------- Extensions

        [Fact]
        public async Task Extensions_List_ReturnsTypedList()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK,
                "{\"data\":[{\"id\":3,\"code\":\"100\",\"dial_string\":\"*920*100#\",\"length\":3," +
                "\"status\":\"active\",\"monthly_price\":50,\"auto_renew\":true,\"app_id\":7,\"expires_at\":\"2026-08-01T00:00:00Z\"}]}");
            var client = Client(handler);

            var extensions = await client.Ussd.Extensions.ListAsync();

            Assert.Single(extensions);
            Assert.Equal("100", extensions[0].Code);
            Assert.Equal("*920*100#", extensions[0].DialString);
            Assert.Equal(50m, extensions[0].MonthlyPrice);
            Assert.True(extensions[0].AutoRenew);
            Assert.Equal(7, extensions[0].AppId);
        }

        [Fact]
        public async Task Extensions_Rent_PostsCodeAndAppIdWhenGiven()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.Created,
                "{\"data\":{\"id\":3,\"code\":\"100\",\"status\":\"active\"}}");
            var client = Client(handler);

            var ext = await client.Ussd.Extensions.RentAsync("100", appId: 7);

            Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
            Assert.Equal("https://api.helliomessaging.com/v1/ussd/extensions",
                handler.LastRequest!.RequestUri!.ToString());
            using var body = JsonDocument.Parse(handler.LastRequestBody!);
            Assert.Equal("100", body.RootElement.GetProperty("code").GetString());
            Assert.Equal(7, body.RootElement.GetProperty("app_id").GetInt64());
            Assert.Equal(3, ext.Id);
        }

        [Fact]
        public async Task Extensions_Rent_OmitsAppIdWhenNull()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.Created,
                "{\"data\":{\"id\":3,\"code\":\"100\"}}");
            var client = Client(handler);

            await client.Ussd.Extensions.RentAsync("100");

            using var body = JsonDocument.Parse(handler.LastRequestBody!);
            Assert.False(body.RootElement.TryGetProperty("app_id", out _));
        }

        [Fact]
        public async Task Extensions_Rent_MapsConflictToConflictException()
        {
            var handler = new MockHttpMessageHandler((HttpStatusCode)409,
                "{\"error\":\"extension_unavailable\"}");
            var client = Client(handler);

            var ex = await Assert.ThrowsAsync<ConflictException>(
                () => client.Ussd.Extensions.RentAsync("100"));

            Assert.Equal(409, ex.StatusCode);
            Assert.Equal("extension_unavailable", ex.Message);
        }

        [Fact]
        public async Task Extensions_Rent_MapsInsufficientBalance()
        {
            var handler = new MockHttpMessageHandler((HttpStatusCode)402,
                "{\"error\":\"insufficient_balance\"}");
            var client = Client(handler);

            var ex = await Assert.ThrowsAsync<InsufficientBalanceException>(
                () => client.Ussd.Extensions.RentAsync("100"));

            Assert.Equal(402, ex.StatusCode);
            Assert.Equal("insufficient_balance", ex.Message);
        }

        [Fact]
        public async Task Extensions_Release_UsesDeleteMethod()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.NoContent, "");
            var client = Client(handler);

            await client.Ussd.Extensions.ReleaseAsync(3);

            Assert.Equal(HttpMethod.Delete, handler.LastRequest!.Method);
            Assert.Equal("https://api.helliomessaging.com/v1/ussd/extensions/3",
                handler.LastRequest!.RequestUri!.ToString());
        }

        // ------------------------------------------------------------ Sessions

        [Fact]
        public async Task Sessions_List_AddsStatusQuery()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK,
                "{\"data\":[{\"id\":9,\"session_ref\":\"ref-9\",\"msisdn\":\"233241234567\"," +
                "\"service_code\":\"*920*100#\",\"status\":\"ended\",\"steps\":4,\"charge\":\"0.12\",\"sandbox\":false}]}");
            var client = Client(handler);

            var sessions = await client.Ussd.Sessions.ListAsync(status: "ended");

            Assert.Equal("https://api.helliomessaging.com/v1/ussd/sessions?status=ended",
                handler.LastRequest!.RequestUri!.ToString());
            Assert.Single(sessions);
            Assert.Equal("ref-9", sessions[0].SessionRef);
            Assert.Equal(4, sessions[0].Steps);
            Assert.Equal(0.12m, sessions[0].Charge);
        }

        [Fact]
        public async Task Sessions_List_NoQueryWhenNoFilters()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"data\":[]}");
            var client = Client(handler);

            await client.Ussd.Sessions.ListAsync();

            Assert.Equal("https://api.helliomessaging.com/v1/ussd/sessions",
                handler.LastRequest!.RequestUri!.ToString());
        }

        [Fact]
        public async Task Sessions_Get_FetchesById()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK,
                "{\"data\":{\"id\":9,\"session_ref\":\"ref-9\",\"status\":\"ended\",\"steps\":2}}");
            var client = Client(handler);

            var session = await client.Ussd.Sessions.GetAsync(9);

            Assert.Equal("https://api.helliomessaging.com/v1/ussd/sessions/9",
                handler.LastRequest!.RequestUri!.ToString());
            Assert.Equal(9, session.Id);
            Assert.Equal("ended", session.Status);
        }

        // ------------------------------------------------------------ Simulate

        [Fact]
        public async Task Simulate_PostsBodyAndMapsResult()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK,
                "{\"data\":{\"message\":\"Welcome\",\"action\":\"continue\",\"continue\":true}}");
            var client = Client(handler);

            var result = await client.Ussd.SimulateAsync(
                msisdn: "233241234567",
                serviceCode: "*920*100#",
                input: "1",
                sessionId: "sess-1",
                newSession: false);

            Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
            Assert.Equal("https://api.helliomessaging.com/v1/ussd/simulate",
                handler.LastRequest!.RequestUri!.ToString());
            using var body = JsonDocument.Parse(handler.LastRequestBody!);
            var root = body.RootElement;
            Assert.Equal("sess-1", root.GetProperty("session_id").GetString());
            Assert.Equal("233241234567", root.GetProperty("msisdn").GetString());
            Assert.Equal("*920*100#", root.GetProperty("service_code").GetString());
            Assert.Equal("1", root.GetProperty("input").GetString());
            Assert.False(root.GetProperty("new_session").GetBoolean());
            Assert.Equal("Welcome", result.Message);
            Assert.Equal("continue", result.Action);
            Assert.True(result.Continue);
        }

        [Fact]
        public async Task Simulate_OmitsSessionIdWhenNewSession()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK,
                "{\"data\":{\"message\":\"Welcome\",\"action\":\"continue\",\"continue\":true}}");
            var client = Client(handler);

            await client.Ussd.SimulateAsync("233241234567", "*920*100#", newSession: true);

            using var body = JsonDocument.Parse(handler.LastRequestBody!);
            Assert.False(body.RootElement.TryGetProperty("session_id", out _));
            Assert.False(body.RootElement.TryGetProperty("input", out _));
            Assert.True(body.RootElement.GetProperty("new_session").GetBoolean());
        }
    }
}
