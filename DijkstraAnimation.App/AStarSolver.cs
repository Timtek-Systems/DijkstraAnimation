namespace DijkstraAnimation.App;

/// <summary>
/// Runs A* search and records every step for animated replay, reusing the same
/// <see cref="AnimationStep"/> vocabulary as <see cref="DijkstraSolver"/> so the existing
/// playback/rendering code works unchanged. The heuristic is the straight-line (Euclidean)
/// distance to the end node, which is admissible for non-negative edge weights.
/// </summary>
public sealed class AStarSolver : IPathfindingSolver
{
    public string Name => "A*";

    public List<AnimationStep> Solve(Graph graph, int startId, int endId)
    {
        var steps = new List<AnimationStep>();
        int n = graph.Nodes.Count;

        var dist = new double[n];
        var prev = new int[n];
        var visited = new bool[n];
        Array.Fill(dist, double.PositiveInfinity);
        Array.Fill(prev, -1);

        double Heuristic(int nodeId)
        {
            var a = graph.Nodes[nodeId];
            var b = graph.Nodes[endId];
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        dist[startId] = 0;
        var pq = new PriorityQueue<int, double>();
        pq.Enqueue(startId, Heuristic(startId));

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

                double newDist = dist[u] + graph.Edges[edgeIdx].Weight;
                bool improved = newDist < dist[neighbor];

                steps.Add(new ExamineEdgeStep(edgeIdx, neighbor, newDist, improved));

                if (improved)
                {
                    dist[neighbor] = newDist;
                    prev[neighbor] = u;
                    pq.Enqueue(neighbor, newDist + Heuristic(neighbor));
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
