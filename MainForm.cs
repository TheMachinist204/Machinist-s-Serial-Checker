using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using HardwareSerialChecker.Models;
using HardwareSerialChecker.Services;
using HardwareSerialChecker.Controls;

namespace HardwareSerialChecker;

public partial class MainForm : Form
{
    private AnimatedBackgroundPanel backgroundPanel;
    private TabControl tabControl;
    private Dictionary<string, DataGridView> dataGridViews;
    private Button btnRefresh;
    private Button btnCopy;
    private Button btnExportJson;
    private Button btnExportCsv;
    private Label lblStatus;
    private HardwareInfoService hardwareService;
    private Dictionary<string, List<HardwareItem>> categoryData;

    public MainForm()
    {
        hardwareService = new HardwareInfoService();
        dataGridViews = new Dictionary<string, DataGridView>();
        categoryData = new Dictionary<string, List<HardwareItem>>();
        InitializeComponent();
    }

    // Try to load ICO from Assets first; if missing, load PNG and convert to Icon
    private Icon? LoadAppIcon()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var icoPath = Path.Combine(baseDir, "Assets", "appicon.ico");
            if (File.Exists(icoPath))
            {
                return new Icon(icoPath);
            }

            var pngPath = Path.Combine(baseDir, "Assets", "appicon.png");
            if (File.Exists(pngPath))
            {
                using var bmp = new Bitmap(pngPath);
                var hIcon = bmp.GetHicon();
                try
                {
                    return (Icon)Icon.FromHandle(hIcon).Clone();
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
        }
        catch { }
        return null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    // Create an .ico file from appicon.png if needed (multi-size PNG entries)
    private void EnsureIcoFromPngAssets()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var assetsDir = Path.Combine(baseDir, "Assets");
            Directory.CreateDirectory(assetsDir);

            var pngPath = Path.Combine(assetsDir, "appicon.png");
            var icoPath = Path.Combine(assetsDir, "appicon.ico");
            if (File.Exists(pngPath) && !File.Exists(icoPath))
            {
                CreateIcoFromPng(pngPath, icoPath, new[] { 16, 24, 32, 48, 64, 128, 256 });
            }
        }
        catch { }
    }

    // Writes a valid ICO with one or more PNG-compressed images (Vista+ compatible)
    private void CreateIcoFromPng(string pngPath, string icoPath, int[] sizes)
    {
        using var original = new Bitmap(pngPath);

        // Prepare PNG bytes for each size
        var images = new List<(int Size, byte[] PngBytes)>();
        foreach (var s in sizes.Distinct().OrderBy(x => x))
        {
            using var bmp = new Bitmap(s, s);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                g.DrawImage(original, new Rectangle(0, 0, s, s));
            }
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            images.Add((s, ms.ToArray()));
        }

        using var fs = new FileStream(icoPath, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        // ICONDIR
        bw.Write((ushort)0); // reserved
        bw.Write((ushort)1); // type = ICON
        bw.Write((ushort)images.Count); // count

        // Compute offsets: header (6) + entries (16*count)
        int offset = 6 + (16 * images.Count);
        foreach (var (size, png) in images)
        {
            // ICONDIRENTRY
            bw.Write((byte)(size == 256 ? 0 : size)); // width
            bw.Write((byte)(size == 256 ? 0 : size)); // height
            bw.Write((byte)0); // color count
            bw.Write((byte)0); // reserved
            bw.Write((ushort)1); // planes
            bw.Write((ushort)32); // bit count (informational)
            bw.Write(png.Length); // bytes in resource
            bw.Write(offset); // image offset
            offset += png.Length;
        }

        // Write the PNG images back-to-back
        foreach (var (_, png) in images)
        {
            bw.Write(png);
        }
    }

    private void InitializeComponent()
    {
        this.Text = "Machinist's Serial Checker";
        this.Size = new Size(1200, 700);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.Black;

        // If only PNG is present, create an ICO alongside it
        EnsureIcoFromPngAssets();

        // Attempt to set window/taskbar icon from Assets
        var appIcon = LoadAppIcon();
        if (appIcon != null)
            this.Icon = appIcon;

        // Animated Background Panel
        backgroundPanel = new AnimatedBackgroundPanel
        {
            Dock = DockStyle.Fill
        };
        this.Controls.Add(backgroundPanel);

        // TabControl - Transparent
        tabControl = new TransparentTabControl
        {
            Location = new Point(10, 10),
            Size = new Size(1160, 550),
            BackColor = Color.Transparent,
            ForeColor = Color.White
        };
        backgroundPanel.Controls.Add(tabControl);

        // Create tabs for each category
        CreateTab("BIOS/System", "BIOS");
        CreateTab("CPU", "CPU");
        CreateTab("Disks", "Disk");
        CreateTab("GPU", "GPU");
        CreateTab("Network", "NIC");

        // Buttons - semi-transparent
        btnRefresh = new Button
        {
            Text = "Refresh All",
            Location = new Point(10, 570),
            Size = new Size(100, 30),
            BackColor = Color.FromArgb(200, 20, 20, 20),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnRefresh.FlatAppearance.BorderColor = Color.White;
        btnRefresh.Click += BtnRefresh_Click;
        backgroundPanel.Controls.Add(btnRefresh);

        btnCopy = new Button
        {
            Text = "Copy Selected",
            Location = new Point(120, 570),
            Size = new Size(120, 30),
            BackColor = Color.FromArgb(200, 20, 20, 20),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnCopy.FlatAppearance.BorderColor = Color.White;
        btnCopy.Click += BtnCopy_Click;
        backgroundPanel.Controls.Add(btnCopy);

        btnExportJson = new Button
        {
            Text = "Export JSON",
            Location = new Point(250, 570),
            Size = new Size(120, 30),
            BackColor = Color.FromArgb(200, 20, 20, 20),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnExportJson.FlatAppearance.BorderColor = Color.White;
        btnExportJson.Click += BtnExportJson_Click;
        backgroundPanel.Controls.Add(btnExportJson);

        btnExportCsv = new Button
        {
            Text = "Export CSV",
            Location = new Point(380, 570),
            Size = new Size(120, 30),
            BackColor = Color.FromArgb(200, 20, 20, 20),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnExportCsv.FlatAppearance.BorderColor = Color.White;
        btnExportCsv.Click += BtnExportCsv_Click;
        backgroundPanel.Controls.Add(btnExportCsv);

        // Status Label
        lblStatus = new Label
        {
            Location = new Point(10, 610),
            Size = new Size(1160, 40),
            Text = "Click 'Refresh All' to load hardware information.",
            AutoSize = false,
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(200, 220, 255)
        };
        backgroundPanel.Controls.Add(lblStatus);

        // Load data on startup (wrapped in try-catch to prevent startup crashes)
        this.Load += (s, e) =>
        {
            try
            {
                LoadHardwareInfo();
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error loading hardware info: {ex.Message}";
                lblStatus.ForeColor = Color.Red;
                MessageBox.Show($"Failed to load hardware information on startup:\n{ex.Message}\n\nYou can try clicking 'Refresh All' manually.",
                    "Startup Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
    }

    private void CreateTab(string tabName, string category)
    {
        var tabPage = new TabPage(tabName);
        tabPage.BackColor = Color.Transparent;
        
        var dgv = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = true,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.FromArgb(10, 10, 10), // Very dark, almost black
            GridColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
            EnableHeadersVisualStyles = false,
            RowHeadersVisible = false,
            EditMode = DataGridViewEditMode.EditProgrammatically
        };
        
        // Style column headers - very dark, non-selectable
        dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(20, 20, 20);
        dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(20, 20, 20);
        dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
        dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        
        // Style rows - very dark
        dgv.DefaultCellStyle.BackColor = Color.FromArgb(15, 15, 15);
        dgv.DefaultCellStyle.ForeColor = Color.White;
        dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(50, 50, 50); // Dark gray selection
        dgv.DefaultCellStyle.SelectionForeColor = Color.White;
        
        // Alternating row style - slightly lighter
        dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(25, 25, 25);
        dgv.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(50, 50, 50); // Same dark gray selection
        dgv.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;
        
        tabPage.Controls.Add(dgv);
        tabControl.TabPages.Add(tabPage);
        dataGridViews[category] = dgv;
    }

    private void BtnRefresh_Click(object? sender, EventArgs e)
    {
        LoadHardwareInfo();
    }

    private void LoadHardwareInfo()
    {
        lblStatus.Text = "Loading hardware information...";
        lblStatus.ForeColor = Color.Blue;
        Application.DoEvents();

        try
        {
            categoryData.Clear();
            
            // Load BIOS/System info
            var biosData = hardwareService.GetBiosInfo();
            categoryData["BIOS"] = biosData;
            dataGridViews["BIOS"].DataSource = null;
            dataGridViews["BIOS"].DataSource = biosData;
            
            // Load CPU info
            var cpuData = hardwareService.GetProcessorInfo();
            categoryData["CPU"] = cpuData;
            dataGridViews["CPU"].DataSource = null;
            dataGridViews["CPU"].DataSource = cpuData;
            
            // Load Disk info
            var diskData = hardwareService.GetDiskInfo();
            categoryData["Disk"] = diskData;
            dataGridViews["Disk"].DataSource = null;
            dataGridViews["Disk"].DataSource = diskData;
            
            // Load GPU info
            var gpuData = hardwareService.GetVideoControllerInfo();
            categoryData["GPU"] = gpuData;
            dataGridViews["GPU"].DataSource = null;
            dataGridViews["GPU"].DataSource = gpuData;
            
            // Load NIC info
            var nicData = hardwareService.GetNetworkAdapterInfo();
            categoryData["NIC"] = nicData;
            dataGridViews["NIC"].DataSource = null;
            dataGridViews["NIC"].DataSource = nicData;
            
            var totalItems = categoryData.Values.Sum(list => list.Count);
            lblStatus.Text = $"Loaded {totalItems} hardware items across {categoryData.Count} categories.";
            lblStatus.ForeColor = Color.Green;
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"Error: {ex.Message}";
            lblStatus.ForeColor = Color.Red;
            MessageBox.Show($"Failed to load hardware information:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnCopy_Click(object? sender, EventArgs e)
    {
        var currentTab = tabControl.SelectedTab;
        if (currentTab == null)
            return;
        
        var category = dataGridViews.FirstOrDefault(kvp => kvp.Value.Parent == currentTab).Key;
        if (category == null)
            return;
        
        var dgv = dataGridViews[category];
        
        if (dgv.SelectedRows.Count == 0)
        {
            MessageBox.Show("No rows selected.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Category\tName\tValue\tNotes");

        foreach (DataGridViewRow row in dgv.SelectedRows)
        {
            if (row.DataBoundItem is HardwareItem item)
            {
                sb.AppendLine($"{item.Category}\t{item.Name}\t{item.Value}\t{item.Notes}");
            }
        }

        try
        {
            Clipboard.SetText(sb.ToString());
            lblStatus.Text = $"Copied {dgv.SelectedRows.Count} rows to clipboard.";
            lblStatus.ForeColor = Color.Green;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to copy to clipboard:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnExportJson_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = "json",
            FileName = $"hardware_info_{DateTime.Now:yyyyMMdd_HHmmss}.json"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var allData = categoryData.Values.SelectMany(list => list).ToList();
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(allData, options);
                File.WriteAllText(dialog.FileName, json);
                lblStatus.Text = $"Exported {allData.Count} items to {dialog.FileName}";
                lblStatus.ForeColor = Color.Green;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export JSON:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void BtnExportCsv_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            DefaultExt = "csv",
            FileName = $"hardware_info_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Category,Name,Value,Notes");

                var allData = categoryData.Values.SelectMany(list => list).ToList();
                foreach (var item in allData)
                {
                    sb.AppendLine($"\"{item.Category}\",\"{item.Name}\",\"{item.Value}\",\"{item.Notes}\"");
                }

                File.WriteAllText(dialog.FileName, sb.ToString());
                lblStatus.Text = $"Exported {allData.Count} items to {dialog.FileName}";
                lblStatus.ForeColor = Color.Green;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export CSV:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
