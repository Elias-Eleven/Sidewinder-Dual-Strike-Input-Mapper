using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DualStrikeMapper.App.ViewModels;
using DualStrikeMapper.Core.Controller;

namespace DualStrikeMapper.App.Views;

/// <summary>
/// The SideWinder Dual Strike photo with labelled callouts, laid out on a fixed
/// 1000x560 canvas. Pressed buttons light up live.
/// </summary>
public class ControllerCanvas : UserControl
{
    public const double DesignWidth = 1000;
    public const double DesignHeight = 560;

    // Photo placement: the 1200x1200 source photo is drawn at 400x400 here.
    private const double PhotoLeft = 300;
    // Low enough that the button cluster sits level with the middle of the chip columns,
    // so callout lines fan in from above and below instead of all climbing.
    private const double PhotoTop = 115;
    private const double PhotoSize = 400;
    private const double PhotoSourceSize = 1200;

    private const double LeftChipX = 232;
    private const double RightChipX = 768;
    private const double ChipSize = 40;

    private static readonly Color Accent = Color.Parse("#46D79A");
    private static readonly IBrush LineBrush = new SolidColorBrush(Colors.White, 0.85);
    private static readonly IBrush LineActive = new SolidColorBrush(Accent);

    /// <summary>
    /// Where each control is on the photo (source-photo pixels), its highlight size, and its callout:
    /// side, row, and elbow x. Lines aim at the button's centre and stop at its edge (or exactly on the
    /// point when Gap is 0: the D-pad directions), unless a landing angle is given.
    /// Both sides use the same 50px row grid. No two lines cross, every line keeps ≥ 5px from other glows
    /// (≥ 14px from other D-pad points) and lines stay ≥ 10px apart on the left.
    /// Re-check that if you move anything (or PhotoTop).
    /// </summary>
    private static readonly Dictionary<ControlId, Spot> Layout = new()
    {
        // Triggers sit on top of the grips; the callouts mirror each other (elbow 308 / 1000 - 308).
        // Gap 12: the line stops just above the grip, so it doesn't run onto the controller.
        [ControlId.Button7] = new(new Point(233, 108), 16, true, 65, 308, Gap: 12),
        [ControlId.Button8] = new(new Point(985, 85), 16, false, 65, 692, Gap: 12),
        // D-pad, a mirror of the right-hand diamond: same rows (175–325, row 125 left empty like the right),
        // same order (top, outer, bottom, far) and the same final angles (◀ 73° like C, ▼ 77° like D, ▶ 73°
        // like B). ▲ sits on A's row; its point is 10px lower than A, so it ends in a short drop.
        // The points form a cross 66px out from the centre of the disc's face (219, 276).
        [ControlId.DpadUp] = new(new Point(219, 210), 9, true, 175, 353, Gap: 0),
        [ControlId.DpadLeft] = new(new Point(153, 276), 9, true, 225, 345.4, Gap: 0),
        [ControlId.DpadDown] = new(new Point(219, 342), 9, true, 275, 362, Gap: 0),
        [ControlId.DpadRight] = new(new Point(285, 276), 9, true, 325, 359.6, Gap: 0),
        // ↑ X Y sit below and climb at the diamond's 73° (RiseAngle). They're a tight vertical stack, so ↑ and X
        // climb past the buttons beneath them, then turn level into their button's left side (both turn at
        // x ≈ 401); Y, with nothing below it, comes straight in. The climbs run parallel, 12px apart, and 12px
        // right of ▶'s.
        // Face buttons are ovals: Shape is the button's outline (traced off the photo), drawn as its glow, and
        // the line stops on that outline. ↑ is a teardrop whose middle sits a little below its aim point.
        [ControlId.Button9] = new(new Point(400, 344), 14, true, 375, 356.3, LandAngle: 180, LevelIn: true,
            Shape: new(new Point(397.8, 358.2), 11.8, 18, 15)),
        [ControlId.Button5] = new(new Point(377, 460), 14, true, 425, 353, LandAngle: 180, LevelIn: true,
            Shape: new(new Point(379.2, 461.7), 15.3, 12.3, -14)),
        [ControlId.Button6] = new(new Point(369, 544), 14, true, 475, 368.4,
            Shape: new(new Point(368.7, 545.1), 14.5, 13.6, -10)),
        // Right-hand diamond: A ends in the same short drop as ▲ (20px across, 10px down, mirrored), landing
        // on its lower-right edge; C, D and B rise steeply.
        [ControlId.Button1] = new(new Point(1033, 180), 13, false, 175, 669.2, LandAngle: 63.9,
            Shape: new(new Point(1031.4, 178.8), 10, 12, 8)),
        [ControlId.Button3] = new(new Point(1100, 240), 13, false, 225, 676,
            Shape: new(new Point(1104, 237), 14, 9.5, 10)),
        [ControlId.Button4] = new(new Point(1047, 291), 13, false, 275, 664,
            Shape: new(new Point(1046.4, 291), 11.3, 13.6, -12)),
        [ControlId.Button2] = new(new Point(963, 240), 13, false, 325, 660,
            Shape: new(new Point(965.4, 238.8), 14.8, 10, -10)),
    };

