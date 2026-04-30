using System.Collections.Generic;

namespace HeyeTodo.Shared.Models;

public sealed class UserProfileModel
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PreferredLanguage { get; set; } = "zh-CN";
    public Dictionary<string, string> Metadata { get; set; } = new();
}
