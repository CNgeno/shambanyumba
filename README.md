# SmartBiz Real Estate ERP

ASP.NET Core MVC on .NET 10 (LTS), SQL Server, EF Core 10 code-first, ASP.NET Identity.

## Sprint 0 — Foundation
- Registration + email confirmation (Identity UI), login, lockout
- Business creation, membership with per-business roles (Owner/Admin/User/Viewer)
- Business selection screen on a clean layout; switch business at any time
- `TenantResolutionMiddleware`: no business selected -> redirected to selection
- Automatic tenant isolation (named EF query filter) + automatic `BusinessId` stamping
- Cross-tenant write guard, soft delete, audit stamps, rowversion on every domain table
- SuperAdmin role seeding
- Tests proving zero data bleed

## Sprint 1 — Subscriptions (FR-02, NFR-06)
- `SubscriptionPlan` table (Starter, Professional, Enterprise, Agent) — admin-editable, nothing hardcoded
- `SystemConfiguration` table (grace days, trial plan, paybill, bank details, upload limit, billing defaults)
- New business automatically gets a trial (same transaction as the business)
- Status Trial / Active / Grace / Suspended is **computed from dates**, never stored, so no job is needed
  and it can't drift
- Global `SubscriptionGateFilter`: suspended businesses are sent to `/Subscription/Renew`; grace shows a banner
- Renewal: plan + 1/3/6/12 months + M-PESA or bank + reference + proof -> Pending
- Platform admin: approve/reject payments (with proof preview), edit plans, edit settings
- Renewals stack: paying early continues from the current end date; paying after suspension starts today
- Database guarantees: a reference can't be reused, a payment can only create one period

**Migration:** `dotnet ef migrations add Sprint1Subscriptions` (same `--project`/`--startup-project` flags as below).
**Before go-live:** change the `CHANGE ME` values under Admin > Settings (paybill, bank details).

