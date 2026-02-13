# Hamco Railway Migration Plan

## Executive Summary

You're moving from Render to Railway with a two-phase approach:
1. **Phase 1:** Set up Umami analytics (self-hosted on Railway)
2. **Phase 2:** Migrate Hamco dev environment, then retire Render completely

**Key Architecture Decision:** Single PostgreSQL database with schema separation:
- `public` schema → Production data
- `dev` schema → Development data

This avoids paying for two databases when data is small. Eventually, you'll upgrade to Railway's $20/mo plan for self-hosted Umami.

---

## Phase 1: Umami Analytics (Do This First)

### Goal
Self-hosted Umami instance running on Railway to track TheVirtualArmory and HamcoIs.com

### Steps

#### 1. Create Umami Project on Railway

```bash
# Option A: Via Railway CLI (if installed)
railway login
railway init --name umami-analytics

# Option B: Via Railway Dashboard
# 1. Go to https://railway.app/new
# 2. Click "Deploy a template"
# 3. Search for "Umami"
# 4. Select official Umami template
```

#### 2. Configure Umami Environment Variables

```env
# Required
DATABASE_URL=postgresql://user:pass@host:5432/umami
APP_SECRET=your-random-secret-key-min-32-chars

# Optional (but recommended)
TRACKER_SCRIPT_NAME=custom-tracker.js  # Hide that it's Umami
COLLECT_API_ENDPOINT=/api/collect      # Hide analytics endpoint
```

**Note:** For now, use Railway's free PostgreSQL. When you upgrade to $20/mo, you can:
- Move Umami to a self-hosted Postgres on Railway (cheaper)
- Or keep using Railway's managed Postgres

#### 3. Add Tracking Scripts to Hamco

Edit `src/Hamco.Api/Views/Shared/_Layout.cshtml` (or create partial):

```html
<!-- Umami Tracking -->
<script defer src="https://your-umami-app.up.railway.app/custom-tracker.js" 
        data-website-id="your-website-uuid"
        data-host-url="https://your-umami-app.up.railway.app">
</script>
```

**Websites to Track:**
- [ ] thevirtualarmory.com
- [ ] hamcois.com
- [ ] dev.thevirtualarmory.com (when ready)

#### 4. Umami Completion Checklist

- [ ] Umami deployed on Railway
- [ ] Login credentials saved securely (1Password)
- [ ] Tracking script added to Hamco layouts
- [ ] First visit data showing in Umami dashboard
- [ ] Verified both domains are tracked separately

---

## Phase 2: Dev Environment on Railway

### Current State
- **Prod:** Railway (thevirtualarmory.com)
- **Dev:** Render (hamco-dev.onrender.com or similar)
- **Goal:** Move Dev to Railway, share database with schema separation

### Architecture: Shared Database, Separate Schemas

```
┌─────────────────────────────────────────────────────────────┐
│                    Railway Project: hamco                   │
│                                                             │
│  ┌──────────────┐    ┌──────────────────────────────┐      │
│  │  Hamco API   │    │      PostgreSQL Database     │      │
│  │   (Prod)     │    │                              │      │
│  │              │────┤  ┌──────────┐ ┌──────────┐  │      │
│  │  Port: $PORT │    │  │  public  │ │   dev    │  │      │
│  └──────────────┘    │  │  schema  │ │  schema  │  │      │
│                      │  │  (prod)  │ │  (dev)   │  │      │
│  ┌──────────────┐    │  └──────────┘ └──────────┘  │      │
│  │  Hamco API   │    │                              │      │
│  │   (Dev)      │    └──────────────────────────────┘      │
│  │              │                                           │
│  │  Port: $PORT │    Environment Variables:                │
│  └──────────────┘    - Prod: DATABASE_SCHEMA=public        │
│                      - Dev:  DATABASE_SCHEMA=dev           │
└─────────────────────────────────────────────────────────────┘
```

### Implementation Steps

