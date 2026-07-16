using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace DijkstraAnimation.App;

public enum NodeVisualState { Unvisited, InQueue, Visited, Current, Start, End, OnPath }
public enum EdgeVisualState { Default, Examining, Relaxed, OnPath }

/// <summary>
/// GPU-accelerated graph renderer using WPF's DirectX-backed DrawingContext.
/// All geometry is drawn in world space via a single PushTransform call,
/// so the GPU handles the camera matrix multiplication.
/// </summary>
public sealed class GraphCanvas : FrameworkElement
{
    private Graph? _graph;
    private NodeVisualState[]? _nodeStates;
    private EdgeVisualState[]? _edgeStates;
    private double[]? _nodeDistances;
    private int _startNode = -1;
    private int _endNode = -1;
    private double _pulsePhase;

    // Reusable transform to avoid per-frame allocations
    private readonly MatrixTransform _cameraTransform = new();

    // Frozen brushes and pens: eliminates per-frame GC pressure, enables GPU resource caching.
    // Pen thicknesses are in world units and scale naturally with the camera transform.
    private static readonly Brush s_background = Freeze(new SolidColorBrush(Color.FromRgb(24, 24, 37)));

    private static readonly Pen s_edgeDefault = FreezePen(Color.FromArgb(80, 80, 80, 130), 1.0);
    private static readonly Pen s_edgeExamining = FreezePen(Color.FromRgb(255, 180, 50), 2.0);
    private static readonly Pen s_edgeRelaxed = FreezePen(Color.FromArgb(140, 74, 144, 217), 1.2);
    private static readonly Pen s_edgePath = FreezePen(Color.FromRgb(255, 215, 0), 3.0);

    private static readonly Brush s_unvisited = Freeze(new SolidColorBrush(Color.FromRgb(70, 70, 100)));
    private static readonly Brush s_inQueue = Freeze(new SolidColorBrush(Color.FromRgb(255, 165, 0)));
    private static readonly Brush s_visited = Freeze(new SolidColorBrush(Color.FromRgb(74, 144, 217)));
    private static readonly Brush s_current = Freeze(new SolidColorBrush(Colors.White));
    private static readonly Brush s_start = Freeze(new SolidColorBrush(Color.FromRgb(0, 204, 102)));
    private static readonly Brush s_end = Freeze(new SolidColorBrush(Color.FromRgb(255, 68, 68)));
    private static readonly Brush s_path = Freeze(new SolidColorBrush(Color.FromRgb(255, 215, 0)));

    private static readonly Pen s_nodeStroke = FreezePen(Color.FromArgb(50, 255, 255, 255), 0.5);
    private static readonly Pen s_glowPen = FreezePen(Color.FromArgb(60, 255, 255, 255), 1.5);

    private static readonly Brush s_distanceText = Freeze(new SolidColorBrush(Colors.White));
    private static readonly Typeface s_distanceTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    private const double NodeRadius = 16; // world units, enlarged to fit the distance label
    private const double DistanceFontSize = 11;

    public Camera Camera { get; } = new();

    public void SetGraph(Graph graph, int startNode, int endNode)
    {
        _graph = graph;
        _startNode = startNode;
        _endNode = endNode;
        _nodeStates = new NodeVisualState[graph.Nodes.Count];
        _edgeStates = new EdgeVisualState[graph.Edges.Count];
        _nodeDistances = new double[graph.Nodes.Count];
        Array.Fill(_nodeDistances, double.PositiveInfinity);
        _nodeStates[startNode] = NodeVisualState.Start;
        _nodeStates[endNode] = NodeVisualState.End;
    }

    public void SetNodeState(int nodeId, NodeVisualState state)
    {
        if (_nodeStates is not null) _nodeStates[nodeId] = state;
    }

    public void SetNodeDistance(int nodeId, double distance)
    {
        if (_nodeDistances is not null) _nodeDistances[nodeId] = distance;
    }

    public void SetEdgeState(int edgeIndex, EdgeVisualState state)
    {
        if (_edgeStates is not null) _edgeStates[edgeIndex] = state;
    }

    public EdgeVisualState GetEdgeState(int edgeIndex) =>
        _edgeStates is not null ? _edgeStates[edgeIndex] : EdgeVisualState.Default;

    public void SetPulsePhase(double phase) => _pulsePhase = phase;

