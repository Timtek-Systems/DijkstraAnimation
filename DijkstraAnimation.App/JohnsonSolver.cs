namespace DijkstraAnimation.App;

/// <summary>
/// Runs Johnson's algorithm and records every step for animated replay, reusing the
/// same <see cref="AnimationStep"/> vocabulary as <see cref="DijkstraSolver"/> so the
/// existing playback/rendering code works unchanged. Johnson's algorithm first runs
/// Bellman-Ford from a virtual source connected to every node with zero-weight edges
/// to compute a "potential" for each node, then reweights every edge so that Dijkstra
/// can be used even when the original graph has negative edge weights (not produced
/// by this app's graph generator, but supported here for correctness). The
/// potential-computation phase runs silently since it uses a virtual node/edges that
/// have no visual representation; only the reweighted Dijkstra search - which walks
/// the real graph - is animated, with distances translated back to the original
/// weights before being recorded.
/// </summary>
public sealed class JohnsonSolver : IPathfindingSolver
{
    public string Name => "Johnson";

    public List<AnimationStep> Solve(Graph graph, int startId, int endId)
    {
        var steps = new List<AnimationStep>();
        int n = graph.Nodes.Count;

        // Phase 1: Bellman-Ford from a virtual source with a zero-weight edge to every
        // node computes each node's potential h(v) = shortest distance from the virtual
        // source. This phase is not animated since the virtual source/edges don't exist
        // in the visible graph.
        var potential = new double[n];
        Array.Fill(potential, 0);
        for (int iteration = 0; iteration < n - 1; iteration++)
        {
            bool anyImproved = false;
            for (int edgeIdx = 0; edgeIdx < graph.Edges.Count; edgeIdx++)
            {
                var edge = graph.Edges[edgeIdx];
                if (potential[edge.SourceId] + edge.Weight < potential[edge.TargetId])
                {
                    potential[edge.TargetId] = potential[edge.SourceId] + edge.Weight;
                    anyImproved = true;
                }
                if (potential[edge.TargetId] + edge.Weight < potential[edge.SourceId])
                {
                    potential[edge.SourceId] = potential[edge.TargetId] + edge.Weight;
                    anyImproved = true;
                }
            }
            if (!anyImproved) break;
        }

        // Phase 2 + 3: reweight every edge as w'(u, v) = w(u, v) + h(u) - h(v), which is
        // guaranteed to be non-negative, then run Dijkstra on the reweighted graph.
        // Real (un-reweighted) distances are recovered as d(u, v) = d'(u, v) - h(u) + h(v)
        // for display and for the recorded animation steps.
        double Reweight(int from, int to, double weight) => weight + potential[from] - potential[to];

        var dist = new double[n];
        var prev = new int[n];
        var visited = new bool[n];
        Array.Fill(dist, double.PositiveInfinity);
        Array.Fill(prev, -1);

        dist[startId] = 0;
        var pq = new PriorityQueue<int, double>();
        pq.Enqueue(startId, 0);

        while (pq.Count > 0)
        {
            int u = pq.Dequeue();
            if (visited[u]) continue;

            visited[u] = true;
            steps.Add(new VisitNodeStep(u, dist[u]));

            if (u == endId)
            {
                steps.Add(new SettleNodeStep(u));
                break;
            }

            foreach (var (neighbor, edgeIdx) in graph.GetNeighbors(u))
            {
                if (visited[neighbor]) continue;

                double reweightedEdge = Reweight(u, neighbor, graph.Edges[edgeIdx].Weight);
                double newDist = dist[u] + reweightedEdge;
                bool improved = newDist < dist[neighbor];

                // Report the real (un-reweighted) distance so the UI shows true path costs.
                double realNewDist = newDist - potential[startId] + potential[neighbor];
                steps.Add(new ExamineEdgeStep(edgeIdx, neighbor, realNewDist, improved));

                if (improved)
                {
                    dist[neighbor] = newDist;
                    prev[neighbor] = u;
                    pq.Enqueue(neighbor, newDist);
                }
            }

            steps.Add(new SettleNodeStep(u));
        }

        // Trace shortest path
        if (visited[endId] && dist[endId] < double.PositiveInfinity)
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

        steps.Add(new AlgorithmDoneStep(visited[endId] && dist[endId] < double.PositiveInfinity));
        return steps;
    }
}