    /// <summary>The ball joint, for the "Mouse" callout.</summary>
    private static readonly Spot Ball = new(new Point(816, 460), 26, false, 425, 640);

    /// <summary>The climb angle for LevelIn lines: the right-hand diamond's (C and B come in at about 73°).</summary>
    private const double RiseAngle = 73;

    /// <summary>A button's outline: centre (source-photo pixels), radii (canvas pixels), clockwise tilt in degrees.</summary>
    private sealed record Oval(Point Photo, double RadiusX, double RadiusY, double Angle)
    {
        /// <summary>How far a ray from <paramref name="from"/> (canvas) at <paramref name="rad"/> travels before leaving the oval.</summary>
        public double Exit(Point from, double rad)
        {
            // Work in the oval's own frame: centred on it, untilted, then solve the ray against x²/a² + y²/b² = 1.
            var c = ToCanvas(Photo);
            var tilt = Angle * Math.PI / 180;
            var (cos, sin) = (Math.Cos(tilt), Math.Sin(tilt));
            double ox = from.X - c.X, oy = from.Y - c.Y, dx = Math.Cos(rad), dy = Math.Sin(rad);
            (ox, oy) = (ox * cos + oy * sin, -ox * sin + oy * cos);
            (dx, dy) = (dx * cos + dy * sin, -dx * sin + dy * cos);
            double a2 = RadiusX * RadiusX, b2 = RadiusY * RadiusY;
            var qa = dx * dx / a2 + dy * dy / b2;
            var qb = 2 * (ox * dx / a2 + oy * dy / b2);
            var qc = ox * ox / a2 + oy * oy / b2 - 1;
            return (-qb + Math.Sqrt(qb * qb - 4 * qa * qc)) / (2 * qa);
        }
    }

    /// <summary>
    /// LevelIn: after the elbow the line climbs at RiseAngle to the landing's height, then runs level into it.
    /// Shape: the button's outline, used for the glow and for where the line stops.
    /// </summary>
    private sealed record Spot(Point Photo, double Radius, bool Left, double Row, double ElbowX, double? LandAngle = null, double? Gap = null, bool LevelIn = false, Oval? Shape = null)
    {
        /// <summary>
        /// Where the callout line ends: just outside the button's outline (so the dot sits on it), or on the
        /// point itself when Gap is 0.
        /// </summary>
        public Point Landing
        {
            get
            {
                var c = ToCanvas(Photo);
                var rad = LandAngle is { } deg ? deg * Math.PI / 180 : Math.Atan2(Row - c.Y, ElbowX - c.X);
                var gap = Gap ?? (Shape is { } s ? s.Exit(c, rad) + 1 : Radius + 2);
                return new Point(c.X + Math.Cos(rad) * gap, c.Y + Math.Sin(rad) * gap);
            }
        }
    }

    private const double DotSize = 5;

