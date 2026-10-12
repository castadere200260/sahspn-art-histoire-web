using Microsoft.EntityFrameworkCore;
using Sahspn.ArtHistoire.Models;
using System.Text.Json;
namespace Sahspn.ArtHistoire.Data;
public sealed class ContentSeeder(SahspDbContext db, IWebHostEnvironment env)
{
    public async Task SeedAsync()
    {
        if (await db.Pages.AnyAsync() && await db.Bulletins.AnyAsync()) return;

        var path = Path.Combine(env.ContentRootPath, "seed-content.json");

        if (!File.Exists(path)) return;

        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var pages = json.RootElement.GetProperty("pages");

        var map = new Dictionary<string, (string title, string slug)>
        {
            ["historique.htm"] = ("L’association", "association"),
            ["actualites.htm"] = ("Actualités", "actualites"),
            ["biblio.htm"] = ("Bibliothèque & ressources", "ressources"),
            ["ag.htm"] = ("Assemblée générale", "assemblee-generale"),
            ["contact.htm"] = ("Contact", "contact"),
            ["liens.htm"] = ("Liens utiles", "liens"),
            ["auteur.htm"] = ("Publications", "publications"),
            ["dernews.htm"] = ("Dernières nouvelles", "dernieres-nouvelles")
        };

        foreach (var item in pages.EnumerateObject())
        {
            if (!map.TryGetValue(item.Name, out var meta) || string.IsNullOrWhiteSpace(item.Value.GetString())) continue;

            if (!await db.Pages.AnyAsync(x => x.Slug == meta.slug)) db.Pages.Add(new SitePage { Slug = meta.slug, Title = meta.title, Content = item.Value.GetString()! });
        }

        var bulletins = json.RootElement.GetProperty("bulletins");

        foreach (var item in bulletins.EnumerateArray())
        {
            var number = item.GetProperty("number").GetInt32();
            if (await db.Bulletins.AnyAsync(x => x.Number == number)) continue;

            var cover = number == 185 ? "/heritage/bulletin/Bull 185/Couv 185.png" : number == 184 ? "/heritage/image/Bull 184/Couv 184.png" : number == 183 ? "/heritage/image/Bull 183/Couverture.png" : null;

            db.Bulletins.Add(new Bulletin { Number = number, Title = item.GetProperty("title").GetString()!, Content = item.GetProperty("content").GetString() ?? "", CoverImage = cover });
        }
        await db.SaveChangesAsync();
    }
}
