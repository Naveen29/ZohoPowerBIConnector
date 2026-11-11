# Project Task Board

## Completed Tasks

- [x] Create `ZohoPowerBIConnector` repository scaffold
- [x] Add `WebAPI`, `Database`, `Frontend`, `Documentation`, and `Deployment` folders
- [x] Add repository `README.md` and `.gitignore`
- [x] Add database schema script `Database/CreateTables.sql`
- [x] Add placeholder docs in `WebAPI/README.md`, `Database/README.md`, `Documentation/README.md`, and `Deployment/README.md`
- [x] Build modern landing page in `Frontend/index.html`
- [x] Build customer dashboard page in `Frontend/dashboard.html`
- [x] Add supporting frontend assets `Frontend/styles.css` and `Frontend/scripts.js`

## Todo Tasks

### Phase 1 - WebAPI Setup
- [x] Initialize ASP.NET Core 8 Web API project in `WebAPI`
- [ ] Add `appsettings.json` and environment configuration support
- [ ] Configure SQL Server connection and Entity Framework Core models
- [ ] Add JWT authentication and user identity support

### Phase 2 - Database & Seed Data
- [x] Add SQL Server migration or seed scripts for initial data
- [x] Seed `Plans` and `Users` test data
- [x] Create stored procedures or helper scripts for plan management

### Phase 3 - Public Website
- [ ] Add marketing content to `index.html`
- [ ] Implement contact form backend endpoint and store submissions
- [ ] Add actual demo videos and content resources
- [ ] Add site footer links to documentation pages

### Phase 4 - Authentication Pages
- [x] Create `register.html` with registration form and validation
- [x] Create `login.html` with JWT login flow
- [x] Create `forgot-password.html` and password reset flow
- [x] Implement backend API endpoints for registration/login/reset

### Phase 5 - Customer Dashboard
- [x] Add real dashboard data bindings and API integration
- [x] Build user summary, plan expiry, and login history panels
- [x] Add quick actions to navigate configuration, plans, and support

### Phase 6 - Zoho Configuration
- [x] Build Zoho configuration form and save credentials securely
- [x] Add Zoho Connection Config form with input for `zohoClientId`, `ZohoClientSecret`, `refreshToken`, `OrganizationID`
- [x] Add backend validation and Zoho API connection test
- [x] Encrypt `ClientSecret` and `RefreshToken` before storing
- [x] Enable credential edits and re-validation

### Phase 7 - Subscription Module
- [x] Add `dashboard/plans.html` for plan display and upgrade
- [x] Implement subscription status rules in backend
- [x] Block expired plan access for plugin validation

### Phase 8 - Plugin Authentication API
- [x] Add `POST /api/plugin/validate`
- [x] Validate user email, subscription status, and Zoho config
- [x] Return plugin access response including plan and expiry

### Phase 9 - Admin Portal
- [x] Create admin UI under `admin.html`
- [x] Add user management, disable users, and search
- [x] Add subscription assignment, extension, and cancellation
- [x] Add Zoho connection status view without secrets
- [x] Add dashboard statistics for users and subscriptions
- [x] Show a Lable View Users on click it show admin.html this should be visible only for admin login(admin@zoho.com)

### Phase 10 - Documentation Pages
- [x] Create `privacy-policy.html`
- [x] Create `terms.html`
- [x] Create `support.html`
- [x] Create `docs.html`
- [x] Add marketplace-ready legal and support documentation

### Phase 11 - Power BI Data API
- [x] Add `PowerBIController` to existing WebAPI
- [x] Support JWT authentication for Power BI requests
- [x] Enforce multi-tenant filtering by customer identity
- [x] Add `schema` endpoint for enabled Zoho modules
- [x] Add dynamic `ZohoModules` configuration support for enabled modules
- [ ] Add DB models/migrations for enabled Zoho module tables  (in-progress)
- [x] Add enabled module endpoints like `items`, `customers`, `invoices`, and `paymentsreceived`
- [x] Add pagination, filtering, and sorting support
- [x] Return clean JSON suitable for Power BI refresh
- [x] Log requests, execution time, and errors without secrets
- [x] Document Power BI endpoints in Swagger

### Phase 12 - Security
- [x] Ensure JWT authentication is implemented
- [x] Enforce HTTPS-only traffic
- [x] Hash passwords with a secure algorithm
- [x] Encrypt secret fields and prevent SQL injection
- [x] Add rate limiting for public APIs

### Phase 12 - Deployment
- [ ] Add IIS and Azure App Service deployment docs
- [ ] Configure SSL and production settings
- [ ] Add database backup and monitoring guidance
- [ ] Add application logging and error monitoring