#### Step 1: Create Dev Schema in Existing Database

**Option A: Via Railway Dashboard (Easiest)**
1. Go to your Railway project → PostgreSQL service
2. Click "Connect" → "Railway CLI" or "PSQL Command"
3. Run:

```sql
-- Create dev schema
CREATE SCHEMA IF NOT EXISTS dev;

-- Grant permissions (adjust user as needed)
GRANT ALL ON SCHEMA dev TO railway;
GRANT ALL ON SCHEMA dev TO public;

-- Verify
\dn  -- List schemas
```

**Option B: Via Hamco Migration (Recommended for Consistency)**

Create a new migration that creates the schema:

```bash
cd src/Hamco.Api
dotnet ef migrations add CreateDevSchema --project ../Hamco.Data
dotnet ef database update
```

Then add this to the migration:

```csharp
// In the migration file
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql("CREATE SCHEMA IF NOT EXISTS dev;");
}
```

#### Step 2: Modify Hamco to Support Schema Selection

**Option A: Connection String with Search Path (Simplest)**

Update `Program.cs` to inject schema into connection string:

```csharp
// In Program.cs, before building
var schema = Environment.GetEnvironmentVariable("DATABASE_SCHEMA") ?? "public";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// If not already in search path, add it
if (!connectionString.Contains("SearchPath"))
{
    connectionString += $";SearchPath={schema}";
}

// In DbContext configuration
services.AddDbContext<HamcoDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", schema);
    }));
```

**Option B: Environment-Based Schema (More Explicit)**

Add to `appsettings.json`:

```json
{
  "Database": {
    "Schema": "public"
  }
}
```

And `appsettings.Development.json`:

```json
{
  "Database": {
    "Schema": "dev"
  }
}
```

Then in `Program.cs`:

```csharp
var schema = builder.Configuration["Database:Schema"] ?? "public";
```

#### Step 3: Create Railway Service for Dev

In your existing Railway project:

1. **Add New Service:**
   - Click "New" → "GitHub Repo"
   - Select your `hamco-aspnet` repo
   - Name: `hamco-api-dev`

2. **Configure Environment Variables:**
   ```env
   DATABASE_SCHEMA=dev
   DATABASE_URL=${{Postgres.DATABASE_URL}}  # Same DB, different schema
   JWT_KEY=dev-key-different-from-prod
   ASPNETCORE_ENVIRONMENT=Development
   ```

