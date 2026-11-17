using System.Globalization;
using Eto.Drawing;
using Eto.Forms;
using NAPS2.Config;
using NAPS2.EtoForms.Layout;

namespace NAPS2.EtoForms.Ui;

/// <summary>
/// Independent, unscrolled editor: only the grid scrolls, so movement controls
/// never disappear when the chosen toolbar item is far down the list.
/// Changes are returned only when the user presses OK.
/// </summary>
internal sealed class PreviewToolbarSettingsForm : EtoDialogBase
{
    private readonly GridView _grid;
    private readonly Button _moveUp;
    private readonly Button _moveDown;
    private readonly Button _moveStart;
    private readonly Button _moveEnd;
    private readonly Button _showHide;
    private readonly Button _addSeparator;
    private readonly Button _deleteSeparator;
    private readonly List<Row> _rows;
    // Built-in separators removed by the user must stay hidden in the stored
    // layout; otherwise Parse would reinsert them on the next launch.
    private readonly List<PreviewToolbarLayout.Item> _removedDefaultSeparators;

    private sealed class Row(string id, bool visible, string label, Image icon)
    {
        public string Id { get; } = id;
        public bool Visible { get; set; } = visible;
        public string Label { get; } = label;
        public Image Icon { get; } = icon;
        public PreviewToolbarLayout.Item ToItem() => new(Id, Visible);
    }

    private static bool Ru => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru";
    private static string Label(string ru, string en) => Ru ? ru : en;

    public List<PreviewToolbarLayout.Item>? SavedItems { get; private set; }

    public PreviewToolbarSettingsForm(Naps2Config config,
        IEnumerable<PreviewToolbarLayout.Item> source) : base(config)
    {
        Title = Label("Настройка панели инструментов просмотра", "Customize preview toolbar");
        IconName = "application";
        var initial = source.ToList();
        _removedDefaultSeparators = initial
            .Where(x => x.IsSeparator && !x.Visible && PreviewToolbarLayout.DefaultOrder.Contains(x.Id))
            .ToList();
        _rows = initial
            .Where(x => !_removedDefaultSeparators.Any(hidden => hidden.Id == x.Id))
            .Select(ToRow)
            .ToList();

        _moveUp = C.Button(UiStrings.MoveUpShort, () => MoveSelected(-1));
        _moveDown = C.Button(UiStrings.MoveDownShort, () => MoveSelected(1));
        _moveStart = C.Button(Label("В начало", "To start"), () => MoveSelectedTo(0));
        _moveEnd = C.Button(Label("В конец", "To end"), () => MoveSelectedTo(_rows.Count - 1));
        _showHide = C.Button(UiStrings.ShowHide, ToggleSelected);
        _addSeparator = C.Button(Label("+ Разделитель", "+ Separator"), AddSeparator);
        _deleteSeparator = C.Button(Label("Удалить разделитель", "Delete separator"), DeleteSeparator);
        _grid = new GridView
        {
            ShowHeader = false,
            Columns =
            {
                new GridColumn
                {
                    DataCell = new CheckBoxCell
                    {
                        Binding = Binding.Delegate<Row, bool?>(
                            item => item.Visible, (item, value) => item.Visible = value ?? false)
                    },
                    Editable = true
                },
                new GridColumn
                {
                    DataCell = new ImageTextCell
                    {
                        ImageBinding = Binding.Delegate<Row, Image>(item => item.Icon),
                        TextBinding = Binding.Delegate<Row, string>(item => item.Label)
                    },
                    Expand = true
                }
            }
        };
        _grid.SelectionChanged += (_, _) => UpdateButtons();
        _grid.CellDoubleClick += (_, _) => ToggleSelected();
        _grid.KeyDown += (_, e) =>
        {
            if (!e.KeyData.HasFlag(Keys.Control)) return;
            if (e.Key == Keys.Up)
            {
                MoveSelected(-1);
                e.Handled = true;
            }
            else if (e.Key == Keys.Down)
            {
                MoveSelected(1);
                e.Handled = true;
            }
        };
        RefreshRows(0);
    }

    private Row ToRow(PreviewToolbarLayout.Item item) =>
        new(item.Id, item.Visible,
            ToolbarSettingsPanel.GetPreviewItemLabel(item.Id),
            ToolbarSettingsPanel.GetPreviewItemIcon(item.Id, Config));

    protected override void BuildLayout()
    {
        FormStateController.RestoreFormState = false;
        FormStateController.FixedDefaultClientSize = new SizeF(650, 520);
        LayoutController.Content = L.Column(
            L.Row(
                _grid.NaturalSize(400, 430).Scale(),
                L.Column(
                    _moveUp,
                    _moveDown,
                    _moveStart,
                    _moveEnd,
                    _showHide,
                    _addSeparator,
                    _deleteSeparator
                ).MinWidth(175)
            ).Scale(),
            L.Row(
                C.Filler(),
                L.OkCancel(
                    C.OkButton(this, Accept),
                    C.CancelButton(this))
            )
        );
    }

    private void Accept()
    {
        SavedItems = _rows.Select(x => x.ToItem())
            .Concat(_removedDefaultSeparators)
            .ToList();
    }

    private int SelectedIndex => _grid.SelectedRow;

    private void RefreshRows(int index)
    {
        // The selected item stays visible while the controls on the right stay put.
        _grid.DataStore = _rows.ToList();
        if (_rows.Count > 0)
            _grid.SelectedRow = Math.Clamp(index, 0, _rows.Count - 1);
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        int index = SelectedIndex;
        bool selected = index >= 0 && index < _rows.Count;
        _moveUp.Enabled = selected && index > 0;
        _moveDown.Enabled = selected && index < _rows.Count - 1;
        _moveStart.Enabled = _moveUp.Enabled;
        _moveEnd.Enabled = _moveDown.Enabled;
        _showHide.Enabled = selected;
        _deleteSeparator.Enabled = selected && PreviewToolbarLayout.IsSeparator(_rows[index].Id);
    }

    private void MoveSelected(int offset)
    {
        int index = SelectedIndex;
        MoveSelectedTo(index + offset);
    }

    private void MoveSelectedTo(int newIndex)
    {
        int index = SelectedIndex;
        if (index < 0 || index >= _rows.Count || newIndex < 0 || newIndex >= _rows.Count || index == newIndex)
            return;
        var row = _rows[index];
        _rows.RemoveAt(index);
        _rows.Insert(newIndex, row);
        RefreshRows(newIndex);
    }

    private void ToggleSelected()
    {
        int index = SelectedIndex;
        if (index < 0 || index >= _rows.Count) return;
        _rows[index].Visible = !_rows[index].Visible;
        RefreshRows(index);
    }

    private void AddSeparator()
    {
        var items = _rows.Select(x => x.ToItem()).Concat(_removedDefaultSeparators);
        var id = PreviewToolbarLayout.NewSeparatorId(items);
        int index = Math.Clamp(SelectedIndex + 1, 0, _rows.Count);
        _rows.Insert(index, ToRow(new PreviewToolbarLayout.Item(id, true)));
        RefreshRows(index);
    }

    private void DeleteSeparator()
    {
        int index = SelectedIndex;
        if (index < 0 || index >= _rows.Count) return;
        var item = _rows[index];
        if (!PreviewToolbarLayout.IsSeparator(item.Id)) return;

        if (PreviewToolbarLayout.DefaultOrder.Contains(item.Id))
            _removedDefaultSeparators.Add(new PreviewToolbarLayout.Item(item.Id, false));

        _rows.RemoveAt(index);
        RefreshRows(Math.Min(index, _rows.Count - 1));
    }
}
