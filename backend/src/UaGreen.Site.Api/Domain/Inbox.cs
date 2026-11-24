namespace UaGreen.Site.Api.Domain;

/// Job application from the vacancy form (or the generic "didn't find a vacancy" form).
/// An outbox row: CrmSync sends it to Gospodar HR as a candidate; HR works it there.
public sealed class Application : ICrmLinked
{
    public Guid Id { get; set; }
    public Guid? VacancyId { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public string? Message { get; set; }
    /// Private file until Gospodar has it; deleted (null) once the candidate is created.
    public string? CvKey { get; set; }
    public string? CvFileName { get; set; }
    public DateTime CreatedAt { get; set; }

    /// The Gospodar HR candidate this application became; null until delivered.
    public Guid? GospodarCandidateId { get; set; }
    public DateTime? GospodarSyncedAt { get; set; }
    public string? GospodarError { get; set; }
    public int GospodarAttempts { get; set; }
    public DateTime? GospodarLastAttemptAt { get; set; }

    // Same retry/backoff bookkeeping as CRM leads (CrmSync.Due), under HR-named columns.
    Guid? ICrmLinked.CrmDealId { get => GospodarCandidateId; set => GospodarCandidateId = value; }
    DateTime? ICrmLinked.CrmSyncedAt { get => GospodarSyncedAt; set => GospodarSyncedAt = value; }
    string? ICrmLinked.CrmError { get => GospodarError; set => GospodarError = value; }
    int ICrmLinked.CrmAttempts { get => GospodarAttempts; set => GospodarAttempts = value; }
    DateTime? ICrmLinked.CrmLastAttemptAt { get => GospodarLastAttemptAt; set => GospodarLastAttemptAt = value; }
}

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

    /// The Gospodar CRM deal this request became (CrmSync); null until pushed.
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
