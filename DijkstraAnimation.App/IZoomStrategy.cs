namespace DijkstraAnimation.App;

/// <summary>A camera framing: where to center the view and how zoomed in to be.</summary>
public readonly record struct CameraFrame(double X, double Y, double Zoom);

/// <summary>Snapshot of algorithm/viewport state a zoom strategy needs to compute a framing.</summary>
public sealed class ZoomContext
{
    public required Graph Graph { get; init; }
    public required int CurrentNodeId { get; init; }
    public required IReadOnlyCollection<int> FrontierNodeIds { get; init; }
    public required double ViewportWidth { get; init; }
    public required double ViewportHeight { get; init; }
}

/// <summary>
/// A pluggable zoom/framing algorithm used while the algorithm is running. Like
/// <see cref="ICameraMotion"/>, each implementation owns its own persistent settings, so
/// switching between strategies at any time - including mid-animation - preserves each
/// strategy's own configuration. Regardless of the active strategy, the camera is always
/// zoomed to fit the whole graph once the computation finishes.
/// </summary>
public interface IZoomStrategy
{
    /// <summary>A short, user-facing name for this zoom strategy.</summary>
    string Name { get; }

    /// <summary>Computes the desired camera framing for the current algorithm state.</summary>
    CameraFrame ComputeTarget(ZoomContext context);
}
