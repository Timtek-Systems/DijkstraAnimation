using System.Windows;
using System.Windows.Media;

namespace DijkstraAnimation.App;

public partial class MainWindow : Window
{
    private Graph? _graph;
    private int _startNode;
    private int _endNode;
    private List<AnimationStep>? _steps;
    private int _currentStep;
    private double _stepAccumulator;
    private bool _isAnimating;
    private bool _algorithmDone;
    private DateTime _lastFrameTime = DateTime.UtcNow;
    private double _fitAllZoom = 1;
    private double _graphCenterX;
    private double _graphCenterY;

    public MainWindow()
    {
        InitializeComponent();

        NodeCountSlider.ValueChanged += (_, _) =>
            NodeCountText.Text = ((int)NodeCountSlider.Value).ToString();
        SpeedSlider.ValueChanged += (_, _) =>
            SpeedText.Text = ((int)SpeedSlider.Value).ToString();

        StartButton.Click += OnStartClicked;
        ResetButton.Click += OnResetClicked;

        CompositionTarget.Rendering += OnFrame;
    }

    private void OnStartClicked(object sender, RoutedEventArgs e)
    {
        int nodeCount = (int)NodeCountSlider.Value;

        _graph = GraphGenerator.Generate(nodeCount);
        (_startNode, _endNode) = GraphGenerator.PickStartEnd(_graph);

        Canvas.SetGraph(_graph, _startNode, _endNode);
        _steps = DijkstraSolver.Solve(_graph, _startNode, _endNode);
        _currentStep = 0;
        _stepAccumulator = 0;
        _isAnimating = true;
        _algorithmDone = false;
        _lastFrameTime = DateTime.UtcNow;

        ComputeGraphBounds();

        if (nodeCount > 30)
        {
            // Zoom in to the start node; the camera will track the algorithm's progress
            double trackZoom = Camera.TrackingZoom(nodeCount, _fitAllZoom);
            var startPos = _graph.Nodes[_startNode];
            Canvas.Camera.SnapTo(startPos.X, startPos.Y, trackZoom);
        }
        else
        {
            Canvas.Camera.SnapTo(_graphCenterX, _graphCenterY, _fitAllZoom);
        }

        StatusText.Text = $"Solving {nodeCount} nodes, {_graph.Edges.Count} edges…";
        StartButton.IsEnabled = false;
    }

    private void OnResetClicked(object sender, RoutedEventArgs e)
    {
        _isAnimating = false;
        _algorithmDone = false;
        _steps = null;
        _graph = null;
        Canvas.Clear();
        Canvas.InvalidateVisual();
        StatusText.Text = "Click Generate & Run to begin";
        StartButton.IsEnabled = true;
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        if (!_isAnimating) return;

        var now = DateTime.UtcNow;
        double dt = Math.Min((now - _lastFrameTime).TotalSeconds, 0.1);
        _lastFrameTime = now;

        Canvas.Camera.Update(dt);

        if (!_algorithmDone && _steps is not null)
        {
            _stepAccumulator += dt * SpeedSlider.Value;

            int applied = 0;
            while (_stepAccumulator >= 1 && _currentStep < _steps.Count && applied < 60)
            {
                ApplyStep(_steps[_currentStep]);
                _currentStep++;
                _stepAccumulator -= 1;
                applied++;
            }
        }

        Canvas.SetPulsePhase(DateTime.UtcNow.TimeOfDay.TotalSeconds * 5);
        Canvas.InvalidateVisual();
    }

    private void ApplyStep(AnimationStep step)
    {
        switch (step)
        {
            case VisitNodeStep visit:
                Canvas.SetNodeState(visit.NodeId, NodeVisualState.Current);

                // Smoothly track the camera to the current node when zoomed in
                if (_graph!.Nodes.Count > 30)
                {
                    var node = _graph.Nodes[visit.NodeId];
                    double trackZoom = Camera.TrackingZoom(_graph.Nodes.Count, _fitAllZoom);
                    Canvas.Camera.SetTarget(node.X, node.Y, trackZoom);
                }
                break;

            case ExamineEdgeStep examine:
                Canvas.SetEdgeState(examine.EdgeIndex, EdgeVisualState.Examining);
                if (examine.Improved)
                    Canvas.SetNodeState(examine.ToNode, NodeVisualState.InQueue);
                break;

            case SettleNodeStep settle:
                Canvas.SetNodeState(settle.NodeId, NodeVisualState.Visited);

                // Reset all "examining" edges from this node back to "relaxed"
                foreach (var (_, edgeIdx) in _graph!.GetNeighbors(settle.NodeId))
                {
                    if (Canvas.GetEdgeState(edgeIdx) == EdgeVisualState.Examining)
                        Canvas.SetEdgeState(edgeIdx, EdgeVisualState.Relaxed);
                }
                break;

            case ShowPathStep path:
                foreach (var nodeId in path.PathNodeIds)
                    Canvas.SetNodeState(nodeId, NodeVisualState.OnPath);
                foreach (var edgeIdx in path.PathEdgeIndices)
                    Canvas.SetEdgeState(edgeIdx, EdgeVisualState.OnPath);
                break;

            case AlgorithmDoneStep done:
                _algorithmDone = true;

                // Zoom out to show the entire graph with the highlighted path
                Canvas.Camera.SetTarget(_graphCenterX, _graphCenterY, _fitAllZoom);

                StatusText.Text = done.PathFound
                    ? $"✓ Shortest path found ({_steps!.Count} steps)"
                    : "✗ No path exists";
                StartButton.IsEnabled = true;
                break;
        }
    }

    private void ComputeGraphBounds()
    {
        if (_graph is null or { Nodes.Count: 0 }) return;

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var node in _graph.Nodes)
        {
            if (node.X < minX) minX = node.X;
            if (node.Y < minY) minY = node.Y;
            if (node.X > maxX) maxX = node.X;
            if (node.Y > maxY) maxY = node.Y;
        }

        _graphCenterX = (minX + maxX) / 2;
        _graphCenterY = (minY + maxY) / 2;

        double graphW = maxX - minX + 100;
        double graphH = maxY - minY + 100;
        double viewW = Math.Max(Canvas.ActualWidth, 400);
        double viewH = Math.Max(Canvas.ActualHeight, 300);

        _fitAllZoom = Math.Min(viewW / graphW, viewH / graphH);
    }
}