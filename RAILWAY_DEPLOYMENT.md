# Railway Railpack Deployment - Solution Summary

## Problem
Railway's Railpack wasn't building the .NET 10 API. Only source files were being copied, with no compiled output (`out/` directory missing).

## Root Cause
**Railpack requires a `.csproj` file in the repository root directory to detect it as a .NET project.**

From [Railpack .NET docs](https://railpack.com/languages/dotnet):
> "Your project will be detected as a Dotnet application if a `*.csproj` file exists in the root directory."

The project structure had `.csproj` files only in subdirectories (`src/Hamco.Api/`), so Railpack didn't recognize it as a .NET project.

## Solution
Created `App.csproj` in the root directory that:
1. Uses `Microsoft.NET.Sdk.Web` SDK (for ASP.NET Core apps)
2. References all source files from `src/Hamco.Api/**/*.cs`
3. Includes all package dependencies from the original project
4. References other project dependencies (Hamco.Core, Hamco.Data, Hamco.Services)
5. Disables default item inclusion to prevent path conflicts

Updated `global.json` to properly specify .NET SDK version:
```json
{
  "sdk": {
    "version": "10.0.0",
    "rollForward": "latestMinor"
  }
}
```

## How Railpack Works (Automatically)
Once a `.csproj` is in root, Railpack will:
1. ✅ Install .NET 10 SDK (from `global.json` or `TargetFramework`)
2. ✅ Run `dotnet restore` (caches dependencies)
3. ✅ Run `dotnet publish --no-restore -c Release -o out`
4. ✅ Start app with `./out/App` (auto-generated command)
5. ✅ Set `ASPNETCORE_URLS=http://0.0.0.0:${PORT:-3000}`

**No custom scripts or config files needed** - Railpack does everything automatically.

## Local Verification (Completed)
```bash
# Restore dependencies
dotnet restore App.csproj

# Publish to out/ directory (mimics Railpack)
dotnet publish App.csproj --no-restore -c Release -o out

# Run the app
cd out && ASPNETCORE_URLS=http://0.0.0.0:3000 ./App
```

✅ **Result**: App compiled successfully, created executable, and ran on port 3000

## Deployed Files
- `App.csproj` - Root project file for Railpack detection
- `global.json` - .NET SDK version configuration
- `.gitignore` - Updated to exclude `out/` directory

## Railway Deployment Steps
1. ✅ Files committed and pushed to `main` branch
2. 🔄 Railway should auto-detect the push and trigger new deployment
3. 🔍 Watch Railway logs for:
   - "Installing .NET SDK 10.0.x"
   - "dotnet restore"
   - "dotnet publish ... -o out"
   - "Now listening on: http://0.0.0.0:$PORT"

## Troubleshooting
If deployment still fails, check Railway logs for:

### Missing Dependencies
If package restore fails, ensure Railway has network access to NuGet.org

### Database Connection
The app expects PostgreSQL connection string in environment:
- Verify `DATABASE_URL` environment variable is set in Railway
- Check the connection string format in Railway logs

### Port Binding
Railpack automatically sets:
- `PORT` environment variable (Railway's assigned port)
- `ASPNETCORE_URLS=http://0.0.0.0:${PORT:-3000}`

The app should automatically listen on the correct port.

## Next Steps
1. Monitor Railway deployment logs
2. Verify app is accessible at Railway-provided URL
3. Test API endpoints (auth, notes, etc.)
4. Check database migrations ran successfully

## References
- [Railpack .NET Documentation](https://railpack.com/languages/dotnet)
- Repository: https://github.com/hail2skins/hamco-aspnet
- Deployed commits: a9b743e (latest)
