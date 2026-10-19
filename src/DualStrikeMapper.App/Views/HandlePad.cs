using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DualStrikeMapper.App.Views;

/// <summary>Square pad showing where the handle is, with the aim zone outlined.</summary>
public class HandlePad : Control
{
    public static readonly StyledProperty<double> XProperty = AvaloniaProperty.Register<HandlePad, double>(nameof(X));
    public static readonly StyledProperty<double> YProperty = AvaloniaProperty.Register<HandlePad, double>(nameof(Y));
    public static readonly StyledProperty<double> AimZoneProperty = AvaloniaProperty.Register<HandlePad, double>(nameof(AimZone), 0.85);
    public static readonly StyledProperty<bool> ShowZoneProperty = AvaloniaProperty.Register<HandlePad, bool>(nameof(ShowZone), true);
    public static readonly StyledProperty<bool> ActiveProperty = AvaloniaProperty.Register<HandlePad, bool>(nameof(Active), true);

    private static readonly IBrush Background = new SolidColorBrush(Color.Parse("#1C1D1E"));
    private static readonly IPen Edge = new Pen(new SolidColorBrush(Color.Parse("#4A4C4E")), 1);
    private static readonly IPen Cross = new Pen(new SolidColorBrush(Color.Parse("#2E3032")), 1);
    private static readonly IPen Zone = new Pen(new SolidColorBrush(Color.Parse("#46D79A"), 0.55), 1, new DashStyle([3, 3], 0));
    private static readonly IBrush Dot = new SolidColorBrush(Color.Parse("#46D79A"));
    private static readonly IBrush DotOff = new SolidColorBrush(Color.Parse("#6B6E70"));
    private static readonly IBrush Glow = new SolidColorBrush(Color.Parse("#46D79A"), 0.25);

    static HandlePad()
    {
        AffectsRender<HandlePad>(XProperty, YProperty, AimZoneProperty, ShowZoneProperty, ActiveProperty);
    }

    public double X { get => GetValue(XProperty); set => SetValue(XProperty, value); }
    public double Y { get => GetValue(YProperty); set => SetValue(YProperty, value); }
    public double AimZone { get => GetValue(AimZoneProperty); set => SetValue(AimZoneProperty, value); }
    public bool ShowZone { get => GetValue(ShowZoneProperty); set => SetValue(ShowZoneProperty, value); }
    public bool Active { get => GetValue(ActiveProperty); set => SetValue(ActiveProperty, value); }

    public override void Render(DrawingContext context)
    {
        var size = System.Math.Min(Bounds.Width, Bounds.Height);
        var origin = new Point((Bounds.Width - size) / 2, (Bounds.Height - size) / 2);
        var box = new Rect(origin, new Size(size, size));
        var center = box.Center;
        var half = size / 2 - 6;

        context.DrawRectangle(Background, Edge, box, 8, 8);
        context.DrawLine(Cross, new Point(center.X, box.Top + 4), new Point(center.X, box.Bottom - 4));
        context.DrawLine(Cross, new Point(box.Left + 4, center.Y), new Point(box.Right - 4, center.Y));

        if (ShowZone)
        {
            var z = half * System.Math.Clamp(AimZone, 0.3, 0.98);
            context.DrawRectangle(null, Zone, new Rect(center.X - z, center.Y - z, z * 2, z * 2), 4, 4);
        }

        var dot = new Point(center.X + System.Math.Clamp(X, -1, 1) * half, center.Y + System.Math.Clamp(Y, -1, 1) * half);
        if (Active)
            context.DrawEllipse(Glow, null, dot, 9, 9);
        context.DrawEllipse(Active ? Dot : DotOff, null, dot, 4.5, 4.5);
    }
}
