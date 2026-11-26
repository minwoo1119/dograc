using Dograc.Core.Models;

namespace Dograc.Core.Models;

/// <summary>
/// Tabs available inside the collapsible right Context Panel.
/// </summary>
public enum ContextPanelTab
{
    Documents,
    Evidence,
    Preview
}

/// <summary>
/// Lightweight workspace item displayed in the Shell sidebar selector.
/// </summary>
public sealed record WorkspaceSummary(Guid Id, string Name);

/// <summary>
/// Lightweight conversation item displayed in the Shell sidebar list.
/// </summary>
public sealed record ConversationSummary(Guid Id, Guid WorkspaceId, string Title, DateTimeOffset UpdatedAt);
