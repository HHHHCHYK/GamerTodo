using System;

namespace HeyeTodo.Shared.Contracts.Tasks;

public sealed record CreateProjectRequest(
    string Name,
    string? Description);

public sealed record UpdateProjectRequest(
    Guid Id,
    string Name,
    string? Description);
