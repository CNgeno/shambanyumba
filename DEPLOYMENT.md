# Deploying SmartBiz Estate to Azure

Target: **Azure App Service (Linux, .NET 10)** + **Azure SQL Database**. One App Service plan, one database.

## 1. Create the resources

```bash
az group create -n smartbiz-rg -l southafricanorth          # nearest Azure region to Kenya
az sql server create -n smartbiz-sql -g smartbiz-rg -l southafricanorth -u sqladmin -p '<strong password>'
az sql db create -n SmartBizERP -s smartbiz-sql -g smartbiz-rg --service-objective S0
az sql server firewall-rule create -g smartbiz-rg -s smartbiz-sql -n AllowAzure --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0

az appservice plan create -n smartbiz-plan -g smartbiz-rg --sku B1 --is-linux
az webapp create -n smartbiz-estate -g smartbiz-rg -p smartbiz-plan --runtime "DOTNETCORE:10.0"
```
Start on B1/S0; scale up when real customers arrive.

## 2. Configuration (App Service > Settings > Environment variables)

Never put production secrets in `appsettings.json`.

| Setting | Value |
|---|---|
| `ConnectionStrings__DefaultConnection` | `Server=tcp:smartbiz-sql.database.windows.net,1433;Database=SmartBizERP;User ID=sqladmin;Password=...;Encrypt=True;` (add it as a **connection string** of type SQLAzure) |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` (App Service terminates HTTPS in front of the app) |
| `SeedAdmin__Email` / `SeedAdmin__Password` | First platform admin. Remove after the first start. |
| `Storage__LocalRoot` | `/home/data/uploads` (persistent on App Service Linux; see section 5) |
| `EmailSettings__Host` | e.g. `smtp-relay.brevo.com`, `smtp.office365.com` |
| `EmailSettings__Port` | `587` |
| `EmailSettings__UserName` / `EmailSettings__Password` | SMTP login and key (never commit the key) |
| `EmailSettings__FromAddress` / `EmailSettings__FromName` | A sender verified with your provider, ideally on your own domain, e.g. `no-reply@yourdomain.co.ke` |
| `EmailSettings__EnableSsl` | `true` |

In Production the app never seeds demo data. It applies pending migrations (unless `Database__MigrateOnStartup` is
`false`, see section 3), then seeds plans, settings and roles.

## 3. Database migrations

Two ways. Pick one.

**Before every publish:** run `dotnet test`. The *Model matches the migrations* test fails if a model change has no
migration (the same check makes the app stop at startup with "pending model changes"). Fix it with
`dotnet ef migrations add <Name> ...` and check the migration before publishing.

**A. Let the app migrate on startup (the default from Version 1).** Nothing to set: every launch applies pending
migrations before seeding. (You can still set `Database__MigrateOnStartup` = `true` explicitly (optionally `Database__MigrationTimeoutSeconds` = `300`). On every start the app
runs its safety checks, applies pending migrations (EF Core locks so two instances can't migrate at once), then seeds.
Trade-offs: the app's database login needs permission to change the schema (e.g. db_owner), startup is slower while
migrating, and a failed migration stops the site from starting (the log says why). Deploy code and it just works.

**B. Apply migrations yourself before deploying (most control).** Set `Database__MigrateOnStartup` = `false`, then:

```bash
dotnet ef migrations script --idempotent \
  --project src/RealEstateERP.Infrastructure --startup-project src/RealEstateERP.Web -o migrate.sql
```
Run `migrate.sql` against Azure SQL (portal Query editor, Azure Data Studio, `sqlcmd`, or a pipeline step) **before**
deploying new code. The script is idempotent: it only applies what's missing. For pipelines, `dotnet ef migrations bundle`
builds a single executable that does the same.

Either way: if the app starts with migrations still pending, it logs a **critical** message naming them, and `/health`
reports **Degraded** with the list (still HTTP 200, so the site keeps serving; point an Application Insights
availability test at `/health` and alert when the body isn't "Healthy").

## 4. Deploy

```bash
dotnet publish src/RealEstateERP.Web -c Release -o publish
cd publish && zip -r ../app.zip . && cd ..
az webapp deploy -g smartbiz-rg -n smartbiz-estate --src-path app.zip --type zip
```
Then in App Service:
- **Health check**: set the path to `/health` (reports the database too).
- **HTTPS only**: On. **Minimum TLS**: 1.2.
- **Always on**: On (B1 and above).

## 5. Before go-live checklist

- [ ] Admin > Settings: replace every `CHANGE ME` (paybill, bank details) and set support email/phone.
- [ ] Uploaded files (payment proofs, tenant documents) are stored under `Storage:LocalRoot`. `/home` is persistent
      and shared on App Service, which is fine for one instance. **Before scaling out to several instances, move
      `IFileStorage` to Azure Blob Storage** (one new class implementing `IFileStorage`).
- [ ] Data protection keys (login cookies) are kept by App Service automatically under `/home`; fine for one region.
- [ ] Email: set the SMTP server in **Admin > Settings > Email** and use "Send test email" (or use the `EmailSettings__*`
      app settings instead). Without them nothing is delivered in Production (a warning is
      logged), so new users can't confirm their accounts or reset passwords. Send yourself a password reset to test.
      Set up SPF/DKIM for the sending domain so emails don't land in spam.
- [ ] PDFs use QuestPDF (Community licence: free under USD 1M annual revenue; confirm the current terms).
      Open one invoice PDF after deploying to confirm fonts render on Linux.
- [ ] Backups: Azure SQL point-in-time restore is on by default (7 days on S0); consider long-term retention.
- [ ] Kenya Data Protection Act: publish the final Privacy Policy and Terms (the site has drafts).
- [ ] Mobile smoke test at 375px wide: login, dashboard, record payment, receipt.
- [ ] Remove `SeedAdmin__Password` from settings once the admin has logged in and changed the password.

## 6. Background automation

The app runs a daily automation job (automatic invoices, penalties, tenant emails) inside the web app. Keep
**Always on** enabled so it keeps running when nobody is browsing. It's safe with several instances (each business is
claimed once per day). To switch it off on a particular app (e.g. a staging slot), set `Automation__Enabled` = `false`.

## 7. Monitoring

Enable **Application Insights** on the App Service (no code change needed for basic request and exception telemetry).
Watch: failed requests, `/health` status, and SQL DTU usage.
