using System.Text.Json.Serialization;
using CareerOS.Domain.Common;
using CareerOS.Domain.Opportunities;

namespace CareerOS.Application.Opportunities;

public sealed record Page<T>(IReadOnlyList<T> Items, [property: JsonPropertyName("page")] int PageNumber, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
public sealed class OpportunityNotFoundException : Exception;
public sealed class OpportunityConflictException(string message) : Exception(message);
public static class OpportunityValidation
{
    public static T? EnumValue<T>(string? value) where T : struct, Enum => value is null ? null :
        Enum.GetNames<T>().Contains(value) ? Enum.Parse<T>(value) : throw new DomainValidationException($"Choose a valid {typeof(T).Name}.");
    public static string? Text(string? value, int max = 200)
    {
        if (value?.Length > max) throw new DomainValidationException($"Search and filter values must be at most {max} characters.");
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
public sealed record ListQuery(int Page = 1, int PageSize = 20, string? Search = null, string? Location = null,
    string? Sort = null, string? Industry = null, Guid? CompanyId = null, string? WorkMode = null,
    string? EmploymentType = null, string? SeniorityLevel = null, string? Source = null, string? State = null)
{
    public void Validate(bool jobs)
    {
        if (Page is < 1 or > 10000 || PageSize is < 1 or > 100) throw new DomainValidationException("Use page 1–10,000 and page size 1–100.");
        foreach (var text in new[] { Search, Location, Industry, Source }) OpportunityValidation.Text(text);
        var sorts = jobs ? new[] { "newest", "oldest", "posted", "company", "title" } : new[] { "name", "newest", "oldest" };
        if (Sort is not null && !sorts.Contains(Sort)) throw new DomainValidationException("Choose a supported sort order.");
        if (State is not null && State is not ("active" or "archived" or "all")) throw new DomainValidationException("Choose active, archived or all jobs.");
        OpportunityValidation.EnumValue<Domain.Opportunities.WorkMode>(WorkMode);
        OpportunityValidation.EnumValue<Domain.Opportunities.EmploymentType>(EmploymentType);
        OpportunityValidation.EnumValue<Domain.Opportunities.SeniorityLevel>(SeniorityLevel);
    }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CompanyInput(string Name, string? Website = null, string? Industry = null, string? Location = null, string? Description = null, uint Version = 0)
{
    public Company Create(Guid owner)
    {
        var company = new Company(owner, Name);
        company.UpdateDetails(Website, Industry, Location, Description);
        return company;
    }
    public void Apply(Company company)
    {
        var valid = Create(company.UserId);
        company.Rename(valid.Name);
        company.UpdateDetails(valid.Website, valid.Industry, valid.Location, valid.Description);
    }
}
public sealed record CompanySummary(Guid Id, string Name, string? Industry, string? Location, int JobCount, uint Version);
public sealed record CompanyView(Guid Id, string Name, string? Website, string? Industry, string? Location, string? Description,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc, uint Version)
{
    public static CompanyView From(Company c, uint version) => new(c.Id, c.Name, c.Website, c.Industry, c.Location, c.Description, c.CreatedAtUtc, c.UpdatedAtUtc, version);
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record JobInput(Guid CompanyId, string Title, string? Description = null, string? Location = null,
    string? WorkMode = null, string? EmploymentType = null, string? SeniorityLevel = null, string? Source = null, string? SourceUrl = null,
    decimal? SalaryMin = null, decimal? SalaryMax = null, string? SalaryCurrency = null, string? SalaryPeriod = null,
    DateTime? PostedAtUtc = null, DateTime? ExpiresAtUtc = null, uint Version = 0)
{
    public Job Create(Company company)
    {
        var job = new Job(company, Title);
        job.Describe(Description, Location, OpportunityValidation.EnumValue<Domain.Opportunities.WorkMode>(WorkMode),
            OpportunityValidation.EnumValue<Domain.Opportunities.EmploymentType>(EmploymentType), OpportunityValidation.EnumValue<Domain.Opportunities.SeniorityLevel>(SeniorityLevel));
        job.SetSource(Source, SourceUrl);
        job.SetSalary(SalaryMin, SalaryMax, SalaryCurrency, OpportunityValidation.EnumValue<Domain.Opportunities.SalaryPeriod>(SalaryPeriod));
        job.SetPostingWindow(PostedAtUtc, ExpiresAtUtc);
        return job;
    }
    public void Apply(Job job, Company company)
    {
        var valid = Create(company);
        job.ChangeCompany(company);
        job.Rename(valid.Title);
        job.Describe(valid.Description, valid.Location, valid.WorkMode, valid.EmploymentType, valid.SeniorityLevel);
        job.SetSource(valid.Source, valid.SourceUrl);
        job.SetSalary(valid.SalaryMin, valid.SalaryMax, valid.SalaryCurrency, valid.SalaryPeriod);
        job.SetPostingWindow(valid.PostedAtUtc, valid.ExpiresAtUtc);
    }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ArchiveInput(bool Archived, uint Version);
public sealed record JobSummary(Guid Id, Guid CompanyId, string CompanyName, string Title, string? Location, string? WorkMode,
    string? EmploymentType, string? SeniorityLevel, string? Source, DateTime CreatedAtUtc, DateTime? PostedAtUtc, DateTime? ArchivedAtUtc, uint Version);
public sealed record JobView(Guid Id, Guid CompanyId, string CompanyName, string Title, string? Description, string? Location,
    string? WorkMode, string? EmploymentType, string? SeniorityLevel, string? Source, string? SourceUrl,
    decimal? SalaryMin, decimal? SalaryMax, string? SalaryCurrency, string? SalaryPeriod, DateTime? PostedAtUtc, DateTime? ExpiresAtUtc,
    DateTime? ArchivedAtUtc, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, uint Version)
{
    public static JobView From(Job j, string company, uint version) => new(j.Id, j.CompanyId, company, j.Title, j.Description, j.Location,
        j.WorkMode?.ToString(), j.EmploymentType?.ToString(), j.SeniorityLevel?.ToString(), j.Source, j.SourceUrl, j.SalaryMin, j.SalaryMax,
        j.SalaryCurrency, j.SalaryPeriod?.ToString(), j.PostedAtUtc, j.ExpiresAtUtc, j.ArchivedAtUtc, j.CreatedAtUtc, j.UpdatedAtUtc, version);
}