3. **Set Domain:**
   - Add custom domain: `dev.thevirtualarmory.com` (or `dev.hamcois.com`)
   - Wait for DNS/certs (the same issue you're seeing now)

#### Step 4: Auto-Migrate on Deploy

Add to `Program.cs` (already partially there):

```csharp
// After builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HamcoDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        logger.LogInformation("Applying migrations...");
        db.Database.Migrate();
        logger.LogInformation("Migrations applied successfully");
        
        // Seed dev data if in dev environment
        if (app.Environment.IsDevelopment())
        {
            logger.LogInformation("Seeding dev data...");
            await SeedData.InitializeAsync(db);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Migration/seed failed");
    }
}
```

Create `SeedData.cs`:

```csharp
public static class SeedData
{
    public static async Task InitializeAsync(HamcoDbContext context)
    {
        // Only seed if empty
        if (!context.Notes.Any())
        {
            context.Notes.AddRange(
                new Note { Title = "Dev Post 1", Content = "Test content" },
                new Note { Title = "Dev Post 2", Content = "More test content" }
            );
            await context.SaveChangesAsync();
        }
    }
}
```

#### Step 5: GitHub Actions for Dual Deploy

Create `.github/workflows/deploy-dev.yml`:

```yaml
name: Deploy to Railway (Dev)

on:
  push:
    branches: [ develop, dev ]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Deploy to Railway (Dev)
        uses: railway/cli@v3
        with:
          railway_token: ${{ secrets.RAILWAY_TOKEN }}
          service: hamco-api-dev
```

Modify existing prod workflow to only trigger on `main`:

```yaml
on:
  push:
    branches: [ main ]  # Only prod deploys on main
```

---

## Phase 3: Full Railway Transition & Render Retirement

### Retirement Checklist

Once Dev is stable on Railway:

- [ ] Verify dev.thevirtualarmory.com works correctly
- [ ] Test deployment pipeline (push to `dev` branch → auto-deploy)
- [ ] Migrate any data from Render dev (if needed)
- [ ] **Update Render environment to "Maintenance Mode"** (don't delete yet)
- [ ] Wait 1 week to ensure stability
- [ ] Delete Render service (and stop paying)

### Final Railway Project Structure

```
hamco (Railway Project)
├── hamco-api-prod (Service)
│   ├── Domain: thevirtualarmory.com
│   ├── Branch: main
│   └── Schema: public
│
├── hamco-api-dev (Service)
│   ├── Domain: dev.thevirtualarmory.com
│   ├── Branch: dev (or develop)
│   └── Schema: dev
│
├── PostgreSQL (Database)
│   ├── Schema: public (prod)
│   └── Schema: dev
│
└── umami-analytics (Service) ← Phase 1
    ├── Domain: analytics.yourdomain.com
    └── Tracks: prod + dev
```

---

## Cost Analysis

### Current (Render + Future Umami)
| Service | Cost |
|---------|------|
| Render Dev (Web Service) | ~$7/mo |
| Railway Prod (Free tier) | $0 |
| Umami (Railway managed) | ~$5/mo |
| **Total** | **~$12/mo** |

### After $20 Railway Plan
| Service | Cost |
|---------|------|
| Railway Hobby Plan | $20/mo |
| Includes: Prod, Dev, DB, Umami | |
| **Total** | **$20/mo** |

**Value:** For $8 more, you get:
- No usage limits on requests
- Always-on services
- Self-hosted Umami (data ownership)
- Better performance
- Easier management (one platform)

---

## Next Actions (Prioritized)

### This Week (Umami Focus)

1. **Deploy Umami to Railway**
   - Use template or manual deploy
   - Save login credentials

2. **Add tracking to Hamco**
   - Modify `_Layout.cshtml`
   - Deploy to prod
   - Verify data flowing

### Next Week (Dev Environment)

3. **Create dev schema**
   - Run SQL in Railway Postgres
   - Verify schema exists

4. **Create dev service in Railway**
   - Clone prod service config
   - Set `DATABASE_SCHEMA=dev`
   - Test deployment

5. **Configure auto-migrate + seed**
   - Update `Program.cs`
   - Create seed data script
   - Test on dev branch

### Following Week (Cleanup)

6. **Retire Render**
   - Put in maintenance mode
   - Monitor for issues
   - Delete after confidence period

---

## Files to Create/Modify

### New Files
- [ ] `src/Hamco.Api/Data/SeedData.cs`
- [ ] `.github/workflows/deploy-dev.yml`
- [ ] `docs/RAILWAY_DEV_SETUP.md` (this document's successor)

### Modified Files
- [ ] `src/Hamco.Api/Program.cs` - Add schema support, auto-migrate
- [ ] `src/Hamco.Api/Views/Shared/_Layout.cshtml` - Add Umami tracking
- [ ] `src/Hamco.Data/HamcoDbContext.cs` - Schema configuration
- [ ] `.github/workflows/deploy.yml` - Limit to main branch only

---

## Open Questions

1. **Dev Domain:** Do you want `dev.thevirtualarmory.com` or `dev.hamcois.com`?
2. **Git Branching:** Do you use `main`/`dev` or `main`/`develop`?
3. **Umami Domain:** Custom subdomain like `analytics.hamcois.com`?
4. **Render Data:** Any data in Render dev that needs migrating, or start fresh?

---

*Plan created: 2026-02-12*
*Execute Phase 1 first, then we'll revisit Phase 2.*
