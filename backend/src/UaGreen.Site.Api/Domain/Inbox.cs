namespace UaGreen.Site.Api.Domain;

/// Job application from the vacancy form (or the generic "didn't find a vacancy" form).
public sealed class Application
{
    public Guid Id { get; set; }
    public Guid? VacancyId { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public string? Message { get; set; }
    public string? CvKey { get; set; }
    public string? CvFileName { get; set; }
    public bool IsRead { get; set; }
    public ApplicationStatus Status { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum ApplicationStatus { New, Reviewed, Interview, Rejected, Hired }

public sealed class ContactRequest : ICrmLinked
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public string Message { get; set; } = "";
    public bool IsRead { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }

    public Guid? CrmDealId { get; set; }
    public DateTime? CrmSyncedAt { get; set; }
    public string? CrmError { get; set; }
    public int CrmAttempts { get; set; }
    public DateTime? CrmLastAttemptAt { get; set; }
}

/// Back-office roles. Authentication is Gospodar's; these are assigned here by an Admin.
public enum SiteRole { Admin, Sales, Editor, Hr }

public sealed class SiteUser
{
    /// Gospodar user id (the JWT `sub`).
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public List<SiteRole> Roles { get; set; } = [];
    public DateTime UpdatedAt { get; set; }
}
