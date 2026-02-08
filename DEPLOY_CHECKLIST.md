# Railway Deployment Checklist

## Pre-Deployment Verification (✅ COMPLETED)

- [x] Views copied to `out/Views/` (not nested in `src/`)
- [x] wwwroot copied to `out/wwwroot/` (not nested in `src/`)
- [x] Razor runtime compilation enabled
- [x] Content root path uses `AppContext.BaseDirectory`
- [x] Local test passes: `dotnet publish` → run from `out/` directory
- [x] GET / returns 200 with HTML
- [x] GET /about returns 200 with HTML  
- [x] GET /api/notes returns 200 with JSON

## Files to Commit

```bash
git add App.csproj
git add src/Hamco.Api/Program.cs
git add RAILWAY_MVC_FIX.md
git add DEPLOY_CHECKLIST.md
git commit -m "Fix Railway MVC 500 error

- Fix view/wwwroot paths in publish output
- Enable Razor runtime compilation
- Set correct content root path
- Add startup diagnostics logging

Closes: Railway deployment 500 error on MVC pages"
git push origin main
```

## Post-Deployment Verification (Railway)

1. **Monitor Railway logs** for:
   - [ ] `Views directory exists: True`
   - [ ] `wwwroot directory exists: True`
   - [ ] `Found 7 .cshtml files`
   - [ ] `Now listening on: http://0.0.0.0:$PORT`

2. **Test endpoints**:
   - [ ] Visit `https://your-app.railway.app/` → Should show home page (200)
   - [ ] Visit `https://your-app.railway.app/about` → Should show about page (200)
   - [ ] Visit `https://your-app.railway.app/api/notes` → Should return JSON (200)

3. **Check for errors**:
   - [ ] No 500 errors in Railway logs
   - [ ] No "view not found" errors
   - [ ] Database migrations applied successfully

## If Deployment Fails

1. **Check Railway build logs** for:
   - `dotnet restore` succeeded
   - `dotnet publish` succeeded
   - No missing packages

2. **Check Railway runtime logs** for:
   - Content root path (should be `/app/`)
   - Views directory exists check (should be True)
   - Any exception stack traces

3. **Common issues**:
   - Database connection string not set → Set `DATABASE_URL` in Railway env vars
   - Port not binding → Railway sets `PORT` automatically, app should use it
   - Views not found → Re-check publish output file locations

## Rollback Plan

If deployment fails, revert to previous version:
```bash
git revert HEAD
git push origin main
```

Railway will automatically deploy the previous working version (API-only).

## Success Criteria

✅ **Deployment is successful when:**
- Home page loads without errors
- About page loads without errors
- API endpoints return expected responses
- No 500 errors in logs
- Database migrations complete successfully

---

**Expected deployment time:** 2-3 minutes  
**Expected first request:** May be slower (runtime compilation), subsequent requests will be faster
