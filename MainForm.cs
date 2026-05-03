using System.ComponentModel;
using System.Diagnostics;

namespace NaverProductOrganizer;

public sealed class MainForm : Form
{
    private readonly Database _database;
    private readonly NaverCommerceClient _naverClient = new();
    private readonly BindingList<ProductGridRow> _productRows = new();
    private readonly BindingList<ProductGridRow> _renameRows = new();
    private readonly BindingList<ProductGridRow> _tagRows = new();
    private readonly BindingList<ProductGridRow> _discountRows = new();
    private readonly BindingList<DuplicateCandidateRow> _duplicateRows = new();
    private readonly BindingList<GroupCandidateRow> _groupRows = new();

    private readonly ComboBox _accountCombo = new();
    private readonly TextBox _aliasBox = new();
    private readonly TextBox _clientIdBox = new();
    private readonly TextBox _clientSecretBox = new();
    private readonly TextBox _sellerAccountIdBox = new();
    private readonly TextBox _filterBox = new();
    private readonly CheckBox _duplicateOnlyBox = new();
    private readonly CheckBox _discountOnlyBox = new();
    private readonly ComboBox _statusFilterBox = new();
    private readonly DataGridView _productsGrid = new();
    private readonly DataGridView _renameGrid = new();
    private readonly DataGridView _tagGrid = new();
    private readonly DataGridView _discountGrid = new();
    private readonly DataGridView _duplicatesGrid = new();
    private readonly DataGridView _groupsGrid = new();
    private readonly TextBox _logBox = new();
    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _statusLabel = new("준비");

    private List<NaverAccount> _accounts = new();
    private List<ProductRecord> _products = new();

    public MainForm()
    {
        AppPaths.EnsureDirectories();
        _database = new Database(AppPaths.DatabasePath);
        _database.Initialize();

        Text = "Naver Product Organizer - 스마트스토어 상품 DB 정리";
        Width = 1500;
        Height = 900;
        MinimumSize = new Size(1180, 720);
        StartPosition = FormStartPosition.CenterScreen;

        BuildUi();
        LoadAccounts();
        ReloadProducts();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        Controls.Add(root);

        root.Controls.Add(BuildAccountPanel(), 0, 0);
        root.Controls.Add(BuildActionPanel(), 0, 1);
        root.Controls.Add(BuildTabs(), 0, 2);
        root.Controls.Add(BuildLogPanel(), 0, 3);

        _statusStrip.Items.Add(_statusLabel);
        Controls.Add(_statusStrip);
    }

