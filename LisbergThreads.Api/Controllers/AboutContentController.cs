using System.Text.Json;
using LisbergThreads.Api.Data;
using LisbergThreads.Api.Models;
using LisbergThreads.Contracts.Content;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LisbergThreads.Api.Controllers;

[ApiController]
[Route("api/content/about")]
public class AboutContentController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<ContentTextDto>> Get(CancellationToken cancellationToken)
    {
        var saved = await db.ContentTexts.AsNoTracking()
            .Where(t => t.Key.StartsWith("About."))
            .ToDictionaryAsync(t => t.Key, cancellationToken);
        return AboutContentDefaults.Values.Select(item => saved.TryGetValue(item.Key, out var text)
            ? new ContentTextDto(text.Key, text.Value, Convert.ToBase64String(text.RowVersion))
            : new ContentTextDto(item.Key, item.Value, null)).ToList();
    }

    [HttpPut("{key}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ContentTextDto>> Update(string key, UpdateContentTextDto request, CancellationToken cancellationToken)
    {
        if (!AboutContentDefaults.Values.ContainsKey(key)) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Value) || request.Value.Length > 2000)
            return BadRequest("Indholdet skal indeholde mellem 1 og 2000 tegn.");
        if (AboutContentDefaults.IsImageKey(key))
        {
            try
            {
                var images = JsonSerializer.Deserialize<string[]>(request.Value);
                if (images is null || images.Length > 1 || images.Any(url => !IsUploadedImage(url)))
                    return BadRequest("Vælg ét uploadet billede til billedpladsen.");
            }
            catch (JsonException) { return BadRequest("Ugyldige billeddata."); }
        }

        var text = await db.ContentTexts.FindAsync(new object[] { key }, cancellationToken);
        if (text is null)
        {
            if (request.RowVersionBase64 is not null) return Conflict();
            text = new ContentText { Key = key };
            db.ContentTexts.Add(text);
        }
        else
        {
            if (request.RowVersionBase64 is null) return Conflict();
            byte[] version;
            try { version = Convert.FromBase64String(request.RowVersionBase64); }
            catch (FormatException) { return BadRequest("Ugyldig version. Genindlæs siden."); }
            if (version.Length != 8) return BadRequest("Ugyldig version. Genindlæs siden.");
            if (!text.RowVersion.SequenceEqual(version)) return Conflict();
            db.Entry(text).Property(t => t.RowVersion).OriginalValue = version;
        }
        text.Value = request.Value.Trim();
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflict(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 }) { return Conflict(); }
        return Ok(new ContentTextDto(text.Key, text.Value, Convert.ToBase64String(text.RowVersion)));
    }

    private static bool IsUploadedImage(string? url)
    {
        if (url is null || !url.StartsWith("/uploads/products/", StringComparison.Ordinal)) return false;
        var name = url["/uploads/products/".Length..];
        var extension = Path.GetExtension(name);
        return Guid.TryParseExact(Path.GetFileNameWithoutExtension(name), "D", out var id)
            && name == $"{id:D}{extension}"
            && extension is ".jpg" or ".jpeg" or ".png" or ".webp";
    }
}
