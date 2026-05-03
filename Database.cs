using Microsoft.Data.Sqlite;

namespace NaverProductOrganizer;

internal sealed class Database
{
    private readonly string _connectionString;

    public Database(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath
        }.ToString();
    }

    public void Initialize()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
CREATE TABLE IF NOT EXISTS accounts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    alias TEXT NOT NULL,
    client_id TEXT NOT NULL,
    client_secret TEXT NOT NULL,
    seller_account_id TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS products (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    local_account_id INTEGER NOT NULL,
    remote_key TEXT NOT NULL UNIQUE,
    origin_product_no TEXT NOT NULL,
    channel_product_no TEXT NOT NULL,
    seller_management_code TEXT NOT NULL,
    name TEXT NOT NULL,
    normalized_name TEXT NOT NULL,
    status_type TEXT NOT NULL,
    sale_price INTEGER NOT NULL DEFAULT 0,
    stock_quantity INTEGER NOT NULL DEFAULT 0,
    duplicate_key TEXT NOT NULL DEFAULT '',
    representative_image_url TEXT NOT NULL,
    seller_tags TEXT NOT NULL,
    discount_value REAL NOT NULL DEFAULT 0,
    discount_unit_type TEXT NOT NULL DEFAULT '',
    discount_start_date TEXT NOT NULL DEFAULT '',
    discount_end_date TEXT NOT NULL DEFAULT '',
    raw_json TEXT NOT NULL,
    pending_new_name TEXT NOT NULL DEFAULT '',
    pending_seller_tags TEXT NOT NULL DEFAULT '',
    pending_discount_value REAL NOT NULL DEFAULT 0,
    pending_discount_unit_type TEXT NOT NULL DEFAULT '',
    pending_discount_start_date TEXT NOT NULL DEFAULT '',
    pending_discount_end_date TEXT NOT NULL DEFAULT '',
    last_error TEXT NOT NULL DEFAULT '',
    synced_at TEXT NOT NULL,
    FOREIGN KEY(local_account_id) REFERENCES accounts(id)
);

CREATE INDEX IF NOT EXISTS ix_products_account ON products(local_account_id);
CREATE INDEX IF NOT EXISTS ix_products_origin ON products(origin_product_no);
CREATE INDEX IF NOT EXISTS ix_products_channel ON products(channel_product_no);
CREATE INDEX IF NOT EXISTS ix_products_normalized_name ON products(normalized_name);
""";
        command.ExecuteNonQuery();
        EnsureColumn(connection, "products", "duplicate_key", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "products", "pending_seller_tags", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "products", "discount_value", "REAL NOT NULL DEFAULT 0");
        EnsureColumn(connection, "products", "discount_unit_type", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "products", "discount_start_date", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "products", "discount_end_date", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "products", "pending_discount_value", "REAL NOT NULL DEFAULT 0");
        EnsureColumn(connection, "products", "pending_discount_unit_type", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "products", "pending_discount_start_date", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "products", "pending_discount_end_date", "TEXT NOT NULL DEFAULT ''");
    }

    public List<NaverAccount> GetAccounts()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT id, alias, client_id, client_secret, seller_account_id, created_at, updated_at
FROM accounts
ORDER BY CASE WHEN lower(alias) LIKE '%naver%' THEN 0 ELSE 1 END,
         CASE WHEN client_secret LIKE '$2%' THEN 0 ELSE 1 END,
         alias, id
""";

        using var reader = command.ExecuteReader();
        var accounts = new List<NaverAccount>();
        while (reader.Read())
        {
            accounts.Add(new NaverAccount
            {
                Id = reader.GetInt64(0),
                Alias = reader.GetString(1),
                ClientId = reader.GetString(2),
                ClientSecret = reader.GetString(3),
                SellerAccountId = reader.GetString(4),
                CreatedAt = ParseDate(reader.GetString(5)),
                UpdatedAt = ParseDate(reader.GetString(6))
            });
        }

        return accounts;
    }

    public long SaveAccount(NaverAccount account)
    {
        var now = DateTime.Now.ToString("O");
        using var connection = Open();
        using var command = connection.CreateCommand();

        if (account.Id == 0)
        {
            command.CommandText = """
INSERT INTO accounts(alias, client_id, client_secret, seller_account_id, created_at, updated_at)
VALUES($alias, $clientId, $clientSecret, $sellerAccountId, $createdAt, $updatedAt);
SELECT last_insert_rowid();
""";
            command.Parameters.AddWithValue("$createdAt", now);
        }
        else
        {
            command.CommandText = """
UPDATE accounts
SET alias = $alias,
    client_id = $clientId,
    client_secret = $clientSecret,
    seller_account_id = $sellerAccountId,
    updated_at = $updatedAt
WHERE id = $id;
SELECT $id;
""";
            command.Parameters.AddWithValue("$id", account.Id);
        }

        command.Parameters.AddWithValue("$alias", account.Alias.Trim());
        command.Parameters.AddWithValue("$clientId", account.ClientId.Trim());
        command.Parameters.AddWithValue("$clientSecret", account.ClientSecret.Trim());
        command.Parameters.AddWithValue("$sellerAccountId", account.SellerAccountId.Trim());
        command.Parameters.AddWithValue("$updatedAt", now);

        return Convert.ToInt64(command.ExecuteScalar());
    }

    public void UpsertProducts(long accountId, IEnumerable<ProductRecord> products)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        foreach (var product in products)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
