using DualStrikeMapper.Core.Mapping;

namespace DualStrikeMapper.Core.Mouse;

/// <summary>
/// Turns handle readings into relative cursor movement.
///
/// Hybrid mode (the handle stays where you leave it):
///   - inside the aim zone the cursor follows handle position,
///   - past the aim zone the cursor keeps moving faster the further you push,
///   - while Lift is held you can re-centre the handle without moving the cursor,
///     and the cursor keeps some speed and glides to a stop.
/// Joystick mode: tilt sets the cursor speed.
/// </summary>
public sealed class MouseEngine
{
    private const double MomentumDecaySeconds = 0.35;

    private double _smoothX, _smoothY;
    private double _lastAimX, _lastAimY;
    private bool _hasLast;
    private double _remX, _remY;
    private double _velX, _velY;
    private double _momX, _momY;
    private bool _wasLifted;

    public void Reset()
    {
        _hasLast = false;
        _remX = _remY = _velX = _velY = _momX = _momY = 0;
        _wasLifted = false;
    }

    public (int Dx, int Dy) Update(float rawX, float rawY, double dt, MouseSettings s, bool lift, bool precision)
    {
        if (dt <= 0)
            return (0, 0);

        var x = s.InvertX ? -rawX : rawX;
        var y = s.InvertY ? -rawY : rawY;

        if (!_hasLast)
        {
            _smoothX = x;
            _smoothY = y;
        }
        else
        {
            // Frame-rate independent exponential smoothing.
            var alpha = 1 - Math.Pow(Math.Clamp(s.Smoothing, 0, 0.95), dt * 100);
            _smoothX += (x - _smoothX) * alpha;
            _smoothY += (y - _smoothY) * alpha;
        }

        var (mx, my) = s.Mode == MouseMode.Hybrid
            ? Hybrid(dt, s, lift)
            : Joystick(dt, s, lift);

        if (precision)
        {
            mx *= s.PrecisionFactor;
            my *= s.PrecisionFactor;
        }

        _remX += mx;
        _remY += my;
        var dx = (int)Math.Truncate(_remX);
        var dy = (int)Math.Truncate(_remY);
        _remX -= dx;
        _remY -= dy;
        return (dx, dy);
    }

    private (double, double) Hybrid(double dt, MouseSettings s, bool lift)
    {
        var edge = Math.Clamp(s.AimZone, 0.3, 0.98);
        var aimX = Math.Clamp(_smoothX, -edge, edge);
        var aimY = Math.Clamp(_smoothY, -edge, edge);

        double mx = 0, my = 0;
        if (_hasLast)
        {
            mx = (aimX - _lastAimX) * s.AimSensitivity;
            my = (aimY - _lastAimY) * s.AimSensitivity;
        }
        _lastAimX = aimX;
        _lastAimY = aimY;
        _hasLast = true;

        mx += SpinVelocity(_smoothX, edge, s.SpinSpeed) * dt;
        my += SpinVelocity(_smoothY, edge, s.SpinSpeed) * dt;

        if (lift)
        {
            if (!_wasLifted)
            {
                _momX = _velX * Math.Clamp(s.Momentum, 0, 1);
                _momY = _velY * Math.Clamp(s.Momentum, 0, 1);
            }
            _wasLifted = true;
            var decay = Math.Exp(-dt / MomentumDecaySeconds);
            _momX *= decay;
            _momY *= decay;
            return (_momX * dt, _momY * dt);
        }

        _wasLifted = false;
        // Remember recent speed so Lift can fling the cursor.
        _velX += (mx / dt - _velX) * 0.25;
        _velY += (my / dt - _velY) * 0.25;
        return (mx, my);
    }

    private (double, double) Joystick(double dt, MouseSettings s, bool lift)
    {
        _hasLast = true;
        if (lift)
            return (0, 0);
        return (StickVelocity(_smoothX, s) * dt, StickVelocity(_smoothY, s) * dt);
    }

    private static double SpinVelocity(double value, double edge, double speed)
    {
        var over = Math.Abs(value) - edge;
        if (over <= 0)
            return 0;
        return Math.Sign(value) * Math.Min(over / (1 - edge), 1) * speed;
    }

    private static double StickVelocity(double value, MouseSettings s)
    {
        var dz = Math.Clamp(s.Deadzone, 0, 0.9);
        var mag = Math.Abs(value);
        if (mag <= dz)
            return 0;
        var t = Math.Min((mag - dz) / (1 - dz), 1);
        return Math.Sign(value) * Math.Pow(t, Math.Max(s.Curve, 0.2)) * s.SpinSpeed;
    }
}