    private Control BuildAccountPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            ColumnCount = 10,
            RowCount = 3
        };

        for (var i = 0; i < 10; i++)
        {
            panel.ColumnStyles.Add(new ColumnStyle(i % 2 == 0 ? SizeType.Absolute : SizeType.Percent, i % 2 == 0 ? 92 : 25));
        }

        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        AddLabel(panel, "계정 선택", 0, 0);
        _accountCombo.Dock = DockStyle.Fill;
        _accountCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _accountCombo.SelectedIndexChanged += (_, _) =>
        {
            FillAccountInputs();
            ReloadProducts();
        };
        panel.Controls.Add(_accountCombo, 1, 0);

        AddLabel(panel, "별칭", 2, 0);
        _aliasBox.Dock = DockStyle.Fill;
        panel.Controls.Add(_aliasBox, 3, 0);

        AddLabel(panel, "판매자 UID", 4, 0);
        _sellerAccountIdBox.Dock = DockStyle.Fill;
        panel.Controls.Add(_sellerAccountIdBox, 5, 0);

        var saveButton = Button("계정 저장", SaveAccount);
        panel.Controls.Add(saveButton, 6, 0);

        var deleteButton = Button("계정 삭제", DeleteSelectedAccount);
        panel.Controls.Add(deleteButton, 7, 0);

        var scanButton = Button("키 폴더 스캔", ScanKeyFolder);
        panel.Controls.Add(scanButton, 8, 0);

        AddLabel(panel, "Client ID", 0, 1);
        _clientIdBox.Dock = DockStyle.Fill;
        panel.SetColumnSpan(_clientIdBox, 3);
        panel.Controls.Add(_clientIdBox, 1, 1);

        AddLabel(panel, "Secret", 4, 1);
        _clientSecretBox.Dock = DockStyle.Fill;
        _clientSecretBox.UseSystemPasswordChar = true;
        panel.SetColumnSpan(_clientSecretBox, 3);
        panel.Controls.Add(_clientSecretBox, 5, 1);

        AddLabel(panel, "필터", 0, 2);
        _filterBox.Dock = DockStyle.Fill;
        _filterBox.PlaceholderText = "상품명, 상품번호, 판매자상품코드 검색";
        _filterBox.TextChanged += (_, _) => RefreshProductViews();
        panel.SetColumnSpan(_filterBox, 5);
        panel.Controls.Add(_filterBox, 1, 2);

        _duplicateOnlyBox.Text = "중복만";
        _duplicateOnlyBox.Dock = DockStyle.Fill;
        _duplicateOnlyBox.CheckedChanged += (_, _) => RefreshProductViews();
        panel.Controls.Add(_duplicateOnlyBox, 6, 2);

        _discountOnlyBox.Text = "할인만";
        _discountOnlyBox.Dock = DockStyle.Fill;
        _discountOnlyBox.CheckedChanged += (_, _) => RefreshProductViews();
        panel.Controls.Add(_discountOnlyBox, 7, 2);

        _statusFilterBox.Dock = DockStyle.Fill;
        _statusFilterBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _statusFilterBox.Items.AddRange(new object[] { "전체상태", "SALE", "WAIT", "OUTOFSTOCK", "SUSPENSION", "CLOSE", "DELETE" });
        _statusFilterBox.SelectedIndex = 0;
        _statusFilterBox.SelectedIndexChanged += (_, _) => RefreshProductViews();
        panel.SetColumnSpan(_statusFilterBox, 2);
        panel.Controls.Add(_statusFilterBox, 8, 2);

        return panel;
    }

    private Control BuildActionPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 4, 10, 4),
            FlowDirection = FlowDirection.LeftToRight
        };

        panel.Controls.Add(Button("전체 상품 동기화", SyncProducts));
        panel.Controls.Add(Button("화면 수정 저장", SaveVisibleGridEdits));
        panel.Controls.Add(Button("선택 상품명 수동변경", ManualRenameSelectedProducts));
        panel.Controls.Add(Button("상품명 변경 적용", ApplyPendingRenames));
        panel.Controls.Add(Button("태그 변경 적용", ApplyPendingTags));
        panel.Controls.Add(Button("선택 할인 적용", ApplyDiscountToSelection));
        panel.Controls.Add(Button("DB 엑셀 내보내기", ExportProducts));
        panel.Controls.Add(Button("상품명 변경 서식 만들기", ExportRenameTemplate));
        panel.Controls.Add(Button("상품명 엑셀 가져오기", ImportRenameExcel));
        panel.Controls.Add(Button("태그 변경 서식 만들기", ExportTagTemplate));
        panel.Controls.Add(Button("태그 엑셀 가져오기", ImportTagExcel));
        panel.Controls.Add(Button("중복 후보 분석", AnalyzeDuplicates));
        panel.Controls.Add(Button("선택 중복 삭제", DeleteSelectedDuplicates));
        panel.Controls.Add(Button("묶기 후보 체크", AnalyzeGroups));
        panel.Controls.Add(Button("데이터 폴더 열기", OpenDataFolder));

        return panel;
    }

    private Control BuildTabs()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };

        _productsGrid.DataSource = _productRows;
        ConfigureGrid(_productsGrid);
        ConfigureEditableProductGrid(_productsGrid);
        tabs.TabPages.Add(new TabPage("전체 상품") { Controls = { _productsGrid } });

        _renameGrid.DataSource = _renameRows;
        ConfigureGrid(_renameGrid);
        ConfigureEditableProductGrid(_renameGrid);
        tabs.TabPages.Add(new TabPage("상품명 변경") { Controls = { _renameGrid } });

        _tagGrid.DataSource = _tagRows;
        ConfigureGrid(_tagGrid);
        ConfigureEditableProductGrid(_tagGrid);
        tabs.TabPages.Add(new TabPage("태그 변경") { Controls = { _tagGrid } });

        _discountGrid.DataSource = _discountRows;
        ConfigureGrid(_discountGrid);
        ConfigureEditableProductGrid(_discountGrid);
        tabs.TabPages.Add(new TabPage("할인 변경") { Controls = { _discountGrid } });

        _duplicatesGrid.DataSource = _duplicateRows;
        ConfigureGrid(_duplicatesGrid);
        tabs.TabPages.Add(new TabPage("중복 후보") { Controls = { _duplicatesGrid } });

        _groupsGrid.DataSource = _groupRows;
        ConfigureGrid(_groupsGrid);
        tabs.TabPages.Add(new TabPage("상품 묶기 후보") { Controls = { _groupsGrid } });

        return tabs;
    }

    private Control BuildLogPanel()
    {
        _logBox.Dock = DockStyle.Fill;
        _logBox.Multiline = true;
        _logBox.ReadOnly = true;
        _logBox.ScrollBars = ScrollBars.Vertical;
        _logBox.BackColor = Color.White;
        _logBox.Font = new Font("Consolas", 9);
        return _logBox;
    }

    private void LoadAccounts()
    {
        _accounts = _database.GetAccounts();
        _accountCombo.DataSource = null;
        _accountCombo.DataSource = _accounts;
        _accountCombo.DisplayMember = nameof(NaverAccount.Alias);
        if (_accounts.Count > 0)
        {
            _accountCombo.SelectedIndex = 0;
        }
        else
        {
            _aliasBox.Text = "네이버 계정 1";
        }
    }

    private void FillAccountInputs()
    {
        if (_accountCombo.SelectedItem is not NaverAccount account)
        {
            return;
        }

        _aliasBox.Text = account.Alias;
        _clientIdBox.Text = account.ClientId;
        _clientSecretBox.Text = account.ClientSecret;
        _sellerAccountIdBox.Text = account.SellerAccountId;
    }

    private void SaveAccount()
    {
        if (string.IsNullOrWhiteSpace(_aliasBox.Text) ||
            string.IsNullOrWhiteSpace(_clientIdBox.Text) ||
            string.IsNullOrWhiteSpace(_clientSecretBox.Text) ||
            string.IsNullOrWhiteSpace(_sellerAccountIdBox.Text))
        {
            MessageBox.Show("별칭, Client ID, Secret, 판매자 UID를 모두 입력하세요.", "계정 저장", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var selected = _accountCombo.SelectedItem as NaverAccount;
        var account = new NaverAccount
        {
            Id = selected?.Id ?? 0,
            Alias = _aliasBox.Text,
            ClientId = _clientIdBox.Text,
            ClientSecret = _clientSecretBox.Text,
            SellerAccountId = _sellerAccountIdBox.Text
        };

        var id = _database.SaveAccount(account);
        AppendLog($"계정 저장: {account.Alias} (id={id})");
        LoadAccounts();
        SelectAccount(id);
    }

    private void DeleteSelectedAccount()
    {
        if (_accountCombo.SelectedItem is not NaverAccount account)
        {
            return;
        }

        var answer = MessageBox.Show(
            $"'{account.Alias}' 계정과 이 계정으로 저장된 로컬 상품 DB를 삭제합니다.\r\n네이버 상품 자체는 삭제하지 않습니다. 진행할까요?",
            "계정 삭제",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes)
        {
            return;
        }

        _database.DeleteAccount(account.Id);
        AppendLog($"계정 삭제: {account.Alias}");
        LoadAccounts();
        ReloadProducts();
    }

    private void ScanKeyFolder()
    {
        var candidates = KeyFileReader.Scan(AppPaths.DefaultKeyDirectory);
        if (candidates.Count == 0)
        {
            MessageBox.Show("키 후보를 찾지 못했습니다. naver_client_key.txt 또는 JSON 안에 client_id/client_secret/account_id 형태로 저장해두면 자동 인식합니다.", "키 폴더 스캔");
            return;
        }

        var existing = _database.GetAccounts()
            .GroupBy(a => a.ClientId)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var savedIds = new List<long>();

        foreach (var candidate in candidates)
        {
            existing.TryGetValue(candidate.ClientId, out var saved);
            var account = new NaverAccount
            {
                Id = saved?.Id ?? 0,
                Alias = string.IsNullOrWhiteSpace(candidate.Alias) ? Path.GetFileNameWithoutExtension(candidate.SourceFile) : candidate.Alias,
                ClientId = candidate.ClientId,
                ClientSecret = candidate.ClientSecret,
                SellerAccountId = candidate.SellerAccountId
            };
            savedIds.Add(_database.SaveAccount(account));
        }

        LoadAccounts();
        SelectAccount(savedIds.FirstOrDefault());

        AppendLog($"네이버 키 후보 {candidates.Count}개를 계정으로 저장했습니다.");
    }

    private async void SyncProducts()
    {
        if (!TryGetSelectedAccount(out var account))
        {
            return;
        }

        await RunBusyAsync("상품 동기화", async () =>
        {
            var progress = new Progress<string>(SetStatus);
            var products = await _naverClient.FetchAllProductsAsync(account, progress);
            _database.UpsertProducts(account.Id, products);
            AppendLog($"상품 동기화 완료: {products.Count:N0}개 저장");
            ReloadProducts();
        });
    }

    private void ReloadProducts()
    {
        var accountId = (_accountCombo.SelectedItem as NaverAccount)?.Id;
        _products = _database.GetProducts(accountId);
        RefreshProductViews();
        SetStatus($"DB 상품 {_products.Count:N0}개");
    }

    private void RefreshProductViews()
    {
        var filter = _filterBox.Text.Trim();
        var duplicateCounts = _products
            .Where(p => !string.IsNullOrWhiteSpace(p.DuplicateKey))
            .GroupBy(p => p.DuplicateKey)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var product in _products)
        {
            product.DuplicateCount = !string.IsNullOrWhiteSpace(product.DuplicateKey) && duplicateCounts.TryGetValue(product.DuplicateKey, out var count) ? count : 0;
        }

        var selectedStatus = _statusFilterBox.SelectedItem?.ToString() ?? "전체상태";
        var products = _products
            .Where(p => string.IsNullOrWhiteSpace(filter) ||
                        p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        p.OriginProductNo.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        p.ChannelProductNo.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        p.SellerManagementCode.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .Where(p => !_duplicateOnlyBox.Checked || p.DuplicateCount > 1)
            .Where(p => !_discountOnlyBox.Checked || p.DiscountValue > 0 || p.PendingDiscountValue > 0)
            .Where(p => selectedStatus == "전체상태" || p.StatusType.Equals(selectedStatus, StringComparison.OrdinalIgnoreCase))
            .ToList();

        ResetBinding(_productRows, products.Select(ToGridRow));
        ResetBinding(_renameRows, products.Where(p => !string.IsNullOrWhiteSpace(p.PendingNewName)).Select(ToGridRow));
        ResetBinding(_tagRows, products.Where(p => !string.IsNullOrWhiteSpace(p.PendingSellerTags)).Select(ToGridRow));
        ResetBinding(_discountRows, products.Where(p => p.PendingDiscountValue > 0).Select(ToGridRow));
    }

    private void SaveVisibleGridEdits()
    {
        _productsGrid.EndEdit();
        _renameGrid.EndEdit();
        _tagGrid.EndEdit();
        _discountGrid.EndEdit();

        var changed = 0;
        foreach (var row in _productRows.Concat(_renameRows).Concat(_tagRows).Concat(_discountRows)
                     .GroupBy(r => r.RemoteKey)
                     .Select(g => g.First()))
        {
            changed += SaveGridRowEdits(row) ? 1 : 0;
        }

        ReloadProducts();
        AppendLog($"화면 수정 저장 완료: {changed:N0}건 변경 대기값 저장");
    }

    private bool SaveGridRowEdits(ProductGridRow row)
    {
        var product = _products.FirstOrDefault(p => p.RemoteKey == row.RemoteKey);
        if (product is null)
        {
            return false;
        }

        var changed = false;
        var pendingName = row.변경상품명.Trim();
        if (!string.Equals(pendingName, product.PendingNewName, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(pendingName))
            {
                _database.ClearPendingName(product.RemoteKey);
            }
            else
            {
                _database.SavePendingName(product.RemoteKey, pendingName);
            }

            changed = true;
        }

        var pendingTags = ProductText.JoinTagsString(ProductText.ParseSellerTags(row.변경태그));
        if (!string.Equals(pendingTags, product.PendingSellerTags, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(pendingTags))
            {
                _database.ClearPendingSellerTags(product.RemoteKey);
            }
            else
            {
                _database.SavePendingSellerTags(product.RemoteKey, pendingTags);
            }

            changed = true;
        }

        if (row.변경할인값 > 0)
        {
            var unit = NormalizeDiscountUnitForUi(string.IsNullOrWhiteSpace(row.변경할인단위) ? "WON" : row.변경할인단위);
            if (unit is "WON" or "PERCENT")
            {
                _database.SavePendingDiscount(product.RemoteKey, row.변경할인값, unit, product.PendingDiscountStartDate, product.PendingDiscountEndDate);
                changed = true;
            }
        }

        return changed;
    }

    private void ExportProducts()
    {
        using var dialog = SaveExcelDialog($"상품DB_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        ExcelService.ExportProductDatabase(dialog.FileName, _products);
        AppendLog($"DB 엑셀 내보내기: {dialog.FileName}");
    }

    private void ExportRenameTemplate()
    {
        using var dialog = SaveExcelDialog($"상품명변경서식_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        ExcelService.ExportRenameTemplate(dialog.FileName, _products);
        AppendLog($"상품명 변경 서식 생성: {dialog.FileName}");
    }

    private void ImportRenameExcel()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
            InitialDirectory = AppPaths.ExportDirectory
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var rows = ExcelService.ImportRenameRows(dialog.FileName);
        var matched = 0;

        foreach (var row in rows)
        {
            var product = MatchProduct(row);
            if (product is null)
            {
                continue;
            }

            _database.SavePendingName(product.RemoteKey, row.NewName);
            matched++;
        }

        ReloadProducts();
        AppendLog($"상품명 엑셀 가져오기: {rows.Count:N0}행 중 {matched:N0}건 매칭");
    }

    private void ExportTagTemplate()
    {
        using var dialog = SaveExcelDialog($"태그변경서식_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        ExcelService.ExportTagTemplate(dialog.FileName, _products);
        AppendLog($"태그 변경 서식 생성: {dialog.FileName}");
    }

    private void ImportTagExcel()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
            InitialDirectory = AppPaths.ExportDirectory
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var rows = ExcelService.ImportTagRows(dialog.FileName);
        var matched = 0;
        var trimmedCount = 0;

        foreach (var row in rows)
        {
            var product = MatchProduct(row);
            if (product is null)
            {
                continue;
            }

            var parsed = ProductText.ParseSellerTags(row.NewTags);
            if (parsed.Count == 0)
            {
                continue;
            }

            if (row.NewTags.Split(new[] { ',', ';', '\n', '\r', '\t', '|' }, StringSplitOptions.RemoveEmptyEntries).Length > parsed.Count)
            {
                trimmedCount++;
            }

            _database.SavePendingSellerTags(product.RemoteKey, ProductText.JoinTagsString(parsed));
            matched++;
        }

        ReloadProducts();
        AppendLog($"태그 엑셀 가져오기: {rows.Count:N0}행 중 {matched:N0}건 매칭");
        if (trimmedCount > 0)
        {
            AppendLog($"태그는 상품당 최대 10개까지 정리했습니다. 초과/중복 정리 행: {trimmedCount:N0}");
        }
    }

    private async void ApplyPendingTags()
    {
        SaveVisibleGridEdits();
        var targets = _products
            .Where(p => !string.IsNullOrWhiteSpace(p.PendingSellerTags) &&
                        !string.Equals(ProductText.JoinTagsString(ProductText.ParseSellerTags(p.SellerTags)), p.PendingSellerTags.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targets.Count == 0)
        {
            MessageBox.Show("적용할 변경태그가 없습니다.", "태그 변경");
            return;
        }

        var answer = MessageBox.Show(
            $"{targets.Count:N0}개 상품의 판매자 입력 태그를 네이버에 실제 반영합니다.\r\n태그는 쉼표/줄바꿈 구분, 상품당 최대 10개로 정리해서 넣습니다. 진행할까요?",
            "태그 변경 적용",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes)
        {
            return;
        }

        await RunBusyAsync("태그 변경 적용", async () =>
        {
            var accounts = _database.GetAccounts().ToDictionary(a => a.Id);
            var success = 0;

            foreach (var product in targets)
            {
                if (!accounts.TryGetValue(product.LocalAccountId, out var account))
                {
                    continue;
                }

                try
                {
                    var parsed = ProductText.ParseSellerTags(product.PendingSellerTags);
                    SetStatus($"태그 변경 중: {product.OriginProductNo}");
                    await _naverClient.UpdateOriginProductTagsAsync(account, product.OriginProductNo, parsed);
                    var normalizedTags = ProductText.JoinTagsString(parsed);
                    _database.CompletePendingSellerTags(product.RemoteKey, normalizedTags);
                    success++;
                    AppendLog($"태그 변경 성공: {product.OriginProductNo} | {normalizedTags}");
                    await Task.Delay(TimeSpan.FromMilliseconds(900));
                }
                catch (Exception ex)
                {
                    _database.SetLastError(product.RemoteKey, ex.Message);
                    AppendLog($"태그 변경 실패: {product.OriginProductNo} | {ex.Message}");
                }
            }

            ReloadProducts();
            AppendLog($"태그 변경 완료: 성공 {success:N0} / 대상 {targets.Count:N0}");
        });
    }

    private void ExportDiscountTemplate()
    {
        using var dialog = SaveExcelDialog($"할인변경서식_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        ExcelService.ExportDiscountTemplate(dialog.FileName, _products);
        AppendLog($"할인 변경 서식 생성: {dialog.FileName}");
    }

    private void ImportDiscountExcel()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
            InitialDirectory = AppPaths.ExportDirectory
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var rows = ExcelService.ImportDiscountRows(dialog.FileName);
        var matched = 0;

        foreach (var row in rows)
        {
            var product = MatchProduct(row);
            if (product is null)
            {
                continue;
            }

            var unit = NormalizeDiscountUnitForUi(row.UnitType);
            if (unit is not ("PERCENT" or "WON"))
            {
                AppendLog($"할인 행 무시: 단위는 PERCENT 또는 WON만 지원합니다. 입력={row.UnitType}");
                continue;
            }

            _database.SavePendingDiscount(product.RemoteKey, row.DiscountValue, unit, row.StartDate, row.EndDate);
            matched++;
        }

        ReloadProducts();
        AppendLog($"할인 엑셀 가져오기: {rows.Count:N0}행 중 {matched:N0}건 매칭");
    }

    private async void ApplyPendingDiscounts()
    {
        SaveVisibleGridEdits();
        var targets = _products
            .Where(p => p.PendingDiscountValue > 0 && !string.IsNullOrWhiteSpace(p.PendingDiscountUnitType))
            .ToList();

        if (targets.Count == 0)
        {
            MessageBox.Show("적용할 할인 변경안이 없습니다.", "할인 변경");
            return;
        }

        var answer = MessageBox.Show(
            $"{targets.Count:N0}개 상품의 즉시할인을 네이버에 실제 반영합니다.\r\n단위는 PERCENT(정율) 또는 WON(정액)입니다. 진행할까요?",
            "할인 변경 적용",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes)
        {
            return;
        }

        await RunBusyAsync("할인 변경 적용", async () =>
        {
            var accounts = _database.GetAccounts().ToDictionary(a => a.Id);
            var success = 0;

            foreach (var product in targets)
            {
                if (!accounts.TryGetValue(product.LocalAccountId, out var account))
                {
                    continue;
                }

                try
                {
                    var unit = NormalizeDiscountUnitForUi(product.PendingDiscountUnitType);
                    SetStatus($"할인 변경 중: {product.OriginProductNo}");
                    await _naverClient.UpdateOriginProductDiscountAsync(
                        account,
                        product.OriginProductNo,
                        product.PendingDiscountValue,
                        unit,
                        product.PendingDiscountStartDate,
                        product.PendingDiscountEndDate);
                    _database.CompletePendingDiscount(
                        product.RemoteKey,
                        product.PendingDiscountValue,
                        unit,
                        product.PendingDiscountStartDate,
                        product.PendingDiscountEndDate);
                    success++;
                    AppendLog($"할인 변경 성공: {product.OriginProductNo} | {product.PendingDiscountValue}{unit}");
                    await Task.Delay(TimeSpan.FromMilliseconds(900));
                }
                catch (Exception ex)
                {
                    _database.SetLastError(product.RemoteKey, ex.Message);
                    AppendLog($"할인 변경 실패: {product.OriginProductNo} | {ex.Message}");
                }
            }

            ReloadProducts();
            AppendLog($"할인 변경 완료: 성공 {success:N0} / 대상 {targets.Count:N0}");
        });
    }

    private async void ApplyDiscountToSelection()
    {
        _productsGrid.EndEdit();
        var targets = GetSelectedProductsFromMainGrid();
        if (targets.Count == 0)
        {
            MessageBox.Show("전체 상품 탭에서 할인 적용할 상품을 체크하거나 행을 선택하세요.", "선택 할인 적용");
            return;
        }

        using var dialog = new DiscountDialog(targets.Count);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var answer = MessageBox.Show(
            $"{targets.Count:N0}개 상품에 {(dialog.UnitType == "WON" ? $"{dialog.DiscountValue:N0}원" : $"{dialog.DiscountValue:N0}%")} 즉시할인을 실제 반영합니다. 진행할까요?",
            "선택 할인 적용",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes)
        {
            return;
        }

        await ApplyDiscountToProductsAsync(targets, dialog.DiscountValue, dialog.UnitType, dialog.StartDate, dialog.EndDate);
    }

    private async void ManualRenameSelectedProducts()
    {
        _productsGrid.EndEdit();
        var targets = GetSelectedProductsFromMainGrid();
        if (targets.Count == 0)
        {
            MessageBox.Show("전체 상품 탭에서 상품명 변경할 상품을 체크하거나 행을 선택하세요.", "선택 상품명 수동변경");
            return;
        }

        var answer = MessageBox.Show(
            $"{targets.Count:N0}개 상품을 순서대로 열어 상품명을 직접 수정합니다.\r\n각 상품에서 '적용 후 다음'을 누르면 네이버에 바로 반영됩니다.",
            "선택 상품명 수동변경",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Information);
        if (answer != DialogResult.OK)
        {
            return;
        }

        var accounts = _database.GetAccounts().ToDictionary(a => a.Id);
        var success = 0;

        for (var i = 0; i < targets.Count; i++)
        {
            var product = targets[i];
            using var dialog = new ManualRenameDialog(product, i + 1, targets.Count);
            var result = dialog.ShowDialog(this);
            if (result == DialogResult.Cancel)
            {
                break;
            }

            if (result == DialogResult.Ignore)
            {
                continue;
            }

            var newName = dialog.NewName;
            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show("상품명은 비울 수 없습니다. 이 상품은 건너뜁니다.", "상품명 확인", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                continue;
            }

            if (newName == product.Name)
            {
                AppendLog($"상품명 동일, 건너뜀: {product.OriginProductNo}");
                continue;
            }

            if (!accounts.TryGetValue(product.LocalAccountId, out var account))
            {
                AppendLog($"계정 없음, 건너뜀: {product.OriginProductNo}");
                continue;
            }

            await RunBusyAsync("상품명 수동변경", async () =>
            {
                SetStatus($"상품명 수동변경 중: {product.OriginProductNo}");
                await _naverClient.UpdateOriginProductNameAsync(account, product.OriginProductNo, newName);
                var verifiedName = await _naverClient.ReadOriginProductNameAsync(account, product.OriginProductNo);
                _database.CompletePendingName(product.RemoteKey, newName);
                success++;
                AppendLog(string.Equals(verifiedName, newName, StringComparison.Ordinal)
                    ? $"수동변경 성공/API확인: {product.OriginProductNo} | {product.Name} -> {newName}"
                    : $"수동변경 요청 완료/재조회 불일치: {product.OriginProductNo} | 요청={newName} | API조회={verifiedName}");
                await Task.Delay(TimeSpan.FromMilliseconds(900));
            });

            product.Name = newName;
        }

        ReloadProducts();
        AppendLog($"상품명 수동변경 종료: 성공 {success:N0} / 선택 {targets.Count:N0}");
    }

    private List<ProductRecord> GetSelectedProductsFromMainGrid()
    {
        var selectedKeys = _productRows
            .Where(r => r.선택)
            .Select(r => r.RemoteKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (selectedKeys.Count == 0)
        {
            foreach (DataGridViewRow row in _productsGrid.SelectedRows)
            {
                if (row.DataBoundItem is ProductGridRow item && !string.IsNullOrWhiteSpace(item.RemoteKey))
                {
                    selectedKeys.Add(item.RemoteKey);
                }
            }
        }

        return _products.Where(p => selectedKeys.Contains(p.RemoteKey)).ToList();
    }

    private async Task ApplyDiscountToProductsAsync(
        IReadOnlyList<ProductRecord> targets,
        decimal value,
        string unitType,
        string startDate,
        string endDate)
    {
        await RunBusyAsync("선택 할인 적용", async () =>
        {
            var accounts = _database.GetAccounts().ToDictionary(a => a.Id);
            var success = 0;

            foreach (var product in targets)
            {
                if (!accounts.TryGetValue(product.LocalAccountId, out var account))
                {
                    continue;
                }

                try
                {
                    SetStatus($"할인 적용 중: {product.OriginProductNo}");
                    await _naverClient.UpdateOriginProductDiscountAsync(
                        account,
                        product.OriginProductNo,
                        value,
                        unitType,
                        startDate,
                        endDate);
                    _database.CompletePendingDiscount(product.RemoteKey, value, unitType, startDate, endDate);
                    success++;
                    AppendLog($"할인 적용 성공: {product.OriginProductNo} | {value:N0}{(unitType == "WON" ? "원" : "%")}");
                    await Task.Delay(TimeSpan.FromMilliseconds(900));
                }
                catch (Exception ex)
                {
                    _database.SetLastError(product.RemoteKey, ex.Message);
                    AppendLog($"할인 적용 실패: {product.OriginProductNo} | {ex.Message}");
                }
            }

            ReloadProducts();
            AppendLog($"선택 할인 적용 완료: 성공 {success:N0} / 대상 {targets.Count:N0}");
        });
    }

    private async void ApplyPendingRenames()
    {
        SaveVisibleGridEdits();
        var targets = _products
            .Where(p => !string.IsNullOrWhiteSpace(p.PendingNewName) && p.PendingNewName.Trim() != p.Name.Trim())
            .ToList();

        if (targets.Count == 0)
        {
            MessageBox.Show("적용할 변경상품명이 없습니다.", "상품명 변경");
            return;
        }

        var answer = MessageBox.Show(
            $"{targets.Count:N0}개 상품명을 네이버에 실제 반영합니다.\r\n반영 전 DB/엑셀 백업을 권장합니다. 진행할까요?",
            "상품명 변경 적용",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes)
        {
            return;
        }

        await RunBusyAsync("상품명 변경 적용", async () =>
        {
            var accounts = _database.GetAccounts().ToDictionary(a => a.Id);
            var success = 0;

            foreach (var product in targets)
            {
                if (!accounts.TryGetValue(product.LocalAccountId, out var account))
                {
                    continue;
                }

                try
                {
                    SetStatus($"상품명 변경 중: {product.OriginProductNo}");
                    await _naverClient.UpdateOriginProductNameAsync(account, product.OriginProductNo, product.PendingNewName);
                    var verifiedName = await _naverClient.ReadOriginProductNameAsync(account, product.OriginProductNo);
                    _database.CompletePendingName(product.RemoteKey, product.PendingNewName);
                    success++;
                    AppendLog(string.Equals(verifiedName, product.PendingNewName, StringComparison.Ordinal)
                        ? $"변경 성공/API확인: {product.OriginProductNo} | {product.Name} -> {product.PendingNewName}"
                        : $"변경 요청 완료/재조회 불일치: {product.OriginProductNo} | 요청={product.PendingNewName} | API조회={verifiedName}");
                }
                catch (Exception ex)
                {
                    _database.SetLastError(product.RemoteKey, ex.Message);
                    AppendLog($"변경 실패: {product.OriginProductNo} | {ex.Message}");
                }
            }

            ReloadProducts();
            AppendLog($"상품명 변경 완료: 성공 {success:N0} / 대상 {targets.Count:N0}");
        });
    }

    private void AnalyzeDuplicates()
    {
        var rows = ProductAnalyzer.FindDuplicates(_products);
        ResetBinding(_duplicateRows, rows);
        AppendLog($"중복 후보 분석 완료: {rows.Count:N0}행");
    }

    private async void DeleteSelectedDuplicates()
    {
        var selectedKeys = _duplicateRows
            .Where(r => r.선택)
            .Select(r => r.RemoteKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (selectedKeys.Count == 0)
        {
            foreach (DataGridViewRow row in _duplicatesGrid.SelectedRows)
            {
                if (row.DataBoundItem is DuplicateCandidateRow item)
                {
                    selectedKeys.Add(item.RemoteKey);
                }
            }
        }

        var targets = _products.Where(p => selectedKeys.Contains(p.RemoteKey)).ToList();
        if (targets.Count == 0)
        {
            MessageBox.Show("중복 후보 탭에서 삭제할 행을 선택하거나 선택 체크를 켜세요.", "중복 삭제");
            return;
        }

        var answer = MessageBox.Show(
            $"{targets.Count:N0}개 상품을 네이버에서 실제 삭제합니다.\r\n판매 이력/주문/검수 상태에 따라 실패할 수 있습니다. 진행할까요?",
            "중복 상품 삭제",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes)
        {
            return;
        }

        await RunBusyAsync("중복 상품 삭제", async () =>
        {
            var accounts = _database.GetAccounts().ToDictionary(a => a.Id);
            var success = 0;

            foreach (var product in targets)
            {
                if (!accounts.TryGetValue(product.LocalAccountId, out var account))
                {
                    continue;
                }

                try
                {
                    SetStatus($"삭제 중: {product.Name}");
                    if (!string.IsNullOrWhiteSpace(product.ChannelProductNo))
                    {
                        await _naverClient.DeleteChannelProductAsync(account, product.ChannelProductNo);
                    }
                    else if (!string.IsNullOrWhiteSpace(product.OriginProductNo))
                    {
                        await _naverClient.DeleteOriginProductAsync(account, product.OriginProductNo);
                    }
                    else
                    {
                        throw new InvalidOperationException("삭제할 상품번호가 없습니다.");
                    }

                    _database.RemoveProduct(product.RemoteKey);
                    success++;
                    AppendLog($"삭제 성공: {product.Name}");
                }
                catch (Exception ex)
                {
                    _database.SetLastError(product.RemoteKey, ex.Message);
                    AppendLog($"삭제 실패: {product.Name} | {ex.Message}");
                }
            }

            ReloadProducts();
            AnalyzeDuplicates();
            AppendLog($"중복 삭제 완료: 성공 {success:N0} / 대상 {targets.Count:N0}");
        });
    }

    private void AnalyzeGroups()
    {
        var rows = ProductAnalyzer.FindGroupCandidates(_products);
        ResetBinding(_groupRows, rows);
        AppendLog("상품 묶기 후보 체크 완료. 네이버 API에는 그룹상품 등록/전환/해제/수정/삭제가 있으나, 실제 묶기는 옵션/카테고리/상세정보 검증 후 별도 단계로 실행하는 구조가 안전합니다.");
    }

    private void OpenDataFolder()
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = AppPaths.DataDirectory,
            UseShellExecute = true
        });
    }

    private ProductRecord? MatchProduct(RenameImportRow row)
    {
        return _products.FirstOrDefault(p =>
                   !string.IsNullOrWhiteSpace(row.OriginProductNo) &&
                   p.OriginProductNo == row.OriginProductNo)
               ?? _products.FirstOrDefault(p =>
                   !string.IsNullOrWhiteSpace(row.ChannelProductNo) &&
                   p.ChannelProductNo == row.ChannelProductNo)
               ?? _products.FirstOrDefault(p =>
                   !string.IsNullOrWhiteSpace(row.SellerManagementCode) &&
                   p.SellerManagementCode.Equals(row.SellerManagementCode, StringComparison.OrdinalIgnoreCase));
    }

    private ProductRecord? MatchProduct(TagImportRow row)
    {
        return _products.FirstOrDefault(p =>
                   !string.IsNullOrWhiteSpace(row.OriginProductNo) &&
                   p.OriginProductNo == row.OriginProductNo)
               ?? _products.FirstOrDefault(p =>
                   !string.IsNullOrWhiteSpace(row.ChannelProductNo) &&
                   p.ChannelProductNo == row.ChannelProductNo)
               ?? _products.FirstOrDefault(p =>
                   !string.IsNullOrWhiteSpace(row.SellerManagementCode) &&
                   p.SellerManagementCode.Equals(row.SellerManagementCode, StringComparison.OrdinalIgnoreCase));
    }

    private ProductRecord? MatchProduct(DiscountImportRow row)
    {
        return _products.FirstOrDefault(p =>
                   !string.IsNullOrWhiteSpace(row.OriginProductNo) &&
                   p.OriginProductNo == row.OriginProductNo)
               ?? _products.FirstOrDefault(p =>
                   !string.IsNullOrWhiteSpace(row.ChannelProductNo) &&
                   p.ChannelProductNo == row.ChannelProductNo)
               ?? _products.FirstOrDefault(p =>
                   !string.IsNullOrWhiteSpace(row.SellerManagementCode) &&
                   p.SellerManagementCode.Equals(row.SellerManagementCode, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeDiscountUnitForUi(string unitType)
    {
        var normalized = unitType.Trim().ToUpperInvariant();
        return normalized switch
        {
            "%" or "정율" or "퍼센트" => "PERCENT",
            "원" or "정액" => "WON",
            _ => normalized
        };
    }

    private bool TryGetSelectedAccount(out NaverAccount account)
    {
        if (_accountCombo.SelectedItem is NaverAccount selected)
        {
            try
            {
                NaverCommerceClient.ValidateAccount(selected);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "계정 확인", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                account = new NaverAccount();
                return false;
            }

            account = selected;
            return true;
        }

        MessageBox.Show("먼저 계정을 저장하고 선택하세요.", "계정 필요", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        account = new NaverAccount();
        return false;
    }

    private void SelectAccount(long accountId)
    {
        if (accountId == 0)
        {
            return;
        }

        for (var i = 0; i < _accountCombo.Items.Count; i++)
        {
            if (_accountCombo.Items[i] is NaverAccount account && account.Id == accountId)
            {
                _accountCombo.SelectedIndex = i;
                FillAccountInputs();
                ReloadProducts();
                return;
            }
        }
    }

    private async Task RunBusyAsync(string title, Func<Task> action)
    {
        UseWaitCursor = true;
        Enabled = false;
        try
        {
            SetStatus($"{title} 진행 중");
            await action();
            SetStatus($"{title} 완료");
        }
        catch (Exception ex)
        {
            AppendLog($"{title} 실패: {ex.Message}");
            MessageBox.Show(ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus($"{title} 실패");
        }
        finally
        {
            Enabled = true;
            UseWaitCursor = false;
        }
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(message));
            return;
        }

        _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private void SetStatus(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => SetStatus(message));
            return;
        }

        _statusLabel.Text = message;
    }

    private static ProductGridRow ToGridRow(ProductRecord product)
    {
        return new ProductGridRow
        {
            계정 = product.AccountAlias,
            원상품번호 = product.OriginProductNo,
            채널상품번호 = product.ChannelProductNo,
            판매자상품코드 = product.SellerManagementCode,
            상품명 = product.Name,
            변경상품명 = product.PendingNewName,
            판매상태 = product.StatusType,
            판매가 = product.SalePrice,
            재고 = product.StockQuantity,
            중복 = product.DuplicateCount > 1 ? "중복" : "",
            중복키 = product.DuplicateKey,
            중복수 = product.DuplicateCount,
            할인값 = product.DiscountValue,
            할인단위 = product.DiscountUnitType,
            할인시작 = product.DiscountStartDate,
            할인종료 = product.DiscountEndDate,
            변경할인값 = product.PendingDiscountValue,
            변경할인단위 = product.PendingDiscountUnitType,
            태그 = product.SellerTags,
            변경태그 = product.PendingSellerTags,
            대표이미지 = product.RepresentativeImageUrl,
            오류 = product.LastError,
            RemoteKey = product.RemoteKey
        };
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button
        {
            Text = text,
            Width = 140,
            Height = 30,
            Margin = new Padding(3)
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static void AddLabel(TableLayoutPanel panel, string text, int column, int row)
    {
        panel.Controls.Add(new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 6, 0)
        }, column, row);
    }

    private static void ConfigureGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.AutoGenerateColumns = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = true;
        grid.ReadOnly = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        grid.RowHeadersWidth = 24;
        grid.DataBindingComplete += (_, _) =>
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.DataBoundItem is ProductGridRow product && product.중복수 > 1)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 245, 220);
                }
            }
        };
    }

    private void ConfigureEditableProductGrid(DataGridView grid)
    {
        grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grid.IsCurrentCellDirty)
            {
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };

        grid.CellEndEdit += (_, e) =>
        {
            if (e.RowIndex < 0 || grid.Rows[e.RowIndex].DataBoundItem is not ProductGridRow row)
            {
                return;
            }

            SaveGridRowEdits(row);
        };

        grid.DataBindingComplete += (_, _) =>
        {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.ReadOnly = column.Name is not ("선택" or "변경상품명" or "변경태그" or "변경할인값" or "변경할인단위");
            }

            if (grid.Columns.Contains("RemoteKey"))
            {
                grid.Columns["RemoteKey"]!.Visible = false;
            }

            if (grid.Columns.Contains("대표이미지"))
            {
                grid.Columns["대표이미지"]!.Width = 120;
            }

            if (grid.Columns.Contains("상품명"))
            {
                grid.Columns["상품명"]!.Width = 360;
            }

            if (grid.Columns.Contains("변경상품명"))
            {
                grid.Columns["변경상품명"]!.Width = 360;
            }
        };
    }

    private static void ResetBinding<T>(BindingList<T> target, IEnumerable<T> values)
    {
        target.RaiseListChangedEvents = false;
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }

        target.RaiseListChangedEvents = true;
        target.ResetBindings();
    }

    private static SaveFileDialog SaveExcelDialog(string fileName)
    {
        return new SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            InitialDirectory = AppPaths.ExportDirectory,
            FileName = fileName,
            OverwritePrompt = true
        };
    }
}
