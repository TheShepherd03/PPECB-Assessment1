# PPECB Product Catalogue

A secure, multi-user web application for managing **Categories** and **Products**.
Every user sees and manages only their own data.

Submitted for **PPECB Dev Assessment 1**.

| Layer | Technology |
| --- | --- |
| Backend | C# / ASP.NET Core Web API (.NET 8 LTS) |
| Data access | Entity Framework Core 8, code-first migrations |
| Database | SQL Server LocalDB |
| Frontend | Angular 21, standalone components, TypeScript |
| Authentication | ASP.NET Core Identity + JWT bearer tokens |
| Excel | ClosedXML |
| Tests | xUnit, Moq, FluentAssertions |

---

## 1. Prerequisites

| Requirement | Check |
| --- | --- |
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | `dotnet --version` → `8.x` |
| SQL Server LocalDB (ships with SQL Server Express) | `sqllocaldb info` |
| [Node.js](https://nodejs.org/) 20 or 22 LTS | `node --version` |
| EF Core CLI | `dotnet tool install --global dotnet-ef --version 8.*` |

---

## 2. Running the project

### Step 1 — Create the database

From the repository root:

```bash
dotnet ef database update --project src/PPECB.Infrastructure --startup-project src/PPECB.API
```

This creates the `PPECB_Assessment1` database on `(localdb)\MSSQLLocalDB` and applies the
schema. To use a different server, edit `ConnectionStrings:DefaultConnection` in
`src/PPECB.API/appsettings.json`.

### Step 2 — Start the API

```bash
dotnet run --project src/PPECB.API --urls http://localhost:5099
```

The API listens on **http://localhost:5099**, with Swagger UI at
**http://localhost:5099/swagger**.

### Step 3 — Start the Angular client

In a second terminal:

```bash
cd src/PPECB.Web
npm install
npm start
```

Open **http://localhost:4200** and register an account.

> The client expects the API on port 5099. If you change it, update
> `src/PPECB.Web/src/environments/environment.ts` and the `Cors:AllowedOrigins` entry in
> `appsettings.json`.

### Step 4 — Run the tests

```bash
dotnet test
```

52 unit tests covering the code-format rules, the service layer, and per-user isolation.

---

## 3. Project structure

```
PPECB.sln
├── src/
│   ├── PPECB.Domain          Entities and domain exceptions. No dependencies.
│   ├── PPECB.Application     Contracts, DTOs, business rules, services.
│   ├── PPECB.Infrastructure  EF Core, repositories, Identity, Excel, file storage.
│   ├── PPECB.API             Controllers, JWT setup, exception middleware.
│   └── PPECB.Web             Angular client.
├── tests/
│   └── PPECB.UnitTests       xUnit tests.
└── docs/
    └── TECHNICAL.md          Architecture, ERD, and design decisions.
```

Dependencies point inward only: `API → Infrastructure → Application → Domain`.

The Domain layer references no NuGet packages at all, and Application references only a
dependency-injection abstraction. The business rules therefore have no compile-time
knowledge of Entity Framework or ASP.NET, which is what allows them to be unit tested
without a database or a web server.

---

## 4. Features

### Authentication
- Register with email and password; log in to receive a JWT.
- Passwords hashed by ASP.NET Core Identity. Minimum 8 characters with upper, lower and a digit.
- All category and product endpoints require a valid token.

### Categories
- View (paged), add, and edit.
- **Code rule:** 3 letters followed by 3 numbers, e.g. `ABC123`. Stored upper-cased, so
  `abc123` and `ABC123` are treated as the same code.
- Codes are unique per user, with a clear message when the format is wrong or the code is
  already taken.

### Products
- View (paged, **10 per page**), add, edit, and delete.
- **Code rule:** auto-generated as `yyyyMM-###`, e.g. `202105-023`. Never supplied by the
  client. The sequence restarts each month, per user.
- A category must be selected; only active categories are offered.
- Image upload (JPG, PNG, GIF, WEBP — max 5 MB).
- **Excel export** of all products and **Excel import** to bulk-create them. A blank
  import template is downloadable from the products page.

### Excel import rules
- Requires a `Name` column plus either `CategoryCode` or `CategoryName`, and `Price`.
  Column order does not matter — headers are matched by name.
- The import is **all-or-nothing**: if any row fails, nothing is written and every problem
  is listed with its row number so the file can be corrected in one pass.

---

## 5. Security

- JWT bearer authentication; issuer, audience, lifetime and signature all validated.
- **Data isolation** is enforced by EF Core global query filters on `OwnerId`, applied
  centrally in the `DbContext` rather than per query. Requesting another user's record by
  id returns `404`, which avoids confirming that the record exists.
- Audit fields are stamped server-side from the token and are immutable after insert, so a
  client cannot spoof `CreatedBy` via the request body.
- Uploaded images are renamed to a GUID and validated against known image signatures, so a
  renamed script cannot be stored or served.
- Login failures are deliberately identical for an unknown email and a wrong password, to
  prevent account enumeration.
- Errors are returned as RFC 7807 problem documents; stack traces are never exposed in
  Production.
- Security headers are set on every response, and CORS is restricted to the client origin.

### Development signing key

`appsettings.Development.json` contains a clearly-labelled JWT key so the project runs
immediately after cloning. **It is not a secret and must not be used outside local
development.** In a real deployment, supply `Jwt__Key` through environment variables or
user secrets, which override the file:

```bash
dotnet user-secrets set "Jwt:Key" "<a long random value>" --project src/PPECB.API
```

---

## 6. Concurrency

Both tables carry a SQL Server `rowversion` used as an EF Core concurrency token. Clients
round-trip the value they loaded; a stale write is rejected with `409 Conflict` and a
message asking the user to reload, rather than silently overwriting another user's change.

Product code generation is additionally guarded by a unique index on
`(OwnerId, ProductCode)`. If two concurrent creates pick the same code, the database
rejects the loser and the service retries with the next number.

---

## 7. API reference

| Method | Route | Purpose |
| --- | --- | --- |
| POST | `/api/auth/register` | Create an account, returns a JWT |
| POST | `/api/auth/login` | Sign in, returns a JWT |
| GET | `/api/categories` | Paged list (`pageNumber`, `pageSize`, `search`) |
| GET | `/api/categories/lookup` | Active categories for the product dropdown |
| GET | `/api/categories/{id}` | Single category |
| POST | `/api/categories` | Create |
| PUT | `/api/categories/{id}` | Update |
| GET | `/api/products` | Paged list (`pageNumber`, `pageSize`, `search`, `categoryId`) |
| GET | `/api/products/{id}` | Single product |
| POST | `/api/products` | Create (code auto-generated) |
| PUT | `/api/products/{id}` | Update |
| DELETE | `/api/products/{id}` | Delete |
| POST | `/api/products/{id}/image` | Upload product image (multipart) |
| GET | `/api/products/export` | Download all products as `.xlsx` |
| GET | `/api/products/import-template` | Download a blank import workbook |
| POST | `/api/products/import` | Bulk import from `.xlsx` (multipart) |

Full request and response schemas are available in Swagger at `/swagger` when running in
Development.

---

## 8. Further documentation

See [docs/TECHNICAL.md](docs/TECHNICAL.md) for the ERD, layer responsibilities, the design
patterns used, and the reasoning behind the main decisions.