## Sprint 2 — Portfolio (FR-03)
- Properties (assets): type, stage, location, county (all 47), land size, acquisition cost/date, title deed
- Structure tree of any depth: Estate > Block > Floor (> Wing). Moves into a node's own child are blocked
- Units under a property or any node; unit numbers unique per property; natural sorting (A2 before A10)
- Bulk creation: prefix + first number + count (e.g. A101 to A110), all-or-nothing, max 200
- Manual status changes: Available / Reserved / Inactive / Sold. **Occupied only comes from leases (Sprint 3)**
- Plan unit limit enforced on create, bulk create and reactivation (Inactive and Sold units don't count)
- Property page: occupancy, vacancy, rent roll; filter units by status and by block/floor
- Every tenant table now has a real foreign key to `Businesses`

**Migration:** `dotnet ef migrations add Sprint2Portfolio --project src/RealEstateERP.Infrastructure --startup-project src/RealEstateERP.Web --output-dir Data/Migrations`

## Sprint 3 — Tenants and leases (FR-04, FR-05)
- Tenants: person or company, ID / registration number (unique per business), KRA PIN, phones normalised
  to +2547XXXXXXXX, employer, emergency contact, notes; status Prospective / Active / Former is computed
- Tenant documents (ID, KRA PIN, signed agreement, payslips...): private storage, JPG/PNG/PDF, signature-checked
- Leases: numbered L-00001 per business, start / optional end, rent snapshot, deposit, notice period, rent due day (1-28)
- Draft -> Active: activation sets the unit to Occupied in the same save. **The database allows only one active
  lease per unit** (filtered unique index)
- Renew: new lease linked to the old one (old -> Renewed), unit stays occupied; runs in one transaction
- End early: notice date, reason, suggested notice-shortfall penalty, deposit deductions and refund due;
  unit becomes Available or Inactive
- Leases are NOT expired automatically: past-end leases are flagged until someone renews or ends them
- Expiry warnings (30/60/90 days) on the Leases page and dashboard; property page shows each unit's tenant
- Units and tenants with lease history can't be deleted
- Moved: lease charges -> Sprint 4 (needs charge definitions); tenant statement -> Sprint 6 (needs invoices/payments)

**Migration:** `dotnet ef migrations add Sprint3TenantsLeases --project src/RealEstateERP.Infrastructure --startup-project src/RealEstateERP.Web --output-dir Data/Migrations`

## Sprint 4 — Billing (FR-05-B, FR-06, FR-07)
- Charge library: recurring (garbage, security), utility (water per m³, electricity per kWh), one-time, penalty; tax %
- Lease charges with optional special price/rate; carried over automatically on renewal
- Meter readings: one sheet per property + utility + date; previous reading pre-filled; live usage/amount;
  first reading on a meter is an opening reading (never billed); readings must go up and can't be future-dated
- Monthly generation with preview: rent (first month prorated), deposit for brand-new leases only,
  recurring charges, unbilled readings. Drafts by default, or issue straight away
- **Database guarantees:** one monthly invoice per lease per month (cancelled ones excluded);
  a reading is billed once (BilledInvoiceId). Re-running a month only fills gaps
- One-off invoices with a line editor (add from charges, VAT per line); final invoice for termination penalties
- Status: Draft / Pending stored; Paid / Partly paid / **Overdue computed** from AmountPaid and DueDate (no job)
- Cancel (reason; frees readings for re-billing) and write off (reason) for Owner/Admin
- Print-friendly invoice page (PDF comes in Sprint 6)

**Migration:** `dotnet ef migrations add Sprint4Billing --project src/RealEstateERP.Infrastructure --startup-project src/RealEstateERP.Web --output-dir Data/Migrations`

## Sprint 5 — Collections (FR-08, FR-04-C, NFR-07)
- Record payments: M-PESA (10-character code checked), bank transfer, cheque, cash; numbered receipts RCT-00001
- Apply to: oldest invoices first (default), chosen invoices with amounts, or keep as credit; live preview
- **Payments are never edited or deleted.** Reversal (Owner/Admin, reason required) marks the payment and its
  allocations reversed, keeps the original values, and makes the invoices owed again
- The same M-PESA code / bank reference / cheque can't be recorded twice (unique index), unless the first was reversed
- Overpayments are held as credit: apply to later invoices with one click, or refund (RFD-00001)
- Deposit refunds once a lease has ended, up to the deposit less deductions
- Printable receipts showing what each payment paid and the balance after it
- Tenant statement (moved forward from Sprint 6): invoices, payments, reversals, write-offs and refunds with a
  running balance and date range; it is calculated from the records, so it always agrees with the account balance
- Balances: tenants list (with an "Owing money" filter), tenant page, invoice page; "Collected this month" on the dashboard
- `PaymentService` is the ONLY code that changes `Invoice.AmountPaid`, always in the same save as the allocation

**Migration:** `dotnet ef migrations add Sprint5Collections --project src/RealEstateERP.Infrastructure --startup-project src/RealEstateERP.Web --output-dir Data/Migrations`

## Sprint 6 — Arrears, credits, PDFs, deployment (FR-09, FR-15)
- Arrears: overdue balances aged 1-30 / 31-60 / 61-90 / 90+ days, by property and by tenant, with collections
  stage, unapplied credit and last payment
- Late penalties: preview, tick, issue. Grace period = unit's setting or the system default (5 days); amount = unit's
  fixed late penalty or 5% of the overdue balance (Settings). Issued as separate penalty invoices.
  **The database allows one penalty per invoice**, and penalties are never charged on penalties
- Collections workflow: Normal > In arrears > Demand served > Legal > Written off, with a dated notes timeline,
  promised-payment dates and an attached document (e.g. the demand letter)
- Credits: balance + full ledger; plan credits granted on trial and on each approved renewal (credits x months);
  bundles bought by M-PESA/bank and approved by the platform admin; admin adjustments.
  Costs: invoice/receipt PDF 1, statement PDF 2, invoice runs creating more than 10 invoices 5
- PDFs (QuestPDF): invoice, receipt, statement. **Not compiled in the development sandbox (needs NuGet)**
- `/health` endpoint (checks the database) and DEPLOYMENT.md for Azure App Service + Azure SQL
- Penalties are applied from the Arrears page rather than automatically each night; a nightly job needs a
  per-business on/off setting first

**Migration:** `dotnet ef migrations add Sprint6Arrears --project src/RealEstateERP.Infrastructure --startup-project src/RealEstateERP.Web --output-dir Data/Migrations`

## Sprint 7 — Accounts and admin (FR-01-E)
- Own pages for Sign in, Register (now asks for the full name and terms), Forgot password, Reset password
  and the "check your email" screens; show/hide password; friendly errors; lockout after 5 wrong attempts
- Account settings ("Your account") inside the app layout: Profile (name, phone), Email, Password,
  Two-step sign-in, Your data. A custom sign-in manager keeps the selected business when Identity refreshes
  the cookie after a password or profile change
- Email: SMTP in production (`Email:Smtp:*` settings, no extra packages). **Development: every email lands in
  the dev mailbox at `/dev/mailbox`** (linked from the login page), so confirmation, reset and invitation links
  can be tested locally
- Business settings (Owner/Admin): details, logo (shown on PDFs), and Team: invite by email with a role,
  change roles, remove members, resend/cancel invitations. Rules: a business always keeps an Owner; only
  Owners can grant, change or remove the Owner role; an invitation can only be accepted by the email it was
  sent to (confirmed); links expire after 7 days and only a hash of the token is stored
- Invitations also appear on the business selection page
- Platform admin: Users (confirm email, lock/unlock, send reset link, resend confirmation, platform admin access)
  and Businesses (owner, plan, status, paid through, members, credits)

**Migration:** `dotnet ef migrations add Sprint7Accounts --project src/RealEstateERP.Infrastructure --startup-project src/RealEstateERP.Web --output-dir Data/Migrations`

## Sprint 8 — Agency (FR-10)
Visible to **Agency** businesses only (business type is now in the sign-in cookie; other types see an explanation).
- Property owners (the agency's clients) with payout details: bank account or M-PESA number
- Agents with EARB licence number and their share of the agency's commission
- Management contracts MC-00001: owner, agent, % of collections or fixed monthly fee, owner payment day,
  properties covered (with dates). Draft > Active > Ended. **A property can be under only one active contract**
  (filtered unique index)
- Owner remittances REM-00001 per contract per month (one per month, database-enforced): rent and charges
  actually received that month (cash basis), **deposits left out pro rata** (held for tenants), reversed payments
  ignored, less commission, plus/minus adjustments (e.g. repairs). Draft > Approved > Paid (with reference),
  or Cancelled to prepare again. Owner statement PDF (2 credits)
- Agent commission report by month; property pages show who a property is managed for
- **Agents sign in:** link a team member (role User or Viewer) to an agent profile. They then see only their own
  contracts, the owners on them, their statements and their commission ("My contracts", "My commission"). They can
  prepare statements and add adjustments; only the Principal or an Admin can change contracts, approve or pay.
  Enforced in the services (not just hidden buttons), re-checked every 2 minutes, cleared instantly on (un)linking
- Labels: in agencies the account Owner shows as **Principal**, linked members as **Agent**; the switcher shows the
  business type. Permissions are unchanged
- Agents page: login column and "Move contracts" (reassign all open contracts to another agent)
- Dashboard: "Your portfolio" for agents, "Agency, <month>" for the Principal (statements to prepare, commission)
- Demo: james@smartbiz.test signs in as James Kamau (agent at Mji Property Agents)
- Demo: agent@smartbiz.test (Mji Property Agents): 2 owners, 2 agents, 2 contracts, last month paid except W3;
  A5 is a new lease whose deposit is excluded. Prepare last month's statements under Owner remittances

**Migration:** `dotnet ef migrations add Sprint8Agency --project src/RealEstateERP.Infrastructure --startup-project src/RealEstateERP.Web --output-dir Data/Migrations`. If you already added Sprint8Agency, add one more for the agent logins: `dotnet ef migrations add AgentLogins` (same flags).

## Sprint 9 — Sales (FR-11)
Visible to **Developer** and **Agency** businesses.
- Buyers (separate from tenants)
- Sales SL-00001: buyer, unit, price, optional agent + commission %, full payment or deposit + monthly/quarterly
  instalments (schedule generated; rounding cents on the last instalment; live preview on the form)
- Lifecycle: **Reserved** (unit reserved) > **Active** (sale agreement milestone: unit Sold) > **Completed** (paid in
  full AND handed over), or **Cancelled** (unit Available or Inactive; refused while money is held).
  **One live sale per unit** (filtered unique index); a unit with a sale can't be changed by hand
- Buyer payments SR-00001: oldest instalment first, no overpayment, duplicate M-PESA codes refused, immutable
  (reverse with a reason; reversing reopens a completed sale)
- Transfer milestones in order: Agreement > Stamp duty > Title transfer > Handover, each with date, reference,
  notes and a document; **handover only when fully paid**
- Reports: stock by project (available/reserved/sold, unsold value), overdue instalments aged 1-30/31-60/61-90/90+,
  pipeline (reservations), sold units. Dashboard sales panel. Buyer statement PDF (2 credits)
- Sales commission on money collected each month, with agent share, on the agent commission report.
  Agents see and create only their own sales
- Demo: owner@smartbiz.test > Savanna Homes: Plot 1 completed (cash), Plot 2 instalments with one overdue,
  Plot 3 a company paying quarterly (title transfer next), Plot 4 reserved with deposit overdue, Plot 5 booking paid

**Migration:** `dotnet ef migrations add Sprint9Sales --project src/RealEstateERP.Infrastructure --startup-project src/RealEstateERP.Web --output-dir Data/Migrations`

## Referrals and sharing
- **Partners** (Admin > Partners and referrals): influencers, business developers, others; each with a code and a link
  (shambanyumba.com/?ref=CODE, remembered 60 days in the `sn_ref` cookie) and an optional KES commission per paying
  customer. Codes can also be typed on Create your business. Admin sees trials, paying customers, commission owed and
  paid, and marks commission paid with a reference
- **Customer referrals** (Business settings > Refer a landlord): every business gets its own code; each referred
  business that pays gives the referrer 1 month free (their latest subscription period is extended)
- Rewards happen once, at the referred business's first approved subscription payment, in the same save
  (`ReferralRewards`); `PartnerCommissions` has one row per partner and business
- **Share a listing**: WhatsApp, Facebook, X, copy link and the phone's share menu on the public listing page (and on
  the business's listing page); link previews show the listing's photo, title and price
- Migration: `dotnet ef migrations add Referrals` (Partners, PartnerCommissions, Businesses.ReferredByPartnerId)

## Public demo (one click, resets nightly)
- "Try the live demo" on the homepage, tour, announcement bar and sign-in page (also "as an agent" on sign-in).
  `POST /demo/start` (or `?as=agent`) signs in demo@smartbiz.demo (Owner of three demo businesses) or
  demo.agent@smartbiz.demo (James Kamau, agent at Mji Property Agents). No password; the domain doesn't exist
- Demo businesses (`Business.IsDemo`): Acacia Gardens (estate to let), Savanna Homes (land and plot sales),
  Mji Property Agents (agency), seeded with the same journeys as the development seed; 100,000 credits; a 10-year
  Enterprise subscription
- Guard rails: no email ever leaves a demo business; demo listings never appear on the public marketplace; demo
  logins can't open account management, renew, buy credits, create businesses, invite or change settings
  (`DemoGuardMiddleware` -> /demo/not-in-demo); a "Live demo" banner on every page
- `PublicDemoHostedService`: creates the demo at startup if missing; resets it at 03:00 EAT every night (deletes
  every row of demo businesses in one transaction, then seeds again). Settings: `Demo:PublicEnabled` (default true),
  `Demo:ResetHourEat` (3). Run one instance for the reset (or accept a harmless double reset)
- Development databases with the test accounts (DemoData:Enabled) skip the public demo (same business names)
- Migration: `dotnet ef migrations add PublicDemo` (Businesses.IsDemo)

## Version 1 additions
- **Running costs** (Costs > Actual cost, or "Record an expense" on an estate): repairs and maintenance, renovation,
  cleaning, advertising, utilities paid by the owner, rates and insurance, security and caretaker, agent fees, other.
  Never billed to tenants. Net income (rent collected less running costs) on the estate page (this month, 12 months),
  on its dashboard card, and per property in Rent reports
- **"Can see finances"** per team member (Business settings > Team): Rent reports, Costs and margins, running costs,
  net income and Sales reports are for Owners, Admins and members allowed. Users and Viewers: off by default
- **Migrations on every launch by default** (`Database:MigrateOnStartup` now defaults to true; set
  `Database__MigrateOnStartup=false` to manage them yourself). The database login needs schema rights (db_owner)
- Migration: `dotnet ef migrations add Version1` (BusinessUsers.CanViewFinancials)

## Properties decide the menus (no business type)
- Sign-up asks only "We manage properties for other owners?" (agency). Each **property** has a purpose: To let,
  For sale, or both (land defaults to For sale). Existing properties are backfilled at startup (land and every
  property of a former Developer business: For sale; the rest: To let)
- `ICapabilityService` (cached per business, refreshed when properties or settings change): Rentals when any
  property is to let (or none yet), Sales when any is for sale, Agency when switched on AND the plan includes it
  (Agent and Enterprise plans, any plan during the trial; `SubscriptionPlan.IncludesAgency` overrides by plan)
- Sidebar, dashboard, property pages and `[RequireCapability]` gates (Sales, Agency) all follow it; Costs and margins
  is available to every business with properties. The "We also let units" switch is retired
- Plan limits count units in use: everything active, including sold units whose sale is still being paid off
- Migration: `dotnet ef migrations add PropertyPurpose` (Assets.Purpose, SubscriptionPlans.IncludesAgency)

## Estate setup, estate charges, photos and automatic listing
- Model: Account (login) > Business (subscription, credits, team; the paying customer) > Property (estate) > Units.
  The Owner business type now shows as "Landlord"
- **Estate charges** (property page): garbage, security, water... with this estate's amount or rate. New leases in the
  estate get them automatically; "Add to current leases" fills in leases that lack them
- **Guided setup** on each estate's page: units > photos (optional) > charges > opening readings (if metered) >
  leases > first invoices > advertise (optional)
- **Invoice preview warnings**: an estate charge missing from a lease, or no unbilled meter reading to bill, with
  one-click fixes (add missing charges, enter readings)
- **Photos**: estate photos and a listing description on the property page; optional interior photos per unit
  (unit menu > Interior photos)
- **Automatic listing** (Business settings > Automation): the daily job lists every vacant rental unit without a
  listing, using the estate description and photos (interiors first). Public listings show only while the business's
  subscription is usable (trial, paid or grace)
- Migration: `dotnet ef migrations add EstateSetup` (PropertyCharges, AssetPhotos, UnitPhotos, Business.AutoListVacantUnits)

## Developer dashboard and land projects
- Developers don't see the rental menus (Tenants, Leases, Invoices, Meter readings, Charges, Payments, Arrears,
  Reports) unless they tick *Business settings > Details > "We also let units"*. Owners and agencies always see
  them. The choice travels in the sign-in cookie (refreshed on save). The Sales menu gains "Sales reports"
  (Migration: `dotnet ef migrations add RentalToolsSetting`)
- Developer dashboard: a developer setup checklist (project, purchase and budget, plots, first sale) and a card per
  project (plots sold/reserved/available, expected cost, spent, collected, overdue, margin, break-even). Occupancy and
  rent roll only appear once the developer has leases
- A Land property's page (or a developer's property with units for sale) is a project dashboard: land and title,
  plots and roads, expected cost, projected revenue and margin, collections and overdue, quick actions

