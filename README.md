# Client WOE entry

A simplified **Client Work Order Entry (WOE)** module for a diagnostic lab.
Pick a client, capture patient details, search and select tests, review the total, and submit.
The API generates a unique WOE number and saves the order and its test lines to SQL Server in one transaction.

**Stack:** ASP.NET Core 8 Web API · EF Core 8 (Code-First) · SQL Server · Swagger · plain HTML/CSS/JS frontend (no build step)

The full write-up (architecture, database design, API reference) is in
[`docs/Solution_Documentation.pdf`](docs/Solution_Documentation.pdf).

---

## What it does

- Choose a **client**, then enter a **new patient** (name, age, gender, mobile) or pick an **existing patient** from the look-up.
- **Search the test catalogue**, click tests to add or remove them, and change quantities. The total updates live.
- **Save work order** calls `POST /api/workorders`. The response (WOE number, patient, line items, total) is shown on screen.
- Inline validation stops bad input before it reaches the API. The API validates again and returns clear `400` messages.

## Repository layout

```
ClientWOE/
├── ClientWOE.sln
├── backend/ClientWOE.API/
│   ├── Controllers/     Clients, Patients, TestMaster, WorkOrders
│   ├── Models/          EF Core entities (Client, Patient, TestMaster, WorkOrder, WorkOrderTestDetail)
│   ├── DTOs/            Request/response contracts
│   ├── Data/            WoeDbContext (relationships, unique indexes, seed data)
│   ├── Properties/      launchSettings.json (ports 5080 / 7080)
│   ├── Program.cs       DI, Swagger, CORS, global exception handler, DB initialisation
│   └── appsettings.json Connection string
├── database/schema.sql  Standalone SQL Server script (tables, indexes, seed data)
├── frontend/            index.html, styles.css, app.js
└── docs/                Solution documentation + screenshots
```

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server: LocalDB (installed with Visual Studio), Developer/Express edition, Docker, or Azure SQL
- A modern browser
- Optional: Visual Studio 2022 (17.8+) or VS Code

## Quick start

### 1. Configure the database connection

Open `backend/ClientWOE.API/appsettings.json`. The default targets LocalDB:

```json
"DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=ClientWOE_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

For a full SQL Server instance use, for example, `Server=localhost;Database=ClientWOE_DB;Trusted_Connection=True;TrustServerCertificate=True`
(or `User Id=sa;Password=...` for SQL authentication).

### 2. Create the database (pick one option)

**Option A: EF Core migrations (recommended, this is the Code-First path)**

```bash
cd backend/ClientWOE.API
dotnet tool install --global dotnet-ef --version 8.*     # first time only
dotnet ef migrations add InitialCreate                   # creates the Migrations/ folder; commit it
```

The API applies pending migrations automatically on startup, so there is nothing else to run.
(To create the database without starting the API: `dotnet ef database update`.)

**Option B: no commands at all**

If the project has no `Migrations/` folder, the API creates the schema and seed data itself on first start.

**Option C: raw SQL script**

Run [`database/schema.sql`](database/schema.sql) in SSMS / Azure Data Studio / `sqlcmd`. It creates `ClientWOE_DB`, all five tables, indexes and seed data.
Use this **instead of** migrations, not in addition to them.

> Seed data: 5 clients and 15 tests (for example client 2 = Apollo Diagnostics, test 1 = Complete Blood Count at 350, test 3 = Liver Function Test at 700).

### 3. Run the API

```bash
cd backend/ClientWOE.API
dotnet run
```

| Profile | URL |
|---|---|
| `http` (default for `dotnet run`) | http://localhost:5080 |
| `https` (`dotnet run --launch-profile https`) | https://localhost:7080 |

Swagger UI: **http://localhost:5080/swagger** (the root URL redirects there).

In Visual Studio: open `ClientWOE.sln`, set `ClientWOE.API` as the startup project, choose the `http` or `https` profile, press F5.

### 4. Run the frontend

`frontend/app.js` starts with the API address:

```js
const API_BASE = "http://localhost:5080/api";
```

Change it if you run the `https` profile (`https://localhost:7080/api`) or a different port. Then serve the folder with any static server:

```bash
cd frontend
python3 -m http.server 8080        # Windows: py -m http.server 8080
```

Open **http://localhost:8080**. (VS Code's Live Server extension also works. Opening `index.html` directly from disk works too, because the API allows cross-origin calls.)

> If you use the `https` profile and the browser blocks requests, trust the dev certificate once: `dotnet dev-certs https --trust`.

## API reference

Interactive docs are at `/swagger`.

| Method | Route | Description |
|---|---|---|
| GET | `/api/clients` | List active clients |
| GET | `/api/clients/{id}` | Get one client |
| POST | `/api/clients` | Create a client (`409` if the code already exists) |
| GET | `/api/patients?search=&clientId=` | Search patients by name, code or mobile |
| GET | `/api/patients/{id}` | Get one patient |
| GET | `/api/testmaster?search=` | List / search active tests |
| GET | `/api/testmaster/{id}` | Get one test |
| GET | `/api/workorders?clientId=&from=&to=` | List work orders, newest first |
| GET | `/api/workorders/{id}` | Get a saved work order with its lines |
| POST | `/api/workorders` | Submit a WOE |

