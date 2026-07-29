using Application.Contracts.Persistence;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Data;

public class DatabaseSeeder
{
    public static async Task SeedAsync(IDataContext db, CancellationToken ct)
    {
        await SeedRoles(db, ct);
        await SeedAdminUser(db, ct);
        await SeedCategories(db, ct);
    }

    private static async Task SeedRoles(IDataContext db, CancellationToken ct)
    {
        if (await db.Roles.AnyAsync(ct))
            return;

        db.Roles.AddRange(
            new Role("Admin"),
            new Role("Manager"),
            new Role("Cashier")
        );

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedAdminUser(IDataContext db, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(ct))
            return;

        var adminRole = await db.Roles
            .FirstAsync(r => r.Name == "Admin", ct);

        var user = new User(
            username: "admin",
            password: "admin123",
            firstname: "System",
            lastname: "Administrator",
            patronymic: null,
            roleIds: new[] { adminRole.Id }
        );

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedCategories(IDataContext db, CancellationToken ct)
    {
        if (await db.Categories.AnyAsync(ct))
            return;

        db.Categories.AddRange(
            new Category("Электроника", null),
            new Category("Продукты питания", null),
            new Category("Одежда", null),
            new Category("Канцтовары", null)
        );

        await db.SaveChangesAsync(ct);
    }
}