## Sprint 11B — Reports, charge frequencies, notifications, mobile, real-database tests
- **Reports** (sidebar > Money > Reports; 3/6/12/24 months): invoiced vs collected by month with collection rate,
  revenue by property and by unit type, occupancy and tenant turnover per property, vacant units longest-first with
  rent lost so far. **Platform reports** (Admin): MRR at plan prices, businesses by status, lapsed in 30 days,
  monthly new businesses, subscription + credit revenue, credits used, MRR by plan
- **Charge frequencies**: recurring charges billed monthly, quarterly or annually; quarterly/annual charges fall due in
  the chosen month (quarterly: that month and every 3 months after). The amount is per billing
- **Notifications** (bell in the top bar + Notifications page): new leads, automatic run results, weekly
  "leases ending within 30 days", subscription and credit approvals. Shared per business; unread = newer than when
  you last opened them. Raised through `NotificationLog.Add` in the same save as the event
- **Mobile**: the tenants, leases, invoices, payments and sales lists (and reports) become cards on phones (column
  labels added by `wwwroot/js/site.js`); bigger tap targets; actions panel first on detail pages; scrollable filters
- **Real SQL Server tests** (`SqlServerTests.cs`): set `SMARTBIZ_TEST_SQL` to a connection string (LocalDB works)
  and `dotnet test` creates a throwaway database from the migrations, runs the queries the in-memory tests can't
  check (sales list, reports, public search + view count, owner statement preview, cost overview, arrears, leads,
  filtered unique index), then drops it. Without the variable they're reported as skipped

