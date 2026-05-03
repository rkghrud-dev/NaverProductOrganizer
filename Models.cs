using System.Text.Json;
using System.Text.RegularExpressions;

namespace NaverProductOrganizer;

internal sealed class NaverAccount
{
    public long Id { get; set; }
    public string Alias { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string SellerAccountId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(Alias) ? $"계정 {Id}" : Alias;
    }
}

internal sealed class ProductRecord
{
    public long Id { get; set; }
    public long LocalAccountId { get; set; }
    public string AccountAlias { get; set; } = "";
    public string RemoteKey { get; set; } = "";
    public string OriginProductNo { get; set; } = "";
    public string ChannelProductNo { get; set; } = "";
    public string SellerManagementCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public string StatusType { get; set; } = "";
    public long SalePrice { get; set; }
    public long StockQuantity { get; set; }
    public string DuplicateKey { get; set; } = "";
    public int DuplicateCount { get; set; }
    public string RepresentativeImageUrl { get; set; } = "";
    public string SellerTags { get; set; } = "";
    public decimal DiscountValue { get; set; }
    public string DiscountUnitType { get; set; } = "";
    public string DiscountStartDate { get; set; } = "";
    public string DiscountEndDate { get; set; } = "";
    public string RawJson { get; set; } = "";
    public string PendingNewName { get; set; } = "";
    public string PendingSellerTags { get; set; } = "";
    public decimal PendingDiscountValue { get; set; }
    public string PendingDiscountUnitType { get; set; } = "";
    public string PendingDiscountStartDate { get; set; } = "";
    public string PendingDiscountEndDate { get; set; } = "";
    public string LastError { get; set; } = "";
    public DateTime SyncedAt { get; set; }
}

internal sealed class ProductGridRow
{
    public bool 선택 { get; set; }
    public string 계정 { get; set; } = "";
    public string 원상품번호 { get; set; } = "";
    public string 채널상품번호 { get; set; } = "";
    public string 판매자상품코드 { get; set; } = "";
    public string 상품명 { get; set; } = "";
    public string 변경상품명 { get; set; } = "";
    public string 판매상태 { get; set; } = "";
    public long 판매가 { get; set; }
    public long 재고 { get; set; }
    public string 중복 { get; set; } = "";
    public string 중복키 { get; set; } = "";
    public int 중복수 { get; set; }
    public decimal 할인값 { get; set; }
    public string 할인단위 { get; set; } = "";
    public string 할인시작 { get; set; } = "";
    public string 할인종료 { get; set; } = "";
    public decimal 변경할인값 { get; set; }
    public string 변경할인단위 { get; set; } = "";
    public string 태그 { get; set; } = "";
    public string 변경태그 { get; set; } = "";
    public string 대표이미지 { get; set; } = "";
    public string 오류 { get; set; } = "";
    public string RemoteKey { get; set; } = "";
}

internal sealed class DuplicateCandidateRow
{
    public bool 선택 { get; set; }
    public string 기준 { get; set; } = "";
    public int 묶음수 { get; set; }
    public string 계정 { get; set; } = "";
    public string 원상품번호 { get; set; } = "";
    public string 채널상품번호 { get; set; } = "";
    public string 판매자상품코드 { get; set; } = "";
    public string 상품명 { get; set; } = "";
    public string 판매상태 { get; set; } = "";
    public long 판매가 { get; set; }
    public string RemoteKey { get; set; } = "";
}

internal sealed class GroupCandidateRow
{
    public string 묶기기준 { get; set; } = "";
    public int 후보수 { get; set; }
    public string 원상품번호들 { get; set; } = "";
    public string 상품명샘플 { get; set; } = "";
    public string 판단 { get; set; } = "";
}

internal sealed class RenameImportRow
{
    public string OriginProductNo { get; set; } = "";
    public string ChannelProductNo { get; set; } = "";
    public string SellerManagementCode { get; set; } = "";
    public string CurrentName { get; set; } = "";
    public string NewName { get; set; } = "";
}

internal sealed class TagImportRow
{
    public string OriginProductNo { get; set; } = "";
    public string ChannelProductNo { get; set; } = "";
    public string SellerManagementCode { get; set; } = "";
    public string CurrentTags { get; set; } = "";
    public string NewTags { get; set; } = "";
}

internal sealed class DiscountImportRow
{
    public string OriginProductNo { get; set; } = "";
    public string ChannelProductNo { get; set; } = "";
    public string SellerManagementCode { get; set; } = "";
    public decimal DiscountValue { get; set; }
    public string UnitType { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
}

internal sealed class KeyFileCandidate
{
    public string SourceFile { get; set; } = "";
    public string Alias { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string SellerAccountId { get; set; } = "";
}

internal static class ProductText
{
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex Noise = new(@"[\[\]\(\)\{\}/\\_\-+,:;|~!@#$%^&*=.'""`]", RegexOptions.Compiled);
    private static readonly Regex OptionWords = new(
        @"\b(색상|사이즈|옵션|택1|선택|대형|중형|소형|블랙|화이트|실버|그레이|빨강|파랑|노랑|검정|흰색|회색)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var normalized = value.Normalize();
        normalized = OptionWords.Replace(normalized, " ");
        normalized = Noise.Replace(normalized, " ");
        normalized = Spaces.Replace(normalized, "");
        return normalized.ToLowerInvariant();
    }

    public static string GroupKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var text = value.Normalize();
        text = Regex.Replace(text, @"\b\d+(\.\d+)?\s*(mm|cm|m|kg|g|개|pcs|p|호|인치)\b", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\b(m\d+|s\d+|no\.?\d+)\b", " ", RegexOptions.IgnoreCase);
        return NormalizeName(text);
    }

    public static string SellerCodeDuplicateKey(string sellerManagementCode)
    {
        if (string.IsNullOrWhiteSpace(sellerManagementCode))
        {
            return "";
        }

        var normalized = sellerManagementCode.Trim().ToUpperInvariant();
        var match = Regex.Match(normalized, @"^(GS\d{7})");
        return match.Success ? match.Groups[1].Value : normalized;
    }

    public static string JoinSellerTags(JsonElement element)
    {
        try
        {
            if (element.ValueKind != JsonValueKind.Array)
            {
                return "";
            }

            var tags = new List<string>();
            foreach (var tag in element.EnumerateArray())
            {
                if (tag.TryGetProperty("text", out var text))
                {
                    var value = text.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        tags.Add(value.Trim());
                    }
                }
            }

            return string.Join(", ", tags.Distinct(StringComparer.OrdinalIgnoreCase));
        }
        catch
        {
            return "";
        }
    }

    public static IReadOnlyList<string> ParseSellerTags(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value
            .Split(new[] { ',', ';', '\n', '\r', '\t', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeTag)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    public static string JoinTagsString(IEnumerable<string> tags)
    {
        return string.Join(", ", tags.Select(NormalizeTag).Where(tag => tag.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(10));
    }

    private static string NormalizeTag(string value)
    {
        return value.Trim().TrimStart('#').Trim();
    }
}
