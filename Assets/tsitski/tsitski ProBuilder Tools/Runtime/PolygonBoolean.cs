using System;
using System.Collections.Generic;
using System.Linq;

namespace Tsitski.ProBuilderTools {
    internal static class PolygonBoolean {
        private const double Epsilon = 1e-4;

        public static List<(List<D2> outline, List<List<D2>> holes)> Subtract(List<D2> outline, IEnumerable<List<D2>> cutters) {
            List<List<D2>> loops = new() { Counterclockwise(outline) };
            loops.AddRange(cutters.Where(c => c is { Count: >= 3 } && Math.Abs(RoofGeometry.SignedArea(c)) > Epsilon * Epsilon).Select(Counterclockwise));

            List<(D2 a, D2 b)> kept = new();
            List<double> cuts = new();
            for (int i = 0; i < loops.Count; i++) {
                List<D2> loop = loops[i];
                for (int e = 0; e < loop.Count; e++) {
                    D2 a = loop[e], b = loop[(e + 1) % loop.Count];
                    double length = (b - a).Length;
                    if (length < Epsilon)
                        continue;

                    D2 direction = (b - a) * (1 / length);
                    cuts.Clear();
                    cuts.Add(0);
                    cuts.Add(length);
                    for (int j = 0; j < loops.Count; j++)
                        if (j != i)
                            for (int f = 0; f < loops[j].Count; f++)
                                AddCuts(a, direction, length, loops[j][f], loops[j][(f + 1) % loops[j].Count], cuts);

                    cuts.Sort();
                    for (int k = 0; k + 1 < cuts.Count; k++) {
                        if (cuts[k + 1] - cuts[k] < Epsilon)
                            continue;

                        D2 start = a + direction * cuts[k], end = a + direction * cuts[k + 1];
                        D2 mid = (start + end) * 0.5;
                        if (!Keep(loops, i, mid, direction))
                            continue;

                        if (i == 0)
                            kept.Add((start, end));
                        else
                            kept.Add((end, start));
                    }
                }
            }

            List<List<D2>> result = Chain(kept);
            List<List<D2>> outers = result.Where(l => RoofGeometry.SignedArea(l) > 0).OrderBy(RoofGeometry.SignedArea).ToList();
            List<(List<D2> outline, List<List<D2>> holes)> regions = outers.Select(o => (o, new List<List<D2>>())).ToList();
            foreach (List<D2> hole in result.Where(l => RoofGeometry.SignedArea(l) < 0)) {
                D2 probe = (hole[0] + hole[1]) * 0.5 + (hole[1] - hole[0]).Normalized.Left * (Epsilon * 10);
                int owner = outers.FindIndex(o => RoofGeometry.Contains(o, probe));
                if (owner >= 0)
                    regions[owner].Item2.Add(hole);
            }

            return regions;
        }

        private static List<D2> Counterclockwise(List<D2> loop) {
            List<D2> points = new(loop);
            if (RoofGeometry.SignedArea(points) < 0)
                points.Reverse();
            return points;
        }

        private static bool Keep(List<List<D2>> loops, int index, D2 mid, D2 direction) {
            for (int j = 0; j < loops.Count; j++) {
                if (j == index)
                    continue;

                int side = Side(loops[j], mid, direction, out bool sameDirection);
                if (side == 0) {
                    if (index > 0 && j > 0 ? !sameDirection || j < index : index > 0 || sameDirection)
                        return false;
                    continue;
                }

                bool inside = side > 0;
                if (j == 0 ? !inside : inside)
                    return false;
            }

            return true;
        }

        private static int Side(List<D2> loop, D2 point, D2 direction, out bool sameDirection) {
            sameDirection = false;
            for (int f = 0; f < loop.Count; f++) {
                D2 c = loop[f], cd = loop[(f + 1) % loop.Count] - c;
                double length = cd.Length;
                if (length < 1e-12)
                    continue;

                D2 unit = cd * (1 / length);
                D2 offset = point - c;
                double along = unit.Dot(offset);
                if (Math.Abs(unit.Cross(offset)) < Epsilon && along > -Epsilon && along < length + Epsilon) {
                    sameDirection = unit.Dot(direction) > 0;
                    return 0;
                }
            }

            return RoofGeometry.Contains(loop, point) ? 1 : -1;
        }

        private static void AddCuts(D2 a, D2 direction, double length, D2 c, D2 d, List<double> cuts) {
            foreach (D2 q in new[] { c, d }) {
                if (Math.Abs(direction.Cross(q - a)) >= Epsilon)
                    continue;
                double t = direction.Dot(q - a);
                if (t > Epsilon && t < length - Epsilon)
                    cuts.Add(t);
            }

            D2 cd = d - c;
            double denominator = direction.Cross(cd);
            if (Math.Abs(denominator) < 1e-12)
                return;

            double along = (c - a).Cross(cd) / denominator;
            double other = (c - a).Cross(direction) / denominator;
            double slack = Epsilon / Math.Max(cd.Length, Epsilon);
            if (along > Epsilon && along < length - Epsilon && other >= -slack && other <= 1 + slack)
                cuts.Add(along);
        }

        private static List<List<D2>> Chain(List<(D2 a, D2 b)> segments) {
            List<D2> nodes = new();

            int Node(D2 point) {
                for (int i = 0; i < nodes.Count; i++)
                    if ((nodes[i] - point).Length < Epsilon)
                        return i;
                nodes.Add(point);
                return nodes.Count - 1;
            }

            List<(int from, int to)> edges = new();
            HashSet<(int, int)> seen = new();
            foreach ((D2 a, D2 b) in segments) {
                int from = Node(a), to = Node(b);
                if (from != to && seen.Add((from, to)))
                    edges.Add((from, to));
            }

            Dictionary<int, List<int>> outgoing = new();
            for (int i = 0; i < edges.Count; i++) {
                if (!outgoing.TryGetValue(edges[i].from, out List<int> list))
                    outgoing[edges[i].from] = list = new List<int>();
                list.Add(i);
            }

            bool[] used = new bool[edges.Count];
            List<List<D2>> loops = new();
            for (int start = 0; start < edges.Count; start++) {
                if (used[start])
                    continue;

                List<D2> loop = new();
                int edge = start;
                bool closed = false;
                while (true) {
                    used[edge] = true;
                    loop.Add(nodes[edges[edge].from]);
                    int at = edges[edge].to;
                    if (at == edges[start].from) {
                        closed = true;
                        break;
                    }

                    D2 incoming = (nodes[at] - nodes[edges[edge].from]).Normalized;
                    int next = -1;
                    double best = double.MaxValue;
                    if (outgoing.TryGetValue(at, out List<int> candidates))
                        foreach (int candidate in candidates) {
                            if (used[candidate])
                                continue;
                            D2 direction = (nodes[edges[candidate].to] - nodes[at]).Normalized;
                            double turn = Math.Atan2(incoming.Cross(direction), incoming.Dot(direction));
                            if (turn < best) {
                                best = turn;
                                next = candidate;
                            }
                        }

                    if (next < 0)
                        break;
                    edge = next;
                }

                if (!closed)
                    continue;

                loop = RoofGeometry.Simplify(loop);
                if (loop.Count >= 3 && Math.Abs(RoofGeometry.SignedArea(loop)) > Epsilon * Epsilon)
                    loops.Add(loop);
            }

            return loops;
        }
    }
}
