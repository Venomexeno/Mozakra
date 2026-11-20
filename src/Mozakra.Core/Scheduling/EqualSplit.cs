namespace Mozakra.Core.Scheduling;

/// <summary>
/// Splits minutes equally between plans; a plan that needs less than its share takes only what it
/// needs and the rest goes to the others ("water filling").
/// </summary>
internal static class EqualSplit
{
    public static Dictionary<Guid, double> Split(IEnumerable<(Guid Id, double Need)> plans, double minutes)
    {
        var result = new Dictionary<Guid, double>();
        var ordered = plans.OrderBy(p => p.Need).ToList();
        double left = Math.Max(0, minutes);

        for (int i = 0; i < ordered.Count; i++)
        {
            double share = left / (ordered.Count - i);
            double give = Math.Min(share, Math.Max(0, ordered[i].Need));
            result[ordered[i].Id] = give;
            left -= give;
        }
        return result;
    }
}
