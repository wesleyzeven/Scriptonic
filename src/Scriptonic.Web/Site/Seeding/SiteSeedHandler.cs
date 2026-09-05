using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;

namespace Scriptonic.Web.Site.Seeding;

/// <summary>
/// Idempotent first-boot seeding: document types, templates, member type and
/// starter content (Dutch marketing copy + portal plumbing) so a fresh
/// container comes up as a complete site. Every step checks for existence
/// first, so subsequent boots are no-ops and editor changes are never
/// overwritten.
/// </summary>
public class SiteSeedHandler : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly IRuntimeState _runtimeState;
    private readonly IContentTypeService _contentTypeService;
    private readonly IContentService _contentService;
    private readonly IDataTypeService _dataTypeService;
    private readonly ITemplateService _templateService;
    private readonly IMemberTypeService _memberTypeService;
    private readonly IMemberGroupService _memberGroupService;
    private readonly IMemberService _memberService;
    private readonly IMemberManager _memberManager;
    private readonly PropertyEditorCollection _propertyEditors;
    private readonly IConfigurationEditorJsonSerializer _configSerializer;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly SiteOptions _options;
    private readonly ILogger<SiteSeedHandler> _logger;

    public SiteSeedHandler(
        IRuntimeState runtimeState,
        IContentTypeService contentTypeService,
        IContentService contentService,
        IDataTypeService dataTypeService,
        ITemplateService templateService,
        IMemberTypeService memberTypeService,
        IMemberGroupService memberGroupService,
        IMemberService memberService,
        IMemberManager memberManager,
        PropertyEditorCollection propertyEditors,
        IConfigurationEditorJsonSerializer configSerializer,
        IShortStringHelper shortStringHelper,
        IWebHostEnvironment webHostEnvironment,
        IOptions<SiteOptions> options,
        ILogger<SiteSeedHandler> logger)
    {
        _runtimeState = runtimeState;
        _contentTypeService = contentTypeService;
        _contentService = contentService;
        _dataTypeService = dataTypeService;
        _templateService = templateService;
        _memberTypeService = memberTypeService;
        _memberGroupService = memberGroupService;
        _memberService = memberService;
        _memberManager = memberManager;
        _propertyEditors = propertyEditors;
        _configSerializer = configSerializer;
        _shortStringHelper = shortStringHelper;
        _webHostEnvironment = webHostEnvironment;
        _options = options.Value;
        _logger = logger;
    }

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            return;
        }

        try
        {
            ITemplate homeTemplate = await EnsureTemplateAsync("Scriptonic Home", SiteAliases.Home);
            ITemplate dienstenTemplate = await EnsureTemplateAsync("Scriptonic Diensten", SiteAliases.Diensten);
            ITemplate dienstTemplate = await EnsureTemplateAsync("Scriptonic Dienst", SiteAliases.Dienst);
            ITemplate portfolioTemplate = await EnsureTemplateAsync("Scriptonic Portfolio", SiteAliases.Portfolio);
            ITemplate caseTemplate = await EnsureTemplateAsync("Scriptonic Case", SiteAliases.Case);
            ITemplate pageTemplate = await EnsureTemplateAsync("Scriptonic Pagina", SiteAliases.Page);
            ITemplate contactTemplate = await EnsureTemplateAsync("Scriptonic Contact", SiteAliases.Contact);

            IContentType dienstType = await EnsureDienstTypeAsync(dienstTemplate);
            IContentType dienstenType = await EnsureListTypeAsync(SiteAliases.Diensten, "Diensten (overzicht)", "icon-wrench color-blue", dienstenTemplate, dienstType);
            IContentType caseType = await EnsureCaseTypeAsync(caseTemplate);
            IContentType portfolioType = await EnsureListTypeAsync(SiteAliases.Portfolio, "Portfolio (overzicht)", "icon-pictures-alt-2 color-purple", portfolioTemplate, caseType);
            IContentType pageType = await EnsurePageTypeAsync(pageTemplate);
            IContentType contactType = await EnsureContactTypeAsync(contactTemplate);
            IContentType offerteType = await EnsureOfferteTypeAsync();
            IContentType portalRootType = await EnsurePortalRootTypeAsync(offerteType);
            IContentType homeType = await EnsureHomeTypeAsync(homeTemplate, [dienstenType, portfolioType, pageType, contactType, portalRootType]);

            await EnsureMemberTypeAsync();
            EnsureMemberGroup();

            SeedContent(homeTemplate, dienstenTemplate, dienstTemplate, portfolioTemplate, caseTemplate, pageTemplate, contactTemplate);

            if (_options.Portal.SeedDemoMember)
            {
                await EnsureDemoMemberAsync();
            }
        }
        catch (Exception ex)
        {
            // Seeding must never take the site down; log and continue.
            _logger.LogError(ex, "Site seeding failed");
        }
    }

    private async Task<ITemplate> EnsureTemplateAsync(string name, string alias)
    {
        ITemplate? existing = await _templateService.GetAsync(alias);
        if (existing is not null)
        {
            return existing;
        }

        // The view files ship with the project; pass their content so the
        // template record matches the file on disk instead of overwriting it.
        string viewPath = Path.Combine(_webHostEnvironment.ContentRootPath, "Views", alias + ".cshtml");
        string? content = System.IO.File.Exists(viewPath) ? await System.IO.File.ReadAllTextAsync(viewPath) : null;

        var attempt = await _templateService.CreateAsync(name, alias, content, Constants.Security.SuperUserKey);
        if (!attempt.Success)
        {
            throw new InvalidOperationException($"Failed to create template {alias}: {attempt.Status}.");
        }
        _logger.LogInformation("Seeded template {Alias}", alias);
        return attempt.Result;
    }

    // ---- Document types -------------------------------------------------

    private async Task<IContentType> EnsureDienstTypeAsync(ITemplate template)
    {
        IContentType? existing = _contentTypeService.Get(SiteAliases.Dienst);
        if (existing is not null)
        {
            return existing;
        }

        var type = await NewTypeAsync(SiteAliases.Dienst, "Dienst", "icon-lightbulb color-yellow", "inhoud", "Inhoud");
        await AddTextAsync(type, "icon", "Icoon", 1, area: false, description: "Emoji of kort symbool voor de kaart, bijv. 🌐");
        await AddTextAsync(type, "summary", "Samenvatting", 2, area: true, description: "Korte tekst op het overzicht en de homepage.");
        await AddTextAsync(type, "bodyText", "Tekst", 3, area: true, description: "Alinea's scheiden met een lege regel.");
        await AddTextAsync(type, "features", "Kenmerken", 4, area: true, description: "Eén kenmerk per regel; getoond als lijst met vinkjes.");
        await AddSeoGroupAsync(type);
        FinishType(type, template);
        await _contentTypeService.CreateAsync(type, Constants.Security.SuperUserKey);
        return type;
    }

    private async Task<IContentType> EnsureCaseTypeAsync(ITemplate template)
    {
        IContentType? existing = _contentTypeService.Get(SiteAliases.Case);
        if (existing is not null)
        {
            await EnsureCaseMediaPropertiesAsync(existing);
            return existing;
        }

        IDataType casePreviewPicker = await RequireDataTypeAsync(Constants.PropertyEditors.Aliases.MediaPicker3, "Media Picker");
        IDataType caseGalleryPicker = await EnsureMultipleImagePickerAsync();
        var type = await NewTypeAsync(SiteAliases.Case, "Case (portfolio item)", "icon-star color-orange", "inhoud", "Inhoud");
        await AddTextAsync(type, "client", "Opdrachtgever", 1);
        await AddTextAsync(type, "projectType", "Soort project", 2, description: "Bijv. Website, Game, Webshop.");
        await AddTextAsync(type, "status", "Status", 3, description: "Bijv. Live of In ontwikkeling.");
        await AddTextAsync(type, "projectUrl", "Project-URL", 4, description: "Publieke link naar het project (optioneel).");
        await AddTextAsync(type, "summary", "Samenvatting", 5, area: true);
        await AddTextAsync(type, "bodyText", "Tekst", 6, area: true, description: "Alinea's scheiden met een lege regel.");
        await AddTextAsync(type, "tags", "Tags", 7, description: "Kommagescheiden, bijv. Umbraco, .NET, Unity.");
        AddProperty(type, casePreviewPicker, CasePreviewImageAlias, "Voorbeeldfoto", 8, CasePreviewImageDescription);
        AddProperty(type, caseGalleryPicker, CaseImagesAlias, "Foto's", 9, CaseImagesDescription);
        await AddSeoGroupAsync(type);
        FinishType(type, template);
        await _contentTypeService.CreateAsync(type, Constants.Security.SuperUserKey);
        return type;
    }

    private const string CasePreviewImageAlias = "previewImage";
    private const string CasePreviewImageDescription =
        "Afbeelding op de portfolio-kaarten en bovenaan de casepagina (schermafdruk of foto). Zonder afbeelding tonen we op de kaarten een neutrale placeholder.";

    private const string CaseImagesAlias = "images";
    private const string CaseImagesDescription =
        "Extra foto's of schermafdrukken voor de galerij op de casepagina (optioneel).";

    /// <summary>
    /// Adds the media properties (preview image, gallery) to a case type that
    /// was seeded before they existed, so sites from an earlier boot get them too.
    /// </summary>
    private async Task EnsureCaseMediaPropertiesAsync(IContentType caseType)
    {
        bool changed = false;
        string groupAlias = caseType.PropertyGroups.FirstOrDefault(g => g.Alias == "inhoud")?.Alias
            ?? caseType.PropertyGroups.First().Alias!;

        if (caseType.PropertyTypes.All(p => p.Alias != CasePreviewImageAlias))
        {
            IDataType mediaPicker = await RequireDataTypeAsync(Constants.PropertyEditors.Aliases.MediaPicker3, "Media Picker");
            caseType.AddPropertyType(new PropertyType(_shortStringHelper, mediaPicker, CasePreviewImageAlias)
            {
                Name = "Voorbeeldfoto",
                Description = CasePreviewImageDescription,
                SortOrder = 8,
            }, groupAlias);
            changed = true;
        }

        if (caseType.PropertyTypes.All(p => p.Alias != CaseImagesAlias))
        {
            IDataType gallery = await EnsureMultipleImagePickerAsync();
            caseType.AddPropertyType(new PropertyType(_shortStringHelper, gallery, CaseImagesAlias)
            {
                Name = "Foto's",
                Description = CaseImagesDescription,
                SortOrder = 9,
            }, groupAlias);
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        var attempt = await _contentTypeService.UpdateAsync(caseType, Constants.Security.SuperUserKey);
        if (attempt.Success)
        {
            _logger.LogInformation("Added media properties to {Alias}", SiteAliases.Case);
        }
        else
        {
            _logger.LogWarning("Could not add media properties to {Alias}: {Status}", SiteAliases.Case, attempt.Result);
        }
    }

    /// <summary>
    /// Umbraco ships a "Multiple Image Media Picker" data type; use that, and
    /// only if it is missing create our own multi-select picker. A plain
    /// fallback to the first Media Picker would silently yield a single-select
    /// picker, which the gallery cannot work with.
    /// </summary>
    private async Task<IDataType> EnsureMultipleImagePickerAsync()
    {
        IEnumerable<IDataType> pickers = await _dataTypeService.GetByEditorAliasAsync(Constants.PropertyEditors.Aliases.MediaPicker3);
        IDataType? existing = pickers.FirstOrDefault(d => d.Name == "Multiple Image Media Picker")
            ?? pickers.FirstOrDefault(d => d.Name == "Multiple Media Picker")
            ?? pickers.FirstOrDefault(d => d.Name == "Foto's (meerdere)");
        if (existing is not null)
        {
            return existing;
        }

        if (!_propertyEditors.TryGet(Constants.PropertyEditors.Aliases.MediaPicker3, out IDataEditor? editor))
        {
            throw new InvalidOperationException("Media Picker property editor not found.");
        }

        var dataType = new DataType(editor, _configSerializer)
        {
            Name = "Foto's (meerdere)",
            DatabaseType = ValueStorageType.Ntext,
            ConfigurationData = new Dictionary<string, object>
            {
                ["multiple"] = true,
                ["filter"] = Constants.Conventions.MediaTypes.Image,
            },
        };
        var attempt = await _dataTypeService.CreateAsync(dataType, Constants.Security.SuperUserKey);
        return attempt.Success ? attempt.Result : throw new InvalidOperationException("Failed to create multiple image picker data type.");
    }

    private async Task<IContentType> EnsureListTypeAsync(string alias, string name, string icon, ITemplate template, IContentType childType)
    {
        IContentType? existing = _contentTypeService.Get(alias);
        if (existing is not null)
        {
            return existing;
        }

        var type = await NewTypeAsync(alias, name, icon, "inhoud", "Inhoud");
        type.AllowedContentTypes = [new ContentTypeSort(childType.Key, 0, childType.Alias)];
        await AddTextAsync(type, "intro", "Introductie", 1, area: true);
        await AddSeoGroupAsync(type);
        FinishType(type, template);
        await _contentTypeService.CreateAsync(type, Constants.Security.SuperUserKey);
        return type;
    }

    private async Task<IContentType> EnsurePageTypeAsync(ITemplate template)
    {
        IContentType? existing = _contentTypeService.Get(SiteAliases.Page);
        if (existing is not null)
        {
            return existing;
        }

        var type = await NewTypeAsync(SiteAliases.Page, "Pagina", "icon-document color-blue", "inhoud", "Inhoud");
        await AddTextAsync(type, "intro", "Introductie", 1, area: true);
        await AddTextAsync(type, "bodyText", "Tekst", 2, area: true, description: "Alinea's scheiden met een lege regel; kopjes beginnen met ##.");
        await AddSeoGroupAsync(type);
        FinishType(type, template);
        await _contentTypeService.CreateAsync(type, Constants.Security.SuperUserKey);
        return type;
    }

    private async Task<IContentType> EnsureContactTypeAsync(ITemplate template)
    {
        IContentType? existing = _contentTypeService.Get(SiteAliases.Contact);
        if (existing is not null)
        {
            return existing;
        }

        var type = await NewTypeAsync(SiteAliases.Contact, "Contactpagina", "icon-message color-green", "inhoud", "Inhoud");
        await AddTextAsync(type, "intro", "Introductie", 1, area: true);
        await AddTextAsync(type, "email", "E-mailadres", 2);
        await AddTextAsync(type, "kvk", "KvK-nummer", 3);
        await AddSeoGroupAsync(type);
        FinishType(type, template);
        await _contentTypeService.CreateAsync(type, Constants.Security.SuperUserKey);
        return type;
    }

    private async Task<IContentType> EnsureOfferteTypeAsync()
    {
        IContentType? existing = _contentTypeService.Get(SiteAliases.Offerte);
        if (existing is not null)
        {
            return existing;
        }

        // No template on purpose: offertes are rendered inside the customer
        // portal only and must not get a public URL of their own.
        var type = await NewTypeAsync(SiteAliases.Offerte, "Offerte", "icon-coin-euro color-green", "offerte", "Offerte");
        IDataType datePicker = await RequireDataTypeAsync(Constants.PropertyEditors.Aliases.DateTime, "Date Picker");
        IDataType decimalType = await EnsureBedragDataTypeAsync();
        IDataType mediaPicker = await RequireDataTypeAsync(Constants.PropertyEditors.Aliases.MediaPicker3, "Media Picker");

        await AddTextAsync(type, "relationCode", "Relatiecode (e-Boekhouden)", 1, description: "Koppelt deze offerte aan de klant met dezelfde relatiecode.");
        await AddTextAsync(type, "offerteNumber", "Offertenummer", 2);
        AddProperty(type, datePicker, "offerteDate", "Datum", 3);
        AddProperty(type, datePicker, "validUntil", "Geldig tot", 4);
        AddProperty(type, decimalType, "amount", "Bedrag (€, incl. btw)", 5);
        await AddTextAsync(type, "status", "Status", 6, description: "Open, Geaccepteerd of Verlopen.");
        await AddTextAsync(type, "description", "Omschrijving", 7, area: true);
        AddProperty(type, mediaPicker, "pdf", "PDF-bestand", 8, description: "Upload de offerte-PDF in de mediabibliotheek en kies hem hier.");
        await _contentTypeService.CreateAsync(type, Constants.Security.SuperUserKey);
        return type;
    }

    private async Task<IContentType> EnsurePortalRootTypeAsync(IContentType offerteType)
    {
        IContentType? existing = _contentTypeService.Get(SiteAliases.PortalRoot);
        if (existing is not null)
        {
            return existing;
        }

        var type = await NewTypeAsync(SiteAliases.PortalRoot, "Portaal (offertes)", "icon-lock color-red", "portaal", "Portaal");
        type.AllowedContentTypes = [new ContentTypeSort(offerteType.Key, 0, offerteType.Alias)];
        await _contentTypeService.CreateAsync(type, Constants.Security.SuperUserKey);
        return type;
    }

    private async Task<IContentType> EnsureHomeTypeAsync(ITemplate template, IContentType[] childTypes)
    {
        IContentType? existing = _contentTypeService.Get(SiteAliases.Home);
        if (existing is not null)
        {
            return existing;
        }

        var type = await NewTypeAsync(SiteAliases.Home, "Scriptonic Home", "icon-home color-blue", "inhoud", "Inhoud");
        type.AllowedAsRoot = true;
        type.AllowedContentTypes = childTypes
            .Select((t, i) => new ContentTypeSort(t.Key, i, t.Alias))
            .ToArray();
        await AddTextAsync(type, "tagline", "Tagline", 1);
        await AddTextAsync(type, "heroTitle", "Hero titel", 2);
        await AddTextAsync(type, "heroText", "Hero tekst", 3, area: true);
        await AddTextAsync(type, "ctaLabel", "CTA-knop tekst", 4);
        await AddSeoGroupAsync(type);
        FinishType(type, template);
        await _contentTypeService.CreateAsync(type, Constants.Security.SuperUserKey);
        return type;
    }

    // ---- Member type & group --------------------------------------------

    private async Task EnsureMemberTypeAsync()
    {
        if (_memberTypeService.Get(SiteAliases.MemberType) is not null)
        {
            return;
        }

        IDataType textstring = await RequireDataTypeAsync(Constants.PropertyEditors.Aliases.TextBox, "Textstring");
        IDataType numeric = await RequireDataTypeAsync(Constants.PropertyEditors.Aliases.Integer, "Numeric");

        var memberType = new MemberType(_shortStringHelper, -1)
        {
            Alias = SiteAliases.MemberType,
            Name = "Portaal klant",
            Icon = "icon-users color-blue",
        };
        memberType.AddPropertyGroup("portaal", "Portaal");
        var relationId = new PropertyType(_shortStringHelper, numeric, "relationId")
        {
            Name = "e-Boekhouden relatie-ID",
            Description = "Numeriek ID van de relatie in e-Boekhouden (Relaties > kolom ID).",
            SortOrder = 1,
        };
        memberType.AddPropertyType(relationId, "portaal");
        var relationCode = new PropertyType(_shortStringHelper, textstring, "relationCode")
        {
            Name = "Relatiecode",
            Description = "Relatiecode in e-Boekhouden; koppelt ook de offertes in het portaal.",
            SortOrder = 2,
        };
        memberType.AddPropertyType(relationCode, "portaal");
        var company = new PropertyType(_shortStringHelper, textstring, "companyName")
        {
            Name = "Bedrijfsnaam",
            SortOrder = 3,
        };
        memberType.AddPropertyType(company, "portaal");

        _memberTypeService.Save(memberType);
        _logger.LogInformation("Seeded member type {Alias}", SiteAliases.MemberType);
    }

    private void EnsureMemberGroup()
    {
        if (_memberGroupService.GetByName(SiteAliases.MemberGroup) is null)
        {
            _memberGroupService.Save(new MemberGroup { Name = SiteAliases.MemberGroup });
            _logger.LogInformation("Seeded member group {Name}", SiteAliases.MemberGroup);
        }
    }

    private async Task EnsureDemoMemberAsync()
    {
        string email = _options.Portal.DemoMemberEmail;
        if (_memberService.GetByEmail(email) is null)
        {
            MemberIdentityUser identityUser = MemberIdentityUser.CreateNew(email, email, SiteAliases.MemberType, isApproved: true, name: "Demo Klant B.V.");
            var created = await _memberManager.CreateAsync(identityUser, _options.Portal.DemoMemberPassword);
            if (!created.Succeeded)
            {
                _logger.LogWarning("Demo member creation failed: {Errors}", string.Join("; ", created.Errors.Select(e => e.Description)));
                return;
            }

            IMember? member = _memberService.GetByEmail(email);
            if (member is not null)
            {
                member.SetValue("relationId", 1001);
                member.SetValue("relationCode", "DEMO001");
                member.SetValue("companyName", "Demo Klant B.V.");
                _memberService.Save(member);
                _memberService.AssignRoles([member.Id], [SiteAliases.MemberGroup]);
            }
            _logger.LogInformation("Seeded demo portal member {Email}", email);
        }

        // Second demo member with ONLY an email address: exercises the
        // e-mail auto-link (MemberRelationAutoLinkHandler fills the relation
        // from the demo store's klant2 entry during the save).
        const string email2 = "klant2@scriptonic.nl";
        if (_memberService.GetByEmail(email2) is null)
        {
            MemberIdentityUser identity2 = MemberIdentityUser.CreateNew(email2, email2, SiteAliases.MemberType, isApproved: true, name: "Klant 2");
            var created2 = await _memberManager.CreateAsync(identity2, _options.Portal.DemoMemberPassword);
            if (created2.Succeeded)
            {
                IMember? member2 = _memberService.GetByEmail(email2);
                if (member2 is not null)
                {
                    // No relation values on purpose; save triggers the auto-link.
                    _memberService.Save(member2);
                    _memberService.AssignRoles([member2.Id], [SiteAliases.MemberGroup]);
                }
                _logger.LogInformation("Seeded second demo portal member {Email} (auto-link test)", email2);
            }
            else
            {
                _logger.LogWarning("Second demo member creation failed: {Errors}", string.Join("; ", created2.Errors.Select(e => e.Description)));
            }
        }
    }

    // ---- Content ---------------------------------------------------------

    private void SeedContent(ITemplate homeTemplate, ITemplate dienstenTemplate, ITemplate dienstTemplate,
        ITemplate portfolioTemplate, ITemplate caseTemplate, ITemplate pageTemplate, ITemplate contactTemplate)
    {
        // Idempotent per node, so an interrupted first boot picks up where it
        // left off without duplicating anything that already exists.
        IContent? home = _contentService.GetRootContent().FirstOrDefault(c => c.ContentType.Alias == SiteAliases.Home);
        if (home is null)
        {
            home = _contentService.Create("Scriptonic", Constants.System.Root, SiteAliases.Home);
            home.TemplateId = homeTemplate.Id;
            home.SetValue("tagline", "Software met karakter");
            home.SetValue("heroTitle", "Websites en games die werken. Punt.");
            home.SetValue("heroText", "Scriptonic bouwt websites, webapplicaties en games — van auteurswebsite tot klantportaal. Modern, snel en zonder gedoe: wij regelen ontwerp, bouw, hosting en beheer.");
            home.SetValue("ctaLabel", "Plan een kennismaking");
            home.SetValue("seoDescription", "Scriptonic bouwt moderne websites, webapplicaties en games. Van ontwerp tot hosting en beheer — software met karakter.");
            SavePublish(home);
        }

        List<IContent> children = _contentService.GetPagedChildren(home.Id, 0, 100, out _).ToList();
        bool Exists(string alias, string? name = null) => children.Any(c =>
            c.ContentType.Alias == alias && (name is null || c.Name == name));

        // A node created by an interrupted boot may exist unpublished; finish the job.
        foreach (IContent child in children.Where(c => !c.Published))
        {
            SavePublish(child);
        }

        // Diensten
        if (!Exists(SiteAliases.Diensten))
        {
            IContent diensten = _contentService.Create("Diensten", home.Id, SiteAliases.Diensten);
            diensten.TemplateId = dienstenTemplate.Id;
            diensten.SetValue("intro", "Van eerste schets tot livegang en daarna: wij bouwen digitale producten waar je jaren mee vooruit kunt.");
            diensten.SetValue("seoDescription", "Diensten van Scriptonic: websites op maat, games en interactieve ervaringen, en maatwerkkoppelingen zoals e-Boekhouden.");
            SavePublish(diensten);

            SeedDienst(diensten.Id, dienstTemplate, "Websites op maat", "🌐",
            "Snelle, vindbare websites op Umbraco CMS — zelf eenvoudig te beheren, door ons gehost en onderhouden.",
            "Een website moet meer doen dan er goed uitzien. Wij bouwen websites die snel laden, goed scoren in zoekmachines en die je zelf kunt bijhouden zonder technische kennis.\n\nWe werken met Umbraco, een bewezen open-source CMS. Jij krijgt een redactieomgeving waarin je teksten, foto's en pagina's zelf aanpast; wij zorgen voor hosting, updates en back-ups.",
            "Umbraco CMS — zelf content beheren\nRazendsnel en mobielvriendelijk\nZoekmachine-optimalisatie (SEO) vanaf dag één\nHosting, updates en back-ups inbegrepen\nToegankelijk en AVG-proof");

            SeedDienst(diensten.Id, dienstTemplate, "Games & interactieve ervaringen", "🎮",
                "Van webgames tot volwaardige gametitels: speelse software die blijft hangen.",
                "Games zijn de leukste manier om een verhaal te vertellen of een merk te laten spelen. We ontwikkelen games voor web en desktop — van kleine promotiegames tot titels met eigen werelden, zoals ons eigen Trails of Hooves.\n\nWe pakken het hele traject op: concept, gamedesign, art-richting, ontwikkeling en het testen met echte spelers.",
                "Web- en desktopgames\nConcept en gamedesign\nEigen titels én games in opdracht\nPlaytesting en doorontwikkeling");

            SeedDienst(diensten.Id, dienstTemplate, "Maatwerk & koppelingen", "🔗",
                "Klantportalen, API-koppelingen en interne tools die precies doen wat jouw proces vraagt.",
                "Standaardsoftware houdt een keer op. Wij bouwen maatwerk dat op je bestaande systemen aansluit: klantportalen waar je klanten hun facturen en offertes inzien, koppelingen met pakketten als e-Boekhouden, en interne tools die handwerk wegautomatiseren.\n\nKlein beginnen kan; meegroeien ook. Ons eigen klantportaal op deze site draait op precies dezelfde bouwstenen.",
                "Klantportalen met veilige login\nKoppelingen met o.a. e-Boekhouden\nInterne tools en automatisering\nAPI-ontwerp en integraties");
        }

        // Portfolio
        if (!Exists(SiteAliases.Portfolio))
        {
            IContent portfolio = _contentService.Create("Portfolio", home.Id, SiteAliases.Portfolio);
            portfolio.TemplateId = portfolioTemplate.Id;
            portfolio.SetValue("intro", "Een greep uit wat we maken — voor opdrachtgevers en uit eigen koker.");
            portfolio.SetValue("seoDescription", "Werk van Scriptonic: de auteurswebsites van Vivian den Hollander en Hans den Hollander en de game Trails of Hooves.");
            SavePublish(portfolio);

            SeedCase(portfolio.Id, caseTemplate, "Vivian den Hollander", "Vivian den Hollander", "Website", "Live", "",
                "Auteurswebsite voor kinderboekenschrijfster Vivian den Hollander, met haar boeken, series en agenda overzichtelijk bij elkaar.",
                "Voor kinderboekenauteur Vivian den Hollander bouwden we een website die haar grote oeuvre net zo toegankelijk maakt als haar boeken: overzichtelijk per serie, met aandacht voor scholen, ouders en jonge lezers.\n\nDe redactie doet ze zelf — nieuwe titels en agenda-items staan binnen een paar minuten online.",
                "Umbraco, Website, Auteur");

            SeedCase(portfolio.Id, caseTemplate, "Hans den Hollander", "Hans den Hollander", "Website", "Live", "",
                "Persoonlijke website met werk en portfolio van Hans den Hollander.",
                "Een persoonlijke website die het werk centraal zet: rustig vormgegeven, snel en eenvoudig zelf bij te houden.\n\nDezelfde solide basis als al onze sites: modern CMS, nette SEO en hosting in eigen beheer.",
                "Umbraco, Website, Portfolio");

            SeedCase(portfolio.Id, caseTemplate, "Trails of Hooves", "Eigen productie", "Game", "In ontwikkeling", "",
                "Ons eigen paardenavontuur: een game waarin je te paard een open wereld verkent. Nu in ontwikkeling.",
                "Trails of Hooves is onze eigen gametitel: een sfeervol avontuur te paard, gebouwd met dezelfde zorg die we in klantprojecten stoppen.\n\nWe ontwikkelen 'm stap voor stap en testen met echte spelers. Volg de voortgang — of vraag ons wat we hiervan meenemen naar jouw game-idee.",
                "Game, Eigen titel, In ontwikkeling");
        }

        // Over ons
        if (!Exists(SiteAliases.Page, "Over ons"))
        {
            IContent over = _contentService.Create("Over ons", home.Id, SiteAliases.Page);
            over.TemplateId = pageTemplate.Id;
            over.SetValue("intro", "Scriptonic is een klein en wendbaar softwarebedrijf: korte lijnen, eerlijk advies en code waar we trots op zijn.");
            over.SetValue("bodyText", "## Waarom Scriptonic\nGrote bureaus leveren lagen; wij leveren software. Je schakelt rechtstreeks met de mensen die bouwen, en die blijven ook na de livegang verantwoordelijk voor hosting, updates en doorontwikkeling.\n\n## Hoe we werken\nWe beginnen klein en concreet: eerst iets werkends neerzetten, dan uitbreiden. Je ziet elke stap terug op een acceptatieomgeving voordat iets live gaat — geen verrassingen.\n\n## Techniek\nWe bouwen op een moderne, bewezen stack: Umbraco CMS en .NET voor websites en portalen, en moderne game-engines voor onze games. Alles draait in containers met een gescheiden acceptatie- en productieomgeving.");
            over.SetValue("seoDescription", "Over Scriptonic: klein softwarebedrijf met korte lijnen. Websites, games en maatwerk — gebouwd, gehost en beheerd door hetzelfde team.");
            SavePublish(over);
        }

        // Contact
        if (!Exists(SiteAliases.Contact))
        {
            IContent contact = _contentService.Create("Contact", home.Id, SiteAliases.Contact);
            contact.TemplateId = contactTemplate.Id;
            contact.SetValue("intro", "Een idee, een vraag of gewoon benieuwd wat er kan? Stuur een bericht — we reageren meestal binnen één werkdag.");
            contact.SetValue("email", "info@scriptonic.nl");
            contact.SetValue("seoDescription", "Neem contact op met Scriptonic voor websites, games en maatwerk software.");
            SavePublish(contact);
        }

        // Portaal-root (verborgen, alleen offertes) + demo-offerte
        if (!Exists(SiteAliases.PortalRoot))
        {
            IContent portal = _contentService.Create("Portaal", home.Id, SiteAliases.PortalRoot);
            SavePublish(portal);

            // Demo-offerte zodat het portaal direct iets toont in demo-modus.
            IContent offerte = _contentService.Create("Offerte O2026-007 — Webshop uitbreiding", portal.Id, SiteAliases.Offerte);
            offerte.SetValue("relationCode", "DEMO001");
            offerte.SetValue("offerteNumber", "O2026-007");
            offerte.SetValue("offerteDate", DateTime.Today.AddDays(-5));
            offerte.SetValue("validUntil", DateTime.Today.AddDays(25));
            offerte.SetValue("amount", 3630.00m);
            offerte.SetValue("status", "Open");
            offerte.SetValue("description", "Uitbreiding van de webshop met een klantenaccount, verlanglijstjes en een koppeling met het voorraadsysteem. Inclusief ontwerp, bouw en oplevering op de acceptatieomgeving.");
            SavePublish(offerte);
        }

        _logger.LogInformation("Seeded starter content");
    }

    private void SeedDienst(int parentId, ITemplate template, string name, string icon, string summary, string body, string features)
    {
        IContent dienst = _contentService.Create(name, parentId, SiteAliases.Dienst);
        dienst.TemplateId = template.Id;
        dienst.SetValue("icon", icon);
        dienst.SetValue("summary", summary);
        dienst.SetValue("bodyText", body);
        dienst.SetValue("features", features);
        SavePublish(dienst);
    }

    private void SeedCase(int parentId, ITemplate template, string name, string client, string projectType,
        string status, string projectUrl, string summary, string body, string tags)
    {
        IContent item = _contentService.Create(name, parentId, SiteAliases.Case);
        item.TemplateId = template.Id;
        item.SetValue("client", client);
        item.SetValue("projectType", projectType);
        item.SetValue("status", status);
        if (!string.IsNullOrEmpty(projectUrl))
        {
            item.SetValue("projectUrl", projectUrl);
        }
        item.SetValue("summary", summary);
        item.SetValue("bodyText", body);
        item.SetValue("tags", tags);
        SavePublish(item);
    }

    private void SavePublish(IContent content)
    {
        _contentService.Save(content);
        var result = _contentService.Publish(content, ["*"]);
        if (!result.Success)
        {
            _logger.LogWarning("Publish failed for {Name}: {Reason}", content.Name, result.Result);
        }
    }

    // ---- Helpers ----------------------------------------------------------

    private async Task<ContentType> NewTypeAsync(string alias, string name, string icon, string groupAlias, string groupName)
    {
        await Task.CompletedTask;
        var type = new ContentType(_shortStringHelper, -1)
        {
            Alias = alias,
            Name = name,
            Icon = icon,
            AllowedAsRoot = false,
        };
        type.AddPropertyGroup(groupAlias, groupName);
        return type;
    }

    private void FinishType(ContentType type, ITemplate template)
    {
        type.AllowedTemplates = [template];
        type.SetDefaultTemplate(template);
    }

    private async Task AddSeoGroupAsync(ContentType type)
    {
        IDataType textstring = await RequireDataTypeAsync(Constants.PropertyEditors.Aliases.TextBox, "Textstring");
        IDataType textarea = await RequireDataTypeAsync(Constants.PropertyEditors.Aliases.TextArea, "Textarea");
        IDataType toggle = await RequireDataTypeAsync(Constants.PropertyEditors.Aliases.Boolean, "True/false");
        type.AddPropertyGroup("seo", "SEO");
        AddPropertyToGroup(type, textstring, "seoTitle", "SEO-titel", 1, "seo", "Overschrijft de paginatitel in zoekmachines.");
        AddPropertyToGroup(type, textarea, "seoDescription", "SEO-omschrijving", 2, "seo", "Max ± 155 tekens; getoond in zoekresultaten.");
        AddPropertyToGroup(type, toggle, "umbracoNaviHide", "Verbergen in navigatie", 3, "seo", null);
    }

    private async Task AddTextAsync(ContentType type, string alias, string name, int sortOrder,
        bool area = false, string? description = null)
    {
        IDataType dataType = area
            ? await RequireDataTypeAsync(Constants.PropertyEditors.Aliases.TextArea, "Textarea")
            : await RequireDataTypeAsync(Constants.PropertyEditors.Aliases.TextBox, "Textstring");
        AddProperty(type, dataType, alias, name, sortOrder, description);
    }

    private void AddProperty(ContentType type, IDataType dataType, string alias, string name, int sortOrder, string? description = null)
        => AddPropertyToGroup(type, dataType, alias, name, sortOrder, type.PropertyGroups.First().Alias ?? "inhoud", description);

    private void AddPropertyToGroup(ContentType type, IDataType dataType, string alias, string name, int sortOrder, string groupAlias, string? description)
    {
        var property = new PropertyType(_shortStringHelper, dataType, alias)
        {
            Name = name,
            SortOrder = sortOrder,
            Description = description,
        };
        type.AddPropertyType(property, groupAlias);
    }

    private async Task<IDataType> EnsureBedragDataTypeAsync()
    {
        IEnumerable<IDataType> byEditor = await _dataTypeService.GetByEditorAliasAsync(Constants.PropertyEditors.Aliases.Decimal);
        IDataType? existing = byEditor.FirstOrDefault(d => d.Name == "Bedrag");
        if (existing is not null)
        {
            return existing;
        }

        if (!_propertyEditors.TryGet(Constants.PropertyEditors.Aliases.Decimal, out IDataEditor? editor))
        {
            throw new InvalidOperationException("Decimal property editor not found.");
        }

        var dataType = new DataType(editor, _configSerializer)
        {
            Name = "Bedrag",
            DatabaseType = ValueStorageType.Decimal,
        };
        var attempt = await _dataTypeService.CreateAsync(dataType, Constants.Security.SuperUserKey);
        return attempt.Success ? attempt.Result : throw new InvalidOperationException("Failed to create Bedrag data type.");
    }

    private async Task<IDataType> RequireDataTypeAsync(string editorAlias, string preferredName)
    {
        IEnumerable<IDataType> candidates = await _dataTypeService.GetByEditorAliasAsync(editorAlias);
        IDataType? match = candidates.FirstOrDefault(d => d.Name == preferredName) ?? candidates.FirstOrDefault();
        return match ?? throw new InvalidOperationException($"No data type found for editor {editorAlias}.");
    }
}
