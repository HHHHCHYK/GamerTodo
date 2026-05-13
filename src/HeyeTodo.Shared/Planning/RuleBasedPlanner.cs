using HeyeTodo.Shared.Contracts.Planning;
using HeyeTodo.Shared.Enums;
using TaskStatus = HeyeTodo.Shared.Enums.TaskStatus;

namespace HeyeTodo.Shared.Planning;

public static class RuleBasedPlanner
{
    public static PlanningResponse Plan(PlanningRequest request)
    {
        var issues = new List<PlanningIssue>();
        var anchor = request.AnchorDate ?? DateTimeOffset.UtcNow;
        var cursor = new DateTimeOffset(anchor.Year, anchor.Month, anchor.Day, 9, 0, 0, anchor.Offset);
        var planned = new List<PlannedTask>();

        var orderedTasks = request.Tasks
            .Where(x => x.Status is not TaskStatus.Done and not TaskStatus.Cancelled)
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.EndDate ?? DateTimeOffset.MaxValue)
            .ThenBy(x => x.Title)
            .ToList();

        foreach (var task in orderedTasks)
        {
            var hours = Math.Clamp(task.EstimatedHours ?? 4, 1, 8);
            var start = task.StartDate is { } explicitStart && explicitStart > cursor ? explicitStart : cursor;
            var end = start.AddHours(hours);

            planned.Add(new PlannedTask(task.Id, start, end, "Rule-based MVP ordering by priority, due date, and title."));
            cursor = end.AddMinutes(30);
        }

        var knownTaskIds = request.Tasks.Select(x => x.Id).ToHashSet();
        foreach (var dependency in request.Dependencies)
        {
            if (!knownTaskIds.Contains(dependency.PredecessorId) || !knownTaskIds.Contains(dependency.SuccessorId))
            {
                issues.Add(new PlanningIssue("DependencyTaskMissing", "A dependency references a task that is not part of the planning request."));
            }
        }

        return new PlanningResponse(
            "rule",
            planned.Count == 0 ? "No open tasks to plan." : $"Planned {planned.Count} open task(s).",
            planned,
            issues);
    }
}