    /// <summary>A dotted callout line plus a small dot where it lands.</summary>
    private static (Polyline Line, Ellipse Dot) CalloutLine(Spot spot, double startX)
    {
        var points = new AvaloniaList<Point> { new(startX, spot.Row), new(spot.ElbowX, spot.Row) };
        if (spot.LevelIn)
        {
            var rise = spot.Row - spot.Landing.Y;
            points.Add(new Point(spot.ElbowX + rise / Math.Tan(RiseAngle * Math.PI / 180), spot.Landing.Y));
        }
        points.Add(spot.Landing);
        var line = new Polyline
        {
            Points = points,
            Stroke = LineBrush,
            StrokeThickness = 1,
            StrokeDashArray = [2, 3],
            Tag = "callout",
            IsHitTestVisible = false,
        };
        var dot = new Ellipse
        {
            Width = DotSize,
            Height = DotSize,
            Fill = Brushes.White,
            Tag = "callout",
            IsHitTestVisible = false,
        };
        Place(dot, spot.Landing.X - DotSize / 2, spot.Landing.Y - DotSize / 2);
        return (line, dot);
    }

    private readonly Canvas _canvas = new() { Width = DesignWidth, Height = DesignHeight };
    private readonly Dictionary<ControlId, Ellipse> _glows = [];
    private readonly Dictionary<ControlId, (Button Chip, Polyline Line, Ellipse Dot)> _callouts = [];
    private MainViewModel? _vm;

    public ControllerCanvas()
    {
        Content = _canvas;
        DrawController();
    }

