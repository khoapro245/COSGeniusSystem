# Project review and Patient API

## Review findings

- The project is a .NET 10 ASP.NET Core API using SQL Server and a scaffolded ApplicationDbContext. Models cover patients, clinical records, billing, herbs, purchasing, inventory, users and roles. Before this change the only controller was WeatherForecast; DTO, service and repository folders were placeholders.
- Database initialization needs attention: database.sql creates ClinicHerbManagementDB, but Docker Compose selects COSGeniusDb. Compose does not mount an initialization script. There are no EF migrations, so the development startup migration loop cannot provision the schema. Apply the existing database scripts explicitly to the configured database.
- Patient_Update.sql adds the patient fields, computed PatientCode and PatientContact table required by these APIs. Run it once after the base schema on an existing compatible SQL Server database. The base SQL script contains database creation and file-path settings that need review before use on another machine; do not execute it blindly on an existing database.
- appsettings.json and Compose contain hardcoded database credentials. Move deployment credentials into environment variables or user secrets. Their values and database names currently differ.
- No authentication or authorization policy is configured. The new routes follow the current application's public access model; assigning access to clinical data remains deployment work.
- There are duplicate root/application Dockerfiles. Compose uses COSGeniusSystem/Dockerfile.

## Implementation

Controllers depend on service interfaces. Services contain business rules and explicit DTO mapping, and depend on repository interfaces. Repository implementations alone access the existing ApplicationDbContext. All four interface/implementation pairs are registered as scoped services in Program.cs.

Request DTOs are separate from response DTOs, with distinct create/update types. FullName is required; field lengths match EF mappings; email and patient dates are validated. IDs, database-generated PatientCode and CreatedAt are response-only. PUT replaces writable fields; omitted optional fields become null. Patient responses contain scalar fields; fetch contacts through their collection endpoint.

All database operations are async and accept cancellation tokens. List patients uses stable ID ordering and pageNumber (default 1) and pageSize (default 20, maximum 100). Both lists return a single PagedResult inside data: items, pageNumber, pageSize, totalCount, totalPages, hasPreviousPage and hasNextPage. Contact lists accept the same pagination parameters. Contact reads and writes are scoped by both PatientId and ContactId. The route supplies PatientId, preventing reassignment in request bodies.

Deleting a patient with contacts or medical records returns 409; no cascading deletion is introduced. A SQL foreign-key or uniqueness conflict, including a concurrent relationship change, also returns 409. Missing patients/contacts return 404, invalid input returns 400, and unexpected errors are logged in detail on the server and return a generic 500 message. Creates return 201 with Location; successful updates/deletes return 200 with data set to null.

## Unified response contract

Patient and PatientContact controllers inherit BaseApiController and use SuccessResponse or CreatedResponse. There is exactly one response envelope, ApiResponseModel<T>, with code, message, isSuccess and data. Success messages are centralized in SystemMessages. CreatedResponse uses CreatedAtAction and enforces code 201. The code in every envelope matches its HTTP status.

ApiException and its BadRequestException, NotFoundException, UnauthorizedException, ForbiddenException, ConflictException and InternalServerErrorException subclasses always report isSuccess=false. SystemMessage constructors reject success messages and mismatched status codes. Services throw the appropriate business exceptions without depending on HTTP result types or HttpContext. Database exceptions are handled centrally, without controller database dependencies or added Phone/Email/IdentityCard uniqueness rules.

Automatic ApiController validation returns HTTP 400 with the same envelope and data.errors, a dictionary of field names to error-message arrays. MVC's default ProblemDetails mapping is disabled. The exception handler returns the same envelope and logs unexpected failures without exposing exception details. Authentication is not configured in the current project; an exception handler alone does not handle authentication challenges. No TypedSuccessResponse or TypedEmptyResponse helper existed, and none was added.

Example list response:

```json
{
  "code": 200,
  "message": "Patients retrieved successfully.",
  "isSuccess": true,
  "data": {
    "items": [],
    "pageNumber": 1,
    "pageSize": 20,
    "totalCount": 0,
    "totalPages": 0,
    "hasPreviousPage": false,
    "hasNextPage": false
  }
}
```

## Routes

| Method | Route | Purpose |
| --- | --- | --- |
| GET | /api/patients?pageNumber=1&pageSize=20 | List patients |
| GET | /api/patients/{patientId} | Get patient |
| POST | /api/patients | Create patient |
| PUT | /api/patients/{patientId} | Replace writable patient fields |
| DELETE | /api/patients/{patientId} | Delete patient without dependents |
| GET | /api/patients/{patientId}/contacts?pageNumber=1&pageSize=20 | List patient's contacts |
| GET | /api/patients/{patientId}/contacts/{contactId} | Get contact |
| POST | /api/patients/{patientId}/contacts | Create contact |
| PUT | /api/patients/{patientId}/contacts/{contactId} | Replace contact fields |
| DELETE | /api/patients/{patientId}/contacts/{contactId} | Delete contact |

## Verification

Requires the .NET 10 SDK and a provisioned SQL Server schema for live requests.

```powershell
dotnet build COSGeniusSystem/COSGeniusSystem.csproj
dotnet run --project tests/PatientApi.Checks/PatientApi.Checks.csproj
dotnet run --project COSGeniusSystem/COSGeniusSystem.csproj --launch-profile http
```

The executable checks require no test-framework packages. They cover service-level contact ownership, missing parents, CRUD behavior, protected patient deletion, date validation, both paginated lists, DTO validation, controller envelope/status results, CreatedAtAction route values, JSON field names and exception/message invariants. They do not replace SQL Server or HTTP integration tests.

On the implementation machine, build execution is blocked by NETSDK1045: installed SDKs are .NET 8 and 9, while this project requires .NET 10. Docker is not running. The service, DTO, response and controller checks passed using a temporary .NET 9 harness compiling the same source files with the ASP.NET Core shared framework, excluding EF persistence and the application startup. The temporary harness was removed. The committed .NET 10 check project and the full application still need verification with .NET 10; no successful full application build or live database test is claimed.

See COSGeniusSystem/PatientApi.http for manual HTTP requests. Change the sample IDs to those returned by POST. Development OpenAPI is available at /openapi/v1.json.

