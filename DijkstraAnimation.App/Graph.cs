namespace DijkstraAnimation.App;

/// <summary>A node in the graph at a fixed position in world space.</summary>
public sealed record GraphNode(int Id, double X, double Y);

/// <summary>A weighted undirected edge.</summary>
public sealed record GraphEdge(int SourceId, int TargetId, double Weight);

/// <summary>An undirected weighted graph with spatial node positions.</summary>
public sealed class Graph
{
    private readonly Dictionary<int, List<(int Neighbor, int EdgeIndex)>> _adjacency;

    public IReadOnlyList<GraphNode> Nodes { get; }
    public IReadOnlyList<GraphEdge> Edges { get; }

    public Graph(IReadOnlyList<GraphNode> nodes, IReadOnlyList<GraphEdge> edges)
    {
        Nodes = nodes;
        Edges = edges;
        _adjacency = [];
        foreach (var node in nodes)
            _adjacency[node.Id] = [];
        for (int i = 0; i < edges.Count; i++)
        {
            _adjacency[edges[i].SourceId].Add((edges[i].TargetId, i));
            _adjacency[edges[i].TargetId].Add((edges[i].SourceId, i));
        }
    }

    public IReadOnlyList<(int Neighbor, int EdgeIndex)> GetNeighbors(int nodeId) => _adjacency[nodeId];
}

/// <summary>Generates random connected graphs suitable for Dijkstra visualization.</summary>
public static class GraphGenerator
{
    private const double WorldSize = 1000;
    private const double Padding = 50;

    public static Graph Generate(int nodeCount, int seed = -1)
    {
        var rng = seed >= 0 ? new Random(seed) : new Random();
        var nodes = PlaceNodes(nodeCount, rng);
        var edges = ConnectNodes(nodes, nodeCount);
        return new Graph(nodes, edges);
    }

    /// <summary>Picks a random start and the farthest node as end for an interesting path.</summary>
    public static (int Start, int End) PickStartEnd(Graph graph, int seed = -1)
    {
        var rng = seed >= 0 ? new Random(seed) : new Random();
        int start = rng.Next(graph.Nodes.Count);

        double maxDist = 0;
        int end = (start + 1) % graph.Nodes.Count;
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            if (i == start) continue;
            double dx = graph.Nodes[start].X - graph.Nodes[i].X;
            double dy = graph.Nodes[start].Y - graph.Nodes[i].Y;
            double d = dx * dx + dy * dy;
            if (d > maxDist)
            {
                maxDist = d;
                end = i;
            }
        }

