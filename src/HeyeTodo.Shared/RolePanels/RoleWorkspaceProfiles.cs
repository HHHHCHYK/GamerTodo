using HeyeTodo.Shared.Contracts.Tasks;
using HeyeTodo.Shared.Enums;
using TaskStatus = HeyeTodo.Shared.Enums.TaskStatus;

namespace HeyeTodo.Shared.RolePanels;

public enum RoleFieldKind
{
    Text,
    Number,
    Choice,
    Multiline,
}

public sealed record RoleFieldDefinition(
    RoleType Role,
    string Key,
    string LabelKey,
    RoleFieldKind Kind,
    string? PlaceholderKey = null,
    IReadOnlyList<string>? Options = null);

public sealed record RoleDashboardCard(RoleType Role, string TitleKey, string DescriptionKey);

public sealed record RoleTaskAction(RoleType Role, string LabelKey, string TargetStatus, string DescriptionKey);

public sealed record RoleWorkspaceProfile(
    RoleType Role,
    string NameKey,
    IReadOnlyList<RoleFieldDefinition> Fields,
    IReadOnlyList<RoleTaskAction> Actions,
    IReadOnlyList<RoleDashboardCard> DashboardCards,
    TaskSortField DefaultSort,
    SortDirection DefaultSortDirection,
    TaskStatus? DefaultStatusFilter = null);

