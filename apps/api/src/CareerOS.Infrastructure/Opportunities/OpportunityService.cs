using CareerOS.Application.Authentication;
using CareerOS.Application.Opportunities;
using CareerOS.Domain.Opportunities;
using CareerOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareerOS.Infrastructure.Opportunities;

public sealed class OpportunityService(CareerOSDbContext db, OwnedData owned, ICurrentUser current, TimeProvider clock)
{
    private uint Version(object entity) => (uint)db.Entry(entity).Property("Version").CurrentValue!;
    private void Expect(object entity, uint version)
    {
        if (version == 0 || Version(entity) != version) throw new OpportunityConflictException("This record changed. Reload the latest version before editing again.");
        db.Entry(entity).Property("Version").OriginalValue = version;
    }
    private static string? Pattern(string? value)
    {
        var text = OpportunityValidation.Text(value);
        return text is null ? null : "%" + text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
    }
    private async Task<Company> Company(Guid id, CancellationToken ct) => await owned.Query<Company>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new OpportunityNotFoundException();
    private async Task<Job> Job(Guid id, CancellationToken ct) => await owned.Query<Job>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new OpportunityNotFoundException();
    public async Task<Page<CompanySummary>> Companies(ListQuery query, CancellationToken ct)
    {
        query.Validate(false);
        var rows = owned.Query<Company>().AsNoTracking();
        var search = Pattern(query.Search); var location = Pattern(query.Location); var industry = Pattern(query.Industry);
        if (search is not null) rows = rows.Where(x => EF.Functions.ILike(x.Name, search, "\\"));
        if (location is not null) rows = rows.Where(x => x.Location != null && EF.Functions.ILike(x.Location, location, "\\"));
        if (industry is not null) rows = rows.Where(x => x.Industry != null && EF.Functions.ILike(x.Industry, industry, "\\"));
        var count = await rows.CountAsync(ct);
        var sorted = query.Sort switch { "newest" => rows.OrderByDescending(x => x.CreatedAtUtc), "oldest" => rows.OrderBy(x => x.CreatedAtUtc), _ => rows.OrderBy(x => x.Name) };
        var jobs = owned.Query<Job>();
        var items = await sorted.ThenBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new CompanySummary(x.Id, x.Name, x.Industry, x.Location, jobs.Count(j => j.CompanyId == x.Id), EF.Property<uint>(x, "Version"))).ToListAsync(ct);
        return new(items, query.Page, query.PageSize, count);
    }
    public async Task<CompanyView> GetCompany(Guid id, CancellationToken ct)
    { var c = await Company(id, ct); return CompanyView.From(c, Version(c)); }
    public async Task<CompanyView> SaveCompany(Guid? id, CompanyInput input, CancellationToken ct)
    {
        Company company;
        if (id.HasValue) { company = await Company(id.Value, ct); Expect(company, input.Version); input.Apply(company); }
        else { company = input.Create(current.UserId); owned.Add(company); }
        await owned.SaveChangesAsync(ct);
        return CompanyView.From(company, Version(company));
    }
    public async Task DeleteCompany(Guid id, uint version, CancellationToken ct)
    {
        var c = await Company(id, ct); Expect(c, version); owned.Remove(c); await owned.SaveChangesAsync(ct);
    }
    public async Task<Page<JobSummary>> Jobs(ListQuery query, CancellationToken ct)
    {
        query.Validate(true);
        var work = OpportunityValidation.EnumValue<WorkMode>(query.WorkMode);
        var employment = OpportunityValidation.EnumValue<EmploymentType>(query.EmploymentType);
        var seniority = OpportunityValidation.EnumValue<SeniorityLevel>(query.SeniorityLevel);
        var rows = from j in owned.Query<Job>().AsNoTracking() join c in owned.Query<Company>().AsNoTracking() on j.CompanyId equals c.Id select new { Job = j, Company = c.Name };
        var search = Pattern(query.Search); var location = Pattern(query.Location); var source = Pattern(query.Source);
        if (query.State is null or "active") rows = rows.Where(x => x.Job.ArchivedAtUtc == null);
        if (query.State == "archived") rows = rows.Where(x => x.Job.ArchivedAtUtc != null);
        if (query.CompanyId.HasValue) rows = rows.Where(x => x.Job.CompanyId == query.CompanyId);
        if (work.HasValue) rows = rows.Where(x => x.Job.WorkMode == work);
        if (employment.HasValue) rows = rows.Where(x => x.Job.EmploymentType == employment);
        if (seniority.HasValue) rows = rows.Where(x => x.Job.SeniorityLevel == seniority);
        if (location is not null) rows = rows.Where(x => x.Job.Location != null && EF.Functions.ILike(x.Job.Location, location, "\\"));
        if (source is not null) rows = rows.Where(x => x.Job.Source != null && EF.Functions.ILike(x.Job.Source, source, "\\"));
        if (search is not null) rows = rows.Where(x => EF.Functions.ILike(x.Job.Title, search, "\\") || EF.Functions.ILike(x.Company, search, "\\") || (x.Job.Location != null && EF.Functions.ILike(x.Job.Location, search, "\\")));
        var count = await rows.CountAsync(ct);
        var sorted = query.Sort switch
        {
            "oldest" => rows.OrderBy(x => x.Job.CreatedAtUtc),
            "posted" => rows.OrderBy(x => x.Job.PostedAtUtc == null).ThenByDescending(x => x.Job.PostedAtUtc),
            "company" => rows.OrderBy(x => x.Company),
            "title" => rows.OrderBy(x => x.Job.Title),
            _ => rows.OrderByDescending(x => x.Job.CreatedAtUtc)
        };
        var items = await sorted.ThenBy(x => x.Job.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new JobSummary(x.Job.Id, x.Job.CompanyId, x.Company, x.Job.Title, x.Job.Location,
                x.Job.WorkMode == null ? null : x.Job.WorkMode.ToString(), x.Job.EmploymentType == null ? null : x.Job.EmploymentType.ToString(),
                x.Job.SeniorityLevel == null ? null : x.Job.SeniorityLevel.ToString(), x.Job.Source, x.Job.CreatedAtUtc, x.Job.PostedAtUtc, x.Job.ArchivedAtUtc, EF.Property<uint>(x.Job, "Version"))).ToListAsync(ct);
        return new(items, query.Page, query.PageSize, count);
    }
    public async Task<JobView> GetJob(Guid id, CancellationToken ct)
    {
        var j = await Job(id, ct);
        var name = await owned.Query<Company>().Where(c => c.Id == j.CompanyId).Select(c => c.Name).SingleAsync(ct);
        return JobView.From(j, name, Version(j));
    }
    public async Task<JobView> SaveJob(Guid? id, JobInput input, CancellationToken ct)
    {
        // Resolve the target before any supplied relationship, so foreign target IDs always return 404.
        var job = id.HasValue ? await Job(id.Value, ct) : null;
        if (job is not null) Expect(job, input.Version);
        var company = await Company(input.CompanyId, ct);
        if (job is null) { job = input.Create(company); owned.Add(job); }
        else input.Apply(job, company);
        await owned.SaveChangesAsync(ct);
        return JobView.From(job, company.Name, Version(job));
    }
    public async Task<JobView> Archive(Guid id, ArchiveInput input, CancellationToken ct)
    {
        var job = await Job(id, ct); Expect(job, input.Version); job.SetArchived(input.Archived, clock); await owned.SaveChangesAsync(ct);
        return await GetJob(id, ct);
    }
    public async Task DeleteJob(Guid id, uint version, CancellationToken ct)
    { var job = await Job(id, ct); Expect(job, version); owned.Remove(job); await owned.SaveChangesAsync(ct); }
}
