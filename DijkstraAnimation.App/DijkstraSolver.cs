namespace DijkstraAnimation.App;

/// <summary>Base type for animation steps produced by the Dijkstra solver.</summary>
public abstract record AnimationStep;

/// <summary>A node is dequeued and becomes the current focus.</summary>
public sealed record VisitNodeStep(int NodeId, double Distance) : AnimationStep;

/// <summary>An edge from the current node to a neighbor is examined.</summary>
public sealed record ExamineEdgeStep(int EdgeIndex, int ToNode, double NewDistance, bool Improved) : AnimationStep;

/// <summary>Processing of a node is complete; it is now settled.</summary>
public sealed record SettleNodeStep(int NodeId) : AnimationStep;

/// <summary>The shortest path has been found. Nodes and edges listed start-to-end.</summary>
public sealed record ShowPathStep(IReadOnlyList<int> PathNodeIds, IReadOnlyList<int> PathEdgeIndices) : AnimationStep;

/// <summary>The algorithm has finished.</summary>
public sealed record AlgorithmDoneStep(bool PathFound) : AnimationStep;

/// <summary>Runs Dijkstra's algorithm and records every step for animated replay.</summary>
public static class DijkstraSolver
{
    public static List<AnimationStep> Solve(Graph graph, int startId, int endId)
    {
        var steps = new List<AnimationStep>();
        int n = graph.Nodes.Count;

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

                double newDist = dist[u] + graph.Edges[edgeIdx].Weight;
                bool improved = newDist < dist[neighbor];

                steps.Add(new ExamineEdgeStep(edgeIdx, neighbor, newDist, improved));

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
