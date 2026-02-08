# Railway Deployment - DON'T BREAK THIS

**TL;DR: NEVER DELETE `App.csproj` FROM REPO ROOT. EVER.**

---

## The Golden Rule

**Railway/Railpack REQUIRE a `.csproj` file in the repository root.**

Even though our actual project lives in `src/Hamco.Api/`, Railpack's build detector ONLY looks at the root level. Without `App.csproj` at root, you get:

```
⚠ Script start.sh not found
✖ Railpack could not determine how to build the app
```

---

## What Happened (Feb 8, 2026)

**The Crime:**
- Commit `1605c92` (Phase 2 completion) accidentally deleted `App.csproj`
- Railway deployment broke immediately
- Error: "Railpack could not determine how to build the app"

**The Fix:**
- Restored `App.csproj` with all Phase 1 & 2 packages (Markdig, HtmlSanitizer, etc.)
- Committed as `0bb6648`

---

## Required Files at Repo Root

```
hamco/
├── App.csproj          # ⭐ MANDATORY - Never delete this
├── global.json         # ⭐ MANDATORY - SDK version for Railway
├── .gitignore          # Should exclude .env, bin/, obj/
└── src/
    └── Hamco.Api/      # Actual project (Railway doesn't look here)
```

---

## App.csproj Requirements

The root `App.csproj` must include:

```xml
<PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <OutputType>Exe</OutputType>
    
    <!-- CRITICAL: Disable defaults to prevent duplicate item errors -->
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <EnableDefaultContentItems>false</EnableDefaultContentItems>
    
    <!-- CRITICAL: Required for OpenAPI source generators -->
    <InterceptorsNamespaces>$(InterceptorsNamespaces);Microsoft.AspNetCore.OpenApi.Generated</InterceptorsNamespaces>
</PropertyGroup>

<!-- Include all source files from subdirectories -->
<ItemGroup>
    <Compile Include="src/**/*.cs" Exclude="src/**/obj/**;src/**/bin/**;src/**/*.Tests/**" />
</ItemGroup>

<!-- Include appsettings -->
<ItemGroup>
    <Content Include="src/Hamco.Api/appsettings*.json">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
</ItemGroup>

<!-- CRITICAL FOR MVC: Include Razor views and static files -->
<!-- Without these, Railway returns 500 errors (views not found) -->
<ItemGroup>
    <Content Include="src/Hamco.Api/Views/**/*.cshtml">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
    <Content Include="src/Hamco.Api/wwwroot/**/*">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
</ItemGroup>

<!-- ALL packages from ALL projects must be listed here -->
<ItemGroup>
    <!-- When you add a package to src/Hamco.Api/ or src/Hamco.Services/,
         YOU MUST ALSO ADD IT HERE OR RAILWAY WILL FAIL -->
</ItemGroup>
```

---

## When Adding New Packages

**DO THIS:**

1. Add package to the appropriate project (`src/Hamco.Api/` or `src/Hamco.Services/`)
2. **ALSO add it to root `App.csproj`**
3. Test build locally: `dotnet restore`
4. Commit both changes together

**EXAMPLE:**

```bash
# Local dev - add to Services project
cd src/Hamco.Services
dotnet add package SomeNewPackage --version 1.0.0

# ALSO add to root App.csproj!
cd ../..
vim App.csproj  # Add <PackageReference Include="SomeNewPackage" Version="1.0.0" />

# Test
dotnet restore

# Commit together
git add src/Hamco.Services/Hamco.Services.csproj App.csproj
git commit -m "feat: Add SomeNewPackage for X feature"
```

---

## What NOT To Do

❌ **DON'T delete `App.csproj` "because we don't need it for local dev"**

❌ **DON'T assume `global.json` is enough** — Railpack needs the `.csproj`

❌ **DON'T put a solution file at root** — confuses Railpack (`776243d` removed `hamco.slnx` for this reason)

❌ **DON'T add packages to sub-projects and forget to update root `App.csproj`**

---

## Quick Diagnostic Commands

**Check if Railway will detect your project:**
```bash
# Must see App.csproj in root
ls -la *.csproj

# Must restore without errors
dotnet restore
```

**If Railway fails with "could not determine how to build":**
```bash
# Check if App.csproj exists
git ls-files | grep -E "^App\.csproj$"

# If missing, restore from last known good commit
git show 0bb6648:App.csproj > App.csproj
```

---

## Testing Before Pushing to Railway

```bash
# 1. Clean build test
dotnet clean
dotnet restore
dotnet build

# 2. Verify all packages are in root App.csproj
grep -h "PackageReference" src/*/*.csproj | sort -u
grep "PackageReference" App.csproj | sort -u
# ^ These should have the same packages

# 3. Push to Railway only after local build succeeds
```

---

## MVC Web App Requirements (CRITICAL)

**API-only apps** (just controllers, no views):
- Only need `appsettings*.json` content
- No Razor views or wwwroot to include

**MVC apps with views** (what we have):
- MUST include `Views/**/*.cshtml` in root `App.csproj`
- MUST include `wwwroot/**/*` (CSS, JS, images)  
- MUST set correct **Content Root** in `Program.cs`
- Returns 500 errors at runtime if any of these are wrong

### Content Root Configuration

When running from root `App.csproj`, the content root defaults to the root directory, but views are in `src/Hamco.Api/Views/`. **You must set the content root explicitly:**

```csharp
var apiProjectPath = Path.Combine(AppContext.BaseDirectory, "src", "Hamco.Api");
var contentRoot = Directory.Exists(apiProjectPath) 
    ? apiProjectPath   // Railway: root App.csproj
    : Directory.GetCurrentDirectory();  // Local: src/Hamco.Api/

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRoot,
    ApplicationName = "Hamco.Api"
});
```

**Without this:** `The view 'Index' was not found. Searched locations: /Views/Home/Index.cshtml`

**Lessons learned:**
- 2026-02-08 16:00 - 500 errors because views weren't in App.csproj Content items
- 2026-02-08 16:20 - 500 errors because content root was wrong (looked in /Views/ instead of /src/Hamco.Api/Views/)

---

## Historical Context

Read `memory/2026-02-06.md` for the full war story of getting Railway working.

**Key commits in that battle:**
- `776243d` - Removed solution file (confused Railpack)
- `4fc3d13` - Disabled default compile items
- `ecfa191` - Disabled default content items  
- `34300f4` - Added InterceptorsNamespaces
- `0bb6648` - **RESTORED App.csproj after it was accidentally deleted**
- `a44e115` - **Added Views and wwwroot content items (MVC fix)**

---

## Emergency Contacts

If you break this again:
1. Check `App.csproj` exists at root
2. Compare with commit `0bb6648` (the fix)
3. Ensure all packages from `src/*/*.csproj` are in root `App.csproj`
4. Test with `dotnet restore && dotnet build`
5. Commit and push

---

*Written with love and trauma by Skippy the Magnificent*
*Last updated: 2026-02-08*
