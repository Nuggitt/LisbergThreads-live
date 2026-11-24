using LisbergThreads.Api.Data;
using LisbergThreads.Api.Models;
using LisbergThreads.Contracts.Content;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LisbergThreads.Api.Controllers;

[ApiController]
[Route("api/content/home")]
public class ContentController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<ContentTextDto>> Get(CancellationToken cancellationToken)
    {
        var saved = await db.ContentTexts.AsNoTracking().ToDictionaryAsync(t => t.Key, cancellationToken);
        return HomeTextDefaults.Values.Select(item => saved.TryGetValue(item.Key, out var text)
            ? new ContentTextDto(text.Key, text.Value, Convert.ToBase64String(text.RowVersion))
            : new ContentTextDto(item.Key, item.Value, null)).ToList();
    }

    [HttpPut("{key}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ContentTextDto>> Update(
        string key, UpdateContentTextDto request, CancellationToken cancellationToken)
    {
        if (!HomeTextDefaults.Values.ContainsKey(key)) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Value) || request.Value.Length > 2000)
            return BadRequest("Teksten skal indeholde mellem 1 og 2000 tegn.");

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
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return Conflict();
        }
        return Ok(new ContentTextDto(text.Key, text.Value, Convert.ToBase64String(text.RowVersion)));
    }
}
