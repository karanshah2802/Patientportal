using Patientportal.Model;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Linq;

namespace Patientportal.AllApicall
{
    public class ApiService
    {
        private static readonly JsonSerializerOptions JsonReadOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;

        public ApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// GET <c>getProfileforpatientportal?id=…</c> / <c>vwPatientProfileforportal</c>: unwraps <c>data</c>/<c>value</c>/<c>result</c>,
        /// maps <c>id</c> / <c>patientProfileId</c> (and similar) onto <see cref="ProfileListItem.Id"/>, and uses <paramref name="queryPatientId"/> if JSON still has no id.
        /// </summary>
        public async Task<ProfileListItem?> GetProfileForPatientPortalAsync(string url, string token, long queryPatientId)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"API call failed: {response.StatusCode}");
                    return null;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);
                var root = UnwrapProfilePayload(doc.RootElement);
                if (root.ValueKind != JsonValueKind.Object)
                    return null;

                ProfileListItem? profile;
                try
                {
                    profile = JsonSerializer.Deserialize<ProfileListItem>(root.GetRawText(), JsonReadOptions);
                }
                catch (JsonException)
                {
                    profile = new ProfileListItem();
                }

                if (profile == null)
                    return null;

                if (profile.Id <= 0)
                {
                    var fromJson = TryReadProfileIdFromObject(root);
                    if (fromJson > 0)
                        profile.Id = fromJson;
                }

                if (profile.Id <= 0 && queryPatientId > 0)
                    profile.Id = queryPatientId;

                return profile;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                return null;
            }
        }

        private static JsonElement UnwrapProfilePayload(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object)
                return root;
            foreach (var wrap in new[] { "data", "value", "result", "item" })
            {
                if (root.TryGetProperty(wrap, out var inner) && inner.ValueKind == JsonValueKind.Object)
                    return inner;
            }
            return root;
        }

        private static long TryReadProfileIdFromObject(JsonElement obj)
        {
            foreach (var prop in obj.EnumerateObject())
            {
                if (!prop.Name.Equals("id", StringComparison.OrdinalIgnoreCase)
                    && !prop.Name.Equals("patientprofileid", StringComparison.OrdinalIgnoreCase)
                    && !prop.Name.Equals("profileid", StringComparison.OrdinalIgnoreCase)
                    && !prop.Name.Equals("patientid", StringComparison.OrdinalIgnoreCase))
                    continue;

                var el = prop.Value;
                if (el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var n) && n > 0)
                    return n;
                if (el.ValueKind == JsonValueKind.String
                    && long.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n)
                    && n > 0)
                    return n;
            }
            return 0;
        }

        /// <summary>
        /// When several profiles share a mobile number, prefer the independent account (not a dependent), then main profile.
        /// Requires <see cref="ProfileListItem.Isdependent"/> / <see cref="ProfileListItem.IsMainProfile"/> from the API; if both are default, ordering cannot distinguish rows.
        /// </summary>
        public static ProfileListItem? PreferIndependentProfile(IReadOnlyList<ProfileListItem> profiles)
        {
            if (profiles == null || profiles.Count == 0)
                return null;
            var withId = profiles.Where(p => p != null && p.Id > 0).ToList();
            if (withId.Count == 0)
                return null;
            if (withId.Count == 1)
                return withId[0];
            return withId
                .OrderByDescending(p => !p.Isdependent)
                .ThenByDescending(p => p.IsMainProfile)
                .ThenBy(p => p.Id)
                .First();
        }

        private static List<ProfileListItem> ParseProfilesFromMobileLookupResponse(string jsonResponse)
        {
            var list = new List<ProfileListItem>();
            if (string.IsNullOrWhiteSpace(jsonResponse))
                return list;
            try
            {
                using var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Array)
                {
                    AddProfilesFromJsonArray(root, list);
                    return list;
                }
                if (root.ValueKind == JsonValueKind.Object)
                {
                    foreach (var wrap in new[] { "data", "value", "result", "items" })
                    {
                        if (!root.TryGetProperty(wrap, out var inner))
                            continue;
                        if (inner.ValueKind == JsonValueKind.Array)
                        {
                            AddProfilesFromJsonArray(inner, list);
                            if (list.Count > 0)
                                return list;
                        }
                        if (inner.ValueKind == JsonValueKind.Object)
                        {
                            TryAddSingleProfileFromObject(inner, list);
                            if (list.Count > 0)
                                return list;
                        }
                    }
                    TryAddSingleProfileFromObject(root, list);
                }
            }
            catch (JsonException)
            {
                // ignore
            }
            return list;
        }

        private static void AddProfilesFromJsonArray(JsonElement array, List<ProfileListItem> list)
        {
            foreach (var el in array.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object)
                    continue;
                TryAddSingleProfileFromObject(el, list);
            }
        }

        private static void TryAddSingleProfileFromObject(JsonElement obj, List<ProfileListItem> list)
        {
            ProfileListItem? p;
            try
            {
                p = JsonSerializer.Deserialize<ProfileListItem>(obj.GetRawText(), JsonReadOptions);
            }
            catch (JsonException)
            {
                return;
            }
            if (p == null)
                return;
            if (p.Id <= 0)
            {
                var id = TryReadProfileIdFromObject(obj);
                if (id > 0)
                    p.Id = id;
            }
            if (p.Id > 0)
                list.Add(p);
        }

        /// <summary>
        /// GET <c>GetpatientByMobilenumber</c>: handles a single profile JSON or an array; picks independent/main when multiple rows are returned.
        /// </summary>
        public async Task<ProfileListItem?> GetPatientByMobilePreferIndependentAsync(string url, string token)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"API call failed: {response.StatusCode}");
                    return null;
                }
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var profiles = ParseProfilesFromMobileLookupResponse(jsonResponse);
                return PreferIndependentProfile(profiles);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>GET with access to the raw response body (for diagnostics).</summary>
        public async Task<(T? Result, string RawBody, HttpStatusCode StatusCode)> GetAsyncWithRawBody<T>(string url, string token)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await _httpClient.GetAsync(url);
                var rawBody = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    return (default, rawBody, response.StatusCode);
                try
                {
                    var result = JsonSerializer.Deserialize<T>(rawBody, JsonReadOptions);
                    return (result, rawBody, response.StatusCode);
                }
                catch (JsonException)
                {
                    return (default, rawBody, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                return (default, ex.ToString(), HttpStatusCode.ServiceUnavailable);
            }
        }

        public async Task<T?> GetAsync<T>(string url, string token)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                HttpResponseMessage response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<T>(jsonResponse, JsonReadOptions);
                }
                else
                {
                    Console.WriteLine($"API call failed: {response.StatusCode}");
                    return default;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                return default;
            }
        }
        public async Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest requestData, string token)
        {
            try
            {
                if (!string.IsNullOrEmpty(token))
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                string jsonRequest = JsonSerializer.Serialize(requestData);
                HttpContent content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<TResponse>(jsonResponse, JsonReadOptions);

                    // Ensure IsSuccess is true when the response is successful
                    if (result is ApiResponse apiResponse)
                    {
                        apiResponse.IsSuccess = true;
                        return (TResponse)(object)apiResponse;
                    }

                    return result;
                }
                else
                {
                    Console.WriteLine($"POST API call failed: {response.StatusCode}");
                    return default;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in POST: {ex.Message}");
                return default;
            }
        }
    }
}
