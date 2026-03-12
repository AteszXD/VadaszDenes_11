using System;
using System.Collections.Generic;

namespace VadaszDenes
{
    public class SimplePathfinder
    {
        private string[,] map;
        private int size = 50;

        public SimplePathfinder(string[,] map)
        {
            this.map = map;
        }

        private readonly (int, int)[] directions =
        {
            (-1,0),(1,0),(0,-1),(0,1),
            (-1,-1),(-1,1),(1,-1),(1,1)
        };

        public (int, int)? FindNearestMineral(int startX, int startY)
        {
            double bestDist = double.MaxValue;
            (int, int)? best = null;

            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    if (map[i, j] == "B" || map[i, j] == "Y" || map[i, j] == "G")
                    {
                        double d = Math.Sqrt(
                            (i - startX) * (i - startX) +
                            (j - startY) * (j - startY));

                        if (d < bestDist)
                        {
                            bestDist = d;
                            best = (i, j);
                        }
                    }
                }
            }

            return best;
        }

        public List<(int, int)> FindPath((int, int) start, (int, int) goal)
        {
            Queue<(int, int)> q = new Queue<(int, int)>();
            Dictionary<(int, int), (int, int)> parent = new Dictionary<(int, int), (int, int)>();
            HashSet<(int, int)> visited = new HashSet<(int, int)>();

            q.Enqueue(start);
            visited.Add(start);

            while (q.Count > 0)
            {
                var current = q.Dequeue();

                if (current == goal)
                    break;

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

                    visited.Add(next);
                    parent[next] = current;
                    q.Enqueue(next);
                }
            }

            List<(int, int)> path = new List<(int, int)>();

            if (!parent.ContainsKey(goal))
                return path;

            var step = goal;

            while (step != start)
            {
                path.Add(step);
                step = parent[step];
            }

            path.Reverse();
            return path;
        }
    }
}