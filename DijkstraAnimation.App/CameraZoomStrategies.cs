namespace DijkstraAnimation.App;

/// <summary>
/// Keeps a fixed world-space radius around the currently active node in view, regardless
/// of what else is happening in the graph. Simple and predictable: the zoom level only
/// depends on the viewport size and <see cref="Radius"/>.
/// </summary>
public sealed class FixedRadiusZoom : IZoomStrategy
{
    private const double DefaultRadius = 150.0;

    public string Name => "Fixed Radius";

    /// <summary>The world-space radius around the active node that should remain visible.</summary>
    public double Radius { get; set; } = DefaultRadius;

    public CameraFrame ComputeTarget(ZoomContext context)
    {
        var node = context.Graph.Nodes[context.CurrentNodeId];
        double zoom = Math.Min(context.ViewportWidth, context.ViewportHeight) / (2 * Radius);
        return new CameraFrame(node.X, node.Y, zoom);
    }
}

/// <summary>
/// Frames the view so that every currently frontier ("in queue") node, along with the
/// active node, stays in view - the camera zooms out as the frontier spreads and back in
/// as it narrows.
/// </summary>
public sealed class FrontierZoom : IZoomStrategy
{
    private const double DefaultPadding = 60.0;
    private const double DefaultMinRadius = 80.0;

    public string Name => "Frontier Nodes";

    /// <summary>Extra world-space margin added around the frontier's bounding box.</summary>
    public double Padding { get; set; } = DefaultPadding;

    /// <summary>
    /// Minimum radius enforced around the active node even when the frontier is empty or
    /// tightly clustered, so the camera doesn't zoom in to an unreasonable extreme.
    /// </summary>
    public double MinRadius { get; set; } = DefaultMinRadius;

    public CameraFrame ComputeTarget(ZoomContext context)
    {
        var nodes = context.Graph.Nodes;
        var current = nodes[context.CurrentNodeId];

        double minX = current.X, maxX = current.X;
        double minY = current.Y, maxY = current.Y;

        foreach (var id in context.FrontierNodeIds)
        {
            var n = nodes[id];
            if (n.X < minX) minX = n.X;
            if (n.X > maxX) maxX = n.X;
            if (n.Y < minY) minY = n.Y;
            if (n.Y > maxY) maxY = n.Y;
        }

        double width = Math.Max(maxX - minX, MinRadius * 2) + Padding * 2;
        double height = Math.Max(maxY - minY, MinRadius * 2) + Padding * 2;

        double centerX = (minX + maxX) / 2;
        double centerY = (minY + maxY) / 2;

        double zoom = Math.Min(context.ViewportWidth / width, context.ViewportHeight / height);
        return new CameraFrame(centerX, centerY, zoom);
    }
}
