# Hamco ASP.NET Core API

C#/.NET rewrite of the Django Hamco blog with API-first architecture.

## Source Reference
Original Django implementation: https://github.com/hail2skins/hamco-python

## Philosophy
- **TDD First:** Tests written before implementation
- **API First:** JSON endpoints before GUI
- **Match Django:** Keep schema/behavior compatible with existing PostgreSQL DB

## Database
PostgreSQL (local dev uses existing Postgres instance)

## Project Structure
```
/src
  /Hamco.Api          - Web API + Controllers
  /Hamco.Core         - Domain models, interfaces  
  /Hamco.Data         - EF Core, migrations
  /Hamco.Services     - Business logic
/tests
  /Hamco.Api.Tests    - Integration tests
  /Hamco.Core.Tests   - Unit tests
```

## Development Approach
Each feature:
1. Write test (RED)
2. Implement minimal code (GREEN) 
3. Refactor
4. Compare against Django implementation for compatibility

## Current Phase
Notes/Posts API - CRUD operations with TDD
