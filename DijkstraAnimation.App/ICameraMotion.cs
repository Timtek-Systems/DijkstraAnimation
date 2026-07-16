namespace DijkstraAnimation.App;

/// <summary>
/// A pluggable camera motion algorithm. Each implementation owns its own persistent
/// settings and internal state (e.g. velocity), so switching between algorithms at
/// runtime - even mid-animation - preserves each algorithm's own configuration and
/// picks up smoothly from the camera's current position.
/// </summary>
public interface ICameraMotion
{
    /// <summary>A short, user-facing name for this motion algorithm.</summary>
    string Name { get; }

    /// <summary>Called when the camera is snapped to a position, so internal state (e.g. velocity) can be cleared.</summary>
    void Reset();

    /// <summary>
    /// Advances <paramref name="camera"/> toward its current target by <paramref name="deltaTime"/> seconds.
    /// </summary>
    void Update(Camera camera, double deltaTime);
}
