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
        var edges = ConnectNodes(nodes, nodeCount, rng);
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

    private static List<GraphEdge> ConnectNodes(IReadOnlyList<GraphNode> nodes, int n, Random rng)
    {
        int k = Math.Clamp(4, 2, Math.Min(6, n - 1));
        var edgeSet = new HashSet<long>();
        var edges = new List<GraphEdge>();

        long EdgeKey(int a, int b) => (long)Math.Min(a, b) * n + Math.Max(a, b);

        // K-nearest neighbors
        for (int i = 0; i < n; i++)
        {
            var dists = new List<(int Id, double Dist)>(n);
            for (int j = 0; j < n; j++)
            {
                if (i == j) continue;
                double dx = nodes[i].X - nodes[j].X;
                double dy = nodes[i].Y - nodes[j].Y;
                dists.Add((j, Math.Sqrt(dx * dx + dy * dy)));
            }

            dists.Sort((a, b) => a.Dist.CompareTo(b.Dist));

            for (int m = 0; m < Math.Min(k, dists.Count); m++)
            {
                long key = EdgeKey(i, dists[m].Id);
                if (edgeSet.Add(key))
                    edges.Add(new GraphEdge(i, dists[m].Id, dists[m].Dist));
            }
        }

        // Ensure connectivity via union-find
        var parent = Enumerable.Range(0, n).ToArray();
        var rank = new int[n];

        int Find(int x)
        {
            while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
            return x;
        }

        void Union(int a, int b)
        {
            a = Find(a); b = Find(b);
            if (a == b) return;
            if (rank[a] < rank[b]) (a, b) = (b, a);
            parent[b] = a;
            if (rank[a] == rank[b]) rank[a]++;
        }

        foreach (var edge in edges)
            Union(edge.SourceId, edge.TargetId);

        // Connect any disconnected components to component 0
        for (int i = 1; i < n; i++)
        {
            if (Find(i) == Find(0)) continue;

            double best = double.MaxValue;
            int bestJ = 0;
            for (int j = 0; j < n; j++)
            {
                if (Find(j) != Find(0)) continue;
                double dx = nodes[i].X - nodes[j].X;
                double dy = nodes[i].Y - nodes[j].Y;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d < best) { best = d; bestJ = j; }
            }

            long key = EdgeKey(i, bestJ);
            if (edgeSet.Add(key))
                edges.Add(new GraphEdge(i, bestJ, best));
            Union(i, bestJ);
        }

        // Extra random edges for alternative paths
        int extra = n / 3;
        for (int e = 0; e < extra; e++)
        {
            int a = rng.Next(n), b = rng.Next(n);
            if (a == b) continue;
            long key = EdgeKey(a, b);
            if (edgeSet.Add(key))
            {
                double dx = nodes[a].X - nodes[b].X;
                double dy = nodes[a].Y - nodes[b].Y;
                edges.Add(new GraphEdge(a, b, Math.Sqrt(dx * dx + dy * dy)));
            }
        }

        return edges;
    }
}
