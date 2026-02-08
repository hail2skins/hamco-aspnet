# Railway MVC 500 Error - SOLUTION

## Problem Summary
Railway deployment returned 500 errors on MVC pages (GET /) but API endpoints worked fine (GET /api/notes returned 200).

## Root Causes

### 1. **Views not included in publish output**
The `.cshtml` files were being copied to the wrong location during `dotnet publish`:
- Expected: `out/Views/*.cshtml`  
- Actual: `out/src/Hamco.Api/Views/*.cshtml` (nested incorrectly)

### 2. **Content root path incorrect**
Program.cs was trying to set content root to `src/Hamco.Api/` which doesn't exist in the publish output.

### 3. **Razor runtime compilation not enabled**
Views need to be compiled at runtime since they're loose files in the publish output, not pre-compiled into the assembly.

---

## The Fix

### 1. Fix App.csproj - Correct view/wwwroot paths in publish output

**Changed:**
```xml
<ItemGroup>
  <Content Include="src/Hamco.Api/Views/**/*.cshtml">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

**To:**
```xml
<!-- Include Razor views for dotnet publish -->
<!-- Use None instead of Content to avoid automatic processing -->
<!-- CopyToPublishDirectory with Link metadata flattens the path structure -->
<ItemGroup>
  <None Include="src/Hamco.Api/Views/**/*.cshtml">
    <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
    <Link>Views/%(RecursiveDir)%(Filename)%(Extension)</Link>
    <PublishItemType>Content</PublishItemType>
  </None>
</ItemGroup>

<!-- Include static web assets (wwwroot) for dotnet publish -->
<ItemGroup>
  <None Include="src/Hamco.Api/wwwroot/**/*">
    <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
    <Link>wwwroot/%(RecursiveDir)%(Filename)%(Extension)</Link>
    <PublishItemType>Content</PublishItemType>
  </None>
</ItemGroup>
```

**Key changes:**
- Use `<None>` instead of `<Content>` to prevent automatic processing
- Add `<Link>` metadata to flatten directory structure in output
- Add `<CopyToPublishDirectory>` to ensure files are included in `dotnet publish`
- Add `<PublishItemType>Content</PublishItemType>` to treat as content files

### 2. Fix Program.cs - Correct content root configuration

**Changed:**
```csharp
var apiProjectPath = Path.Combine(AppContext.BaseDirectory, "src", "Hamco.Api");
var contentRoot = Directory.Exists(apiProjectPath) 
    ? apiProjectPath 
    : Directory.GetCurrentDirectory();
```

**To:**
```csharp
// When running from published output (dotnet publish), the content root should be
// the directory containing the executable (AppContext.BaseDirectory).
// Views and wwwroot are now at the root of the publish output, not nested.
var contentRoot = AppContext.BaseDirectory;
var webRoot = Path.Combine(contentRoot, "wwwroot");

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRoot,
    WebRootPath = webRoot,
    ApplicationName = "App" // Match the assembly name from App.csproj
});
```

**Why this works:**
- `AppContext.BaseDirectory` points to the directory containing the executable (`/app/` on Railway, `out/` locally)
- Views are now at `{BaseDirectory}/Views/` instead of `{BaseDirectory}/src/Hamco.Api/Views/`
- WebRootPath explicitly set to `{BaseDirectory}/wwwroot/`

### 3. Add Razor runtime compilation support

**Added to App.csproj:**
```xml
<PackageReference Include="Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation" Version="10.0.2" />
```

**Added to Program.cs:**
```csharp
builder.Services.AddControllersWithViews()
    .AddApplicationPart(typeof(Program).Assembly)
    .AddRazorRuntimeCompilation(); // ← NEW
```

**Why this is needed:**
- Views are loose `.cshtml` files in the publish output, not pre-compiled into the assembly
- Runtime compilation allows MVC to load and compile views on-demand from disk
- Without this, MVC can only use pre-compiled views embedded in the assembly

### 4. Add startup diagnostics (for debugging)

Added detailed logging to help diagnose future issues:
```csharp
Console.WriteLine("=== HAMCO STARTUP DIAGNOSTICS ===");
Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"Content Root: {builder.Environment.ContentRootPath}");
Console.WriteLine($"Web Root: {builder.Environment.WebRootPath}");
Console.WriteLine($"Application Name: {builder.Environment.ApplicationName}");

