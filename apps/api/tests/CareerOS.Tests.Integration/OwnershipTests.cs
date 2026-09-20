using System.Collections;
using CareerOS.Application.Authentication;
using CareerOS.Domain.Accounts;
using CareerOS.Domain.Opportunities;
using CareerOS.Domain.Skills;
using CareerOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CareerOS.Tests.Integration;

[Collection("PostgreSQL")]
public sealed class OwnershipTests(PostgreSqlFixture fixture)
{
    [PostgreSqlFact]
    public async Task EveryOwnedEntityIsScopedAndForeignWritesFail()
    {
        await using var database = await fixture.CreateDatabaseAsync(); await using var db = database.Context(); await db.Database.MigrateAsync();
        var a = await PostgreSqlTests.AddGraph(db); var b = await PostgreSqlTests.AddGraph(db); db.ChangeTracker.Clear();
        var owned = new OwnedData(db, new Current(a.User.Id));
        var types = db.Model.GetEntityTypes().Where(x => x.FindProperty("UserId") is not null && x.ClrType != typeof(RefreshToken) && x.ClrType != typeof(PasswordResetToken)).Select(x => x.ClrType).ToArray();
        Assert.Equal(15, types.Length);
        foreach (var type in types)
        {
            var query = (IEnumerable)typeof(OwnedData).GetMethod("Query")!.MakeGenericMethod(type).Invoke(owned, null)!;
            var rows = query.Cast<object>().ToArray(); Assert.Single(rows);
            Assert.All(rows, row => Assert.Equal(a.User.Id, type.GetProperty("UserId")!.GetValue(row)));
        }
        Assert.Null(await owned.Query<Company>().SingleOrDefaultAsync(x => x.Id == b.Company.Id));
        var ownCompany = await owned.Query<Company>().SingleAsync(); ownCompany.Rename("Allowed"); await owned.SaveChangesAsync();
        db.ChangeTracker.Clear(); var foreign = await db.Companies.SingleAsync(x => x.Id == b.Company.Id); foreign.Rename("Denied");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => owned.SaveChangesAsync());
        Assert.Throws<UnauthorizedAccessException>(() => owned.Remove(foreign));
        Assert.Throws<UnauthorizedAccessException>(() => owned.Add(new Company(b.User.Id, "Denied")));
        Assert.Throws<InvalidOperationException>(() => owned.Query<User>()); Assert.Throws<InvalidOperationException>(() => owned.Query<Skill>());
        var anonymous = new OwnedData(db, new Anonymous()); Assert.Throws<UnauthorizedAccessException>(() => anonymous.Query<Company>());
        await using var check = database.Context(); Assert.Equal("Example", (await check.Companies.SingleAsync(x => x.Id == b.Company.Id)).Name);
    }
    private sealed class Current(Guid id) : ICurrentUser { public Guid UserId => id; }
    private sealed class Anonymous : ICurrentUser { public Guid UserId => throw new UnauthorizedAccessException(); }
}