**Migration:** `dotnet ef migrations add Sprint11BReportsNotifications --project src/RealEstateERP.Infrastructure --startup-project src/RealEstateERP.Web --output-dir Data/Migrations`

## Sprint 11A — Automation and tenant emails
- **Business settings > Automation** (Owner/Admin):
  - Monthly invoices: create and issue every active lease's invoice on a chosen day (1-28; runs the next day if missed),
    once per month, exactly like Invoices > Generate (leases already invoiced are skipped; >10 invoices uses 5 credits;
    a run refused for lack of credits is retried the next day)
  - Late penalties: applied daily, once per overdue invoice. Business grace days and % override the system defaults
    (also for the manual preview); a unit's own settings still win
  - Emails to tenants: invoice emails (automatic invoices and penalty invoices) and receipt emails on every payment,
    with your payment instructions (paybill, account format, bank). Sent from the platform address with Reply-To set
    to the business email
  - "Run now" and a "Last automatic run" summary
- Buttons: "Email to tenant" on issued invoices, "Email receipt" on receipts (shows when last emailed)
- **Daily job** (`AutomationHostedService`): every hour, for businesses with automation on and a usable subscription
  (not suspended), claims the day with the business row's rowversion (safe with several servers) and runs it inside a
  **BusinessScope**, so the job acts as that business with the Owner role and every isolation rule still applies.
  Settings: `Automation:Enabled` (default true), `Automation:IntervalMinutes` (60), `Automation:StartDelayMinutes` (2)
