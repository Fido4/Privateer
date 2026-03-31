using System;
using System.Drawing;
using Privateer.Desktop.Models;
using System.Windows.Forms;

namespace Privateer.Desktop.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;

    public TrayIconService(Icon icon, Action onPreferencesRequested, Action onExitRequested)
    {
        _contextMenu = new ContextMenuStrip();
        _contextMenu.ShowImageMargin = false;
        _contextMenu.ShowCheckMargin = false;
        _contextMenu.Padding = Padding.Empty;
        _contextMenu.Font = new Font("Consolas", 8.5f, FontStyle.Regular);
        _contextMenu.Items.Add(CreateMenuItem("Preferences", onPreferencesRequested));
        _contextMenu.Items.Add(CreateSeparator());
        _contextMenu.Items.Add(CreateMenuItem("Exit", onExitRequested));

        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Text = "Privateer",
            Visible = true,
            ContextMenuStrip = _contextMenu
        };

        _notifyIcon.DoubleClick += (_, _) => onPreferencesRequested();
    }

    public void ApplyTheme(AppTheme resolvedTheme)
    {
        try
        {
            var palette = TrayMenuPalette.ForTheme(resolvedTheme);

            _contextMenu.Renderer = new TrayMenuRenderer(palette);
            _contextMenu.BackColor = palette.Background;
            _contextMenu.ForeColor = palette.Foreground;

            foreach (ToolStripItem item in _contextMenu.Items)
            {
                if (item is ToolStripSeparator separator)
                {
                    separator.Margin = Padding.Empty;
                    separator.Padding = Padding.Empty;
                    separator.AutoSize = false;
                    separator.Height = 9;
                    continue;
                }

                item.BackColor = palette.Background;
                item.ForeColor = palette.Foreground;
                item.Font = _contextMenu.Font;
            }
        }
        catch
        {
            // Keep the app alive even if tray menu theming ever fails.
        }
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
    }

    public void ShowWarning(string title, string text)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.BalloonTipIcon = ToolTipIcon.Warning;
        _notifyIcon.ShowBalloonTip(4000);
    }

    private static ToolStripMenuItem CreateMenuItem(string text, Action onClick)
    {
        var item = new ToolStripMenuItem(text, null, (_, _) => onClick())
        {
            AutoSize = false,
            Width = 108,
            Height = 30,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            TextAlign = ContentAlignment.MiddleCenter
        };

        return item;
    }

    private static ToolStripSeparator CreateSeparator()
    {
        return new ToolStripSeparator
        {
            AutoSize = false,
            Height = 9,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
    }

    private sealed record TrayMenuPalette(
        Color Background,
        Color Foreground,
        Color Border,
        Color HoverBackground,
        Color HoverForeground,
        Color Separator)
    {
        public static TrayMenuPalette ForTheme(AppTheme theme)
        {
            return theme switch
            {
                AppTheme.Dark => new TrayMenuPalette(
                    FromHex("#FF202020"),
                    FromHex("#FFF5F5F5"),
                    FromHex("#FF3B3B3B"),
                    FromHex("#FF2E4A1B"),
                    FromHex("#FFF5F5F5"),
                    FromHex("#FF3B3B3B")),
                AppTheme.TanSepia => new TrayMenuPalette(
                    FromHex("#FFF7ECDD"),
                    FromHex("#FF3F2E1F"),
                    FromHex("#FF6E5843"),
                    FromHex("#FFE3D9C6"),
                    FromHex("#FF2F2419"),
                    FromHex("#FFB8A48D")),
                AppTheme.BrownSepia => new TrayMenuPalette(
                    FromHex("#FF2A1F19"),
                    FromHex("#FFF4E6D3"),
                    FromHex("#FF5A4638"),
                    FromHex("#FF3B2B22"),
                    FromHex("#FFFFF2E2"),
                    FromHex("#FF5A4638")),
                AppTheme.GreenSepia => new TrayMenuPalette(
                    FromHex("#FF20271E"),
                    FromHex("#FFEAF0E2"),
                    FromHex("#FF455243"),
                    FromHex("#FF2E392C"),
                    FromHex("#FFF4F8EE"),
                    FromHex("#FF546252")),
                _ => new TrayMenuPalette(
                    FromHex("#FFF8FFFF"),
                    FromHex("#FF101828"),
                    FromHex("#331F2937"),
                    FromHex("#FFE7F7E2"),
                    FromHex("#FF101828"),
                    FromHex("#331F2937"))
            };
        }

        private static Color FromHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                throw new ArgumentException("Hex color cannot be empty.", nameof(hex));
            }

            var normalized = hex.TrimStart('#');
            return normalized.Length switch
            {
                6 => Color.FromArgb(
                    255,
                    Convert.ToByte(normalized[0..2], 16),
                    Convert.ToByte(normalized[2..4], 16),
                    Convert.ToByte(normalized[4..6], 16)),
                8 => Color.FromArgb(
                    Convert.ToByte(normalized[0..2], 16),
                    Convert.ToByte(normalized[2..4], 16),
                    Convert.ToByte(normalized[4..6], 16),
                    Convert.ToByte(normalized[6..8], 16)),
                _ => throw new FormatException($"Unsupported color value '{hex}'.")
            };
        }
    }

    private sealed class TrayColorTable(TrayMenuPalette palette) : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => palette.Background;
        public override Color ImageMarginGradientBegin => palette.Background;
        public override Color ImageMarginGradientMiddle => palette.Background;
        public override Color ImageMarginGradientEnd => palette.Background;
        public override Color MenuBorder => palette.Border;
        public override Color MenuItemBorder => palette.Border;
        public override Color MenuItemSelected => palette.HoverBackground;
        public override Color MenuItemSelectedGradientBegin => palette.HoverBackground;
        public override Color MenuItemSelectedGradientEnd => palette.HoverBackground;
        public override Color SeparatorDark => palette.Separator;
        public override Color SeparatorLight => palette.Separator;
    }

    private sealed class TrayMenuRenderer(TrayMenuPalette palette) : ToolStripProfessionalRenderer(new TrayColorTable(palette))
    {
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            using var backgroundBrush = new SolidBrush(palette.Background);
            e.Graphics.FillRectangle(backgroundBrush, new Rectangle(Point.Empty, e.Item.Size));
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            TextRenderer.DrawText(
                e.Graphics,
                e.Text,
                e.TextFont,
                new Rectangle(Point.Empty, e.Item.Size),
                palette.Foreground,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var y = e.Item.Height / 2;
            using var separatorPen = new Pen(palette.Separator);
            e.Graphics.DrawLine(
                separatorPen,
                0,
                y,
                e.Item.Width,
                y);
        }
    }
}