INSERT INTO products(
    local_account_id, remote_key, origin_product_no, channel_product_no, seller_management_code,
    name, normalized_name, status_type, sale_price, stock_quantity, duplicate_key, representative_image_url,
    seller_tags, discount_value, discount_unit_type, discount_start_date, discount_end_date, raw_json, synced_at
)
VALUES(
    $localAccountId, $remoteKey, $originProductNo, $channelProductNo, $sellerManagementCode,
    $name, $normalizedName, $statusType, $salePrice, $stockQuantity, $duplicateKey, $representativeImageUrl,
    $sellerTags, $discountValue, $discountUnitType, $discountStartDate, $discountEndDate, $rawJson, $syncedAt
)
ON CONFLICT(remote_key) DO UPDATE SET
    origin_product_no = excluded.origin_product_no,
    channel_product_no = excluded.channel_product_no,
    seller_management_code = excluded.seller_management_code,
    name = excluded.name,
    normalized_name = excluded.normalized_name,
    status_type = excluded.status_type,
    sale_price = excluded.sale_price,
    stock_quantity = excluded.stock_quantity,
    duplicate_key = excluded.duplicate_key,
    representative_image_url = excluded.representative_image_url,
    seller_tags = excluded.seller_tags,
    discount_value = excluded.discount_value,
    discount_unit_type = excluded.discount_unit_type,
    discount_start_date = excluded.discount_start_date,
    discount_end_date = excluded.discount_end_date,
    raw_json = excluded.raw_json,
    synced_at = excluded.synced_at;
