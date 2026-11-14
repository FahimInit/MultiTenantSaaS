using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Exceptions;
using SaaS.Domain.AccessControl;

namespace SaaS.Application.Modules.Roles;

/// <summary>
/// Prevents non-owner actors from elevating their own or another user's privileges beyond their own permissions.
/// </summary>
internal static class PermissionHierarchyGuard
{
    public static void EnsureCallerCanAssignRole(ITenantContext caller, TenantRole targetRole)
    {
        if (caller.RoleCode == SystemRoleCodes.OrganizationOwner)
        {
            return;
        }

        if (targetRole.Code == SystemRoleCodes.OrganizationOwner)
        {
            throw new ForbiddenAppException(
                "Only an Organization Owner can assign or modify the Organization Owner role.",
                "privilege_escalation_denied");
        }

        var unheld = targetRole.Permissions
            .Select(p => p.PermissionCode)
            .Where(code => !caller.HasPermission(code))
            .ToList();

        if (unheld.Count > 0)
        {
            throw new ForbiddenAppException(
                $"You cannot assign a role containing permissions you do not hold: {string.Join(", ", unheld)}.",
                "privilege_escalation_denied");
        }
    }

    public static void EnsureCallerCanGrantPermissions(
        ITenantContext caller,
        IEnumerable<string> requestedPermissions)
    {
        if (caller.RoleCode == SystemRoleCodes.OrganizationOwner)
        {
            return;
        }

        var unheld = requestedPermissions
            .Where(p => !caller.HasPermission(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (unheld.Count > 0)
        {
            throw new ForbiddenAppException(
                $"You cannot grant permissions you do not possess: {string.Join(", ", unheld)}.",
                "privilege_escalation_denied");
        }
    }

    public static void EnsureCallerCanModifyMemberWithCurrentRole(
        ITenantContext caller,
        string targetMemberCurrentRoleCode)
    {
        if (caller.RoleCode != SystemRoleCodes.OrganizationOwner &&
            targetMemberCurrentRoleCode == SystemRoleCodes.OrganizationOwner)
        {
            throw new ForbiddenAppException(
                "Only an Organization Owner can modify or remove another Organization Owner.",
                "privilege_escalation_denied");
        }
    }
}
