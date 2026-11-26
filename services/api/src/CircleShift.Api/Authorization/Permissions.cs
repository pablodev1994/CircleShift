using CircleShift.Domain.Members;

namespace CircleShift.Api.Authorization;

/// <summary>
/// The permission matrix from <c>docs/product/personas.md</c>. Each permission
/// is also an authorization policy name, so controllers say what they need
/// (<c>[Authorize(Policy = Permissions.RolesManage)]</c>) rather than which
/// roles happen to have it.
/// </summary>
public static class Permissions
{
    public const string ProfileEdit = "profile.edit";
    public const string PostsWrite = "posts.write";
    public const string ExchangesParticipate = "exchanges.participate";
    public const string WalletViewOwn = "wallet.view_own";
    public const string CirclesManage = "circles.manage";
    public const string CircleSessionsApprove = "circles.approve_sessions";
    public const string ModerationReview = "moderation.review";
    public const string ModerationAppeals = "moderation.appeals";
    public const string CatalogManage = "catalog.manage";
    public const string PoliciesManage = "policies.manage";
    public const string RolesManage = "roles.manage";

    /// <summary>Every adult member, whatever their roles.</summary>
    public static IReadOnlyList<string> Baseline { get; } = [ProfileEdit, PostsWrite, ExchangesParticipate, WalletViewOwn];

    public static IReadOnlyDictionary<CommunityRole, IReadOnlyList<string>> ByRole { get; } =
        new Dictionary<CommunityRole, IReadOnlyList<string>>
        {
            [CommunityRole.Coordinator] = [CirclesManage, CircleSessionsApprove],
            [CommunityRole.Moderator] = [ModerationReview, ModerationAppeals],
            [CommunityRole.Admin] = [CirclesManage, ModerationReview, ModerationAppeals, CatalogManage, PoliciesManage, RolesManage],
        };

    public static IReadOnlyList<string> All { get; } =
        [.. Baseline.Concat(ByRole.Values.SelectMany(permissions => permissions)).Distinct(StringComparer.Ordinal)];

    public static IReadOnlyList<string> For(IEnumerable<CommunityRole> roles) =>
        [.. Baseline.Concat(roles.SelectMany(role => ByRole[role])).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
}
