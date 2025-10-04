namespace HardwareSerialChecker.Controls;

public class TransparentTabControl : TabControl
{
    public TransparentTabControl()
    {
        this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | 
                      ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        this.DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // Draw transparent background
        e.Graphics.Clear(Color.Transparent);
        
        // Draw tabs
        for (int i = 0; i < this.TabCount; i++)
        {
            DrawTab(e.Graphics, this.TabPages[i], i);
        }
    }

    private void DrawTab(Graphics g, TabPage tabPage, int index)
    {
        Rectangle tabRect = this.GetTabRect(index);
        bool selected = (this.SelectedIndex == index);
        
        // Draw tab background
        using (var brush = new SolidBrush(selected ? Color.FromArgb(180, 45, 45, 48) : Color.FromArgb(150, 30, 30, 30)))
        {
            g.FillRectangle(brush, tabRect);
        }
        
        // Draw tab border
        using (var pen = new Pen(Color.FromArgb(100, 70, 130, 180), 1))
        {
            g.DrawRectangle(pen, tabRect);
        }
        
        // Draw tab text
        using (var brush = new SolidBrush(Color.White))
        {
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(tabPage.Text, this.Font, brush, tabRect, sf);
        }
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        this.Invalidate();
    }
}
