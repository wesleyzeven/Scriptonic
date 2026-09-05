# Scriptonic site — notes for Claude

Umbraco 17 LTS (.NET 10) company site + customer portal. Follows the same
conventions as D:\Repos\ToyShop (see that repo's CLAUDE.md heritage):

- SQLite (`|DataDirectory|/…` connection strings), no external DB.
- ModelsBuilder `Nothing`, Razor precompiled at build → **edit views in the
  repo**, backoffice template editing does nothing.
- `SiteSeedHandler` seeds doc types/templates/member type/content idempotently
  on first boot; it never overwrites editor changes.
- Custom EF `SiteDbContext` (contact messages) migrates after `BootUmbracoAsync`.
- Tailwind v4 via `@tailwindcss/cli`; source `Assets/site.css`, output
  `wwwroot/css/site.css` (gitignored, built in Dockerfile stage).

Portal specifics:

- Members (type `portalKlant`) log in at `/portaal/login`; properties
  `relationId`/`relationCode` link them to e-Boekhouden.
- `IEboekhoudenClient`: live REST client when `Site:Eboekhouden:ApiToken` set,
  else demo client with sample data. Session token cached in
  `EboekhoudenSessionCache`, retried once on 401.
- e-Boekhouden REST API has **no quotes endpoint**: offertes are content nodes
  under the hidden "Portaal" node, matched by `relationCode`.
- The offerte doc type intentionally has **no template** (no public URL).

Deploy:

- **Production = Qweb** (Plesk/IIS on a shared Windows server, no Docker):
  `deploy-qweb.yml` publishes self-contained win-x64 and pushes it with Web
  Deploy to IIS site `scriptonic.nl`. Secrets live in `secrets.json` in the site
  root (skipped by the sync); non-secret prod config is in
  `src/Scriptonic.Web/web.config`. Details: `deploy/qweb/README.md`.
- **Homelab (Linux, Docker) = staging/acceptance**: main → port 8220
  (scriptonic.local.io), develop/acceptance → 8230 (scriptonic-acc.local.io);
  8200/8210 are taken by the Beszel/Scrutiny monitoring stack. Compose dirs
  under C:\DockerProjects\Scriptonic\.

Planned: application portal ("sollicitatieportaal") as a later addition.
