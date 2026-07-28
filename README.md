# Scriptonic

Bedrijfswebsite + klantportaal van Scriptonic, gebouwd op **Umbraco 17 LTS (.NET 10)**.

- **Marketing site** (Nederlands): diensten (websites, games, maatwerk), portfolio
  (Vivian den Hollander, Hans den Hollander, Trails of Hooves), over ons, contact.
- **Klantportaal** (`/portaal`): klanten loggen in als Umbraco-member en zien hun
  **facturen live uit e-Boekhouden**, hun **offertes** (beheerd als content onder
  *Portaal* in de backoffice) en kunnen hun **gegevens inzien en wijzigen** —
  wijzigingen gaan via `PATCH /v1/relation` direct naar e-Boekhouden.
- **SEO**: per-pagina meta/OG-tags, canonical URLs, JSON-LD Organization,
  `/sitemap.xml` en `/robots.txt`.
- **Contactformulier** met honeypot; berichten in SQLite, uitlezen via
  `/admin/berichten?key=<SITE_ADMIN_KEY>`.

## e-Boekhouden

De REST API (`https://api.e-boekhouden.nl`) kent **geen offertes-endpoint** —
alleen facturen, relaties, mutaties enz. Daarom:

- **Facturen**: live via `GET /v1/invoice?relationId=…` + open posten via
  `GET /v1/mutation/invoice/outstanding`.
- **Offertes**: als content-items onder *Portaal* in de backoffice (nummer,
  bedrag, status, PDF), gekoppeld aan de klant via **relatiecode**.
- Members van type *Portaal klant* hebben `relationId` + `relationCode`
  properties; vul die in bij het aanmaken van een klant-login (waardes staan in
  e-Boekhouden onder Relaties).

Zonder `Site:Eboekhouden:ApiToken` draait het portaal in **demo-modus** met
voorbeelddata en (op acc/dev) een demo-login: `demo@scriptonic.nl` / `DemoKlant123!`.

## Lokaal draaien

```bash
cd src/Scriptonic.Web
npm install && npm run build   # Tailwind → wwwroot/css/site.css
dotnet run                     # http://localhost:5210 (admin: wesley@scriptonic.nl / ChangeMe123!)
```

Of als container: `docker compose up --build` → http://localhost:8209.

Eerste boot: unattended install + idempotente seeding (documenttypes, templates,
membertype, startcontent). Templates zijn build-time gecompileerd
(ModelsBuilder `Nothing`) — views wijzig je in de repo, niet in de backoffice.

## Deploy (homelab-patroon)

| Omgeving  | Branch             | Poort | Hostnaam                  |
|-----------|--------------------|-------|---------------------------|
| acceptance| `develop`/`acceptance` | 8230  | scriptonic-acc.local.io   |
| production| `main` / `v*`-tag  | 8220  | scriptonic.local.io       |

GitHub Actions (self-hosted runner `homelab, windows`) bouwt de image en doet
`docker compose up -d` vanuit `C:\DockerProjects\Scriptonic\{acceptance,production}`.
Zet daar eerst `.env` neer (zie `deploy/*/.env.example`).

## Later

- **Sollicitatieportaal**: komt als aparte sectie/module op deze site.
- SMTP voor wachtwoord-reset en contactformulier-notificaties.
