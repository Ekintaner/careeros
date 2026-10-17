namespace CareerOS.Application.Dashboard;
public sealed record DashboardApplication(Guid Id, string Title, string Company, string Status);
public sealed record DashboardInterview(Guid Id, string Title, string Company, DateTime StartUtc, DateTime EndUtc);
public sealed record DashboardTask(Guid Id, string Title, DateTime? DueUtc, string Priority);
public sealed record DashboardView(DateTime AsOfUtc, int TotalApplications, int ActiveApplications, int Offers, int SavedJobs,
    int OpenTasks, int OverdueTasks, int UpcomingInterviews, IReadOnlyList<DashboardApplication> RecentApplications,
    IReadOnlyList<DashboardInterview> NextInterviews, IReadOnlyList<DashboardTask> NextTasks);
