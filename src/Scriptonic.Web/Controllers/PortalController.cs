using Microsoft.AspNetCore.Mvc;
using Scriptonic.Web.Site;
using Scriptonic.Web.Site.Boekhouden;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.Security;

namespace Scriptonic.Web.Controllers;

public record PortalUser(string Name, string Email, long RelationId, string RelationCode, string CompanyName);

public class PortalLoginModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
}

public class PortalProfileModel
{
    public string Name { get; set; } = string.Empty;
    public string? Contact { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? PhoneNumber { get; set; }
    public string? EmailAddress { get; set; }
    public string? Website { get; set; }
}

/// <summary>
/// Customer portal: login, dashboard, invoices and quotes ("facturen en
/// offertes"), and profile editing synced to e-Boekhouden. All pages except
/// login require an authenticated Umbraco member of type portalKlant.
/// </summary>
[Route("portaal")]
public class PortalController : Controller
{
    private readonly IMemberManager _memberManager;
    private readonly IMemberSignInManager _signInManager;
    private readonly IMemberService _memberService;
    private readonly IEboekhoudenClient _boekhouden;
    private readonly SiteContentService _siteContent;
    private readonly ILogger<PortalController> _logger;

    public PortalController(
        IMemberManager memberManager,
        IMemberSignInManager signInManager,
        IMemberService memberService,
        IEboekhoudenClient boekhouden,
        SiteContentService siteContent,
        ILogger<PortalController> logger)
    {
        _memberManager = memberManager;
        _signInManager = signInManager;
        _memberService = memberService;
        _boekhouden = boekhouden;
        _siteContent = siteContent;
        _logger = logger;
    }

    // ---- Auth -------------------------------------------------------------

    [HttpGet("login")]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        if (await GetPortalUserAsync() is not null)
        {
            return RedirectToAction(nameof(Dashboard));
        }
        return View("~/Views/Portal/Login.cshtml", new PortalLoginModel { ReturnUrl = returnUrl });
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(PortalLoginModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
        {
            ModelState.AddModelError(string.Empty, "Vul je e-mailadres en wachtwoord in.");
            return View("~/Views/Portal/Login.cshtml", model);
        }

        var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, isPersistent: true, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Portal login failed for {Email}", model.Email);
            ModelState.AddModelError(string.Empty, "Onjuiste inloggegevens. Probeer het opnieuw.");
            return View("~/Views/Portal/Login.cshtml", model);
        }

        return !string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
            ? Redirect(model.ReturnUrl)
            : RedirectToAction(nameof(Dashboard));
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Redirect("/");
    }

    // ---- Pages ------------------------------------------------------------

