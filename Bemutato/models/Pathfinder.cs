using System;
using System.Collections.Generic;

namespace VadaszDenes
{
    public class SimplePathfinder
    {
        private string[,] map;
        private int size = 50;
        private List<(int, int)> cachedMinerals;

        public SimplePathfinder(string[,] map)
        {
            this.map = map;
            CacheMinerals();
        }

        private readonly (int, int)[] directions =
        {
            (-1,0),(1,0),(0,-1),(0,1),
            (-1,-1),(-1,1),(1,-1),(1,1)
        };

        private void CacheMinerals()
        {
            cachedMinerals = new List<(int, int)>();
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    if (map[i, j] == "B" || map[i, j] == "Y" || map[i, j] == "G")
                    {
                        cachedMinerals.Add((i, j));
                    }
                }
            }
        }

        public (int, int)? FindNearestMineral(int startX, int startY)
        {
            if (cachedMinerals.Count == 0)
                return null;

            double bestDist = double.MaxValue;
            (int, int)? best = null;

            foreach (var mineral in cachedMinerals)
            {
                double d = (mineral.Item1 - startX) * (mineral.Item1 - startX) +
                           (mineral.Item2 - startY) * (mineral.Item2 - startY);

                if (d < bestDist)
                {
                    bestDist = d;
                    best = mineral;
                }
            }

            return best;
        }

        private double Heuristic((int, int) a, (int, int) b)
        {
            return Math.Abs(a.Item1 - b.Item1) + Math.Abs(a.Item2 - b.Item2);
        }

        public List<(int, int)> FindPath((int, int) start, (int, int) goal)
        {
            var openSet = new List<(double score, (int, int) pos)>();
            var cameFrom = new Dictionary<(int, int), (int, int)>();
            var gScore = new Dictionary<(int, int), double>();
            var visited = new HashSet<(int, int)>();

            openSet.Add((0, start));
            gScore[start] = 0;

            while (openSet.Count > 0)
            {
                openSet.Sort((a, b) => a.score.CompareTo(b.score));
                var current = openSet[0].pos;
                openSet.RemoveAt(0);

                if (current == goal)
                    return ReconstructPath(cameFrom, current, start);

                if (visited.Contains(current))
                    continue;

                visited.Add(current);

                foreach (var d in directions)
                {
                    int nx = current.Item1 + d.Item1;
                    int ny = current.Item2 + d.Item2;

                    if (nx < 0 || ny < 0 || nx >= size || ny >= size)
                        continue;

                    if (map[nx, ny] == "#")
                        continue;

                    var next = (nx, ny);

                    if (visited.Contains(next))
                        continue;

                    double tentativeG = gScore[current] + 1;

                    if (!gScore.ContainsKey(next) || tentativeG < gScore[next])
                    {
                        cameFrom[next] = current;
                        gScore[next] = tentativeG;
                        double fScore = tentativeG + Heuristic(next, goal);
                        openSet.Add((fScore, next));
                    }
                }
            }

            return new List<(int, int)>();
        }

        private List<(int, int)> ReconstructPath(Dictionary<(int, int), (int, int)> cameFrom, (int, int) current, (int, int) start)
        {
            var path = new List<(int, int)>();
            var step = current;

            while (step != start)
            {
                path.Add(step);
                step = cameFrom[step];
            }

            path.Reverse();
            return path;
        }
    }
}