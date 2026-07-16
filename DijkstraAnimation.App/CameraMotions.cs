namespace DijkstraAnimation.App;

/// <summary>
/// Classic exponential-smoothing ("lerp toward target") camera motion. Each frame the
/// camera moves a fraction of the remaining distance, controlled by <see cref="Speed"/>.
/// Simple and always stable, but has no persistent velocity/inertia.
/// </summary>
public sealed class ExponentialMotion : ICameraMotion
{
    private const double DefaultSpeed = 4.0;

    public string Name => "Exponential";

    /// <summary>Controls how quickly the camera eases toward its target. Higher values track more tightly.</summary>
    public double Speed { get; set; } = DefaultSpeed;

    public void Reset()
    {
        // Stateless: nothing to reset.
    }

    public void Update(Camera camera, double deltaTime)
    {
        double t = 1 - Math.Exp(-Speed * deltaTime);
        camera.CenterX += (camera.TargetX - camera.CenterX) * t;
        camera.CenterY += (camera.TargetY - camera.CenterY) * t;
    }
}

/// <summary>
/// Damped-spring ("gravity") camera motion: acceleration is proportional to the distance
/// from the target, opposed by a velocity-proportional drag term. With
/// <see cref="Damping"/> set to <see cref="CriticalDamping"/> (the default) the camera
/// glides to a stop with no overshoot or orbiting; lower damping values allow springy,
/// oscillating motion.
/// </summary>
public sealed class GravityDampedMotion : ICameraMotion
{
    private const double DefaultGravity = 4.0;
    private const double DefaultMaxVelocity = 2000.0;

    public string Name => "Gravity";

    /// <summary>
    /// Constant of proportionality between the camera-to-target distance and the
    /// gravitational acceleration pulling the camera toward the target.
    /// </summary>
    public double Gravity { get; set; } = DefaultGravity;

    /// <summary>Velocity-proportional drag ("friction") applied opposite to the camera's motion.</summary>
    public double Damping { get; set; }

    /// <summary>The maximum speed (world units/second) the camera may move at.</summary>
    public double MaxVelocity { get; set; } = DefaultMaxVelocity;

    /// <summary>The damping value that makes the spring critically damped for the current <see cref="Gravity"/>.</summary>
    public double CriticalDamping => 2 * Math.Sqrt(Gravity);

    private double _velX, _velY;

    public GravityDampedMotion()
    {
        Damping = CriticalDamping;
    }

    public void Reset()
    {
        _velX = 0;
        _velY = 0;
    }

    public void Update(Camera camera, double deltaTime)
    {
        double dx = camera.TargetX - camera.CenterX;
        double dy = camera.TargetY - camera.CenterY;

        _velX += (dx * Gravity - _velX * Damping) * deltaTime;
        _velY += (dy * Gravity - _velY * Damping) * deltaTime;

        double speed = Math.Sqrt(_velX * _velX + _velY * _velY);
        if (speed > MaxVelocity && speed > 0)
        {
            double scale = MaxVelocity / speed;
            _velX *= scale;
            _velY *= scale;
        }

        camera.CenterX += _velX * deltaTime;
        camera.CenterY += _velY * deltaTime;
    }
}

/// <summary>
/// Camera motion modeled closely on Gource's own technique:
/// persistent velocity/inertia, the camera's speed each frame is recomputed directly as
/// proportional to its current distance from the target (<see cref="PanSpeed"/>), then
/// clamped to <see cref="MaxSpeed"/> world units/second. Because velocity is never
/// carried over between frames, the camera can never overshoot or oscillate/orbit around
/// the target - it simply glides toward it, slowing naturally as it gets close, capped at
/// a maximum pixel-like speed for large jumps (e.g. when the tracked node changes).
/// </summary>
public sealed class GourceMotion : ICameraMotion
{
    private const double DefaultPanSpeed = 2.0;
    private const double DefaultMaxSpeed = 1500.0;

    public string Name => "Gource";

    /// <summary>Proportionality constant between distance-to-target and instantaneous pan speed.</summary>
    public double PanSpeed { get; set; } = DefaultPanSpeed;

    /// <summary>Maximum pan speed (world units/second), regardless of distance.</summary>
    public double MaxSpeed { get; set; } = DefaultMaxSpeed;

    public void Reset()
    {
        // Stateless: nothing to reset.
    }

    public void Update(Camera camera, double deltaTime)
    {
        double dx = camera.TargetX - camera.CenterX;
        double dy = camera.TargetY - camera.CenterY;

        double vx = dx * PanSpeed;
        double vy = dy * PanSpeed;

        double speed = Math.Sqrt(vx * vx + vy * vy);
        if (speed > MaxSpeed && speed > 0)
        {
            double scale = MaxSpeed / speed;
            vx *= scale;
            vy *= scale;
        }

        camera.CenterX += vx * deltaTime;
        camera.CenterY += vy * deltaTime;
    }
}
