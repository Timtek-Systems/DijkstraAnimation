namespace DijkstraAnimation.App;

/// <summary>Smooth-interpolating 2D camera for the graph viewport.</summary>
public sealed class Camera
{
    private const double SmoothSpeed = 4.0;

    public double CenterX { get; private set; }
    public double CenterY { get; private set; }
    public double Zoom { get; private set; } = 1;

    private double _targetX, _targetY, _targetZoom = 1;

    /// <summary>Sets the camera target for smooth interpolation.</summary>
    public void SetTarget(double x, double y, double zoom)
    {
        _targetX = x;
        _targetY = y;
        _targetZoom = Math.Max(zoom, 0.01);
    }

    /// <summary>Immediately positions the camera with no interpolation.</summary>
    public void SnapTo(double x, double y, double zoom)
    {
        CenterX = _targetX = x;
        CenterY = _targetY = y;
        Zoom = _targetZoom = Math.Max(zoom, 0.01);
    }

    /// <summary>Advances the camera toward its target using exponential smoothing.</summary>
    public void Update(double deltaTime)
    {
        double t = 1 - Math.Exp(-SmoothSpeed * deltaTime);
        CenterX += (_targetX - CenterX) * t;
        CenterY += (_targetY - CenterY) * t;
        Zoom += (_targetZoom - Zoom) * t;
    }

    /// <summary>Converts a world-space point to screen-space.</summary>
    public (double X, double Y) WorldToScreen(double worldX, double worldY, double viewWidth, double viewHeight)
    {
        return (
            (worldX - CenterX) * Zoom + viewWidth / 2,
            (worldY - CenterY) * Zoom + viewHeight / 2
        );
    }

    /// <summary>Calculates an appropriate tracking zoom as a multiple of the fit-all zoom.</summary>
    public static double TrackingZoom(int nodeCount, double fitAllZoom)
    {
        double mult = Math.Clamp(Math.Sqrt(nodeCount / 25.0), 1, 8);
        return fitAllZoom * mult;
    }
}
