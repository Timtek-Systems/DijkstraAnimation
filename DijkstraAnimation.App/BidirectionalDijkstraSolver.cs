namespace DijkstraAnimation.App;

/// <summary>
/// Runs Dijkstra's algorithm simultaneously from both the start and end nodes,
/// alternating one expansion from each frontier, and stops as soon as the two
/// searches meet and no shorter path can remain. Reuses the same
/// <see cref="AnimationStep"/> vocabulary as <see cref="DijkstraSolver"/> so the
/// existing playback/rendering code works unchanged.
/// </summary>
public sealed class BidirectionalDijkstraSolver : IPathfindingSolver
{
    public string Name => "Bi-Dijkstra";

    public List<AnimationStep> Solve(Graph graph, int startId, int endId)
    {
        var steps = new List<AnimationStep>();
        int n = graph.Nodes.Count;

        var distF = new double[n];
        var distB = new double[n];
        var prevF = new int[n];
        var prevB = new int[n];
        var visitedF = new bool[n];
        var visitedB = new bool[n];
        Array.Fill(distF, double.PositiveInfinity);
        Array.Fill(distB, double.PositiveInfinity);
        Array.Fill(prevF, -1);
        Array.Fill(prevB, -1);

        distF[startId] = 0;
        distB[endId] = 0;

        var pqF = new PriorityQueue<int, double>();
        var pqB = new PriorityQueue<int, double>();
        pqF.Enqueue(startId, 0);
        pqB.Enqueue(endId, 0);

        double bestDist = double.PositiveInfinity;
        int meetingNode = -1;

        void ExpandFrontier(
            PriorityQueue<int, double> pq,
            double[] dist,
            double[] otherDist,
            int[] prev,
            bool[] visited,
            bool[] otherVisited)
        {
            if (!pq.TryDequeue(out int u, out _)) return;
            if (visited[u]) return;

            visited[u] = true;
            steps.Add(new VisitNodeStep(u, dist[u]));

            if (otherVisited[u] && dist[u] + otherDist[u] < bestDist)
            {
                bestDist = dist[u] + otherDist[u];
                meetingNode = u;
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

        while (pqF.Count > 0 || pqB.Count > 0)
        {
            ExpandFrontier(pqF, distF, distB, prevF, visitedF, visitedB);
            ExpandFrontier(pqB, distB, distF, prevB, visitedB, visitedF);

            if (pqF.TryPeek(out _, out double topF) && pqB.TryPeek(out _, out double topB) &&
                topF + topB >= bestDist)
            {
                break;
            }
        }

        if (meetingNode != -1)
        {
            var pathNodes = new List<int>();
            int cur = meetingNode;
            while (cur != -1)
            {
                pathNodes.Add(cur);
                cur = prevF[cur];
            }
            pathNodes.Reverse();

            cur = meetingNode;
            while (cur != endId)
            {
                cur = prevB[cur];
                pathNodes.Add(cur);
            }

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

        steps.Add(new AlgorithmDoneStep(meetingNode != -1));
        return steps;
    }
}
