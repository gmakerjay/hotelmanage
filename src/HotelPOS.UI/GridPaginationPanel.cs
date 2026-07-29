using System;
using System.Drawing;
using System.Windows.Forms;

namespace HotelPOS.UI;

/// <summary>
/// แผงควบคุมการแบ่งหน้าแสดงผล (Pagination) สำหรับ DataGridView เพื่อเพิ่มความลื่นไหลและเสถียรภาพเมื่อมีปริมาณข้อมูลสูง
/// </summary>
public class GridPaginationPanel : Panel
{
    private readonly Action _onPageChanged;
    private int _currentPage = 1;
    private int _pageSize = 25;
    private int _totalItems = 0;
    private int _totalPages = 1;

    private Button _btnFirst = null!;
    private Button _btnPrev = null!;
    private Button _btnNext = null!;
    private Button _btnLast = null!;
    private Label _lblPageInfo = null!;

    public int CurrentPage => _currentPage;
    public int PageSize => _pageSize;
    public int TotalPages => _totalPages;

    public GridPaginationPanel(Action onPageChanged)
    {
        _onPageChanged = onPageChanged;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Dock = DockStyle.Bottom;
        Height = 42;
        BackColor = Color.FromArgb(248, 250, 252);
        Padding = new Padding(8, 4, 8, 4);

        _btnFirst = new Button { Text = "«", Width = 35, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Cursor = Cursors.Hand };
        _btnFirst.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);
        _btnFirst.Click += (s, e) => { if (_currentPage > 1) { _currentPage = 1; _onPageChanged(); } };

        _btnPrev = new Button { Text = "‹", Width = 35, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Cursor = Cursors.Hand };
        _btnPrev.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);
        _btnPrev.Click += (s, e) => { if (_currentPage > 1) { _currentPage--; _onPageChanged(); } };

        _lblPageInfo = new Label
        {
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            Text = "หน้า 1 / 1 (ทั้งหมด 0 รายการ)",
            AutoSize = true
        };

        _btnNext = new Button { Text = "›", Width = 35, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Cursor = Cursors.Hand, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        _btnNext.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);
        _btnNext.Click += (s, e) => { if (_currentPage < _totalPages) { _currentPage++; _onPageChanged(); } };

        _btnLast = new Button { Text = "»", Width = 35, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Cursor = Cursors.Hand, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        _btnLast.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);
        _btnLast.Click += (s, e) => { if (_currentPage < _totalPages) { _currentPage = _totalPages; _onPageChanged(); } };

        Controls.AddRange(new Control[] { _btnFirst, _btnPrev, _lblPageInfo, _btnNext, _btnLast });

        SizeChanged += GridPaginationPanel_SizeChanged;
        GridPaginationPanel_SizeChanged(this, EventArgs.Empty);
    }

    private void GridPaginationPanel_SizeChanged(object? sender, EventArgs e)
    {
        _btnFirst.Location = new Point(8, 7);
        _btnPrev.Location = new Point(48, 7);
        _lblPageInfo.Location = new Point(88, 11);
        _btnNext.Location = new Point(Width - 88, 7);
        _btnLast.Location = new Point(Width - 48, 7);
    }

    public void Reset()
    {
        _currentPage = 1;
    }

    public void UpdateState(int totalItems)
    {
        _totalItems = totalItems;
        _totalPages = (int)Math.Ceiling((double)_totalItems / _pageSize);
        if (_totalPages < 1) _totalPages = 1;
        if (_currentPage > _totalPages) _currentPage = _totalPages;
        if (_currentPage < 1) _currentPage = 1;

        _lblPageInfo.Text = $"หน้า {_currentPage} / {_totalPages} (ทั้งหมด {_totalItems} รายการ)";
        _btnFirst.Enabled = _currentPage > 1;
        _btnPrev.Enabled = _currentPage > 1;
        _btnNext.Enabled = _currentPage < _totalPages;
        _btnLast.Enabled = _currentPage < _totalPages;
    }

    public System.Collections.Generic.IEnumerable<T> GetPageData<T>(System.Collections.Generic.IEnumerable<T> source)
    {
        return source.Skip((_currentPage - 1) * _pageSize).Take(_pageSize);
    }
}
