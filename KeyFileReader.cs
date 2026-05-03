using System.Text.Json;
using System.Text.RegularExpressions;

namespace NaverProductOrganizer;

internal static class KeyFileReader
{
    private static readonly string[] AllowedExtensions = { ".txt", ".json", ".md", ".env" };

    public static IReadOnlyList<KeyFileCandidate> Scan(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return Array.Empty<KeyFileCandidate>();
        }

        var results = new List<KeyFileCandidate>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                     .Where(f => AllowedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)))
        {
            try
            {
                var text = File.ReadAllText(file);
                if (!LooksLikeNaverCommerceKey(file, text))
                {
                    continue;
                }

                var candidate = Path.GetExtension(file).Equals(".json", StringComparison.OrdinalIgnoreCase)
                    ? FromJson(file, text)
                    : FromText(file, text);

                if (!string.IsNullOrWhiteSpace(candidate.ClientId) &&
                    !string.IsNullOrWhiteSpace(candidate.ClientSecret))
                {
                    results.Add(candidate);
                }
            }
            catch
            {
                // Key scanning is convenience-only. Ignore unreadable files.
            }
        }

        return results
            .OrderByDescending(r => Path.GetFileName(r.SourceFile).Contains("naver", StringComparison.OrdinalIgnoreCase))
            .ThenBy(r => r.SourceFile)
            .ToList();
    }

    private static KeyFileCandidate FromJson(string file, string text)
    {
        var candidate = new KeyFileCandidate
        {
            SourceFile = file,
            Alias = Path.GetFileNameWithoutExtension(file)
        };

        using var document = JsonDocument.Parse(text);
        candidate.ClientId = FindJsonString(document.RootElement, "naver_commerce_client_id", "client_id", "clientId", "application_id", "applicationId", "appId");
        candidate.ClientSecret = FindJsonString(document.RootElement, "naver_commerce_client_secret", "client_secret", "clientSecret", "application_secret", "applicationSecret", "secret");
        candidate.SellerAccountId = FindJsonString(document.RootElement, "naver_commerce_account_id", "account_id", "accountId", "seller_account_id", "sellerAccountId", "sellerId");
        return candidate;
    }

    private static KeyFileCandidate FromText(string file, string text)
    {
        return new KeyFileCandidate
        {
            SourceFile = file,
            Alias = Path.GetFileNameWithoutExtension(file),
            ClientId = FindTextValue(text, "NAVER_COMMERCE_CLIENT_ID", "client_id", "client id", "clientId", "application id", "애플리케이션 id"),
            ClientSecret = FindTextValue(text, "NAVER_COMMERCE_CLIENT_SECRET", "client_secret", "client secret", "clientSecret", "application secret", "애플리케이션 시크릿"),
            SellerAccountId = FindTextValue(text, "NAVER_COMMERCE_ACCOUNT_ID", "account_id", "account id", "seller_account_id", "판매자 uid", "판매자 id")
        };
    }

    private static bool LooksLikeNaverCommerceKey(string file, string text)
    {
        var fileName = Path.GetFileName(file);
        return fileName.Contains("naver", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("NAVER_COMMERCE", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("commerce.naver", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("smartstore", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindTextValue(string text, params string[] keys)
    {
        foreach (var key in keys)
        {
            var escaped = Regex.Escape(key);
            var match = Regex.Match(text, $@"(?im)^\s*{escaped}\s*[:=]\s*(.+?)\s*$");
            if (match.Success)
            {
                return match.Groups[1].Value.Trim().Trim('"', '\'');
            }
        }

        return "";
    }

    private static string FindJsonString(JsonElement element, params string[] names)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (names.Any(n => string.Equals(n, property.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    return property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString() ?? ""
                        : property.Value.GetRawText();
                }

                var nested = FindJsonString(property.Value, names);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindJsonString(item, names);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }

        return "";
    }
}