        return (start, end);
    }

    private static List<GraphNode> PlaceNodes(int count, Random rng)
    {
        double usable = WorldSize - 2 * Padding;
        double minDist = usable / (Math.Sqrt(count) * 2.2);
        var nodes = new List<GraphNode>(count);

        for (int attempts = 0; nodes.Count < count && attempts < count * 200; attempts++)
        {
            double x = rng.NextDouble() * usable + Padding;
            double y = rng.NextDouble() * usable + Padding;

            bool tooClose = false;
            foreach (var n in nodes)
            {
                double dx = x - n.X, dy = y - n.Y;
                if (dx * dx + dy * dy < minDist * minDist)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
                nodes.Add(new GraphNode(nodes.Count, x, y));
        }

        // Fill remaining without distance constraint if placement was too constrained
        while (nodes.Count < count)
        {
            double x = rng.NextDouble() * usable + Padding;
            double y = rng.NextDouble() * usable + Padding;
            nodes.Add(new GraphNode(nodes.Count, x, y));
        }

        return nodes;
    }

    /// <summary>
    /// Connects nodes into a planar graph (no crossing edges) using a Delaunay
    /// triangulation of the node positions. A Delaunay triangulation is guaranteed to be
    /// a valid planar straight-line embedding - i.e. its edges never cross - and, for 3 or
    /// more non-degenerate points, is automatically fully connected, while still giving
    /// each node several neighbors (average degree ~6) so Dijkstra has interesting
    /// alternate routes to explore.
    /// </summary>
    private static List<GraphEdge> ConnectNodes(IReadOnlyList<GraphNode> nodes, int n)
    {
        if (n < 2) return [];

        if (n == 2)
            return [new GraphEdge(0, 1, Distance(nodes[0], nodes[1]))];

        var triangles = TriangulateDelaunay(nodes);

        var edgeSet = new HashSet<long>();
        var edges = new List<GraphEdge>();

        long EdgeKey(int a, int b) => (long)Math.Min(a, b) * n + Math.Max(a, b);

        void TryAddEdge(int a, int b)
        {
            if (edgeSet.Add(EdgeKey(a, b)))
                edges.Add(new GraphEdge(a, b, Distance(nodes[a], nodes[b])));
        }

        foreach (var t in triangles)
        {
            TryAddEdge(t.A, t.B);
            TryAddEdge(t.B, t.C);
            TryAddEdge(t.C, t.A);
        }

        return edges;
    }

    private readonly record struct Triangle(int A, int B, int C);

    /// <summary>
    /// Computes the Delaunay triangulation of the given points using the Bowyer-Watson
    /// algorithm, returning the resulting triangles indexed into <paramref name="nodes"/>.
    /// </summary>
    private static List<Triangle> TriangulateDelaunay(IReadOnlyList<GraphNode> nodes)
    {
        int n = nodes.Count;
        var pts = new List<GraphNode>(nodes);

        // A super-triangle large enough to contain every input point, so the incremental
        // algorithm always has a valid starting triangulation to insert points into.
        double minX = nodes.Min(p => p.X), minY = nodes.Min(p => p.Y);
        double maxX = nodes.Max(p => p.X), maxY = nodes.Max(p => p.Y);
        double deltaMax = Math.Max(maxX - minX, maxY - minY) * 10 + 10;
        double midX = (minX + maxX) / 2, midY = (minY + maxY) / 2;

        int superA = n, superB = n + 1, superC = n + 2;
        pts.Add(new GraphNode(superA, midX - 20 * deltaMax, midY - deltaMax));
        pts.Add(new GraphNode(superB, midX, midY + 20 * deltaMax));
        pts.Add(new GraphNode(superC, midX + 20 * deltaMax, midY - deltaMax));

        var triangles = new List<Triangle> { new(superA, superB, superC) };

        for (int i = 0; i < n; i++)
        {
            var p = pts[i];

            var badTriangles = triangles.Where(t => InCircumcircle(pts[t.A], pts[t.B], pts[t.C], p)).ToList();

            // The boundary of the union of "bad" triangles forms a polygon hole; edges
            // shared by two bad triangles are interior to the hole and are discarded,
            // leaving only the edges that appear exactly once.
            var edgeCount = new Dictionary<(int, int), int>();
            void CountEdge(int a, int b)
            {
                var key = a < b ? (a, b) : (b, a);
                edgeCount[key] = edgeCount.GetValueOrDefault(key) + 1;
            }

            foreach (var t in badTriangles)
            {
                CountEdge(t.A, t.B);
                CountEdge(t.B, t.C);
                CountEdge(t.C, t.A);
            }

            triangles.RemoveAll(badTriangles.Contains);

            foreach (var ((a, b), count) in edgeCount)
            {
                if (count == 1)
                    triangles.Add(new Triangle(a, b, i));
            }
        }

        // Discard triangles that still reference a super-triangle vertex.
        triangles.RemoveAll(t => t.A >= n || t.B >= n || t.C >= n);
        return triangles;
    }

    /// <summary>Tests whether point <paramref name="p"/> lies inside the circumcircle of triangle abc.</summary>
    private static bool InCircumcircle(GraphNode a, GraphNode b, GraphNode c, GraphNode p)
    {
        double ax = a.X - p.X, ay = a.Y - p.Y;
        double bx = b.X - p.X, by = b.Y - p.Y;
        double cx = c.X - p.X, cy = c.Y - p.Y;

        double det =
            (ax * ax + ay * ay) * (bx * cy - cx * by) -
            (bx * bx + by * by) * (ax * cy - cx * ay) +
            (cx * cx + cy * cy) * (ax * by - bx * ay);

        // The determinant test above assumes a, b, c are in counter-clockwise order;
        // flip the comparison if they're wound clockwise.
        double orientation = (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
        return orientation > 0 ? det > 0 : det < 0;
    }

    private static double Distance(GraphNode a, GraphNode b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