- Email templates now share one frame; new table layout for invoice and receipt emails

**Migration:** `dotnet ef migrations add Sprint11Automation --project src/RealEstateERP.Infrastructure --startup-project src/RealEstateERP.Web --output-dir Data/Migrations`
(or let production apply it with `Database__MigrateOnStartup=true`).

## Deployment: migrations
- Development: pending migrations are applied at startup (with safety checks).
- Production: set `Database:MigrateOnStartup` = `true` (App Service: `Database__MigrateOnStartup`) to do the same, with
  `Database:MigrationTimeoutSeconds` (default 300). Otherwise apply `migrate.sql` before deploying. With migrations
  pending and the setting off, the app logs a critical message and `/health` reports Degraded. See DEPLOYMENT.md

## Sprint 10 — Marketplace and verified leads (FR-12)
- **Listings** (sidebar > Marketplace): advertise a unit to let, or a unit/plot for sale, with up to 12 photos
  (JPG/PNG, choose the cover). Draft > Published > Paused > Closed. One open listing per unit and type
  (filtered unique index). Shown publicly only while Published **and** the unit is Available, so it disappears
  automatically once let or sold
- **Public pages** at `/marketplace` (moved from /properties, which clashed with the app's Properties pages; old links redirect): search by rent/buy, area, county, type, bedrooms and max price; listing page
  with gallery, details, "More at this project" and an enquiry form. Linked from the public menu ("Find a property")
  and the homepage ("Just listed")
- **Verified leads**: the enquirer confirms by email (link valid 48 hours, token stored hashed, single use). Only
  then is the business charged `Marketplace.LeadCredits` (default 10) and notified by email. Repeat enquiries from
  the same email or phone on the same listing within 30 days are free. Not enough credits: the lead waits
  ("Open lead" after topping up). Fake leads can be reported within 7 days for a refund, up to 5 a month
  (all three numbers are in Admin > Settings). Stages: New, Contacted, Viewing, Converted, Lost
- Abuse protection: 5 enquiries per visitor per 10 minutes (ASP.NET Core rate limiter), hidden honeypot field,
  explicit consent to share details, nothing reaches the business until the email is confirmed
- Leads are platform-level rows (written by anonymous visitors), always queried with an explicit BusinessId;
  public reads switch off only the tenant filter (soft delete stays on); view counts use a direct UPDATE
- Homepage and Features now cover plot and house sales, development costing, agency tools and listings
- Email: **Admin > Settings > Email (SMTP)**: server, port, SSL, login, password/key, sender address and name, plus
  "Send test email" (shows the mail server's own error if it fails). The password is a new **Secret** setting type:
  encrypted with ASP.NET Core data protection, never shown again, only replaced or removed. Checked on every send,
  so changes apply immediately. If the SMTP server there is blank, the appsettings `EmailSettings` section is used;
  with neither, the dev mailbox (Development) or a logged warning (Production). If data protection keys change
  (new machine), the saved password can't be read and must be entered again
- Demo: listings at Acacia Court, Savanna Heights and Kilimani Heights; leads at Acacia (one paid, one waiting
  for credits) and Savanna (at viewing stage). Demo listings have no photos (placeholder shown)

**Migration:** `dotnet ef migrations add Sprint10Marketplace --project src/RealEstateERP.Infrastructure --startup-project src/RealEstateERP.Web --output-dir Data/Migrations`
(unzip and build first; check it contains `CreateTable(name: "Listings"`).

## Development costing (developers and owners)
Sidebar > Portfolio > **Costs and margins**, or a property's menu > Costs and margins.
- Purchase details: price, date, seller, title, land size, and a **budgeted land price** for before it's bought
- Cost ledger per property: every line is a **Budget** estimate or an **Actual** cost, in categories grouped as
  Purchase (stamp duty, legal, valuation), Development (construction, infrastructure, professional fees,
  approvals), Subdivision, Other (marketing, finance, holding) and Letting (agent/management fees).
  Lines apply to the whole project or to one unit; actuals need a date (not future) and can carry a receipt
- **Expected cost** per budget line = the higher of budget and actual (overspend counts; unspent budget is still
  expected). Budget vs actual vs expected table with variance
- Shared costs spread over units **by size, equally or by sale value** (falls back to equal if a size/price is
  missing; cents always add up); unit-specific costs stay with the unit
- Sale units: margin on the contract price (or asking price if unsold); projected revenue and margin; break-even
  ("sell N more units to cover the expected cost", cheapest first so it's never optimistic)
- Let units: gross yield and payback; last 12 months rent collected (deposits excluded) less letting costs = net yield.
  Letting costs are not development cost
- Subdivision: approval reference/date, "Create plots" (size and price), area surrendered to roads and common
  areas calculated from the land size, title number per plot
- Demo: Savanna Heights (plots; subdivision over budget, power still to come) and Acacia Court (flats to let)

**Migration:** `dotnet ef migrations add DevelopmentCosting --project src/RealEstateERP.Infrastructure --startup-project src/RealEstateERP.Web --output-dir Data/Migrations`
(Unzip and `dotnet build` first, then add the migration, and check it contains `CreateTable(name: "ProjectCosts"`.)

## Brand and public site
- Original design (no third-party template): navy + blue with an amber accent. Public site uses Poppins;
  the app keeps system fonts for speed. Colours live in CSS variables: `--pub-*` in `public.css`,
  `--sb-brand*` / `--sb-accent` in `site.css` (PDFs and emails use the same blue)
- Home: full-width hero with an original skyline illustration (`wwwroot/img/hero-skyline.svg`), headline,
  "Start free trial" email card (opens registration with the email filled in), highlights, features, steps, CTA.
  To use your own property photo, add it as the first background layer of `.hero` in `public.css`
- Inner pages get a matching navy header band. The header is transparent over the hero and turns solid on scroll
- Footer only links to pages that exist (no placeholder social links)

## Demo data (Development only)
On `dotnet run` in Development, demo users and businesses are seeded (`DemoData:Enabled` in
`appsettings.Development.json`). All passwords: `Demo@12345`. The login page lists them.

| Login | What it tests |
|---|---|
| superadmin@smartbiz.test | Platform admin: payment approvals, plans, settings |
| owner@smartbiz.test | Two businesses: Acacia Gardens (trial; charges on every lease, water meters, last month's invoices with payments: most paid, Lucy part-paid, Mercy in credit, David and Kwa Njeri Salon unpaid, Moses's cheque bounced and reversed, Hassan's deposit refunded; Lucy in arrears with a promise to pay, Moses served a demand; 200 credits; 18 tenants, 15 active leases incl. one ending in 20 days, one past its end date, a renewal, a terminated lease and a draft) and Savanna Homes (active, ends in 4 days) |
| admin@smartbiz.test | Admin role at Acacia Gardens |
| staff@smartbiz.test | User role at Acacia Gardens (can't renew) |
| viewer@smartbiz.test | Viewer role at Acacia Gardens |
| agent@smartbiz.test | Mji Property Agents, in grace period (banner) |
| lapsed@smartbiz.test | Lapsed Holdings, suspended, with a pending payment to approve. Has 9 units: once approved (Starter, 10 units) you can add exactly one more |
| newuser@smartbiz.test | No business: lands on Create business |

States are relative to the first seed. To reset: `dotnet ef database drop --project src/RealEstateERP.Infrastructure --startup-project src/RealEstateERP.Web`, then run again.
Never runs outside Development, even if the setting is copied.

## Public site
`/` is the public site (`_PublicLayout`): Home, Features, Pricing (live from the plans table), About,
Contact (support details from Settings), Privacy and Terms (drafts). Signed-in users visiting `/` go
to `/Dashboard`.

## First run
```bash
# 1. Tools
dotnet tool install --global dotnet-ef

# 2. Restore + build
dotnet build SmartBiz.slnx

# 3. Create the first migration
dotnet ef migrations add InitialFoundation \
  --project src/RealEstateERP.Infrastructure \
  --startup-project src/RealEstateERP.Web \
  --output-dir Data/Migrations

# 4. (Optional) seed a platform admin — kept out of appsettings
cd src/RealEstateERP.Web
dotnet user-secrets set "SeedAdmin:Email" "you@example.com"
dotnet user-secrets set "SeedAdmin:Password" "Str0ng!Passw0rd"

# 5. Run (Development applies migrations + seeds automatically)
dotnet run

# 6. Tests
dotnet test SmartBiz.slnx
```
In Development there is no email sender, so after registering Identity shows a
"click here to confirm your account" link on screen. A real sender comes with notifications.

Smoke test: register -> confirm -> log in -> create business -> dashboard ->
Switch business -> create a second one -> switch between them.

## Rules (these are what prevent the old cyclic errors)
1. **No cascade deletes.** Enforced globally in `ApplicationDbContext`. Nothing is hard-deleted.
2. **Relationships point one way.** Child -> parent only (e.g. Subscription -> Business).
   Never add a "current X" FK on the parent that points back at the child.
3. **Services are layered and never call "upward".** Current graph:
   `SystemConfigService`, `LocalFileStorage`, `NumberSequenceService` (low) <- `PlanService`, `SubscriptionService`,
   `AssetService`, `TenantService`, `LeaseService`, `ChargeService`, `InvoiceService`, `BillingRunService`,
   `PaymentService`, `StatementService`, `ArrearsService`. `CreditService` is low-level; credit balances only change
   through the static `CreditLedger` (balance and ledger entry together).
   <- `BusinessService`, `UnitService`. Shared invoice arithmetic lives in the static `InvoiceComposer`
   (a helper, not a service) so invoice services never depend on each other.
   `LeaseService` is the ONLY code that moves a unit into or out of Occupied.
9. **Work needing several `SaveChanges` calls goes through `db.InTransactionAsync(...)`** (execution strategy + transaction).
   Low-level (numbering, credits, ledger) <- domain (leases, invoices, payments) <- controllers.
   A low-level service never depends on a domain service. If two services seem to need each
   other, move the shared logic down a level.
4. **Services use `ApplicationDbContext` directly.** No generic repository / unit-of-work wrappers.
   Look rows up with `FirstOrDefaultAsync(x => x.Id == id)`, never `FindAsync`, so query filters always apply.
5. **Services never set `BusinessId`, audit fields or `IsDeleted`.** The DbContext does.
6. **Enums everywhere, stored as strings.** No magic status strings in code.
7. **Every new tenant-owned entity inherits `TenantEntity`.** That is all it takes to be isolated.
8. **Money is `decimal`** (convention makes it `decimal(18,2)`). Dates are stored in UTC.
