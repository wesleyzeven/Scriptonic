# Deploy to Qweb (Plesk, IIS)

Production runs on the Qweb shared Windows server (`web22.foxxl.com`, Plesk
Obsidian) as an IIS site at `scriptonic.nl`. There is no Docker there; the site
is published **self-contained for win-x64** and pushed with **Web Deploy** by
`.github/workflows/deploy-qweb.yml`.

## One-time setup

1. **GitHub secret** `QWEB_DEPLOY_PASSWORD`: the password of the Plesk system
   user `scriptonic` (Plesk > scriptonic.nl > Hosting Settings > System user).
   Add it under *Settings > Secrets and variables > Actions* (repository secret,
   or on the `production` environment).
2. **`secrets.json` on the server**: copy `secrets.example.json` to
   `secrets.json` in the site root (Plesk > Files > `scriptonic.nl`) and fill
   in the backoffice admin password, the admin key and the e-Boekhouden token.
   Web Deploy never overwrites or deletes this file.
3. **First deploy over the old site**: Actions > *Deploy Qweb* > *Run workflow*
   with **fresh_install** ticked. This is the only run that also removes the
   old `umbraco/Data`, `umbraco/Logs` and `wwwroot/media` folders; every later
   run preserves them.
4. After the first boot, in Plesk > Hosting Settings tick *Redirect visitors
   from HTTP to HTTPS* (Let's Encrypt certificate is already there).

## What each deploy does

- Builds Tailwind, runs `dotnet publish -r win-x64 --self-contained` (no
  dependency on which .NET runtime Qweb installed), stamps the version into
  `web.config`, then `msdeploy -verb:sync` with `AppOffline` so locked DLLs
  can be replaced.
- Skipped on the server: `umbraco/Data` (SQLite DBs + data-protection keys),
  `umbraco/Logs`, `wwwroot/media`, `secrets.json`.
- Smoke-checks `https://scriptonic.nl/health`.

## Notes

- `src/Scriptonic.Web/web.config` holds the IIS settings and the non-secret
  production config (public URL, https, unattended install). Plesk's ".NET
  Core" page edits the same file and is overwritten on every deploy.
- The app pool idles out after 30 minutes; the first request after that is a
  cold start (tens of seconds). The pool also throttles at 50 % CPU.
- If the site returns HTTP 500.3x on first boot, switch `hostingModel` in
  `web.config` to `outofprocess` (older ASP.NET Core Module on the server).
