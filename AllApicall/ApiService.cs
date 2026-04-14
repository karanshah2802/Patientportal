using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Patientportal.Model;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;

namespace Patientportal.AllApicall
{
    public class ApiService
    {
        private static readonly JsonSerializerOptions JsonReadOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions AppointmentRequestActionJsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly HttpClient _httpClient;
        private readonly ILogger<ApiService> _logger;

        public ApiService(HttpClient httpClient, ILogger<ApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        private static string TruncateForLog(string? text, int maxChars = 8192)
        {
            if (string.IsNullOrEmpty(text))
                return "(empty)";
            if (text.Length <= maxChars)
                return text;
            return text.Substring(0, maxChars) + "…(truncated)";
        }

        /// <summary>
        /// Many OPS APIs return lists as <c>{ "data": [...] }</c> or <c>{ "value": [...] }</c> instead of a raw JSON array.
        /// Direct <see cref="JsonSerializer.Deserialize{T}(string, JsonSerializerOptions?)"/> then fails with Path: $.
        /// </summary>
        private static JsonElement UnwrapRootToJsonArrayElement(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Array)
                return root;
            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var propName in new[] { "data", "value", "result", "items", "appointments" })
                {
                    if (!root.TryGetProperty(propName, out var inner))
                        continue;
                    if (inner.ValueKind == JsonValueKind.Array)
                        return inner;
                    if (inner.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var nested in new[] { "items", "data", "value", "result" })
                        {
                            if (inner.TryGetProperty(nested, out var arr) && arr.ValueKind == JsonValueKind.Array)
                                return arr;
                        }
                    }
                }
            }

            return root;
        }

        private static T? TryDeserializeWrappedJsonList<T>(string rawBody)
        {
            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                var arrayEl = UnwrapRootToJsonArrayElement(doc.RootElement);
                if (arrayEl.ValueKind != JsonValueKind.Array)
                    return default;
                return JsonSerializer.Deserialize<T>(arrayEl.GetRawText(), JsonReadOptions);
            }
            catch
            {
                return default;
            }
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
                    if (result == null)
                    {
                        var recoveredNull = TryDeserializeWrappedJsonList<T>(rawBody);
                        if (recoveredNull != null)
                        {
                            _logger.LogInformation(
                                "GET list response was an envelope (not top-level array); unwrapped for {Url}",
                                url);
                            return (recoveredNull, rawBody, response.StatusCode);
                        }
                    }

                    return (result, rawBody, response.StatusCode);
                }
                catch (JsonException)
                {
                    var recovered = TryDeserializeWrappedJsonList<T>(rawBody);
                    if (recovered != null)
                    {
                        _logger.LogInformation(
                            "GET list JSON was wrapped; unwrapped after JsonException for {Url}",
                            url);
                        return (recovered, rawBody, response.StatusCode);
                    }

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
                    try
                    {
                        var result = JsonSerializer.Deserialize<T>(jsonResponse, JsonReadOptions);
                        if (result == null)
                        {
                            var recovered = TryDeserializeWrappedJsonList<T>(jsonResponse);
                            if (recovered != null)
                            {
                                _logger.LogInformation(
                                    "GET list response was an envelope; unwrapped for {Url}",
                                    url);
                                return recovered;
                            }
                        }

                        return result;
                    }
                    catch (JsonException)
                    {
                        var recovered = TryDeserializeWrappedJsonList<T>(jsonResponse);
                        if (recovered != null)
                        {
                            _logger.LogInformation(
                                "GET list JSON was wrapped; unwrapped after JsonException for {Url}",
                                url);
                            return recovered;
                        }

                        throw;
                    }
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
        /// <summary>POST JSON; on failure returns response body and status so the portal can forward e.g. 400 + errorCode to the browser.</summary>
        public async Task<(bool Ok, string? ResponseBody, HttpStatusCode Status)> PostJsonAsync(string url, object body, string token, JsonSerializerOptions? options = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(token))
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var opts = options ?? AppointmentRequestActionJsonOptions;
                string jsonRequest = JsonSerializer.Serialize(body, opts);
                HttpContent content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await _httpClient.PostAsync(url, content);
                var responseText = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                    return (true, null, response.StatusCode);
                var errBody = string.IsNullOrWhiteSpace(responseText) ? response.StatusCode.ToString() : NormalizeForwardedJsonPayload(responseText);
                _logger.LogWarning(
                    "PostJsonAsync upstream error. Url={Url} StatusCode={StatusCode} ResponseBody={ResponseBody}",
                    url,
                    (int)response.StatusCode,
                    TruncateForLog(errBody));
                return (false, errBody, response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PostJsonAsync exception for {Url}", url);
                return (false, ex.Message, HttpStatusCode.ServiceUnavailable);
            }
        }

        /// <summary>Leading BOM / whitespace must not block forwarding; <see cref="TrimStart"/> does not remove U+FEFF.</summary>
        private static string NormalizeForwardedJsonPayload(string? rawBody)
        {
            if (string.IsNullOrEmpty(rawBody))
                return string.Empty;
            var s = rawBody.TrimStart();
            while (s.Length > 0 && s[0] == '\uFEFF')
                s = s.Substring(1);
            return s.TrimStart();
        }

        /// <summary>
        /// Passes JSON error payloads to the browser as HTTP 400: upstream 400, other 4xx with a recognizable JSON body (except 401),
        /// or HTTP 200 with <c>succeeded: false</c> (patient portal contract).
        /// </summary>
        public static ContentResult? TryForwardBadRequestJson(HttpStatusCode status, string? rawBody)
        {
            if (string.IsNullOrWhiteSpace(rawBody))
                return null;
            var t = NormalizeForwardedJsonPayload(rawBody);
            if (t.Length == 0 || (t[0] != '{' && t[0] != '['))
                return null;

            if (status == HttpStatusCode.BadRequest)
            {
                return new ContentResult
                {
                    Content = t,
                    ContentType = "application/json; charset=utf-8",
                    StatusCode = (int)HttpStatusCode.BadRequest
                };
            }

            // HTTP 200 with business-failure JSON (camelCase succeeded, PascalCase Succeeded, or error envelope).
            if (status == HttpStatusCode.OK
                && (JsonBodyIndicatesPortalSucceededFalse(t) || JsonLooksLikeForwardablePortalError(t)))
            {
                return new ContentResult
                {
                    Content = t,
                    ContentType = "application/json; charset=utf-8",
                    StatusCode = (int)HttpStatusCode.BadRequest
                };
            }

            var code = (int)status;
            if (code >= 400 && code < 500 && status != HttpStatusCode.Unauthorized && JsonLooksLikeForwardablePortalError(t))
            {
                return new ContentResult
                {
                    Content = t,
                    ContentType = "application/json; charset=utf-8",
                    StatusCode = (int)HttpStatusCode.BadRequest
                };
            }

            if (code >= 500 && code < 600 && JsonLooksLikeForwardablePortalError(t))
            {
                return new ContentResult
                {
                    Content = t,
                    ContentType = "application/json; charset=utf-8",
                    StatusCode = (int)HttpStatusCode.BadRequest
                };
            }

            // Any other 4xx (except 401) with a JSON object body — forward so the browser can show message/errors.
            if (code >= 400 && code < 500 && status != HttpStatusCode.Unauthorized && t[0] == '{')
            {
                try
                {
                    using var doc = JsonDocument.Parse(t);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        return new ContentResult
                        {
                            Content = t,
                            ContentType = "application/json; charset=utf-8",
                            StatusCode = (int)HttpStatusCode.BadRequest
                        };
                    }
                }
                catch (JsonException)
                {
                    // not JSON
                }
            }

            return null;
        }

        /// <summary>True when JSON looks like an API error envelope we can show in the portal (message, errorCode, errors, etc.).</summary>
        private static bool JsonLooksLikeForwardablePortalError(string rawBody)
        {
            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    return false;
                var root = doc.RootElement;
                if (JsonBodyIndicatesPortalSucceededFalse(rawBody))
                    return true;

                if (root.TryGetProperty("errorCode", out var ec) || root.TryGetProperty("ErrorCode", out ec))
                {
                    if (ec.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(ec.GetString()))
                        return true;
                    if (ec.ValueKind == JsonValueKind.Number)
                        return true;
                }

                if (root.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(msg.GetString()))
                    return true;
                if (root.TryGetProperty("Message", out var msg2) && msg2.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(msg2.GetString()))
                    return true;
                if (root.TryGetProperty("detail", out var det) && det.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(det.GetString()))
                    return true;
                if (root.TryGetProperty("title", out var ttl) && ttl.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(ttl.GetString()))
                    return true;

                if (root.TryGetProperty("errors", out var errs) || root.TryGetProperty("Errors", out errs))
                {
                    if (errs.ValueKind == JsonValueKind.Array && errs.GetArrayLength() > 0)
                        return true;
                    if (errs.ValueKind == JsonValueKind.Object && errs.EnumerateObject().Any())
                        return true;
                }

                return false;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool JsonBodyIndicatesPortalSucceededFalse(string rawBody)
        {
            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    return false;
                // JsonDocument.TryGetProperty is case-sensitive; APIs vary (succeeded vs Succeeded).
                if (root.TryGetProperty("succeeded", out var s) && s.ValueKind == JsonValueKind.False)
                    return true;
                if (root.TryGetProperty("SUCCEEDED", out var s2) && s2.ValueKind == JsonValueKind.False)
                    return true;
                if (root.TryGetProperty("Succeeded", out var s3) && s3.ValueKind == JsonValueKind.False)
                    return true;
                return false;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static void NormalizePatientPortalApiResponse(ApiResponse apiResponse)
        {
            if (apiResponse.Succeeded.HasValue)
                apiResponse.IsSuccess = apiResponse.Succeeded.Value;
            else
                apiResponse.IsSuccess = true;
        }

        public async Task<(bool Ok, TResponse? Data, HttpStatusCode Status, string? RawBody)> PostAsyncWithStatus<TRequest, TResponse>(string url, TRequest requestData, string token)
        {
            try
            {
                if (!string.IsNullOrEmpty(token))
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                string jsonRequest = JsonSerializer.Serialize(requestData);
                HttpContent content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.PostAsync(url, content);
                string jsonResponse = NormalizeForwardedJsonPayload(await response.Content.ReadAsStringAsync());

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<TResponse>(jsonResponse, JsonReadOptions);

                    if (result is ApiResponse apiResponse)
                    {
                        NormalizePatientPortalApiResponse(apiResponse);
                        if (!apiResponse.IsSuccess)
                        {
                            _logger.LogWarning(
                                "PostAsyncWithStatus upstream business failure (HTTP {StatusCode}). Url={Url} ResponseBody={ResponseBody}",
                                (int)response.StatusCode,
                                url,
                                TruncateForLog(jsonResponse));
                            return (false, (TResponse)(object)apiResponse, response.StatusCode, jsonResponse);
                        }
                        return (true, (TResponse)(object)apiResponse, response.StatusCode, null);
                    }

                    return (true, result, response.StatusCode, null);
                }

                _logger.LogWarning(
                    "PostAsyncWithStatus upstream HTTP error. Url={Url} StatusCode={StatusCode} ResponseBody={ResponseBody}",
                    url,
                    (int)response.StatusCode,
                    TruncateForLog(jsonResponse));
                return (false, default, response.StatusCode, jsonResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PostAsyncWithStatus exception for {Url}", url);
                return (false, default, HttpStatusCode.ServiceUnavailable, ex.Message);
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

                    if (result is ApiResponse apiResponse)
                    {
                        NormalizePatientPortalApiResponse(apiResponse);
                        return (TResponse)(object)apiResponse;
                    }

                    return result;
                }
                else
                {
                    var failBody = NormalizeForwardedJsonPayload(await response.Content.ReadAsStringAsync());
                    _logger.LogWarning(
                        "PostAsync upstream HTTP error. Url={Url} StatusCode={StatusCode} ResponseBody={ResponseBody}",
                        url,
                        (int)response.StatusCode,
                        TruncateForLog(failBody));
                    return default;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PostAsync exception for {Url}", url);
                return default;
            }
        }
    }
}
