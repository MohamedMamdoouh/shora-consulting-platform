# Custom domain and TLS (spec 09.6)

Bind a custom domain to the Shora App Service **after** the first deploy succeeds on the default `*.azurewebsites.net` hostname.

**Related:** [production-config.md](production-config.md) · [azure-prerequisites.md](azure-prerequisites.md)

## Prerequisites

- App Service deployed and healthy on default hostname
- Domain registered (e.g. `shora.example.com`)
- DNS access at your registrar or DNS host

## 1. Add custom domain in Azure Portal

1. **App Service** → **Custom domains** → **Add custom domain**
2. Enter hostname, e.g. `shora.example.com` (apex) or `app.shora.example.com` (subdomain)
3. Follow validation instructions:
   - **Subdomain:** add **CNAME** record pointing to `<webapp>.azurewebsites.net`
   - **Apex (root):** add **A** records to Azure IP addresses shown, or use **ALIAS/ANAME** if your DNS provider supports it
4. Wait for **Validated** status (DNS propagation may take up to 48 h; usually minutes)

## 2. Enable HTTPS

1. **Custom domains** → select domain → **Add binding**
2. **TLS/SSL type:** **SNI SSL**
3. **Certificate:** **App Service Managed Certificate** (free; requires validated domain on Basic+ plan)
4. Confirm binding shows **Secure** with HTTPS

Alternatively upload your own certificate under **TLS/SSL settings → Private Key Certificates**.

## 3. Update App Service settings

Update **all** URL-dependent settings to the new HTTPS origin (no trailing slash):

| Setting | Example |
| --- | --- |
| `Frontend__BaseUrl` | `https://shora.example.com` |
| `Cors__AllowedOrigins__0` | `https://shora.example.com` |

Restart the web app after saving.

Or use Azure CLI:

```powershell
az webapp config appsettings set `
  --resource-group rg-shora-prod `
  --name app-shora-prod `
  --settings `
    Frontend__BaseUrl=https://shora.example.com `
    Cors__AllowedOrigins__0=https://shora.example.com
```

## 4. Google OAuth (if enabled)

In [Google Cloud Console](https://console.cloud.google.com/) → **APIs & Services → Credentials** → your OAuth client:

- Add **Authorized JavaScript origins:** `https://shora.example.com`
- Keep the old `*.azurewebsites.net` origin until you fully cut over, then remove it

No redeploy needed for backend `Google__ClientId` — only origins change. Redeploy only if you change `googleClientId` in the frontend build.

## 5. Verify

```powershell
.\scripts\post-deploy-verify.ps1 -BaseUrl https://shora.example.com
```

Manual checks:

1. Browse `https://shora.example.com/` — SPA loads
2. Login / refresh token works (same-site cookies)
3. Trigger a password-reset or verification email — links use the new domain
4. Google sign-in button works (if configured)

## 6. Optional — redirect default hostname

To force traffic to the custom domain:

1. **App Service** → **Custom domains** → set your custom domain as **primary**
2. Or add a redirect rule in `web.config` / middleware (not required for MVP if you share the custom domain publicly)

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| Domain validation fails | Confirm CNAME/A records; use `nslookup` or online DNS checker |
| Managed certificate stuck | Domain must be validated; plan must be Basic or higher |
| Login works on azurewebsites.net but not custom domain | Update `Frontend__BaseUrl` and `Cors__AllowedOrigins__0`; clear cookies |
| Google sign-in fails on custom domain | Add origin in Google Cloud Console |
