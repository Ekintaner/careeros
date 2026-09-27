using CareerOS.Domain.Common;

namespace CareerOS.Domain.Opportunities;

public sealed class Job : IAuditedEntity
{
    private Job() { }
    public Job(Company company, string title)
    {
        Id = Guid.NewGuid();
        UserId = company.UserId;
        CompanyId = company.Id;
        Rename(title);
    }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid CompanyId { get; private set; }
    public string Title { get; private set; } = "";
    public string? Description { get; private set; }
    public string? Location { get; private set; }
    public WorkMode? WorkMode { get; private set; }
    public EmploymentType? EmploymentType { get; private set; }
    public SeniorityLevel? SeniorityLevel { get; private set; }
    public string? Source { get; private set; }
    public string? SourceUrl { get; private set; }
    public decimal? SalaryMin { get; private set; }
    public decimal? SalaryMax { get; private set; }
    public string? SalaryCurrency { get; private set; }
    public SalaryPeriod? SalaryPeriod { get; private set; }
    public DateTime? PostedAtUtc { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void Rename(string title) => Title = Rules.Required(title, 200, nameof(title));

    public void ChangeCompany(Company company)
    {
        Rules.SameOwner(UserId, company.UserId);
        CompanyId = company.Id;
    }

    public void SetArchived(bool archived, TimeProvider clock)
    {
        ArchivedAtUtc = archived ? ArchivedAtUtc ?? clock.GetUtcNow().UtcDateTime : null;
    }

    public void Describe(string? description, string? location, WorkMode? workMode, EmploymentType? employmentType, SeniorityLevel? seniorityLevel)
    {
        var validDescription = Rules.Optional(description, 50000, nameof(description));
        var validLocation = Rules.Optional(location, 200, nameof(location));
        if (workMode.HasValue) Rules.EnumValue(workMode.Value);
        if (employmentType.HasValue) Rules.EnumValue(employmentType.Value);
        if (seniorityLevel.HasValue) Rules.EnumValue(seniorityLevel.Value);
        Description = validDescription;
        Location = validLocation;
        WorkMode = workMode;
        EmploymentType = employmentType;
        SeniorityLevel = seniorityLevel;
    }

    public void SetSource(string? source, string? url)
    {
        var validSource = Rules.Optional(source, 120, nameof(source));
        var validUrl = Rules.Url(url);
        Source = validSource;
        SourceUrl = validUrl;
    }

    public void SetSalary(decimal? minimum, decimal? maximum, string? currency, SalaryPeriod? period)
    {
        if (minimum is < 0 or > 999999999999.99m || maximum is < 0 or > 999999999999.99m
            || (minimum.HasValue && decimal.Round(minimum.Value, 2) != minimum)
            || (maximum.HasValue && decimal.Round(maximum.Value, 2) != maximum)
            || (minimum.HasValue && maximum.HasValue && minimum > maximum))
            throw new DomainValidationException("Salary must be nonnegative, within numeric(14,2), and ordered.");
        var hasAmount = minimum.HasValue || maximum.HasValue;
        var code = currency?.Trim().ToUpperInvariant();
        if (hasAmount && (code is not { Length: 3 } || code.Any(c => c is < 'A' or > 'Z') || !period.HasValue))
            throw new DomainValidationException("Salary requires a three-letter currency code and pay period.");
        if (!hasAmount && (currency is not null || period.HasValue))
            throw new DomainValidationException("Currency and pay period require a salary amount.");
        if (period.HasValue) Rules.EnumValue(period.Value);
        SalaryMin = minimum;
        SalaryMax = maximum;
        SalaryCurrency = code;
        SalaryPeriod = period;
    }

    public void SetPostingWindow(DateTime? postedAtUtc, DateTime? expiresAtUtc)
    {
        Rules.Utc(postedAtUtc);
        Rules.Utc(expiresAtUtc);
        if (postedAtUtc.HasValue && expiresAtUtc < postedAtUtc)
            throw new DomainValidationException("Expiry cannot precede posting.");
        PostedAtUtc = postedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }
}
