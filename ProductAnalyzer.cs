namespace NaverProductOrganizer;

internal static class ProductAnalyzer
{
    public static IReadOnlyList<DuplicateCandidateRow> FindDuplicates(IReadOnlyList<ProductRecord> products)
    {
        return products
            .Where(p => !string.IsNullOrWhiteSpace(p.DuplicateKey))
            .GroupBy(p => p.DuplicateKey)
            .Where(g => g.Count() > 1)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .SelectMany(g => g
                .OrderBy(p => p.Name)
                .Select(p => new DuplicateCandidateRow
                {
                    기준 = g.Key,
                    묶음수 = g.Count(),
                    계정 = p.AccountAlias,
                    원상품번호 = p.OriginProductNo,
                    채널상품번호 = p.ChannelProductNo,
                    판매자상품코드 = p.SellerManagementCode,
                    상품명 = p.Name,
                    판매상태 = p.StatusType,
                    판매가 = p.SalePrice,
                    RemoteKey = p.RemoteKey
                }))
            .ToList();
    }

    public static IReadOnlyList<GroupCandidateRow> FindGroupCandidates(IReadOnlyList<ProductRecord> products)
    {
        return products
            .Select(p => new { Product = p, Key = ProductText.GroupKey(p.Name) })
            .Where(x => x.Key.Length >= 4)
            .GroupBy(x => x.Key)
            .Where(g => g.Count() > 1)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g =>
            {
                var sampleProducts = g.Select(x => x.Product).ToList();
                return new GroupCandidateRow
                {
                    묶기기준 = g.Key,
                    후보수 = sampleProducts.Count,
                    원상품번호들 = string.Join(", ", sampleProducts.Select(p => p.OriginProductNo).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().Take(20)),
                    상품명샘플 = string.Join(" / ", sampleProducts.Select(p => p.Name).Distinct().Take(3)),
                    판단 = "API 묶기 가능성 있음 - 옵션/카테고리/상세정보 동일성 확인 필요"
                };
            })
            .ToList();
    }
}