    private static Point ToCanvas(Point photo) =>
        new(PhotoLeft + photo.X * PhotoSize / PhotoSourceSize, PhotoTop + photo.Y * PhotoSize / PhotoSourceSize);

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null)
            foreach (var c in _vm.Callouts)
                c.PropertyChanged -= OnCalloutChanged;

        _vm = DataContext as MainViewModel;
        _canvas.Children.RemoveAll(_canvas.Children.Where(c => c.Tag is "callout").ToList());
        _callouts.Clear();

        if (_vm is null)
            return;
        foreach (var callout in _vm.Callouts)
        {
            AddCallout(callout);
            callout.PropertyChanged += OnCalloutChanged;
        }
        AddHandleCallout();
    }

    private void OnCalloutChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not CalloutViewModel c || !_callouts.TryGetValue(c.Id, out var ui))
            return;
        if (e.PropertyName is not (nameof(CalloutViewModel.IsPressed) or nameof(CalloutViewModel.IsSelected)))
            return;

        ui.Chip.Classes.Set("live", c.IsPressed);
        ui.Chip.Classes.Set("selected", c.IsSelected);
        ui.Line.Stroke = c.IsPressed || c.IsSelected ? LineActive : LineBrush;
        ui.Dot.Fill = c.IsPressed || c.IsSelected ? LineActive : Brushes.White;
        if (_glows.TryGetValue(c.Id, out var glow))
            glow.Opacity = c.IsPressed ? 1 : c.IsSelected ? 0.45 : 0;
    }

    private void AddCallout(CalloutViewModel callout)
    {
        if (!Layout.TryGetValue(callout.Id, out var spot))
            return;

        var chipX = spot.Left ? LeftChipX : RightChipX;
        var (line, dot) = CalloutLine(spot, spot.Left ? chipX + ChipSize / 2 + 4 : chipX - ChipSize / 2 - 4);
        // Lines and dots go above the photo but below the glows and chips.
        _canvas.Children.Insert(1, line);
        _canvas.Children.Insert(2, dot);

        var chip = new Button
        {
            Content = callout.Chip,
            Width = ChipSize,
            Height = ChipSize,
            Command = _vm!.SelectCommand,
            CommandParameter = callout,
            Tag = "callout",
        };
        chip.Classes.Add("chip");
        // The triggers are buttons on the back; square chips set them apart from the face buttons.
        if (callout.Id is ControlId.Button7 or ControlId.Button8)
            chip.Classes.Add("square");
        ToolTip.SetTip(chip, callout.ControlName);
        Place(chip, chipX - ChipSize / 2, spot.Row - ChipSize / 2);

        var label = new Button
        {
            Width = 186,
            HorizontalContentAlignment = spot.Left ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Command = _vm.SelectCommand,
            CommandParameter = callout,
            Tag = "callout",
            Content = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        [!TextBlock.TextProperty] = new Binding(nameof(CalloutViewModel.ActionText)) { Source = callout },
                        TextAlignment = spot.Left ? TextAlignment.Right : TextAlignment.Left,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        Classes = { "calloutAction" },
                    },
                    new TextBlock
                    {
                        Text = callout.ControlName,
                        TextAlignment = spot.Left ? TextAlignment.Right : TextAlignment.Left,
                        Classes = { "calloutControl" },
                    },
                },
            },
        };
        label.Classes.Add("calloutLabel");
        Place(label, spot.Left ? chipX - ChipSize / 2 - 196 : chipX + ChipSize / 2 + 10, spot.Row - 19);

        _canvas.Children.Add(chip);
        _canvas.Children.Add(label);
        _callouts[callout.Id] = (chip, line, dot);
    }

    /// <summary>Handle callout: live handle position, opens the settings sheet.</summary>
    private void AddHandleCallout()
    {
        var row = Ball.Row;
        var (ballLine, ballDot) = CalloutLine(Ball, RightChipX - 30);
        _canvas.Children.Insert(1, ballLine);
        _canvas.Children.Insert(2, ballDot);

        var pad = new HandlePad
        {
            Width = 52,
            Height = 52,
            [!HandlePad.XProperty] = new Binding(nameof(MainViewModel.HandleX)),
            [!HandlePad.YProperty] = new Binding(nameof(MainViewModel.HandleY)),
            [!HandlePad.AimZoneProperty] = new Binding(nameof(MainViewModel.AimZone)),
            [!HandlePad.ShowZoneProperty] = new Binding(nameof(MainViewModel.IsHybrid)),
            [!HandlePad.ActiveProperty] = new Binding(nameof(MainViewModel.MouseEnabled)),
            IsHitTestVisible = false,
        };
        var button = new Button
        {
            Content = pad,
            Command = _vm!.OpenMouseSettingsCommand,
            Tag = "callout",
            Padding = new Thickness(0),
        };
        button.Classes.Add("padButton");
        ToolTip.SetTip(button, "Where the handle is. Click to tune the mouse.");
        Place(button, RightChipX - 26, row - 26);

        var label = new Button
        {
            Width = 186,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Command = _vm.OpenMouseSettingsCommand,
            Tag = "callout",
            Content = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "Mouse", Classes = { "calloutAction" } },
                    new TextBlock { Text = "Handle (ball joint)", Classes = { "calloutControl" } },
                },
            },
        };
        label.Classes.Add("calloutLabel");
        Place(label, RightChipX + ChipSize / 2 + 10, row - 19);

        _canvas.Children.Add(button);
        _canvas.Children.Add(label);
    }

    private void DrawController()
    {
        var photo = new Image
        {
            Source = new Bitmap(AssetLoader.Open(new Uri("avares://DualStrikeMapper/Assets/dualstrike.png"))),
            Width = PhotoSize,
            Height = PhotoSize,
            IsHitTestVisible = false,
            // Faint accent glow so the black controller stands out from the dark panel.
            Effect = new DropShadowEffect { Color = Accent, BlurRadius = 26, OffsetX = 0, OffsetY = 0, Opacity = 0.35 },
        };
        Place(photo, PhotoLeft, PhotoTop);
        _canvas.Children.Add(photo);

        foreach (var (id, spot) in Layout)
        {
            // Buttons with a traced outline glow in that shape; the rest get a circle.
            var shape = spot.Shape ?? new Oval(spot.Photo, spot.Radius, spot.Radius, 0);
            var center = ToCanvas(shape.Photo);
            var glow = new Ellipse
            {
                Width = shape.RadiusX * 2,
                Height = shape.RadiusY * 2,
                RenderTransform = new RotateTransform(shape.Angle),
                Fill = new SolidColorBrush(Accent, 0.45),
                Stroke = new SolidColorBrush(Accent),
                StrokeThickness = 2,
                Opacity = 0,
                IsHitTestVisible = false,
                Effect = new DropShadowEffect { Color = Accent, BlurRadius = 14, OffsetX = 0, OffsetY = 0, Opacity = 0.9 },
            };
            Place(glow, center.X - shape.RadiusX, center.Y - shape.RadiusY);
            _canvas.Children.Add(glow);
            _glows[id] = glow;
        }
    }

    private static void Place(Control control, double x, double y)
    {
        Canvas.SetLeft(control, x);
        Canvas.SetTop(control, y);
    }
}
