using RealEstateERP.Core.Models;

namespace RealEstateERP.Core.Common;

/// <summary>Pure helpers for the structure tree. No database, fully unit-tested.</summary>
public static class StructureTree
{
    /// <summary>Depth-first, ordered by SortOrder then name, with depth for indentation.</summary>
    public static IReadOnlyList<StructureTreeRow> Flatten(IReadOnlyList<StructureNodeDto> nodes, IReadOnlyDictionary<int, int>? unitCounts = null)
    {
        var byParent = nodes.ToLookup(n => n.ParentId);
        var paths = Paths(nodes);
        var rows = new List<StructureTreeRow>(nodes.Count);
        var visited = new HashSet<int>();

        void Walk(int? parentId, int depth)
        {
            foreach (var n in byParent[parentId].OrderBy(n => n.SortOrder).ThenBy(n => n.Name, NaturalComparer.Instance))
            {
                if (!visited.Add(n.Id)) continue;   // defensive: never loop on bad data
                rows.Add(new StructureTreeRow(n.Id, n.ParentId, n.Name, n.NodeType, n.SortOrder, depth,
                    paths[n.Id], unitCounts?.GetValueOrDefault(n.Id) ?? 0, byParent[n.Id].Any()));
                Walk(n.Id, depth + 1);
            }
        }

        Walk(null, 0);
        return rows;
    }

    /// <summary>Node id -> "Block A › Floor 1".</summary>
    public static IReadOnlyDictionary<int, string> Paths(IReadOnlyList<StructureNodeDto> nodes)
    {
        var byId = nodes.ToDictionary(n => n.Id);
        var result = new Dictionary<int, string>(nodes.Count);
        foreach (var node in nodes)
        {
            var parts = new List<string>();
            var seen = new HashSet<int>();
            for (StructureNodeDto? cur = node; cur is not null && seen.Add(cur.Id);
                 cur = cur.ParentId is int p && byId.TryGetValue(p, out var parent) ? parent : null)
                parts.Add(cur.Name);
            parts.Reverse();
            result[node.Id] = string.Join(" › ", parts);
        }
        return result;
    }

    /// <summary>True if <paramref name="candidateId"/> is <paramref name="nodeId"/> itself or sits anywhere below it.</summary>
    public static bool IsSelfOrDescendant(IReadOnlyList<StructureNodeDto> nodes, int nodeId, int candidateId)
    {
        var byId = nodes.ToDictionary(n => n.Id);
        var seen = new HashSet<int>();
        for (int? cur = candidateId; cur is int id && seen.Add(id); cur = byId.TryGetValue(id, out var n) ? n.ParentId : null)
            if (id == nodeId) return true;
        return false;
    }
}

/// <summary>"A-2" before "A-10"; case-insensitive.</summary>
public sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
            {
                int si = i, sj = j;
                while (i < x.Length && char.IsDigit(x[i])) i++;
                while (j < y.Length && char.IsDigit(y[j])) j++;
                var a = x[si..i].TrimStart('0');
                var b = y[sj..j].TrimStart('0');
                var cmp = a.Length != b.Length ? a.Length.CompareTo(b.Length) : string.CompareOrdinal(a, b);
                if (cmp != 0) return cmp;
            }
            else
            {
                var cmp = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
                if (cmp != 0) return cmp;
                i++; j++;
            }
        }
        return (x.Length - i).CompareTo(y.Length - j);
    }
}
