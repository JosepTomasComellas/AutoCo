using AutoCo.Api.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace AutoCo.Api.Data;

public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext db, IConfiguration config)
    {
        var email   = config["Admin:Email"]    ?? "admin@autoco.cat";
        var password = config["Admin:Password"] ?? "Admin123!";
        var nom     = config["Admin:Nom"]      ?? "Administrador";
        var cognoms = config["Admin:Cognoms"]  ?? "";

        var admin = await db.Professors.FirstOrDefaultAsync(p => p.IsAdmin);

        if (admin is null)
        {
            // Primera arrencada: crear l'admin amb les credencials del .env
            db.Professors.Add(new Professor
            {
                Email        = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Nom          = nom,
                Cognoms      = cognoms,
                IsAdmin      = true
            });
            await db.SaveChangesAsync();
            Console.WriteLine($"[Seed] Administrador creat: {email}");
        }
        else if (admin.Email != email)
        {
            // L'email configurat al .env ha canviat respecte al de la BD:
            // actualitzem l'admin principal i resetegem la contrasenya.
            admin.Email        = email;
            admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            admin.Nom          = nom;
            admin.Cognoms      = cognoms;
            await db.SaveChangesAsync();
            Console.WriteLine($"[Seed] Administrador actualitzat: {email}");
        }
        // Si l'email ja coincideix no toquem res: preservem qualsevol
        // canvi de contrasenya que l'admin hagi fet des de la UI.
    }
}
