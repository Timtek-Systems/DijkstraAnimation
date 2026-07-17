namespace DijkstraAnimation.App;

/// <summary>
/// Runs the Bellman-Ford algorithm and records every edge relaxation for animated
/// replay, reusing the same <see cref="AnimationStep"/> vocabulary as
/// <see cref="DijkstraSolver"/> so the existing playback/rendering code works
/// unchanged. Unlike Dijkstra, Bellman-Ford relaxes every edge in the graph on each
/// pass rather than following a priority queue, which lets it also handle negative
/// edge weights (not produced by this app's graph generator, but supported here for
/// correctness).
/// </summary>
public sealed class BellmanFordSolver : IPathfindingSolver
{
    public string Name => "Bellman-Ford";

    public List<AnimationStep> Solve(Graph graph, int startId, int endId)
    {
        var steps = new List<AnimationStep>();
        int n = graph.Nodes.Count;

        var dist = new double[n];
        var prev = new int[n];
        Array.Fill(dist, double.PositiveInfinity);
        Array.Fill(prev, -1);
        dist[startId] = 0;

        void TryRelax(int from, int to, int edgeIdx)
        {
            if (double.IsPositiveInfinity(dist[from])) return;

            steps.Add(new VisitNodeStep(from, dist[from]));

            double newDist = dist[from] + graph.Edges[edgeIdx].Weight;
            bool improved = newDist < dist[to];

            steps.Add(new ExamineEdgeStep(edgeIdx, to, newDist, improved));

            if (improved)
            {
                dist[to] = newDist;
                prev[to] = from;
            }
        }

        // Relax every edge (in both directions, since the graph is undirected) up to
        // n - 1 times; stop early once a full pass makes no further improvement.
        for (int iteration = 0; iteration < n - 1; iteration++)
        {
            bool anyImproved = false;
            double[] distBefore = (double[])dist.Clone();

            for (int edgeIdx = 0; edgeIdx < graph.Edges.Count; edgeIdx++)
            {
                var edge = graph.Edges[edgeIdx];
                TryRelax(edge.SourceId, edge.TargetId, edgeIdx);
                TryRelax(edge.TargetId, edge.SourceId, edgeIdx);
            }

            for (int i = 0; i < n; i++)
            {
                if (dist[i] < distBefore[i]) anyImproved = true;
            }

            if (!anyImproved) break;
        }

        for (int i = 0; i < n; i++)
        {
            if (!double.IsPositiveInfinity(dist[i]))
                steps.Add(new SettleNodeStep(i));
        }

        if (!double.IsPositiveInfinity(dist[endId]))
        {
            var pathNodes = new List<int>();
            int cur = endId;
            while (cur != -1)
            {
                pathNodes.Add(cur);
                cur = prev[cur];
            }
            pathNodes.Reverse();

            var pathEdges = new List<int>();
            for (int i = 0; i < pathNodes.Count - 1; i++)
            {
                int from = pathNodes[i], to = pathNodes[i + 1];
                foreach (var (neighbor, edgeIdx) in graph.GetNeighbors(from))
                {
                    if (neighbor == to)
                    {
                        pathEdges.Add(edgeIdx);
                        break;
                    }
                }
            }

            steps.Add(new ShowPathStep(pathNodes, pathEdges));
        }

        steps.Add(new AlgorithmDoneStep(!double.IsPositiveInfinity(dist[endId])));
        return steps;
    }
}
