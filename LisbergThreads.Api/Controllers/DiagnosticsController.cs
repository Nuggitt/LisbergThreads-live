// Controllers/DiagnosticsController.cs
using LisbergThreads.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LisbergThreads.Api.Controllers
{
    [ApiController]
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    public class DiagnosticsController : ControllerBase
    {
        private readonly AppDbContext _db;
        public DiagnosticsController(AppDbContext db) => _db = db;

        [HttpGet("db")]
        public async Task<IActionResult> Db()
        {
            var canConnect = await _db.Database.CanConnectAsync();
            var users = await _db.Users.CountAsync();           // AspNetUsers
            // Hvis du har en Products-tabel i din model:
            // var products = await _db.Products.CountAsync();

            return Ok(new
            {
                status = canConnect ? "ok" : "fail",
                users
                // , products
            });
        }
    }
}
