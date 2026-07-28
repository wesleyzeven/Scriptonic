using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scriptonic.Web.Site;
using Scriptonic.Web.Site.Data;

namespace Scriptonic.Web.Controllers;

/// <summary>
/// Minimal read-out of contact form submissions, protected by the AdminKey
/// from the environment (same pattern as ToyShop's /admin/orders).
/// </summary>
[Route("admin/berichten")]
public class AdminMessagesController : Controller
{
    private readonly SiteDbContext _db;
    private readonly SiteOptions _options;

    public AdminMessagesController(SiteDbContext db, IOptions<SiteOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    private bool KeyValid(string? key)
        => !string.IsNullOrEmpty(_options.AdminKey) && key == _options.AdminKey;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? key)
    {
        if (!KeyValid(key))
        {
            return NotFound();
        }

        ViewData["AdminKey"] = key;
        List<ContactMessage> messages = await _db.ContactMessages
            .OrderByDescending(m => m.CreatedUtc)
            .Take(200)
            .ToListAsync();
        return View("~/Views/AdminMessages/Index.cshtml", messages);
    }

    [HttpPost("{id:guid}/afgehandeld")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleHandled(Guid id, string? key)
    {
        if (!KeyValid(key))
        {
            return NotFound();
        }

        ContactMessage? message = await _db.ContactMessages.FindAsync(id);
        if (message is not null)
        {
            message.Handled = !message.Handled;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index), new { key });
    }
}
