using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
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

    // Each motion algorithm is instantiated once and keeps its settings/state for the
    // lifetime of the window, so switching between them - even mid-animation - preserves
    // each algorithm's own configuration and resumes smoothly from the camera's position.
    private readonly ExponentialMotion _exponentialMotion = new();
    private readonly GravityDampedMotion _gravityMotion = new();
    private readonly GourceMotion _gourceMotion = new();

    // Zoom strategies are likewise instantiated once so each keeps its own persistent
    // settings when switched between, even mid-animation.
    private readonly FixedRadiusZoom _fixedRadiusZoom = new();
    private readonly FrontierZoom _frontierZoom = new();
    private IZoomStrategy _zoomStrategy = null!;

    // Nodes currently in the priority queue frontier (added when relaxed/improved, removed
    // once dequeued/visited), used by zoom strategies such as "Frontier Nodes".
    private readonly HashSet<int> _frontierNodeIds = [];

    // Pathfinding algorithms are instantiated once and selected by the Algorithm combo box.
    // Switching is only permitted while no run is in progress.
    private readonly DijkstraSolver _dijkstraSolver = new();
    private readonly AStarSolver _aStarSolver = new();
    private readonly BidirectionalDijkstraSolver _bidirectionalDijkstraSolver = new();
    private readonly BellmanFordSolver _bellmanFordSolver = new();
    private readonly JohnsonSolver _johnsonSolver = new();
    private IPathfindingSolver _solver = null!;

    private readonly Stopwatch _runStopwatch = new();

    public MainWindow()
    {
        InitializeComponent();

        NodeCountSlider.ValueChanged += (_, _) =>
            NodeCountText.Text = ((int)NodeCountSlider.Value).ToString();
        SpeedSlider.ValueChanged += (_, _) =>
            SpeedText.Text = ((int)SpeedSlider.Value).ToString();

        ExpSpeedSlider.ValueChanged += (_, _) =>
        {
            ExpSpeedText.Text = ExpSpeedSlider.Value.ToString("0.0");
            _exponentialMotion.Speed = ExpSpeedSlider.Value;
        };
        _exponentialMotion.Speed = ExpSpeedSlider.Value;

        GravitySlider.ValueChanged += (_, _) =>
        {
            GravityText.Text = GravitySlider.Value.ToString("0.0");
            _gravityMotion.Gravity = GravitySlider.Value;
        };
        DampingSlider.ValueChanged += (_, _) =>
        {
            DampingText.Text = DampingSlider.Value.ToString("0.0");
            _gravityMotion.Damping = DampingSlider.Value;
        };
        MaxVelocitySlider.ValueChanged += (_, _) =>
        {
            MaxVelocityText.Text = ((int)MaxVelocitySlider.Value).ToString();
            _gravityMotion.MaxVelocity = MaxVelocitySlider.Value;
        };
        _gravityMotion.Gravity = GravitySlider.Value;
        _gravityMotion.Damping = DampingSlider.Value;
        _gravityMotion.MaxVelocity = MaxVelocitySlider.Value;

        GourcePanSpeedSlider.ValueChanged += (_, _) =>
        {
            GourcePanSpeedText.Text = GourcePanSpeedSlider.Value.ToString("0.0");
            _gourceMotion.PanSpeed = GourcePanSpeedSlider.Value;
        };
        GourceMaxSpeedSlider.ValueChanged += (_, _) =>
        {
            GourceMaxSpeedText.Text = ((int)GourceMaxSpeedSlider.Value).ToString();
            _gourceMotion.MaxSpeed = GourceMaxSpeedSlider.Value;
        };
        _gourceMotion.PanSpeed = GourcePanSpeedSlider.Value;
        _gourceMotion.MaxSpeed = GourceMaxSpeedSlider.Value;

        MotionCombo.SelectionChanged += OnMotionSelectionChanged;
        Canvas.Camera.Motion = _gravityMotion;

        ZoomEaseSlider.ValueChanged += (_, _) =>
        {
            ZoomEaseText.Text = ZoomEaseSlider.Value.ToString("0.0");
            Canvas.Camera.ZoomGravity = ZoomEaseSlider.Value;
        };
        Canvas.Camera.ZoomGravity = ZoomEaseSlider.Value;

        FixedRadiusSlider.ValueChanged += (_, _) =>
        {
            FixedRadiusText.Text = ((int)FixedRadiusSlider.Value).ToString();
            _fixedRadiusZoom.Radius = FixedRadiusSlider.Value;
        };
        _fixedRadiusZoom.Radius = FixedRadiusSlider.Value;

        FrontierPaddingSlider.ValueChanged += (_, _) =>
        {
            FrontierPaddingText.Text = ((int)FrontierPaddingSlider.Value).ToString();
            _frontierZoom.Padding = FrontierPaddingSlider.Value;
        };
        FrontierMinRadiusSlider.ValueChanged += (_, _) =>
        {
            FrontierMinRadiusText.Text = ((int)FrontierMinRadiusSlider.Value).ToString();
            _frontierZoom.MinRadius = FrontierMinRadiusSlider.Value;
        };
        _frontierZoom.Padding = FrontierPaddingSlider.Value;
        _frontierZoom.MinRadius = FrontierMinRadiusSlider.Value;

        ZoomCombo.SelectionChanged += OnZoomSelectionChanged;
        _zoomStrategy = _fixedRadiusZoom;

        AlgorithmCombo.SelectionChanged += OnAlgorithmSelectionChanged;
        _solver = _dijkstraSolver;

        GenerateButton.Click += OnGenerateClicked;
        RunButton.Click += OnRunClicked;
        ResetButton.Click += OnResetClicked;

        CompositionTarget.Rendering += OnFrame;
    }

    private void OnAlgorithmSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _solver = AlgorithmCombo.SelectedIndex switch
        {
            1 => _aStarSolver,
            2 => _bidirectionalDijkstraSolver,
            3 => _bellmanFordSolver,
            4 => _johnsonSolver,
            _ => _dijkstraSolver
        };
    }

    private void OnZoomSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        FixedRadiusPanel.Visibility = Visibility.Collapsed;
        FrontierPanel.Visibility = Visibility.Collapsed;

        _zoomStrategy = ZoomCombo.SelectedIndex switch
        {
            1 => _frontierZoom,
            _ => _fixedRadiusZoom
        };

        switch (ZoomCombo.SelectedIndex)
        {
            case 1:
                FrontierPanel.Visibility = Visibility.Visible;
                break;
            default:
                FixedRadiusPanel.Visibility = Visibility.Visible;
                break;
        }
    }

    private void OnMotionSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ExponentialPanel.Visibility = Visibility.Collapsed;
        GravityPanel.Visibility = Visibility.Collapsed;
        GourcePanel.Visibility = Visibility.Collapsed;

        Canvas.Camera.Motion = MotionCombo.SelectedIndex switch
        {
            0 => _exponentialMotion,
            2 => _gourceMotion,
            _ => _gravityMotion
        };

        switch (MotionCombo.SelectedIndex)
        {
            case 0:
                ExponentialPanel.Visibility = Visibility.Visible;
                break;
            case 2:
                GourcePanel.Visibility = Visibility.Visible;
                break;
            default:
                GravityPanel.Visibility = Visibility.Visible;
                break;
        }
    }

    private void OnGenerateClicked(object sender, RoutedEventArgs e)
    {
        int nodeCount = (int)NodeCountSlider.Value;

        _isAnimating = false;
        _algorithmDone = false;
        _steps = null;
        _currentStep = 0;
        _stepAccumulator = 0;

        _graph = GraphGenerator.Generate(nodeCount);
        (_startNode, _endNode) = GraphGenerator.PickStartEnd(_graph);

        Canvas.SetGraph(_graph, _startNode, _endNode);
        Canvas.SetNodeDistance(_startNode, 0);

        ComputeGraphBounds();
        Canvas.Camera.SnapTo(_graphCenterX, _graphCenterY, _fitAllZoom);

        StatusText.Text = $"Generated {nodeCount} nodes, {_graph.Edges.Count} edges — click Run when ready";
        RunButton.IsEnabled = true;
        AlgorithmCombo.IsEnabled = true;
        ElapsedTimeText.Text = "—";
        Canvas.InvalidateVisual();
    }

    private void OnRunClicked(object sender, RoutedEventArgs e)
    {
        if (_graph is null) return;

        int nodeCount = _graph.Nodes.Count;

        // Reset visual state in case this graph was already run once before
        Canvas.SetGraph(_graph, _startNode, _endNode);
        Canvas.SetNodeDistance(_startNode, 0);

        _runStopwatch.Restart();
        _steps = _solver.Solve(_graph, _startNode, _endNode);
        _runStopwatch.Stop();

        _currentStep = 0;
        _stepAccumulator = 0;
        _isAnimating = true;
        _algorithmDone = false;
        _lastFrameTime = DateTime.UtcNow;

        _frontierNodeIds.Clear();
        _frontierNodeIds.Add(_startNode);

        if (nodeCount > 30)
        {
            // Zoom in to the start node using the active zoom strategy; the camera will
            // track the algorithm's progress as steps are applied.
            var frame = ComputeZoomFrame(_startNode);
            Canvas.Camera.SnapTo(frame.X, frame.Y, frame.Zoom);
        }
        else
        {
            Canvas.Camera.SnapTo(_graphCenterX, _graphCenterY, _fitAllZoom);
        }

        StatusText.Text = $"Solving {nodeCount} nodes, {_graph.Edges.Count} edges with {_solver.Name}…";
        ElapsedTimeText.Text = $"{_runStopwatch.Elapsed.TotalMilliseconds:0.00} ms";
        RunButton.IsEnabled = false;

        // The algorithm cannot be switched while a run's animation is in progress.
        AlgorithmCombo.IsEnabled = false;
    }

    private void OnResetClicked(object sender, RoutedEventArgs e)
    {
        _isAnimating = false;
        _algorithmDone = false;
        _steps = null;
        _graph = null;
        Canvas.Clear();
        Canvas.InvalidateVisual();
        StatusText.Text = "Click Generate to create a graph";
        RunButton.IsEnabled = false;
        AlgorithmCombo.IsEnabled = true;
        ElapsedTimeText.Text = "—";
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
                Canvas.SetNodeDistance(visit.NodeId, visit.Distance);
                _frontierNodeIds.Add(visit.NodeId);

                // Smoothly track the camera to the current node using the active zoom strategy.
                if (_graph!.Nodes.Count > 30)
                {
                    var frame = ComputeZoomFrame(visit.NodeId);
                    Canvas.Camera.SetTarget(frame.X, frame.Y, frame.Zoom);
                }
                break;

            case ExamineEdgeStep examine:
                Canvas.SetEdgeState(examine.EdgeIndex, EdgeVisualState.Examining);
                if (examine.Improved)
                {
                    Canvas.SetNodeState(examine.ToNode, NodeVisualState.InQueue);
                    Canvas.SetNodeDistance(examine.ToNode, examine.NewDistance);
                    _frontierNodeIds.Add(examine.ToNode);
                }
                break;

            case SettleNodeStep settle:
                Canvas.SetNodeState(settle.NodeId, NodeVisualState.Visited);
                _frontierNodeIds.Remove(settle.NodeId);

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

                // Regardless of the active zoom strategy, always end by zooming out to
                // show the entire graph with the highlighted path.
                Canvas.Camera.SetTarget(_graphCenterX, _graphCenterY, _fitAllZoom);

                StatusText.Text = done.PathFound
                    ? $"✓ Shortest path found ({_steps!.Count} steps) — {_solver.Name} in {_runStopwatch.Elapsed.TotalMilliseconds:0.00} ms"
                    : $"✗ No path exists — {_solver.Name} in {_runStopwatch.Elapsed.TotalMilliseconds:0.00} ms";
                RunButton.IsEnabled = true;
                AlgorithmCombo.IsEnabled = true;
                break;
        }
    }

    private CameraFrame ComputeZoomFrame(int currentNodeId)
    {
        double viewW = Math.Max(Canvas.ActualWidth, 400);
        double viewH = Math.Max(Canvas.ActualHeight, 300);

        var context = new ZoomContext
        {
            Graph = _graph!,
            CurrentNodeId = currentNodeId,
            FrontierNodeIds = _frontierNodeIds,
            ViewportWidth = viewW,
            ViewportHeight = viewH
        };
        return _zoomStrategy.ComputeTarget(context);
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