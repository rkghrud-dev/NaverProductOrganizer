using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NaverProductOrganizer;

internal sealed class NaverCommerceClient
{
    private static readonly Uri BaseUri = new("https://api.commerce.naver.com/external");
    private readonly HttpClient _httpClient;
    private readonly Dictionary<long, CachedToken> _tokens = new();

    public NaverCommerceClient()
    {
        _httpClient = new HttpClient { BaseAddress = BaseUri, Timeout = TimeSpan.FromSeconds(60) };
    }

    public async Task<IReadOnlyList<ProductRecord>> FetchAllProductsAsync(
        NaverAccount account,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(account, cancellationToken);
        var products = new List<ProductRecord>();
        const int pageSize = 100;
        const int maxRetryCount = 6;

        for (var page = 1; page <= 10000; page++)
        {
            progress?.Report($"상품 목록 조회 중: {page}페이지");
            string text;
            HttpResponseMessage response;

            for (var attempt = 1; ; attempt++)
            {
                using var request = CreateProductSearchRequest(accessToken, page, pageSize);
                response = await SendWithTokenFallbackAsync(account, request, cancellationToken);
                text = await response.Content.ReadAsStringAsync(cancellationToken);

                if ((int)response.StatusCode != 429 || attempt >= maxRetryCount)
                {
                    break;
                }

                var delay = GetRetryDelay(response, attempt);
                response.Dispose();
                progress?.Report($"요청 제한 감지: {delay.TotalSeconds:N0}초 대기 후 {page}페이지 재시도({attempt}/{maxRetryCount})");
                await Task.Delay(delay, cancellationToken);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"상품 목록 조회 실패: {(int)response.StatusCode} {response.ReasonPhrase}\r\n{text}");
                }
            }

            using var document = JsonDocument.Parse(text);
            var pageItems = ProductJsonParser.ParseSearchResponse(account, document.RootElement).ToList();
            products.AddRange(pageItems);
            progress?.Report($"상품 목록 저장 준비: {products.Count:N0}개 수집");

