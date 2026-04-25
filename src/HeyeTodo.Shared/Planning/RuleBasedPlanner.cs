using HeyeTodo.Shared.Contracts.Planning;
using HeyeTodo.Shared.Enums;
using TaskStatus = HeyeTodo.Shared.Enums.TaskStatus;

namespace HeyeTodo.Shared.Planning;

public static class RuleBasedPlanner
{
    public static PlanningResponse Plan(PlanningRequest request)
    {
        var issues = new List<PlanningIssue>();
        var activeTasks = request.Tasks
            .Where(x => x.Status != TaskStatus.Done)
            .ToDictionary(x => x.Id);
        var dependencies = request.Dependencies
            .Where(x => activeTasks.ContainsKey(x.PredecessorId) && activeTasks.ContainsKey(x.SuccessorId))
            .ToList();
        var successors = activeTasks.Keys.ToDictionary(x => x, _ => new List<Guid>());
        var indegree = activeTasks.Keys.ToDictionary(x => x, _ => 0);

        foreach (var dependency in dependencies)
        {
            successors[dependency.PredecessorId].Add(dependency.SuccessorId);
            indegree[dependency.SuccessorId]++;
        }

        var ready = activeTasks.Values
            .Where(x => indegree[x.Id] == 0)
            .OrderByDescending(WeightTask)
            .ThenBy(x => x.EndDate ?? DateTimeOffset.MaxValue)
            .ThenBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var ordered = new List<PlanningTaskInput>();

        while (ready.Count > 0)
        {
            var current = ready[0];
            ready.RemoveAt(0);
            ordered.Add(current);

            foreach (var successorId in successors[current.Id])
            {
                indegree[successorId]--;
                if (indegree[successorId] == 0)
                {
                    ready.Add(activeTasks[successorId]);
                }
            }

            ready = ready
                .OrderByDescending(WeightTask)
                .ThenBy(x => x.EndDate ?? DateTimeOffset.MaxValue)
                .ThenBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        if (ordered.Count != activeTasks.Count)
        {
            var blocked = activeTasks.Values.Where(x => !ordered.Any(y => y.Id == x.Id)).ToList();
            foreach (var task in blocked)
            {
                issues.Add(new PlanningIssue("DependencyCycle", $"Task '{task.Title}' is blocked by a dependency cycle.", task.Id));
            }

            ordered.AddRange(blocked.OrderByDescending(WeightTask).ThenBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase));
        }

        var cursor = new DateTimeOffset(request.AnchorDate.UtcDateTime.Date, TimeSpan.Zero);
        var suggestions = new List<PlanningSuggestion>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var task = ordered[i];
            var estimatedDays = EstimateDays(task);
            var start = Max(cursor, MaxPredecessorEnd(task, suggestions, dependencies));
            var end = start.AddDays(Math.Max(estimatedDays - 1, 0));
            var score = WeightTask(task) - i * 0.01;
            suggestions.Add(new PlanningSuggestion(
                task.Id,
                i + 1,
                Math.Round(score, 2),
                start,
                end,
                BuildReason(task, dependencies.Count(x => x.SuccessorId == task.Id), estimatedDays)));
            cursor = end.AddDays(1);
        }

        var summary = suggestions.Count == 0
            ? "No active tasks were available for planning."
            : $"Generated {suggestions.Count} rule-based planning suggestions.";
        return new PlanningResponse("rule", summary, suggestions, issues);
    }

    private static double WeightTask(PlanningTaskInput task)
    {
        var priorityWeight = task.Priority switch
        {
            TaskPriority.Critical => 100,
            TaskPriority.High => 75,
            TaskPriority.Normal => 50,
            TaskPriority.Low => 25,
            _ => 10,
        };
        var statusWeight = task.Status switch
        {
            TaskStatus.InProgress => 20,
            TaskStatus.Review => 12,
            TaskStatus.Todo => 8,
            TaskStatus.Backlog => 0,
            _ => 0,
        };
        var dueWeight = task.EndDate is null
            ? 0
            : Math.Max(0, 30 - (task.EndDate.Value.Date - DateTimeOffset.UtcNow.Date).TotalDays);
        return priorityWeight + statusWeight + dueWeight;
    }

    private static int EstimateDays(PlanningTaskInput task)
    {
        if (task.StartDate is not null && task.EndDate is not null)
        {
            return Math.Max(1, (int)Math.Ceiling((task.EndDate.Value.Date - task.StartDate.Value.Date).TotalDays) + 1);
        }

        if (task.EstimatedHours is not null && task.EstimatedHours > 0)
        {
            return Math.Max(1, (int)Math.Ceiling(task.EstimatedHours.Value / 6d));
        }

        return task.Priority switch
        {
            TaskPriority.Critical => 2,
            TaskPriority.High => 2,
            _ => 1,
        };
    }

    private static DateTimeOffset MaxPredecessorEnd(PlanningTaskInput task, IReadOnlyList<PlanningSuggestion> suggestions, IReadOnlyList<PlanningDependencyInput> dependencies)
    {
        var predecessorIds = dependencies.Where(x => x.SuccessorId == task.Id).Select(x => x.PredecessorId).ToHashSet();
        var predecessorEnd = suggestions
            .Where(x => predecessorIds.Contains(x.TaskId) && x.SuggestedEndDate is not null)
            .Select(x => x.SuggestedEndDate!.Value.AddDays(1))
            .DefaultIfEmpty(DateTimeOffset.MinValue)
            .Max();
        return predecessorEnd;
    }

    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right)
        => left >= right ? left : right;

    private static string BuildReason(PlanningTaskInput task, int predecessorCount, int estimatedDays)
    {
        var dependencyText = predecessorCount == 0 ? "no blocking dependencies" : $"{predecessorCount} predecessor dependency";
        return $"Priority {task.Priority}, status {task.Status}, {dependencyText}, estimated {estimatedDays} day(s).";
    }
}