### Submit a WOE

`POST /api/workorders`

```json
{
  "clientId": 2,
  "patientName": "Ravi Kumar",
  "age": 34,
  "gender": 0,
  "mobileNumber": "9880012345",
  "tests": [
    { "testId": 1, "quantity": 1 },
    { "testId": 3, "quantity": 1 }
  ]
}
```

- `gender`: `0` Male, `1` Female, `2` Other (the names also work).
- For an **existing patient**, send `"patientId": 1` instead of the four patient fields.
- Duplicate `testId` lines are merged. Rates come from the server, never from the client.

`201 Created` (with a `Location` header):

```json
{
  "workOrderId": 1,
  "woeNumber": "WOE-20260921-0001",
  "orderDate": "2026-09-21T10:15:00Z",
  "clientId": 2,
  "clientName": "Apollo Diagnostics",
  "patientId": 1,
  "patientCode": "PAT-000001",
  "patientName": "Ravi Kumar",
  "age": 34,
  "gender": "Male",
  "mobileNumber": "9880012345",
  "totalAmount": 1050.00,
  "status": "Saved",
  "tests": [
    { "testId": 1, "testCode": "CBC", "testName": "Complete Blood Count", "rate": 350.00, "quantity": 1, "amount": 350.00 },
    { "testId": 3, "testCode": "LFT", "testName": "Liver Function Test", "rate": 700.00, "quantity": 1, "amount": 700.00 }
  ]
}
```

### Status codes

| Code | When |
|---|---|
| 200 | Successful GET |
| 201 | Work order or client created |
| 400 | Validation failure: bad field, no tests, unknown or inactive client/patient/test, patient belongs to another client |
| 404 | GET by id for something that does not exist |
| 409 | Client code already exists |
| 500 | Unhandled error. Logged server-side; the caller gets `{ "status": 500, "message": "..." }` |

`400` bodies use the standard ASP.NET `errors` dictionary. `404`, `409` and `500` use `{ "status": ..., "message": "..." }`.

## Key design decisions

- **WOE number:** `WOE-yyyyMMdd-####` (UTC date), the sequence restarts each day. It is generated inside the save transaction (next = highest number today + 1). A unique index guards against two simultaneous requests picking the same number; the loser rolls back and retries with a fresh number.
- **One transaction:** new patient, order header and line items commit or roll back together.
- **Rate snapshot:** `WorkOrderTestDetail.Rate` copies the price at order time, so later catalogue changes never alter old orders.
- **Patient codes:** `PAT-000001` style, derived from the identity value.
- **Validation in two layers:** DataAnnotations on the DTOs, explicit business checks in `WorkOrdersController`, and the same rules in the frontend for fast feedback. Mobile numbers must be 10 digits starting with 6 to 9.
- **DTOs instead of entities** at the API boundary, so the contract stays stable if the schema changes.
- **Global exception handler** returns a consistent JSON body and never leaks internals.
- **Foreign keys** from `WorkOrder` to `Client` and `Patient` are `Restrict` (SQL Server disallows multiple cascade paths); only `WorkOrderTestDetail -> WorkOrder` cascades.

Deliberately out of scope for this assignment: authentication/authorization, audit trail, full pagination (list endpoints use a row cap).

## Testing checklist

1. Open `/swagger`, run `POST /api/workorders` with the sample above. Expect `201`, a `woeNumber`, and `totalAmount` 1050.
2. Send it again. The WOE sequence should increment (`...-0002`).
3. Send `"tests": []`, an unknown `testId` (for example 999), or `"mobileNumber": "123"`. Expect `400` with a clear message.
4. Call `GET /api/workorders/{id}` with an id that does not exist. Expect `404`.
5. In the frontend: pick a client, type a patient, add tests, change quantities, confirm the total, save, and confirm the saved WOE renders.
6. Try saving with no tests or a bad mobile number. The form should block the call and show inline errors.
7. Start a new order for the same client, type the first letters of a saved patient's name, and choose them from the suggestions. Only `patientId` is sent.

## Troubleshooting

| Symptom | Fix |
|---|---|
| Frontend shows "API unreachable" | Start the API, and make sure `API_BASE` in `frontend/app.js` matches its URL and port. |
| Browser blocks calls to `https://localhost:7080` | Run `dotnet dev-certs https --trust`, or use the `http` profile. |
| `Cannot open database` / login errors on start | Check `DefaultConnection`; make sure LocalDB or SQL Server is running. |
| `There is already an object named 'Client'` when starting | Tables already exist (created by `schema.sql` or an earlier run) and you then added migrations. Drop the database (`dotnet ef database drop`) and start again with one method only. |
| `dotnet ef` not found | `dotnet tool install --global dotnet-ef --version 8.*` |

## Screenshots

Add screenshots of your own run to `docs/screenshots/` (see the checklist in that folder), then link them here, for example:

```markdown
![Order entry](docs/screenshots/01-order-entry.png)
![Saved work order](docs/screenshots/02-saved-woe.png)
![Swagger](docs/screenshots/03-swagger-post.png)
![Database rows](docs/screenshots/04-sql-rows.png)
```
