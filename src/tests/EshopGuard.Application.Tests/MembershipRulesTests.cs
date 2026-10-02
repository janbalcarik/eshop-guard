using EshopGuard.Application.Tenants;

namespace EshopGuard.Application.Tests;

/// <summary>Every combination of the caller's role, the target role and the action against the matrix of the design (change 9, task 7.7).</summary>
public sealed class MembershipRulesTests
{
    private static readonly TenantRole[] Roles = [TenantRole.Viewer, TenantRole.Editor, TenantRole.Admin, TenantRole.Owner];

    // The matrix of the design, written out as a table (row: caller; what he may do).
    private static readonly Dictionary<TenantRole, (TenantRole[] RemoveOthers, TenantRole[] Invite, (TenantRole From, TenantRole To)[] Change, bool Transfer)> Matrix = new()
    {
        [TenantRole.Viewer] = ([], [], [], false),
        [TenantRole.Editor] = ([], [], [], false),
        [TenantRole.Admin] = (
            [TenantRole.Editor, TenantRole.Viewer],
            [TenantRole.Editor, TenantRole.Viewer],
            [(TenantRole.Editor, TenantRole.Viewer), (TenantRole.Viewer, TenantRole.Editor), (TenantRole.Editor, TenantRole.Editor), (TenantRole.Viewer, TenantRole.Viewer)],
            false),
        [TenantRole.Owner] = (
            [TenantRole.Viewer, TenantRole.Editor, TenantRole.Admin, TenantRole.Owner],
            [TenantRole.Viewer, TenantRole.Editor, TenantRole.Admin],
            [.. Roles.SelectMany(from => new[] { TenantRole.Viewer, TenantRole.Editor, TenantRole.Admin }.Select(to => (from, to)))],
            true),
    };

    public static TheoryData<TenantRole, TenantRole, TenantRole> AllCombinations()
    {
        var data = new TheoryData<TenantRole, TenantRole, TenantRole>();
        foreach (var caller in Roles)
        {
            foreach (var target in Roles)
            {
                foreach (var wanted in Roles)
                {
                    data.Add(caller, target, wanted);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void ChangeOfRole_FollowsTheMatrix(TenantRole caller, TenantRole target, TenantRole wanted) =>
        Assert.Equal(Matrix[caller].Change.Contains((target, wanted)), MembershipRules.CanChangeRole(caller, target, wanted));

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void RemovalOfOthers_FollowsTheMatrix_LeavingIsAlwaysAllowed(TenantRole caller, TenantRole target, TenantRole unused)
    {
        _ = unused;
        Assert.Equal(Matrix[caller].RemoveOthers.Contains(target), MembershipRules.CanRemove(caller, target, self: false));
        Assert.True(MembershipRules.CanRemove(caller, target, self: true));
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void Invitation_FollowsTheMatrix_NeverAnOwner(TenantRole caller, TenantRole role, TenantRole unused)
    {
        _ = unused;
        Assert.Equal(Matrix[caller].Invite.Contains(role), MembershipRules.CanInvite(caller, role));
    }

    [Fact]
    public void OnlyTheOwner_TransfersTheOwnership()
    {
        Assert.All(Roles, r => Assert.Equal(Matrix[r].Transfer, MembershipRules.CanTransferOwnership(r)));
    }

    [Fact]
    public void Roles_AreOrdered_AndHaveTheirCodes()
    {
        Assert.True(TenantRole.Viewer < TenantRole.Editor && TenantRole.Editor < TenantRole.Admin && TenantRole.Admin < TenantRole.Owner);
        Assert.Equal(["viewer", "editor", "admin", "owner"], Roles.Select(r => r.Code()));
        Assert.All(Roles, r => Assert.Equal(r, TenantRoles.FromMembership(r.ToMembership())));
    }
}
