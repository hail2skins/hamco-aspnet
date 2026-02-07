#!/bin/bash
set -e

# 1. Force ASP.NET to listen on the port Railway provides
export ASPNETCORE_URLS="http://0.0.0.0:${PORT:-8080}"

# 2. Find and run the application
# Nixpacks/Railway can publish to different locations, so check common ones

# Check if published as self-contained binary
# Check if published from railpack.json build step
if [ -f "./out/Hamco.Api" ]; then
    echo "Starting Hamco.Api (self-contained binary)..."
    exec ./out/Hamco.Api
fi

if [ -f "./out/Hamco.Api.dll" ]; then
    echo "Starting Hamco.Api (from railpack out/)..."
    exec dotnet ./out/Hamco.Api.dll
fi

# Check alternative locations
if [ -f "/app/Hamco.Api.dll" ]; then
    echo "Starting Hamco.Api from /app..."
    exec dotnet /app/Hamco.Api.dll
fi

if [ -f "/app/out/Hamco.Api.dll" ]; then
    echo "Starting Hamco.Api from /app/out..."
    exec dotnet /app/out/Hamco.Api.dll
fi

# Debug: Show what we have
echo "Error: Cannot find Hamco.Api binary or DLL!"
echo "Current directory: $(pwd)"
echo "Listing current directory:"
ls -la
echo ""
echo "Checking for out/ directory:"
ls -la out/ 2>/dev/null || echo "out/ does not exist"
echo ""
echo "Checking for /app directory:"
ls -la /app/ 2>/dev/null || echo "/app does not exist"
exit 1
