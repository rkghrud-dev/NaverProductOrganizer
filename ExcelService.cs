using ClosedXML.Excel;

namespace NaverProductOrganizer;

internal static class ExcelService
{
    public static void ExportProductDatabase(string path, IReadOnlyList<ProductRecord> products)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("상품DB");
        WriteHeaders(sheet, new[]
        {
            "account_alias", "origin_product_no", "channel_product_no", "seller_management_code",
            "name", "pending_new_name", "status_type", "sale_price", "stock_quantity",
            "discount_value", "discount_unit_type", "pending_discount_value", "pending_discount_unit_type",
            "seller_tags", "pending_seller_tags", "representative_image_url", "synced_at", "last_error"
        });

        for (var i = 0; i < products.Count; i++)
        {
            var row = i + 2;
            var product = products[i];
            sheet.Cell(row, 1).Value = product.AccountAlias;
            sheet.Cell(row, 2).Value = product.OriginProductNo;
            sheet.Cell(row, 3).Value = product.ChannelProductNo;
            sheet.Cell(row, 4).Value = product.SellerManagementCode;
            sheet.Cell(row, 5).Value = product.Name;
            sheet.Cell(row, 6).Value = product.PendingNewName;
            sheet.Cell(row, 7).Value = product.StatusType;
            sheet.Cell(row, 8).Value = product.SalePrice;
            sheet.Cell(row, 9).Value = product.StockQuantity;
            sheet.Cell(row, 10).Value = product.DiscountValue;
            sheet.Cell(row, 11).Value = product.DiscountUnitType;
            sheet.Cell(row, 12).Value = product.PendingDiscountValue;
            sheet.Cell(row, 13).Value = product.PendingDiscountUnitType;
            sheet.Cell(row, 14).Value = product.SellerTags;
            sheet.Cell(row, 15).Value = product.PendingSellerTags;
            sheet.Cell(row, 16).Value = product.RepresentativeImageUrl;
            sheet.Cell(row, 17).Value = product.SyncedAt;
            sheet.Cell(row, 18).Value = product.LastError;
        }