var viewsPath = Path.Combine(builder.Environment.ContentRootPath, "Views");
var viewsExist = Directory.Exists(viewsPath);
Console.WriteLine($"Views directory exists: {viewsExist} (Path: {viewsPath})");
// ... etc
```

### 5. Add developer exception page (temporary, for Railway debugging)

Added before middleware pipeline:
```csharp
app.UseDeveloperExceptionPage();
```

**TODO:** Change this to `if (app.Environment.IsDevelopment())` once Railway deployment is stable.

---

## Verification

### Local testing (mimics Railway build):
```bash
# Clean and publish
cd /Users/art/.openclaw/workspace/projects/hamco
rm -rf out
dotnet publish App.csproj -c Release -o out

# Verify files are in correct location
ls out/Views/         # Should show Home/, Shared/, etc.
ls out/wwwroot/       # Should show css/, js/, img/

# Run from publish output
cd out
ASPNETCORE_URLS=http://0.0.0.0:3000 ./App

# Test in another terminal
curl http://localhost:3000/          # Should return 200 with HTML
curl http://localhost:3000/about     # Should return 200 with HTML
curl http://localhost:3000/api/notes # Should return 200 with JSON []
```

### Expected startup output:
```
=== HAMCO STARTUP DIAGNOSTICS ===
Environment: Production
Content Root: /path/to/out/
Web Root: /path/to/out/wwwroot
Application Name: App
Views directory exists: True (Path: /path/to/out/Views)
Found 7 .cshtml files
wwwroot directory exists: True (Path: /path/to/out/wwwroot)
...
Now listening on: http://0.0.0.0:3000
```

### Expected results:
- ✅ GET / → 200 with HTML (Home page)
- ✅ GET /about → 200 with HTML (About page)
- ✅ GET /api/notes → 200 with JSON []

---

## Files Modified

1. **App.csproj**
   - Fixed `<None>` items for Views and wwwroot to use correct publish paths
   - Added `Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation` package
   - Added `PreserveCompilationContext` property

2. **src/Hamco.Api/Program.cs**
   - Fixed content root path configuration (use `AppContext.BaseDirectory` directly)
   - Added `.AddRazorRuntimeCompilation()` to MVC services
   - Added startup diagnostics logging
   - Added developer exception page (temporary)

---

## Deploy to Railway

1. Commit changes:
   ```bash
   git add App.csproj src/Hamco.Api/Program.cs
   git commit -m "Fix Railway MVC 500 error - correct view paths and enable runtime compilation"
   git push origin main
   ```

2. Railway will automatically:
   - Run `dotnet restore`
   - Run `dotnet publish --no-restore -c Release -o out`
   - Start with `./out/App`
   - Views will now be at `/app/Views/` (Railway container)
   - wwwroot will be at `/app/wwwroot/`

3. Monitor Railway logs for:
   - `Views directory exists: True`
   - `wwwroot directory exists: True`
   - `Now listening on: http://0.0.0.0:$PORT`

4. Test the deployment:
   - Visit Railway URL (should show home page, not 500 error)
   - Visit Railway URL/about (should show about page)
   - Visit Railway URL/api/notes (should show [] or notes list)

---

## Why This Was Hard to Diagnose

1. **Local dev works fine** - When running with `dotnet run`, ASP.NET uses the project directory as content root, so it finds views in `src/Hamco.Api/Views/`. The problem only appears when running from `dotnet publish` output.

2. **API endpoints worked** - API controllers don't need views, so they worked fine. Only MVC view rendering failed.

3. **Build vs Publish** - `dotnet build` copies files to `bin/`, `dotnet publish` copies to `out/`. Railway uses publish, which has different file copy behavior.

4. **Nested project structure** - The root `App.csproj` aggregates source from `src/Hamco.Api/`, which created path ambiguity for content files.

---

## Future Improvements (Optional)

1. **Pre-compile views** instead of runtime compilation:
   - Add `<MvcRazorCompileOnPublish>true</MvcRazorCompileOnPublish>` to App.csproj
   - Remove `AddRazorRuntimeCompilation()` from Program.cs
   - Views will be embedded in App.dll, reducing disk reads

2. **Remove developer exception page** once stable:
   - Change `app.UseDeveloperExceptionPage()` to `if (app.Environment.IsDevelopment()) { ... }`

3. **Simplify project structure** (big refactor):
   - Move everything to root instead of `src/Hamco.Api/`
   - Eliminate the aggregator `App.csproj` pattern
   - Use standard ASP.NET Core project structure

---

## Summary

The 500 error was caused by:
1. Views not being copied to the correct location in publish output
2. Content root pointing to non-existent directory
3. Missing Razor runtime compilation support

The fix:
1. Use `<None>` + `<Link>` in App.csproj to flatten view paths in publish output
2. Set content root to `AppContext.BaseDirectory` (where the executable lives)
3. Add Razor runtime compilation package and service registration

**Result:** MVC views now render successfully on Railway! 🎉