public static class RoleWorkspaceProfiles
{
    public static IReadOnlyList<RoleWorkspaceProfile> All { get; } =
    [
        new RoleWorkspaceProfile(
            RoleType.Producer,
            "Roles.Producer",
            [
                new RoleFieldDefinition(RoleType.Producer, "producer.milestone", "RoleFields.Producer.Milestone", RoleFieldKind.Text),
                new RoleFieldDefinition(RoleType.Producer, "producer.risk", "RoleFields.Producer.Risk", RoleFieldKind.Choice, Options: ["Low", "Medium", "High"]),
                new RoleFieldDefinition(RoleType.Producer, "producer.ownerNote", "RoleFields.Producer.OwnerNote", RoleFieldKind.Multiline),
            ],
            [
                new RoleTaskAction(RoleType.Producer, "RoleActions.Producer.Prioritize", nameof(TaskStatus.Backlog), "RoleActions.Producer.Prioritize.Description"),
                new RoleTaskAction(RoleType.Producer, "RoleActions.Producer.Review", nameof(TaskStatus.Review), "RoleActions.Producer.Review.Description"),
            ],
            [
                new RoleDashboardCard(RoleType.Producer, "RoleDashboard.Producer.Schedule", "RoleDashboard.Producer.Schedule.Description"),
                new RoleDashboardCard(RoleType.Producer, "RoleDashboard.Producer.Risks", "RoleDashboard.Producer.Risks.Description"),
            ],
            TaskSortField.Priority,
            SortDirection.Descending),
        new RoleWorkspaceProfile(
            RoleType.Designer,
            "Roles.Designer",
            [
                new RoleFieldDefinition(RoleType.Designer, "design.featureArea", "RoleFields.Designer.FeatureArea", RoleFieldKind.Text),
                new RoleFieldDefinition(RoleType.Designer, "design.specLink", "RoleFields.Designer.SpecLink", RoleFieldKind.Text),
                new RoleFieldDefinition(RoleType.Designer, "design.acceptance", "RoleFields.Designer.Acceptance", RoleFieldKind.Multiline),
            ],
            [
                new RoleTaskAction(RoleType.Designer, "RoleActions.Designer.Spec", nameof(TaskStatus.Todo), "RoleActions.Designer.Spec.Description"),
                new RoleTaskAction(RoleType.Designer, "RoleActions.Designer.Validate", nameof(TaskStatus.Review), "RoleActions.Designer.Validate.Description"),
            ],
            [
                new RoleDashboardCard(RoleType.Designer, "RoleDashboard.Designer.Specs", "RoleDashboard.Designer.Specs.Description"),
                new RoleDashboardCard(RoleType.Designer, "RoleDashboard.Designer.Validation", "RoleDashboard.Designer.Validation.Description"),
            ],
            TaskSortField.Status,
            SortDirection.Ascending),
        new RoleWorkspaceProfile(
            RoleType.Artist,
            "Roles.Artist",
            [
                new RoleFieldDefinition(RoleType.Artist, "art.assetType", "RoleFields.Artist.AssetType", RoleFieldKind.Choice, Options: ["Concept", "Sprite", "Model", "Animation", "VFX"]),
                new RoleFieldDefinition(RoleType.Artist, "art.reference", "RoleFields.Artist.Reference", RoleFieldKind.Text),
                new RoleFieldDefinition(RoleType.Artist, "art.delivery", "RoleFields.Artist.Delivery", RoleFieldKind.Text),
            ],
            [
                new RoleTaskAction(RoleType.Artist, "RoleActions.Artist.Blockout", nameof(TaskStatus.InProgress), "RoleActions.Artist.Blockout.Description"),
                new RoleTaskAction(RoleType.Artist, "RoleActions.Artist.Polish", nameof(TaskStatus.Review), "RoleActions.Artist.Polish.Description"),
            ],
            [
                new RoleDashboardCard(RoleType.Artist, "RoleDashboard.Artist.Pipeline", "RoleDashboard.Artist.Pipeline.Description"),
                new RoleDashboardCard(RoleType.Artist, "RoleDashboard.Artist.Deliverables", "RoleDashboard.Artist.Deliverables.Description"),
            ],
            TaskSortField.EndDate,
            SortDirection.Ascending),
        new RoleWorkspaceProfile(
            RoleType.Programmer,
            "Roles.Programmer",
            [
                new RoleFieldDefinition(RoleType.Programmer, "code.area", "RoleFields.Programmer.Area", RoleFieldKind.Text),
                new RoleFieldDefinition(RoleType.Programmer, "code.branch", "RoleFields.Programmer.Branch", RoleFieldKind.Text),
                new RoleFieldDefinition(RoleType.Programmer, "code.testPlan", "RoleFields.Programmer.TestPlan", RoleFieldKind.Multiline),
            ],
            [
                new RoleTaskAction(RoleType.Programmer, "RoleActions.Programmer.Implement", nameof(TaskStatus.InProgress), "RoleActions.Programmer.Implement.Description"),
                new RoleTaskAction(RoleType.Programmer, "RoleActions.Programmer.CodeReview", nameof(TaskStatus.Review), "RoleActions.Programmer.CodeReview.Description"),
            ],
            [
                new RoleDashboardCard(RoleType.Programmer, "RoleDashboard.Programmer.Implementation", "RoleDashboard.Programmer.Implementation.Description"),
                new RoleDashboardCard(RoleType.Programmer, "RoleDashboard.Programmer.Quality", "RoleDashboard.Programmer.Quality.Description"),
            ],
            TaskSortField.Priority,
            SortDirection.Descending),
        new RoleWorkspaceProfile(
            RoleType.SoundDesigner,
            "Roles.SoundDesigner",
            [
                new RoleFieldDefinition(RoleType.SoundDesigner, "sound.cue", "RoleFields.Sound.Cue", RoleFieldKind.Text),
                new RoleFieldDefinition(RoleType.SoundDesigner, "sound.mood", "RoleFields.Sound.Mood", RoleFieldKind.Text),
                new RoleFieldDefinition(RoleType.SoundDesigner, "sound.mixNotes", "RoleFields.Sound.MixNotes", RoleFieldKind.Multiline),
            ],
            [
                new RoleTaskAction(RoleType.SoundDesigner, "RoleActions.Sound.Record", nameof(TaskStatus.InProgress), "RoleActions.Sound.Record.Description"),
                new RoleTaskAction(RoleType.SoundDesigner, "RoleActions.Sound.Mix", nameof(TaskStatus.Review), "RoleActions.Sound.Mix.Description"),
            ],
            [
                new RoleDashboardCard(RoleType.SoundDesigner, "RoleDashboard.Sound.Cues", "RoleDashboard.Sound.Cues.Description"),
                new RoleDashboardCard(RoleType.SoundDesigner, "RoleDashboard.Sound.Mix", "RoleDashboard.Sound.Mix.Description"),
            ],
            TaskSortField.EndDate,
            SortDirection.Ascending),
    ];

    public static IReadOnlyList<RoleWorkspaceProfile> ForRoles(RoleType roles)
        => All.Where(x => roles.HasFlag(x.Role)).ToList();

    public static RoleWorkspaceProfile? Find(RoleType role)
        => All.FirstOrDefault(x => x.Role == role);
}