    public void Clear()
    {
        _graph = null;
        _nodeStates = null;
        _edgeStates = null;
        _nodeDistances = null;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        dc.DrawRectangle(s_background, null, new Rect(0, 0, w, h));

        if (_graph is null || _nodeStates is null || _edgeStates is null)
            return;

        var cam = Camera;

        // Compute the visible region in world coordinates for frustum culling
        double invZoom = 1.0 / cam.Zoom;
        double margin = 30 * invZoom;
        double worldLeft = cam.CenterX - w * 0.5 * invZoom - margin;
        double worldRight = cam.CenterX + w * 0.5 * invZoom + margin;
        double worldTop = cam.CenterY - h * 0.5 * invZoom - margin;
        double worldBottom = cam.CenterY + h * 0.5 * invZoom + margin;

        // Single GPU-composited camera transform: world → screen
        _cameraTransform.Matrix = new Matrix(
            cam.Zoom, 0,
            0, cam.Zoom,
            -cam.CenterX * cam.Zoom + w * 0.5,
            -cam.CenterY * cam.Zoom + h * 0.5);
        dc.PushTransform(_cameraTransform);

        DrawEdges(dc, worldLeft, worldTop, worldRight, worldBottom);
        DrawNodes(dc, worldLeft, worldTop, worldRight, worldBottom);

        dc.Pop();
    }

    private void DrawEdges(DrawingContext dc, double left, double top, double right, double bottom)
    {
        var edges = _graph!.Edges;
        var nodes = _graph.Nodes;

        for (int i = 0; i < edges.Count; i++)
        {
            var edge = edges[i];
            var src = nodes[edge.SourceId];
            var tgt = nodes[edge.TargetId];

            // Frustum cull: skip edges fully outside the visible region
            if (src.X < left && tgt.X < left) continue;
            if (src.Y < top && tgt.Y < top) continue;
            if (src.X > right && tgt.X > right) continue;
            if (src.Y > bottom && tgt.Y > bottom) continue;

            var pen = _edgeStates![i] switch
            {
                EdgeVisualState.Examining => s_edgeExamining,
                EdgeVisualState.Relaxed => s_edgeRelaxed,
                EdgeVisualState.OnPath => s_edgePath,
                _ => s_edgeDefault
            };

            dc.DrawLine(pen, new Point(src.X, src.Y), new Point(tgt.X, tgt.Y));
        }
    }

    private void DrawNodes(DrawingContext dc, double left, double top, double right, double bottom)
    {
        var nodes = _graph!.Nodes;

        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];

            if (node.X < left || node.X > right || node.Y < top || node.Y > bottom)
                continue;

            var state = _nodeStates![i];

            // Preserve start/end coloring unless overridden by path or current
            if (i == _startNode && state is not NodeVisualState.OnPath and not NodeVisualState.Current)
                state = NodeVisualState.Start;
            if (i == _endNode && state is not NodeVisualState.OnPath and not NodeVisualState.Current)
                state = NodeVisualState.End;

            var fill = state switch
            {
                NodeVisualState.InQueue => s_inQueue,
                NodeVisualState.Visited => s_visited,
                NodeVisualState.Current => s_current,
                NodeVisualState.Start => s_start,
                NodeVisualState.End => s_end,
                NodeVisualState.OnPath => s_path,
                _ => s_unvisited
            };

            var center = new Point(node.X, node.Y);

            // Pulsing glow for the current node
            if (state == NodeVisualState.Current)
            {
                double glowR = NodeRadius + 4 + 2 * Math.Sin(_pulsePhase);
                dc.DrawEllipse(null, s_glowPen, center, glowR, glowR);
            }

            dc.DrawEllipse(fill, s_nodeStroke, center, NodeRadius, NodeRadius);

            if (_nodeDistances is not null && double.IsFinite(_nodeDistances[i]))
                DrawDistanceLabel(dc, center, _nodeDistances[i]);
        }
    }

    private static void DrawDistanceLabel(DrawingContext dc, Point center, double distance)
    {
        var formattedText = new FormattedText(
            distance.ToString("0.#", CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            s_distanceTypeface,
            DistanceFontSize,
            s_distanceText,
            1.0);

        var origin = new Point(center.X - formattedText.Width / 2, center.Y - formattedText.Height / 2);
        dc.DrawText(formattedText, origin);
    }

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    private static Pen FreezePen(Color color, double thickness)
    {
        var pen = new Pen(new SolidColorBrush(color), thickness);
        pen.Freeze();
        return pen;
    }
}
