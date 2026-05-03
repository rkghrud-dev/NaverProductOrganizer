namespace NaverProductOrganizer;

internal sealed class DiscountDialog : Form
{
    private readonly NumericUpDown _valueBox = new();
    private readonly ComboBox _unitBox = new();
    private readonly CheckBox _periodBox = new();
    private readonly DateTimePicker _startPicker = new();
    private readonly DateTimePicker _endPicker = new();

    public decimal DiscountValue => _valueBox.Value;
    public string UnitType => _unitBox.SelectedItem?.ToString() == "%" ? "PERCENT" : "WON";
    public string StartDate => _periodBox.Checked ? _startPicker.Value.ToString("yyyy-MM-dd") : "";
    public string EndDate => _periodBox.Checked ? _endPicker.Value.ToString("yyyy-MM-dd") : "";

    public DiscountDialog(int selectedCount)
    {
        Text = "선택 상품 즉시할인";
        Width = 440;
        Height = 260;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 3,
            RowCount = 5
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var info = new Label
        {
            Text = $"선택한 {selectedCount:N0}개 상품에 즉시할인을 적용합니다.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        root.SetColumnSpan(info, 3);
        root.Controls.Add(info, 0, 0);

        root.Controls.Add(Label("할인값"), 0, 1);
        _valueBox.Dock = DockStyle.Fill;
        _valueBox.Minimum = 1;
        _valueBox.Maximum = 100000000;
        _valueBox.DecimalPlaces = 0;
        _valueBox.ThousandsSeparator = true;
        root.Controls.Add(_valueBox, 1, 1);

        _unitBox.Dock = DockStyle.Fill;
        _unitBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _unitBox.Items.AddRange(new object[] { "원", "%" });
        _unitBox.SelectedIndex = 0;
        root.Controls.Add(_unitBox, 2, 1);

        _periodBox.Text = "할인 기간 설정";
        _periodBox.Dock = DockStyle.Fill;
        _periodBox.CheckedChanged += (_, _) => ToggleDates();
        root.SetColumnSpan(_periodBox, 3);
        root.Controls.Add(_periodBox, 0, 2);

        root.Controls.Add(Label("시작/종료"), 0, 3);
        _startPicker.Dock = DockStyle.Fill;
        _startPicker.Format = DateTimePickerFormat.Short;
        _startPicker.Value = DateTime.Today;
        root.Controls.Add(_startPicker, 1, 3);

        _endPicker.Dock = DockStyle.Fill;
        _endPicker.Format = DateTimePickerFormat.Short;
        _endPicker.Value = DateTime.Today.AddDays(30);
        root.Controls.Add(_endPicker, 2, 3);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 42
        };
        root.SetColumnSpan(buttons, 3);
        root.Controls.Add(buttons, 0, 4);

        var ok = new Button { Text = "적용", DialogResult = DialogResult.OK, Width = 88, Height = 30 };
        var cancel = new Button { Text = "닫기", DialogResult = DialogResult.Cancel, Width = 88, Height = 30 };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        AcceptButton = ok;
        CancelButton = cancel;
        ToggleDates();
    }

    private static Label Label(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 8, 0)
        };
    }

    private void ToggleDates()
    {
        _startPicker.Enabled = _periodBox.Checked;
        _endPicker.Enabled = _periodBox.Checked;
    }
}
