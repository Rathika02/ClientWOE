# Client WOE Entry

A simplified Client Work Order Entry (WOE) system for a diagnostic lab.

### Features

* Select client and enter patient details
* Search and add laboratory tests
* Set quantities and calculate total
* Submit and save work orders
* Generate unique WOE numbers

### Tech Stack

* ASP.NET Core 8 Web API
* Entity Framework Core 8
* SQL Server LocalDB
* Swagger
* HTML/CSS/JavaScript

### Run the Project

1. Open `ClientWOE.sln` in Visual Studio.
2. Set `ClientWOE.API` as the startup project.
3. Run the API.
4. Open `frontend/index.html` in a browser.
5. Make sure the API is running before using the frontend.

Swagger:

`http://localhost:5080/swagger`

### Database

The project uses SQL Server LocalDB and Entity Framework Core migrations.

```powershell
Add-Migration InitialCreate
Update-Database
```

The main tables are:

`Client`, `Patient`, `TestMaster`, `WorkOrder`, `WorkOrderTestDetail`

### Project Structure

```text
ClientWOE.API/   → Backend Web API
frontend/        → HTML/CSS/JavaScript
database/        → SQL database script
```
