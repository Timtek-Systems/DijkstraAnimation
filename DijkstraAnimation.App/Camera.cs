namespace DijkstraAnimation.App;

/// <summary>
/// 2D camera for the graph viewport. Positional easing is delegated to a pluggable
/// <see cref="ICameraMotion"/> strategy, which can be swapped at any time - including
/// mid-animation - via <see cref="Motion"/>. Zoom is eased independently by a
/// critically-damped spring (see <see cref="ZoomGravity"/>), which produces a smooth
/// ease-in/ease-out curve regardless of which position motion is active.
/// </summary>
public sealed class Camera
{
    private const double DefaultZoomGravity = 6.0;

    public double CenterX { get; internal set; }
    public double CenterY { get; internal set; }
    public double Zoom { get; internal set; } = 1;

    public double TargetX { get; private set; }
    public double TargetY { get; private set; }
    public double TargetZoom { get; private set; } = 1;

    /// <summary>The currently active motion algorithm. Can be reassigned at any time, including mid-animation.</summary>
    public ICameraMotion Motion { get; set; } = new GravityDampedMotion();

    /// <summary>
    /// Constant of proportionality controlling how quickly zoom eases toward its target.
    /// Zoom always uses a critically-damped spring (independent of <see cref="Motion"/>),
    /// which ramps up smoothly (ease-in) then decelerates into the target with no
    /// overshoot (ease-out).
    /// </summary>
    public double ZoomGravity { get; set; } = DefaultZoomGravity;

    private double _zoomVelocity;

    /// <summary>Sets the camera target for smooth interpolation.</summary>
    public void SetTarget(double x, double y, double zoom)
    {
        TargetX = x;
        TargetY = y;
        TargetZoom = Math.Max(zoom, 0.01);
    }

    /// <summary>Immediately positions the camera with no interpolation.</summary>
    public void SnapTo(double x, double y, double zoom)
    {
        CenterX = TargetX = x;
        CenterY = TargetY = y;
        Zoom = TargetZoom = Math.Max(zoom, 0.01);
        _zoomVelocity = 0;
        Motion.Reset();
    }

    /// <summary>
    /// Advances the camera toward its target: position via the current <see cref="Motion"/>
    /// algorithm, zoom via an independent critically-damped ease-in/ease-out spring.
    /// </summary>
    public void Update(double deltaTime)
    {
        Motion.Update(this, deltaTime);

        double zoomDamping = 2 * Math.Sqrt(ZoomGravity);
        double dz = TargetZoom - Zoom;
        _zoomVelocity += (dz * ZoomGravity - _zoomVelocity * zoomDamping) * deltaTime;
        Zoom += _zoomVelocity * deltaTime;
    }

    /// <summary>Converts a world-space point to screen-space.</summary>
    public (double X, double Y) WorldToScreen(double worldX, double worldY, double viewWidth, double viewHeight)
    {
        return (
            (worldX - CenterX) * Zoom + viewWidth / 2,
            (worldY - CenterY) * Zoom + viewHeight / 2
        );
    }
}