            if (pageItems.Count < pageSize || IsLastPage(document.RootElement, page))
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(900), cancellationToken);
        }

        return products;
    }

    public async Task<JsonNode> GetOriginProductAsync(
        NaverAccount account,
        string originProductNo,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(account, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/external/v2/products/origin-products/{originProductNo}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await SendWithTokenFallbackAsync(account, request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"원상품 조회 실패({originProductNo}): {(int)response.StatusCode} {response.ReasonPhrase}\r\n{text}");
        }

        return JsonNode.Parse(text) ?? throw new InvalidOperationException("원상품 조회 응답 JSON을 읽을 수 없습니다.");
    }

    public async Task UpdateOriginProductNameAsync(
        NaverAccount account,
        string originProductNo,
        string newName,
        CancellationToken cancellationToken = default)
    {
        var payload = await GetOriginProductAsync(account, originProductNo, cancellationToken);
        if (!TrySetOriginProductName(payload, newName))
        {
            throw new InvalidOperationException("원상품 응답에서 상품명 필드를 찾지 못했습니다.");
        }

        var accessToken = await GetAccessTokenAsync(account, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/external/v2/products/origin-products/{originProductNo}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await SendWithTokenFallbackAsync(account, request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"상품명 수정 실패({originProductNo}): {(int)response.StatusCode} {response.ReasonPhrase}\r\n{text}");
        }
    }

    public async Task<string> ReadOriginProductNameAsync(
        NaverAccount account,
        string originProductNo,
        CancellationToken cancellationToken = default)
    {
        var payload = await GetOriginProductAsync(account, originProductNo, cancellationToken);
        return ReadOriginProductName(payload);
    }

    public async Task UpdateOriginProductTagsAsync(
        NaverAccount account,
        string originProductNo,
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken = default)
    {
        var payload = await GetOriginProductAsync(account, originProductNo, cancellationToken);
        if (!TrySetOriginProductTags(payload, tags))
        {
            throw new InvalidOperationException("원상품 응답에서 SEO 태그 필드를 구성하지 못했습니다.");
        }

        var accessToken = await GetAccessTokenAsync(account, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/external/v2/products/origin-products/{originProductNo}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await SendWithTokenFallbackAsync(account, request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"태그 수정 실패({originProductNo}): {(int)response.StatusCode} {response.ReasonPhrase}\r\n{text}");
        }
    }

    public async Task UpdateOriginProductDiscountAsync(
        NaverAccount account,
        string originProductNo,
        decimal value,
        string unitType,
        string startDate,
        string endDate,
        CancellationToken cancellationToken = default)
    {
        var payload = await GetOriginProductAsync(account, originProductNo, cancellationToken);
        if (!TrySetImmediateDiscount(payload, value, unitType, startDate, endDate))
        {
            throw new InvalidOperationException("원상품 응답에서 즉시할인 필드를 구성하지 못했습니다.");
        }

        var accessToken = await GetAccessTokenAsync(account, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/external/v2/products/origin-products/{originProductNo}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await SendWithTokenFallbackAsync(account, request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"할인 수정 실패({originProductNo}): {(int)response.StatusCode} {response.ReasonPhrase}\r\n{text}");
        }
    }

    public async Task DeleteChannelProductAsync(
        NaverAccount account,
        string channelProductNo,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(account, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/external/v2/products/channel-products/{channelProductNo}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await SendWithTokenFallbackAsync(account, request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"채널상품 삭제 실패({channelProductNo}): {(int)response.StatusCode} {response.ReasonPhrase}\r\n{text}");
        }
    }

    public async Task DeleteOriginProductAsync(
        NaverAccount account,
        string originProductNo,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(account, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/external/v2/products/origin-products/{originProductNo}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await SendWithTokenFallbackAsync(account, request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"원상품 삭제 실패({originProductNo}): {(int)response.StatusCode} {response.ReasonPhrase}\r\n{text}");
        }
    }

    public async Task<string> GetAccessTokenAsync(NaverAccount account, CancellationToken cancellationToken = default)
    {
        ValidateAccount(account);

        if (_tokens.TryGetValue(account.Id, out var cached) && cached.ExpiresAt > DateTimeOffset.Now.AddMinutes(30))
        {
            return cached.AccessToken;
        }

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var password = $"{account.ClientId}_{timestamp}";
        string hashed;
        try
        {
            hashed = BCrypt.Net.BCrypt.HashPassword(password, account.ClientSecret);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(
                "네이버 Client Secret 형식이 아닙니다. 커머스API 시크릿은 보통 '$2a$...', '$2b$...' 같은 bcrypt salt 형식입니다. 키 폴더 스캔이 카페24 등 다른 키를 잡지 않았는지 확인하세요.",
                ex);
        }

        var signature = Convert.ToBase64String(Encoding.UTF8.GetBytes(hashed));

        var (response, text) = await RequestTokenOnceAsync(
            account,
            timestamp,
            signature,
            type: "SELLER",
            includeAccountId: !string.IsNullOrWhiteSpace(account.SellerAccountId),
            cancellationToken);

        if (!response.IsSuccessStatusCode && ShouldRetryAsSelf(text))
        {
            response.Dispose();
            (response, text) = await RequestTokenOnceAsync(
                account,
                timestamp,
                signature,
                type: "SELF",
                includeAccountId: false,
                cancellationToken);
        }

        using (response)
        {
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"토큰 발급 실패: {(int)response.StatusCode} {response.ReasonPhrase}\r\n{text}");
        }

        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        var token = root.GetProperty("access_token").GetString();
        var expiresIn = root.TryGetProperty("expires_in", out var expires)
            ? expires.GetInt32()
            : 10800;

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("토큰 발급 응답에 access_token이 없습니다.");
        }

        _tokens[account.Id] = new CachedToken(token, DateTimeOffset.Now.AddSeconds(Math.Max(60, expiresIn)));
        return token;
        }
    }

    public static bool LooksLikeCommerceSecret(string clientSecret)
    {
        return clientSecret.StartsWith("$2a$", StringComparison.Ordinal) ||
               clientSecret.StartsWith("$2b$", StringComparison.Ordinal) ||
               clientSecret.StartsWith("$2y$", StringComparison.Ordinal);
    }

    public static void ValidateAccount(NaverAccount account)
    {
        if (string.IsNullOrWhiteSpace(account.ClientId) ||
            string.IsNullOrWhiteSpace(account.ClientSecret))
        {
            throw new InvalidOperationException("Client ID와 Client Secret이 필요합니다.");
        }

        if (!LooksLikeCommerceSecret(account.ClientSecret))
        {
            throw new InvalidOperationException(
                $"'{account.Alias}' 계정의 Secret이 네이버 커머스API 형식이 아닙니다. 키 폴더 스캔 후 'naver_client_key' 계정을 선택하세요. 커머스API Secret은 보통 '$2a$...' 또는 '$2b$...'로 시작합니다.");
        }
    }

    private async Task<(HttpResponseMessage Response, string Text)> RequestTokenOnceAsync(
        NaverAccount account,
        string timestamp,
        string signature,
        string type,
        bool includeAccountId,
        CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = account.ClientId,
            ["timestamp"] = timestamp,
            ["client_secret_sign"] = signature,
            ["grant_type"] = "client_credentials",
            ["type"] = type
        };

        if (includeAccountId)
        {
            form["account_id"] = account.SellerAccountId;
        }

        var response = await _httpClient.PostAsync("/external/v1/oauth2/token", new FormUrlEncodedContent(form), cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        return (response, text);
    }

    private static bool ShouldRetryAsSelf(string responseText)
    {
        return responseText.Contains("\"name\":\"type\"", StringComparison.OrdinalIgnoreCase) ||
               responseText.Contains("type 항목", StringComparison.OrdinalIgnoreCase) ||
               responseText.Contains("account_id", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<HttpResponseMessage> SendWithTokenFallbackAsync(
        NaverAccount account,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        _tokens.Remove(account.Id);
        throw new InvalidOperationException("인증 토큰이 만료되었거나 권한이 없습니다. 다시 실행하면 토큰을 재발급합니다.");
    }

    private static bool IsLastPage(JsonElement root, int currentPage)
    {
        foreach (var name in new[] { "totalPages", "totalPage", "pages" })
        {
            if (TryGetRecursive(root, name, out var value) && value.ValueKind == JsonValueKind.Number)
            {
                return currentPage >= value.GetInt32();
            }
        }

        return false;
    }

    private static HttpRequestMessage CreateProductSearchRequest(string accessToken, int page, int pageSize)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/external/v1/products/search");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var body = new
        {
            productStatusTypes = Array.Empty<string>(),
            page,
            size = pageSize,
            orderType = "NO",
            periodType = "PROD_REG_DAY",
            fromDate = "2000-01-01",
            toDate = DateTime.Today.ToString("yyyy-MM-dd")
        };
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return request;
    }

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return delta;
        }

        if (response.Headers.RetryAfter?.Date is { } date)
        {
            var delay = date - DateTimeOffset.Now;
            if (delay > TimeSpan.Zero)
            {
                return delay;
            }
        }

        var seconds = attempt switch
        {
            1 => 15,
            2 => 30,
            3 => 60,
            4 => 90,
            _ => 120
        };
        return TimeSpan.FromSeconds(seconds);
    }

    private static bool TrySetOriginProductName(JsonNode node, string newName)
    {
        if (node is not JsonObject root)
        {
            return false;
        }

        if (root["originProduct"] is JsonObject originProduct)
        {
            originProduct["name"] = newName;
            if (root["smartstoreChannelProduct"] is JsonObject smartstoreChannelProduct)
            {
                smartstoreChannelProduct["channelProductName"] = newName;
            }

            return true;
        }

        if (root.ContainsKey("name"))
        {
            root["name"] = newName;
            return true;
        }

        return false;
    }

    private static string ReadOriginProductName(JsonNode node)
    {
        if (node is not JsonObject root)
        {
            return "";
        }

        if (root["smartstoreChannelProduct"] is JsonObject smartstoreChannelProduct &&
            smartstoreChannelProduct["channelProductName"] is JsonValue channelProductName)
        {
            var value = channelProductName.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        if (root["originProduct"] is JsonObject originProduct &&
            originProduct["name"] is JsonValue originName)
        {
            return originName.GetValue<string>();
        }

        if (root["name"] is JsonValue name)
        {
            return name.GetValue<string>();
        }

        return "";
    }

    private static bool TrySetOriginProductTags(JsonNode node, IReadOnlyList<string> tags)
    {
        if (node is not JsonObject root)
        {
            return false;
        }

        var product = root["originProduct"] as JsonObject ?? root;
        var detailAttribute = GetOrCreateObject(product, "detailAttribute");
        var seoInfo = GetOrCreateObject(detailAttribute, "seoInfo");
        var sellerTags = new JsonArray();

        foreach (var tag in tags.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(10))
        {
            sellerTags.Add(new JsonObject { ["text"] = tag });
        }

        seoInfo["sellerTags"] = sellerTags;
        return true;
    }

    private static bool TrySetImmediateDiscount(JsonNode node, decimal value, string unitType, string startDate, string endDate)
    {
        if (node is not JsonObject root)
        {
            return false;
        }

        var product = root["originProduct"] as JsonObject ?? root;
        var customerBenefit = GetOrCreateObject(product, "customerBenefit");
        var immediateDiscountPolicy = GetOrCreateObject(customerBenefit, "immediateDiscountPolicy");
        var discountMethod = GetOrCreateObject(immediateDiscountPolicy, "discountMethod");

        discountMethod["value"] = value;
        discountMethod["unitType"] = NormalizeDiscountUnit(unitType);

        var normalizedStart = NormalizeDiscountDate(startDate, isEnd: false);
        var normalizedEnd = NormalizeDiscountDate(endDate, isEnd: true);
        if (!string.IsNullOrWhiteSpace(normalizedStart))
        {
            discountMethod["startDate"] = normalizedStart;
        }

        if (!string.IsNullOrWhiteSpace(normalizedEnd))
        {
            discountMethod["endDate"] = normalizedEnd;
        }

        return true;
    }

    private static JsonObject GetOrCreateObject(JsonObject parent, string propertyName)
    {
        if (parent[propertyName] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        parent[propertyName] = created;
        return created;
    }

    private static string NormalizeDiscountUnit(string unitType)
    {
        var normalized = unitType.Trim().ToUpperInvariant();
        if (normalized is "PERCENT" or "%" or "정율" or "퍼센트")
        {
            return "PERCENT";
        }

        if (normalized is "WON" or "원" or "정액")
        {
            return "WON";
        }

        return normalized;
    }

    private static string NormalizeDiscountDate(string value, bool isEnd)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        if (value.Contains('T', StringComparison.OrdinalIgnoreCase))
        {
            return value.Trim();
        }

        return DateTime.TryParse(value, out var date)
            ? (isEnd ? date.Date.AddHours(23).AddMinutes(59) : date.Date).ToString("yyyy-MM-dd'T'HH:mm:ss+09:00")
            : value.Trim();
    }

    private static bool TryGetRecursive(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }

                if (TryGetRecursive(property.Value, propertyName, out value))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryGetRecursive(item, propertyName, out value))
                {
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAt);
}