        sheet.Columns().AdjustToContents();
        workbook.SaveAs(path);
    }

    public static void ExportDiscountTemplate(string path, IReadOnlyList<ProductRecord> products)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("할인변경");
        WriteHeaders(sheet, new[]
        {
            "origin_product_no", "channel_product_no", "seller_management_code",
            "product_name", "sale_price", "current_discount_value", "current_discount_unit",
            "new_discount_value", "new_discount_unit", "start_date", "end_date", "memo"
        });

        for (var i = 0; i < products.Count; i++)
        {
            var row = i + 2;
            var product = products[i];
            sheet.Cell(row, 1).Value = product.OriginProductNo;
            sheet.Cell(row, 2).Value = product.ChannelProductNo;
            sheet.Cell(row, 3).Value = product.SellerManagementCode;
            sheet.Cell(row, 4).Value = product.Name;
            sheet.Cell(row, 5).Value = product.SalePrice;
            sheet.Cell(row, 6).Value = product.DiscountValue;
            sheet.Cell(row, 7).Value = product.DiscountUnitType;
            sheet.Cell(row, 8).Value = product.PendingDiscountValue;
            sheet.Cell(row, 9).Value = product.PendingDiscountUnitType;
            sheet.Cell(row, 10).Value = product.PendingDiscountStartDate;
            sheet.Cell(row, 11).Value = product.PendingDiscountEndDate;
        }

        sheet.Columns().AdjustToContents();
        workbook.SaveAs(path);
    }

    public static void ExportTagTemplate(string path, IReadOnlyList<ProductRecord> products)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("태그변경");
        WriteHeaders(sheet, new[]
        {
            "origin_product_no", "channel_product_no", "seller_management_code",
            "product_name", "current_tags", "new_tags", "memo"
        });

        for (var i = 0; i < products.Count; i++)
        {
            var row = i + 2;
            var product = products[i];
            sheet.Cell(row, 1).Value = product.OriginProductNo;
            sheet.Cell(row, 2).Value = product.ChannelProductNo;
            sheet.Cell(row, 3).Value = product.SellerManagementCode;
            sheet.Cell(row, 4).Value = product.Name;
            sheet.Cell(row, 5).Value = product.SellerTags;
            sheet.Cell(row, 6).Value = product.PendingSellerTags;
        }

        sheet.Columns().AdjustToContents();
        workbook.SaveAs(path);
    }

    public static void ExportRenameTemplate(string path, IReadOnlyList<ProductRecord> products)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("상품명변경");
        WriteHeaders(sheet, new[]
        {
            "origin_product_no", "channel_product_no", "seller_management_code",
            "current_name", "new_name", "memo"
        });

        for (var i = 0; i < products.Count; i++)
        {
            var row = i + 2;
            var product = products[i];
            sheet.Cell(row, 1).Value = product.OriginProductNo;
            sheet.Cell(row, 2).Value = product.ChannelProductNo;
            sheet.Cell(row, 3).Value = product.SellerManagementCode;
            sheet.Cell(row, 4).Value = product.Name;
            sheet.Cell(row, 5).Value = product.PendingNewName;
        }

        sheet.Columns().AdjustToContents();
        workbook.SaveAs(path);
    }

    public static IReadOnlyList<RenameImportRow> ImportRenameRows(string path)
    {
        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheets.First();
        var firstRow = sheet.FirstRowUsed();
        if (firstRow is null)
        {
            return Array.Empty<RenameImportRow>();
        }

        var headers = firstRow.CellsUsed()
            .ToDictionary(
                c => NormalizeHeader(c.GetString()),
                c => c.Address.ColumnNumber,
                StringComparer.OrdinalIgnoreCase);

        var rows = new List<RenameImportRow>();
        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            var item = new RenameImportRow
            {
                OriginProductNo = GetCell(row, headers, "originproductno", "원상품번호", "상품번호"),
                ChannelProductNo = GetCell(row, headers, "channelproductno", "채널상품번호", "스마트스토어상품번호"),
                SellerManagementCode = GetCell(row, headers, "sellermanagementcode", "판매자상품코드", "판매자관리코드"),
                CurrentName = GetCell(row, headers, "currentname", "기존상품명", "상품명"),
                NewName = GetCell(row, headers, "newname", "신규상품명", "변경상품명", "새상품명")
            };

            if (!string.IsNullOrWhiteSpace(item.NewName))
            {
                rows.Add(item);
            }
        }

        return rows;
    }

    public static IReadOnlyList<TagImportRow> ImportTagRows(string path)
    {
        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheets.First();
        var firstRow = sheet.FirstRowUsed();
        if (firstRow is null)
        {
            return Array.Empty<TagImportRow>();
        }

        var headers = firstRow.CellsUsed()
            .ToDictionary(
                c => NormalizeHeader(c.GetString()),
                c => c.Address.ColumnNumber,
                StringComparer.OrdinalIgnoreCase);

        var rows = new List<TagImportRow>();
        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            var item = new TagImportRow
            {
                OriginProductNo = GetCell(row, headers, "originproductno", "원상품번호", "상품번호"),
                ChannelProductNo = GetCell(row, headers, "channelproductno", "채널상품번호", "스마트스토어상품번호"),
                SellerManagementCode = GetCell(row, headers, "sellermanagementcode", "판매자상품코드", "판매자관리코드"),
                CurrentTags = GetCell(row, headers, "currenttags", "기존태그", "현재태그", "태그"),
                NewTags = GetCell(row, headers, "newtags", "신규태그", "변경태그", "새태그")
            };

            if (!string.IsNullOrWhiteSpace(item.NewTags))
            {
                rows.Add(item);
            }
        }

        return rows;
    }

    public static IReadOnlyList<DiscountImportRow> ImportDiscountRows(string path)
    {
        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheets.First();
        var firstRow = sheet.FirstRowUsed();
        if (firstRow is null)
        {
            return Array.Empty<DiscountImportRow>();
        }

        var headers = firstRow.CellsUsed()
            .ToDictionary(
                c => NormalizeHeader(c.GetString()),
                c => c.Address.ColumnNumber,
                StringComparer.OrdinalIgnoreCase);

        var rows = new List<DiscountImportRow>();
        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            var rawValue = GetCell(row, headers, "newdiscountvalue", "변경할인값", "할인값", "discountvalue");
            if (!decimal.TryParse(rawValue, out var discountValue) || discountValue <= 0)
            {
                continue;
            }

            var unitType = GetCell(row, headers, "newdiscountunit", "변경할인단위", "할인단위", "unittype");
            var item = new DiscountImportRow
            {
                OriginProductNo = GetCell(row, headers, "originproductno", "원상품번호", "상품번호"),
                ChannelProductNo = GetCell(row, headers, "channelproductno", "채널상품번호", "스마트스토어상품번호"),
                SellerManagementCode = GetCell(row, headers, "sellermanagementcode", "판매자상품코드", "판매자관리코드"),
                DiscountValue = discountValue,
                UnitType = string.IsNullOrWhiteSpace(unitType) ? "PERCENT" : unitType.Trim().ToUpperInvariant(),
                StartDate = GetCell(row, headers, "startdate", "할인시작", "시작일"),
                EndDate = GetCell(row, headers, "enddate", "할인종료", "종료일")
            };

            rows.Add(item);
        }

        return rows;
    }

    private static void WriteHeaders(IXLWorksheet sheet, IReadOnlyList<string> headers)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        sheet.SheetView.FreezeRows(1);
    }

    private static string GetCell(IXLRow row, IReadOnlyDictionary<string, int> headers, params string[] names)
    {
        foreach (var name in names)
        {
            if (headers.TryGetValue(NormalizeHeader(name), out var column))
            {
                return row.Cell(column).GetString().Trim();
            }
        }

        return "";
    }

    private static string NormalizeHeader(string value)
    {
        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }
}