""";
            AddProductParameters(command, accountId, product);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public List<ProductRecord> GetProducts(long? accountId = null)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT p.id, p.local_account_id, a.alias, p.remote_key, p.origin_product_no, p.channel_product_no,
       p.seller_management_code, p.name, p.normalized_name, p.status_type, p.sale_price,
       p.stock_quantity, p.duplicate_key, p.representative_image_url, p.seller_tags,
       p.discount_value, p.discount_unit_type, p.discount_start_date, p.discount_end_date,
       p.raw_json, p.pending_new_name, p.pending_seller_tags,
       p.pending_discount_value, p.pending_discount_unit_type, p.pending_discount_start_date,
       p.pending_discount_end_date, p.last_error, p.synced_at
FROM products p
JOIN accounts a ON a.id = p.local_account_id
WHERE ($accountId IS NULL OR p.local_account_id = $accountId)
ORDER BY p.name, p.origin_product_no
""";
        command.Parameters.AddWithValue("$accountId", accountId.HasValue ? accountId.Value : DBNull.Value);

        using var reader = command.ExecuteReader();
        var products = new List<ProductRecord>();
        while (reader.Read())
        {
            products.Add(new ProductRecord
            {
                Id = reader.GetInt64(0),
                LocalAccountId = reader.GetInt64(1),
                AccountAlias = reader.GetString(2),
                RemoteKey = reader.GetString(3),
                OriginProductNo = reader.GetString(4),
                ChannelProductNo = reader.GetString(5),
                SellerManagementCode = reader.GetString(6),
                Name = reader.GetString(7),
                NormalizedName = reader.GetString(8),
                StatusType = reader.GetString(9),
                SalePrice = reader.GetInt64(10),
                StockQuantity = reader.GetInt64(11),
                DuplicateKey = reader.GetString(12),
                RepresentativeImageUrl = reader.GetString(13),
                SellerTags = reader.GetString(14),
                DiscountValue = reader.GetDecimal(15),
                DiscountUnitType = reader.GetString(16),
                DiscountStartDate = reader.GetString(17),
                DiscountEndDate = reader.GetString(18),
                RawJson = reader.GetString(19),
                PendingNewName = reader.GetString(20),
                PendingSellerTags = reader.GetString(21),
                PendingDiscountValue = reader.GetDecimal(22),
                PendingDiscountUnitType = reader.GetString(23),
                PendingDiscountStartDate = reader.GetString(24),
                PendingDiscountEndDate = reader.GetString(25),
                LastError = reader.GetString(26),
                SyncedAt = ParseDate(reader.GetString(27))
            });
        }

        return products;
    }

    public void SavePendingName(string remoteKey, string newName)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
UPDATE products
SET pending_new_name = $newName,
    last_error = ''
WHERE remote_key = $remoteKey
""";
        command.Parameters.AddWithValue("$remoteKey", remoteKey);
        command.Parameters.AddWithValue("$newName", newName.Trim());
        command.ExecuteNonQuery();
    }

    public void SavePendingSellerTags(string remoteKey, string tags)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
UPDATE products
SET pending_seller_tags = $tags,
    last_error = ''
WHERE remote_key = $remoteKey
""";
        command.Parameters.AddWithValue("$remoteKey", remoteKey);
        command.Parameters.AddWithValue("$tags", tags.Trim());
        command.ExecuteNonQuery();
    }

    public void CompletePendingSellerTags(string remoteKey, string tags)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
UPDATE products
SET seller_tags = $tags,
    pending_seller_tags = '',
    last_error = ''
WHERE remote_key = $remoteKey
""";
        command.Parameters.AddWithValue("$remoteKey", remoteKey);
        command.Parameters.AddWithValue("$tags", tags.Trim());
        command.ExecuteNonQuery();
    }

    public void ClearPendingSellerTags(string remoteKey)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
UPDATE products
SET pending_seller_tags = '',
    last_error = ''
WHERE remote_key = $remoteKey
""";
        command.Parameters.AddWithValue("$remoteKey", remoteKey);
        command.ExecuteNonQuery();
    }

    public void SavePendingDiscount(string remoteKey, decimal value, string unitType, string startDate, string endDate)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
UPDATE products
SET pending_discount_value = $value,
    pending_discount_unit_type = $unitType,
    pending_discount_start_date = $startDate,
    pending_discount_end_date = $endDate,
    last_error = ''
WHERE remote_key = $remoteKey
""";
        command.Parameters.AddWithValue("$remoteKey", remoteKey);
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$unitType", unitType.Trim().ToUpperInvariant());
        command.Parameters.AddWithValue("$startDate", startDate.Trim());
        command.Parameters.AddWithValue("$endDate", endDate.Trim());
        command.ExecuteNonQuery();
    }

    public void CompletePendingDiscount(string remoteKey, decimal value, string unitType, string startDate, string endDate)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
UPDATE products
SET discount_value = $value,
    discount_unit_type = $unitType,
    discount_start_date = $startDate,
    discount_end_date = $endDate,
    pending_discount_value = 0,
    pending_discount_unit_type = '',
    pending_discount_start_date = '',
    pending_discount_end_date = '',
    last_error = ''
WHERE remote_key = $remoteKey
""";
        command.Parameters.AddWithValue("$remoteKey", remoteKey);
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$unitType", unitType.Trim().ToUpperInvariant());
        command.Parameters.AddWithValue("$startDate", startDate.Trim());
        command.Parameters.AddWithValue("$endDate", endDate.Trim());
        command.ExecuteNonQuery();
    }

    public void CompletePendingName(string remoteKey, string newName)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