internal static class ProductJsonParser
{
    public static IEnumerable<ProductRecord> ParseSearchResponse(NaverAccount account, JsonElement root)
    {
        foreach (var item in FindProductArray(root))
        {
            var raw = item.GetRawText();
            var originProductNo = FirstString(item, "originProductNo", "originNo");
            var channelProductNo = FirstString(item, "channelProductNo", "smartstoreChannelProductNo");
            var sellerManagementCode = FirstString(item, "sellerManagementCode", "sellerProductCode", "sellerCustomCode1");
            var name = ProductName(item);
            var statusType = FirstString(item, "statusType", "productStatusType", "channelProductDisplayStatusType");
            var imageUrl = RepresentativeImageUrl(item);
            var tags = SellerTags(item);
            var discount = DiscountInfo(item);

            if (string.IsNullOrWhiteSpace(originProductNo) && string.IsNullOrWhiteSpace(channelProductNo))
            {
                continue;
            }

            yield return new ProductRecord
            {
                LocalAccountId = account.Id,
                AccountAlias = account.Alias,
                OriginProductNo = originProductNo,
                ChannelProductNo = channelProductNo,
                SellerManagementCode = sellerManagementCode,
                Name = name,
                NormalizedName = ProductText.NormalizeName(name),
                StatusType = statusType,
                SalePrice = FirstLong(item, "salePrice", "price"),
                StockQuantity = FirstLong(item, "stockQuantity"),
                DuplicateKey = ProductText.SellerCodeDuplicateKey(sellerManagementCode),
                RepresentativeImageUrl = imageUrl,
                SellerTags = tags,
                DiscountValue = discount.Value,
                DiscountUnitType = discount.UnitType,
                DiscountStartDate = discount.StartDate,
                DiscountEndDate = discount.EndDate,
                RawJson = raw,
                RemoteKey = $"{account.Id}:{originProductNo}:{channelProductNo}:{sellerManagementCode}",
                SyncedAt = DateTime.Now
            };
        }
    }

