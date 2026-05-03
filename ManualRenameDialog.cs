namespace NaverProductOrganizer;

internal sealed class ManualRenameDialog : Form
{
    private readonly TextBox _newNameBox = new();

    public string NewName => _newNameBox.Text.Trim();

    public ManualRenameDialog(ProductRecord product, int index, int total)
    {
        Text = "선택 상품명 수동변경";
        Width = 760;
        Height = 430;
        MinimumSize = new Size(640, 360);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ColumnCount = 1,
            RowCount = 6
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        Controls.Add(root);

        var info = new Label
        {
            Dock = DockStyle.Fill,
            Text = $"{index:N0} / {total:N0} | 원상품번호 {product.OriginProductNo} | 판매자상품코드 {product.SellerManagementCode}",
            TextAlign = ContentAlignment.MiddleLeft
        };
        root.Controls.Add(info, 0, 0);

        root.Controls.Add(Label("기존 상품명"), 0, 1);
        var currentNameBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Text = product.Name
        };
        root.Controls.Add(currentNameBox, 0, 2);

        root.Controls.Add(Label("변경 상품명"), 0, 3);
        _newNameBox.Dock = DockStyle.Fill;
        _newNameBox.Multiline = true;
        _newNameBox.ScrollBars = ScrollBars.Vertical;
        _newNameBox.Text = string.IsNullOrWhiteSpace(product.PendingNewName) ? product.Name : product.PendingNewName;
        root.Controls.Add(_newNameBox, 0, 4);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };
        root.Controls.Add(buttons, 0, 5);

        var apply = new Button { Text = "적용 후 다음", DialogResult = DialogResult.OK, Width = 120, Height = 32 };
        var skip = new Button { Text = "건너뛰기", DialogResult = DialogResult.Ignore, Width = 96, Height = 32 };
        var close = new Button { Text = "닫기", DialogResult = DialogResult.Cancel, Width = 96, Height = 32 };
        buttons.Controls.Add(apply);
        buttons.Controls.Add(skip);
        buttons.Controls.Add(close);

        AcceptButton = apply;
        CancelButton = close;
        Shown += (_, _) =>
        {
            _newNameBox.Focus();
            _newNameBox.SelectionStart = _newNameBox.TextLength;
        };
    }

    private static Label Label(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
        };
    }
}
