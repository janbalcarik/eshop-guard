namespace EshopGuard.Data.Entities.Iam;

/// <summary>Values of <c>TenantStatus</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum TenantStatus
{
    Active,
    Suspended,
    Deleted,
}

/// <summary>Values of <c>PartnerKind</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum PartnerKind
{
    Agency,
    Lawyer,
    Certifier,
}

/// <summary>Values of <c>MembershipRole</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum MembershipRole
{
    Owner,
    Admin,
    Editor,
    Viewer,
}

/// <summary>Values of <c>UserTokenPurpose</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum UserTokenPurpose
{
    Reset,
    Confirm,
    MagicLink,
}

/// <summary>Verification of the VAT id of a tenant by Stripe (change 12).</summary>
public enum TaxIdStatus
{
    None,
    Pending,
    Verified,
    Unverified,
}