    private static IEnumerable<JsonElement> FindProductArray(JsonElement root)
    {
        foreach (var name in new[] { "contents", "content", "products", "items", "data" })
        {
            if (TryGetRecursive(root, name, out var value) && value.ValueKind == JsonValueKind.Array)
            {
                return value.EnumerateArray().ToList();
            }
        }

        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray().ToList();
        }

        return Array.Empty<JsonElement>();
    }

    private static string ProductName(JsonElement item)
    {
        var channelProductName = FirstString(item, "channelProductName");
        if (!string.IsNullOrWhiteSpace(channelProductName))
        {
            return channelProductName;
        }

        if (TryGetObject(item, "originProduct", out var originProduct))
        {
            var name = FirstString(originProduct, "name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return FirstString(item, "name", "productName", "channelProductName");
    }

    private static string RepresentativeImageUrl(JsonElement item)
    {
        if (TryGetRecursive(item, "representativeImage", out var image) && image.ValueKind == JsonValueKind.Object)
        {
            var url = FirstString(image, "url");
            if (!string.IsNullOrWhiteSpace(url))
            {
                return url;
            }
        }

        return FirstString(item, "imageUrl", "representativeImageUrl", "thumbnailImageUrl");
    }

    private static string SellerTags(JsonElement item)
    {
        if (TryGetRecursive(item, "sellerTags", out var tags))
        {
            return ProductText.JoinSellerTags(tags);
        }

        return "";
    }

    private static (decimal Value, string UnitType, string StartDate, string EndDate) DiscountInfo(JsonElement item)
    {
        if (!TryGetRecursive(item, "immediateDiscountPolicy", out var policy) || policy.ValueKind != JsonValueKind.Object)
        {
            return (0, "", "", "");
        }

        if (!TryGetRecursive(policy, "discountMethod", out var method) || method.ValueKind != JsonValueKind.Object)
        {
            return (0, "", "", "");
        }

        return (
            FirstDecimal(method, "value"),
            FirstString(method, "unitType"),
            FirstString(method, "startDate"),
            FirstString(method, "endDate"));
    }

    private static string FirstString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetRecursive(element, name, out var value))
            {
                if (value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString() ?? "";
                }

                if (value.ValueKind == JsonValueKind.Number)
                {
                    return value.GetRawText();
                }
            }
        }

        return "";
    }

    private static long FirstLong(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetRecursive(element, name, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
                {
                    return number;
                }

                if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out number))
                {
                    return number;
                }
            }
        }

        return 0;
    }

    private static decimal FirstDecimal(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetRecursive(element, name, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
                {
                    return number;
                }

                if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), out number))
                {
                    return number;
                }
            }
        }

        return 0;
    }

    private static bool TryGetObject(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out value))
        {
            return value.ValueKind == JsonValueKind.Object;
        }

        value = default;
        return false;
    }

    private static bool TryGetRecursive(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }

                if (TryGetRecursive(property.Value, propertyName, out value))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryGetRecursive(item, propertyName, out value))
                {
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}
