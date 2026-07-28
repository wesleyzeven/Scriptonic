using Microsoft.AspNetCore.Mvc;
using Scriptonic.Web.Site.Data;

namespace Scriptonic.Web.Controllers;

public class ContactFormModel
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string Subject { get; set; } = "Algemeen";
    public string Message { get; set; } = string.Empty;

    /// <summary>Honeypot: hidden field that humans leave empty; bots fill it.</summary>
    public string? Website { get; set; }

    public string? ReturnUrl { get; set; }
}

[Route("contact-form")]
public class ContactController : Controller
{
    private readonly SiteDbContext _db;
    private readonly TimeProvider _time;
    private readonly ILogger<ContactController> _logger;

    public ContactController(SiteDbContext db, TimeProvider time, ILogger<ContactController> logger)
    {
        _db = db;
        _time = time;
        _logger = logger;
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(ContactFormModel model)
    {
        string returnUrl = !string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl : "/contact";

        // Bots fill the honeypot; pretend success and drop the message.
        if (!string.IsNullOrWhiteSpace(model.Website))
        {
            TempData["ContactSuccess"] = true;
            return Redirect(returnUrl);
        }

        if (string.IsNullOrWhiteSpace(model.Name) || string.IsNullOrWhiteSpace(model.Email)
            || string.IsNullOrWhiteSpace(model.Message) || !model.Email.Contains('@'))
        {
            TempData["ContactError"] = "Vul minimaal je naam, een geldig e-mailadres en een bericht in.";
            return Redirect(returnUrl);
        }

        _db.ContactMessages.Add(new ContactMessage
        {
            CreatedUtc = _time.GetUtcNow().UtcDateTime,
            Name = model.Name.Trim()[..Math.Min(200, model.Name.Trim().Length)],
            Email = model.Email.Trim()[..Math.Min(320, model.Email.Trim().Length)],
            Company = string.IsNullOrWhiteSpace(model.Company) ? null : model.Company.Trim()[..Math.Min(200, model.Company.Trim().Length)],
            Subject = string.IsNullOrWhiteSpace(model.Subject) ? "Algemeen" : model.Subject.Trim()[..Math.Min(100, model.Subject.Trim().Length)],
            Message = model.Message.Trim()[..Math.Min(4000, model.Message.Trim().Length)],
        });
        await _db.SaveChangesAsync();
        _logger.LogInformation("Contact message stored from {Email}", model.Email);

        TempData["ContactSuccess"] = true;
        return Redirect(returnUrl);
    }
}