    [HttpGet("")]
    public async Task<IActionResult> Dashboard()
    {
        PortalUser? user = await GetPortalUserAsync();
        if (user is null)
        {
            return RedirectToLogin();
        }

        ViewData["PortalUser"] = user;
        ViewData["IsDemo"] = _boekhouden.IsDemo;
        try
        {
            ViewData["Outstanding"] = await _boekhouden.GetOutstandingInvoicesAsync(user.RelationId);
            ViewData["Invoices"] = (await _boekhouden.GetInvoicesAsync(user.RelationId)).Take(5).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "e-Boekhouden unreachable for dashboard of relation {RelationId}", user.RelationId);
            ViewData["BoekhoudenError"] = true;
        }
        ViewData["Offertes"] = _siteContent.GetOffertes(user.RelationCode);
        return View("~/Views/Portal/Dashboard.cshtml");
    }

    [HttpGet("facturen")]
    public async Task<IActionResult> Invoices()
    {
        PortalUser? user = await GetPortalUserAsync();
        if (user is null)
        {
            return RedirectToLogin();
        }

        ViewData["PortalUser"] = user;
        ViewData["IsDemo"] = _boekhouden.IsDemo;
        try
        {
            ViewData["Invoices"] = await _boekhouden.GetInvoicesAsync(user.RelationId);
            ViewData["Outstanding"] = await _boekhouden.GetOutstandingInvoicesAsync(user.RelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "e-Boekhouden unreachable for invoices of relation {RelationId}", user.RelationId);
            ViewData["BoekhoudenError"] = true;
        }
        return View("~/Views/Portal/Invoices.cshtml");
    }

    [HttpGet("offertes")]
    public async Task<IActionResult> Quotes()
    {
        PortalUser? user = await GetPortalUserAsync();
        if (user is null)
        {
            return RedirectToLogin();
        }

        ViewData["PortalUser"] = user;
        ViewData["Offertes"] = _siteContent.GetOffertes(user.RelationCode);
        return View("~/Views/Portal/Quotes.cshtml");
    }

    [HttpGet("profiel")]
    public async Task<IActionResult> Profile()
    {
        PortalUser? user = await GetPortalUserAsync();
        if (user is null)
        {
            return RedirectToLogin();
        }

        ViewData["PortalUser"] = user;
        ViewData["IsDemo"] = _boekhouden.IsDemo;
        try
        {
            EboekRelation? relation = await _boekhouden.GetRelationAsync(user.RelationId);
            if (relation is not null)
            {
                return View("~/Views/Portal/Profile.cshtml", new PortalProfileModel
                {
                    Name = relation.Name,
                    Contact = relation.Contact,
                    Address = relation.Address,
                    PostalCode = relation.PostalCode,
                    City = relation.City,
                    Country = relation.Country,
                    PhoneNumber = relation.PhoneNumber,
                    EmailAddress = relation.EmailAddress,
                    Website = relation.Website,
                });
            }
            ViewData["RelationMissing"] = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "e-Boekhouden unreachable for profile of relation {RelationId}", user.RelationId);
            ViewData["BoekhoudenError"] = true;
        }
        return View("~/Views/Portal/Profile.cshtml", new PortalProfileModel { Name = user.CompanyName });
    }

    [HttpPost("profiel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(PortalProfileModel model)
    {
        PortalUser? user = await GetPortalUserAsync();
        if (user is null)
        {
            return RedirectToLogin();
        }

        ViewData["PortalUser"] = user;
        ViewData["IsDemo"] = _boekhouden.IsDemo;

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Naam is verplicht.");
            return View("~/Views/Portal/Profile.cshtml", model);
        }

        try
        {
            await _boekhouden.UpdateRelationAsync(user.RelationId, new EboekRelationUpdate
            {
                Name = model.Name.Trim(),
                Contact = model.Contact?.Trim(),
                Address = model.Address?.Trim(),
                PostalCode = model.PostalCode?.Trim(),
                City = model.City?.Trim(),
                Country = model.Country?.Trim(),
                PhoneNumber = model.PhoneNumber?.Trim(),
                EmailAddress = model.EmailAddress?.Trim(),
                Website = model.Website?.Trim(),
            });
            TempData["ProfileSaved"] = true;
            return RedirectToAction(nameof(Profile));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Profile update failed for relation {RelationId}", user.RelationId);
            ModelState.AddModelError(string.Empty, "Opslaan is niet gelukt. Probeer het later opnieuw of neem contact met ons op.");
            return View("~/Views/Portal/Profile.cshtml", model);
        }
    }

    // ---- Helpers ----------------------------------------------------------

    private IActionResult RedirectToLogin()
        => RedirectToAction(nameof(Login), new { returnUrl = Request.Path.Value });

    private async Task<PortalUser?> GetPortalUserAsync()
    {
        MemberIdentityUser? identity = await _memberManager.GetCurrentMemberAsync();
        if (identity is null)
        {
            return null;
        }

        var member = _memberService.GetById(identity.Key);
        if (member is null || member.ContentType.Alias != SiteAliases.MemberType)
        {
            return null;
        }

        long relationId = member.GetValue<int>("relationId");
        string relationCode = member.GetValue<string>("relationCode") ?? string.Empty;
        string company = member.GetValue<string>("companyName") ?? member.Name ?? identity.Email ?? "Klant";
        return new PortalUser(member.Name ?? company, identity.Email ?? string.Empty, relationId, relationCode, company);
    }
}