UPDATE products
SET name = $newName,
    normalized_name = $normalizedName,
    pending_new_name = '',
    last_error = ''
WHERE remote_key = $remoteKey
""";
        command.Parameters.AddWithValue("$remoteKey", remoteKey);
        command.Parameters.AddWithValue("$newName", newName.Trim());
        command.Parameters.AddWithValue("$normalizedName", ProductText.NormalizeName(newName));
        command.ExecuteNonQuery();
    }

    public void DeleteAccount(long accountId)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        using (var deleteProducts = connection.CreateCommand())
        {
            deleteProducts.Transaction = transaction;
            deleteProducts.CommandText = "DELETE FROM products WHERE local_account_id = $id";
            deleteProducts.Parameters.AddWithValue("$id", accountId);
            deleteProducts.ExecuteNonQuery();
        }

        using (var deleteAccount = connection.CreateCommand())
        {
            deleteAccount.Transaction = transaction;
            deleteAccount.CommandText = "DELETE FROM accounts WHERE id = $id";
            deleteAccount.Parameters.AddWithValue("$id", accountId);
            deleteAccount.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void ClearPendingName(string remoteKey)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
UPDATE products
SET pending_new_name = '',
    last_error = ''
WHERE remote_key = $remoteKey
""";
        command.Parameters.AddWithValue("$remoteKey", remoteKey);
        command.ExecuteNonQuery();
    }

    public void SetLastError(string remoteKey, string error)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE products SET last_error = $error WHERE remote_key = $remoteKey";
        command.Parameters.AddWithValue("$remoteKey", remoteKey);
        command.Parameters.AddWithValue("$error", error);
        command.ExecuteNonQuery();
    }

    public void RemoveProduct(string remoteKey)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM products WHERE remote_key = $remoteKey";
        command.Parameters.AddWithValue("$remoteKey", remoteKey);
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static void AddProductParameters(SqliteCommand command, long accountId, ProductRecord product)
    {
        command.Parameters.AddWithValue("$localAccountId", accountId);
        command.Parameters.AddWithValue("$remoteKey", product.RemoteKey);
        command.Parameters.AddWithValue("$originProductNo", product.OriginProductNo);
        command.Parameters.AddWithValue("$channelProductNo", product.ChannelProductNo);
        command.Parameters.AddWithValue("$sellerManagementCode", product.SellerManagementCode);
        command.Parameters.AddWithValue("$name", product.Name);
        command.Parameters.AddWithValue("$normalizedName", product.NormalizedName);
        command.Parameters.AddWithValue("$statusType", product.StatusType);
        command.Parameters.AddWithValue("$salePrice", product.SalePrice);
        command.Parameters.AddWithValue("$stockQuantity", product.StockQuantity);
        command.Parameters.AddWithValue("$duplicateKey", product.DuplicateKey);
        command.Parameters.AddWithValue("$representativeImageUrl", product.RepresentativeImageUrl);
        command.Parameters.AddWithValue("$sellerTags", product.SellerTags);
        command.Parameters.AddWithValue("$discountValue", product.DiscountValue);
        command.Parameters.AddWithValue("$discountUnitType", product.DiscountUnitType);
        command.Parameters.AddWithValue("$discountStartDate", product.DiscountStartDate);
        command.Parameters.AddWithValue("$discountEndDate", product.DiscountEndDate);
        command.Parameters.AddWithValue("$rawJson", product.RawJson);
        command.Parameters.AddWithValue("$syncedAt", DateTime.Now.ToString("O"));
    }

    private static void EnsureColumn(SqliteConnection connection, string tableName, string columnName, string columnDefinition)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}";
        try
        {
            command.ExecuteNonQuery();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase))
        {
        }
    }

    private static DateTime ParseDate(string value)
    {
        return DateTime.TryParse(value, out var parsed) ? parsed : DateTime.MinValue;
    }
}
