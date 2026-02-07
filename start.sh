#!/bin/bash
set -e

# 1. Force ASP.NET to listen on the port Railway provides
export ASPNETCORE_URLS="http://0.0.0.0:${PORT:-8080}"

# 2. Check if the binary exists (Helpful for debugging)
if [ -f "./out/Hamco.Api" ]; then
    echo "Starting Hamco.Api..."
    exec ./out/Hamco.Api
else
    echo "Error: ./out/Hamco.Api not found!"
    ls -R ./out
    exit 1
fi
