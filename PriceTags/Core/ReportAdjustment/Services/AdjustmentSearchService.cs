using System.Diagnostics;
using FanShop.ReportAdjustment.Models;

namespace FanShop.ReportAdjustment.Services;

public sealed class AdjustmentSearchService(DiscountCalculationService calculator, SolutionScoringService scoring, SolutionValidator validator)
{
    private sealed record PathNode(PathNode? Previous, RowAdjustment Change);
    private sealed record State(SolutionScore Score, PathNode? Path);
    private sealed record Candidate(SalesReportRow Row, DiscountOption[] Options);
    public IReadOnlyList<DiscountOption> GetOptions(SalesReportRow row, AdjustmentSettings settings)
    {
        var options = new List<DiscountOption>();
        if (row.IsProtected || !row.UseInSearch) return options;
        if (row.CanChangeDiscount)
            foreach (var discount in settings.AllowedDiscounts)
            {
                if (discount == row.CurrentManualDiscountPercent || (!settings.AllowDiscountDecrease && discount < row.CurrentManualDiscountPercent)) continue;
                var total = calculator.Calculate(row.Quantity, row.BasePrice, discount, settings.Rounding);
                var delta = Money.ToMinor(row.CurrentTotal - total);
                if (delta != 0) options.Add(new(discount, total, delta, AdjustmentAction.ChangeDiscount));
            }
        if (row.CanExclude && row.CurrentTotal != 0)
            options.Add(new(row.CurrentManualDiscountPercent, 0, Money.ToMinor(row.CurrentTotal), AdjustmentAction.Exclude));
        // The unchanged option is implicit, preserving the original total and nonstandard discount exactly.
        return options;
    }
    public Task<SearchResult> SearchAsync(IReadOnlyList<SalesReportRow> rows, AdjustmentRequest request, AdjustmentSettings settings,
        IProgress<SearchProgress>? progress = null, CancellationToken cancellationToken = default)
        => Task.Run(() => Search(rows, request, settings, progress, cancellationToken), cancellationToken);

    private SearchResult Search(IReadOnlyList<SalesReportRow> rows, AdjustmentRequest request, AdjustmentSettings settings,
        IProgress<SearchProgress>? progress, CancellationToken token)
    {
        settings.Validate(); token.ThrowIfCancellationRequested();
        if (rows.Select(r => r.RowNumber).Distinct().Count() != rows.Count) throw new InvalidOperationException("Номера строк должны быть уникальными.");
        var required = request.GetRequiredAmount(calculator, settings);
        var target = Money.ToMinor(required);
        var watch = Stopwatch.StartNew();
        var candidates = rows.Select(r => new Candidate(r, GetOptions(r, settings).ToArray())).Where(c => c.Options.Length > 0)
            .OrderBy(c => c.Row.PriorityClass).ThenBy(c => c.Row.RowNumber).ToArray();
        var suffixMin = new long[candidates.Length + 1]; var suffixMax = new long[candidates.Length + 1];
        for (var i = candidates.Length - 1; i >= 0; i--)
        {
            suffixMin[i] = checked(suffixMin[i + 1] + Math.Min(0, candidates[i].Options.Min(o => o.Delta)));
            suffixMax[i] = checked(suffixMax[i + 1] + Math.Max(0, candidates[i].Options.Max(o => o.Delta)));
        }
        var states = new Dictionary<long, List<State>> { [0] = [new(default, null)] };
        List<State> exact = target == 0 ? [new(default, null)] : [];
        long transitions = 0; bool limited = false;
        long? nearest = 0;
        void Consider(long sum, State state)
        {
            if (sum == target) Insert(exact, state, settings.MaxSolutions);
            if (nearest is null || Math.Abs((decimal)target - sum) < Math.Abs((decimal)target - nearest.Value)) nearest = sum;
        }
        for (var index = 0; index < candidates.Length; index++)
        {
            token.ThrowIfCancellationRequested();
            var candidate = candidates[index];
            var next = new Dictionary<long, List<State>>();
            foreach (var (sum, list) in states)
            {
                foreach (var state in list)
                {
                    void Add(long newSum, State newState)
                    {
                        Consider(newSum, newState);
                        // Reachability bounds are sound; nearest display disables pruning to inspect final reachable sums.
                        if (!settings.ShowNearest && (target - newSum < suffixMin[index + 1] || target - newSum > suffixMax[index + 1])) return;
                        if (!next.TryGetValue(newSum, out var bucket))
                        {
                            if (next.Count >= settings.MaxPartialSums) { limited = true; return; }
                            next[newSum] = bucket = [];
                        }
                        Insert(bucket, newState, settings.MaxSolutions);
                    }
                    Add(sum, state);
                    foreach (var option in candidate.Options)
                    {
                        if (++transitions > settings.MaxTransitions || watch.Elapsed.TotalSeconds > settings.MaxSearchSeconds) { limited = true; goto Finished; }
                        if ((transitions & 1023) == 0) token.ThrowIfCancellationRequested();
                        var change = new RowAdjustment(candidate.Row.RowNumber, candidate.Row.Quantity, candidate.Row.CurrentManualDiscountPercent,
                            option.DiscountPercent, candidate.Row.CurrentTotal, option.NewTotal, option.Action);
                        Add(checked(sum + option.Delta), new(state.Score + scoring.Score(candidate.Row, option, settings), new(state.Path, change)));
                    }
                }
            }
            states = next;
            progress?.Report(new(index + 1, candidates.Length, states.Count));
            if (states.Count == 0) break;
        }
        Finished:
        token.ThrowIfCancellationRequested();
        var before = rows.Sum(r => r.CurrentTotal);
        var matches = rows.All(r => calculator.Matches(r, settings.Rounding));
        var solutions = new List<AdjustmentSolution>();
        foreach (var state in exact)
        {
            var changes = new List<RowAdjustment>();
            for (var node = state.Path; node is not null; node = node.Previous) changes.Add(node.Change);
            changes.Sort((a,b) => a.RowNumber.CompareTo(b.RowNumber));
            var solution = new AdjustmentSolution(changes, state.Score, required, before, before - changes.Sum(c => c.Delta) + required, matches);
            var validation = validator.Validate(rows, request, settings, solution);
            if (!validation.IsValid) throw new InvalidOperationException("Независимая проверка отклонила решение: " + string.Join(" ", validation.Errors));
            solutions.Add(solution);
        }
        watch.Stop();
        return new(solutions, limited, transitions, watch.Elapsed,
            settings.ShowNearest && solutions.Count == 0 && nearest is { } value ? Money.FromMinor(value - target) : null);
    }
    private static void Insert(List<State> bucket, State state, int capacity)
    {
        // A path can reappear through unchanged states; reference identity removes these duplicates cheaply.
        if (bucket.Any(s => ReferenceEquals(s.Path, state.Path))) return;
        var position = bucket.FindIndex(s => state.Score.CompareTo(s.Score) < 0);
        if (position < 0) position = bucket.Count;
        if (position >= capacity) return;
        bucket.Insert(position, state);
        if (bucket.Count > capacity) bucket.RemoveAt(bucket.Count - 1);
    }
}
