# LearningApp Azure Services Expansion Plan

## 1. Purpose

This document defines the next set of Azure services and application features to add to the existing LearningApp.

The current application already includes:

- .NET 8 ASP.NET Core Web API
- Hosted Blazor WebAssembly application
- Microsoft Entra ID authentication
- Backend-for-Frontend authentication pattern
- Azure App Service deployment
- Azure SQL Database
- Entity Framework Core
- Serilog and Seq logging
- Health checks
- Rate limiting
- CRUD functionality for:
  - Users
  - Categories
  - Courses
  - Enrollments
  - Learning progress
  - Dashboard summaries

The proposed extension is designed to add practical Azure experience without replacing the application's current architecture.

---

## 2. Recommended New Feature

### Course Completion Certificate and Notification Workflow

When a learner reaches 100% progress for a course, the application should:

1. Mark the course enrollment as completed.
2. Publish a course-completion event.
3. Process the event asynchronously.
4. Generate a course-completion certificate.
5. Store the certificate in Azure Blob Storage.
6. save certificate metadata in Azure SQL Database.
7. Notify the learner that the certificate is available.
8. Display the certificate in the Blazor UI.
9. Capture logs, traces, failures, and performance metrics in Azure Monitor and Application Insights.

This feature introduces event-driven architecture, background processing, object storage, cloud monitoring, passwordless authentication, and distributed-system failure handling.

---

## 3. Proposed Azure Architecture

```text
Browser
   |
   | HttpOnly BFF authentication cookie
   v
LearningApp.UI.Server
   |
   | Microsoft Entra access token
   v
LearningApp.Api
   |
   |-- Azure SQL Database
   |-- Azure Service Bus
   |-- Azure Blob Storage
   |-- Azure Key Vault
   |
   v
Azure Service Bus Queue or Topic
   |
   v
LearningApp.Functions
   |
   |-- Generate certificate PDF
   |-- Upload certificate to Blob Storage
   |-- Save certificate metadata in Azure SQL
   |-- Send completion notification
   |-- Write telemetry to Application Insights
   |
   v
Azure Monitor and Application Insights
```

Existing authentication behavior must remain unchanged:

```text
Browser
   |
   | Same-origin request with HttpOnly cookie
   v
LearningApp.UI.Server
   |
   | Authorization: Bearer <Microsoft Entra access token>
   v
LearningApp.Api
```

The browser must not store raw Microsoft access tokens.

---

## 4. Primary Azure Services

## 4.1 Azure Service Bus

Azure Service Bus should be the central messaging service for course-completion processing.

### Initial design

Create a queue named:

```text
course-completion
```

When learning progress reaches 100%, `LearningApp.Api` should publish a message to this queue.

### Suggested event contract

```json
{
  "eventId": "7c02a00b-dca7-4752-9c57-b17ac746bc02",
  "eventType": "CourseCompleted",
  "userId": 12,
  "courseId": 8,
  "enrollmentId": 25,
  "completedUtc": "2026-08-04T15:30:00Z",
  "correlationId": "0c141369-d662-4465-9447-e4a419508c77",
  "eventVersion": 1
}
```

### Responsibilities of the API

The API should:

1. Validate the progress update.
2. Update progress and completion status in SQL.
3. Commit the transaction.
4. Publish `CourseCompletedEvent`.
5. Return the API response without waiting for certificate generation.

### Why Service Bus is suitable

It introduces:

- Asynchronous communication
- Durable messages
- Retry handling
- Dead-letter queues
- Load leveling
- Loose coupling
- Competing consumers
- Correlation IDs
- Event versioning
- Idempotent consumers

### Future design

The queue can later be replaced with a topic:

```text
course-completed
|-- certificate-generation
|-- email-notification
|-- learning-analytics
```

Each subscription can process the same course-completion event independently.

---

## 4.2 Azure Functions

Create a new .NET 8 isolated-worker project:

```text
LearningApp.Functions
```

### Suggested function

```text
GenerateCourseCompletionCertificate
```

### Trigger

The function should be triggered by the Azure Service Bus queue or topic subscription.

### Responsibilities

The function should:

1. Receive `CourseCompletedEvent`.
2. Validate the message.
3. Check whether the event was already processed.
4. Retrieve the learner, course, and enrollment information.
5. Generate a PDF certificate.
6. Upload the PDF to Azure Blob Storage.
7. Insert or update certificate metadata in Azure SQL Database.
8. Send a completion email or notification.
9. Mark processing as successful.
10. Record structured logs and telemetry.
11. Move unrecoverable messages to the dead-letter queue.

### Processing model

Use the .NET 8 Azure Functions isolated worker model.

Do not use the older in-process Azure Functions model.

### Reliability requirements

The function must support:

- Idempotent processing
- Retry handling
- Poison-message handling
- Dead-letter queue analysis
- Correlation ID propagation
- Cancellation tokens
- Structured logs
- Safe exception handling
- Duplicate-event detection

---

## 4.3 Azure Blob Storage

Generated certificate files should be stored in a private Azure Blob Storage container.

### Suggested container

```text
course-certificates
```

### Suggested blob naming convention

```text
certificates/{userId}/{courseId}/{certificateId}.pdf
```

Example:

```text
certificates/12/8/734ad542-33cc-44f5-b210-9a7ea063d29c.pdf
```

### Storage rules

- Keep the container private.
- Do not store public certificate URLs in the database.
- Do not store certificate PDF binary data directly in SQL Server.
- Store only certificate metadata in SQL.
- Use Managed Identity for Blob Storage access.
- Use short-lived SAS links only when direct download is required.
- Prefer streaming through an authorized API endpoint when stronger access control is needed.

### Certificate database table

Suggested SQL table:

```sql
CREATE TABLE learning.Certificates
(
    CertificateId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT PK_Certificates PRIMARY KEY,

    UserId INT NOT NULL,
    CourseId INT NOT NULL,
    EnrollmentId INT NOT NULL,

    CertificateNumber NVARCHAR(100) NOT NULL,
    BlobName NVARCHAR(500) NOT NULL,
    Status NVARCHAR(50) NOT NULL,

    IssuedUtc DATETIME2 NOT NULL,
    CreatedUtc DATETIME2 NOT NULL,
    UpdatedUtc DATETIME2 NULL,

    CONSTRAINT FK_Certificates_Users
        FOREIGN KEY (UserId)
        REFERENCES learning.Users(UserId),

    CONSTRAINT FK_Certificates_Courses
        FOREIGN KEY (CourseId)
        REFERENCES learning.Courses(CourseId),

    CONSTRAINT FK_Certificates_Enrollments
        FOREIGN KEY (EnrollmentId)
        REFERENCES learning.Enrollments(EnrollmentId),

    CONSTRAINT UQ_Certificates_Enrollment
        UNIQUE (EnrollmentId)
);
```

### Suggested status values

```text
Pending
Generated
Failed
Revoked
```

---

## 4.4 Azure Communication Services Email

Azure Communication Services Email can be used to notify learners when certificates are ready.

### Suggested email workflow

1. Certificate generation succeeds.
2. Certificate metadata is saved.
3. An email is sent to the learner.
4. The email directs the user back to LearningApp.
5. The learner signs in and downloads the certificate from the authenticated application.

### Suggested subject

```text
You completed {CourseName}
```

### Suggested email body

```text
Congratulations, {LearnerName}!

You successfully completed {CourseName}.

Your certificate is now available in LearningApp.

Sign in to LearningApp to view and download it.
```

Do not send a permanently public certificate link.

### Future architecture improvement

Email processing should eventually become a separate Service Bus subscriber:

```text
course-completed topic
|-- certificate-generation subscription
|-- email-notification subscription
```

Alternatively, publish a second event:

```text
CertificateGeneratedEvent
```

The email service can subscribe to that event.

---

## 4.5 Managed Identity

Enable system-assigned Managed Identity for:

- LearningApp API App Service
- LearningApp UI App Service
- LearningApp Function App

Use Managed Identity to access:

- Azure Service Bus
- Azure Blob Storage
- Azure Key Vault
- Azure SQL Database, where practical
- Application Configuration, if introduced later

### Recommended client pattern

```csharp
var credential = new DefaultAzureCredential();

var serviceBusClient = new ServiceBusClient(
    serviceBusNamespace,
    credential);

var blobServiceClient = new BlobServiceClient(
    new Uri(storageAccountUri),
    credential);
```

### Local development behavior

`DefaultAzureCredential` can use:

- Visual Studio credentials
- Azure CLI login
- Azure PowerShell login
- Environment credentials

### Azure behavior

In Azure, `DefaultAzureCredential` should resolve to the application's Managed Identity.

### Benefits

- No storage-account keys in configuration
- No Service Bus connection strings
- Reduced secret rotation
- Better production security
- Role-based access through Azure RBAC

---

## 4.6 Azure Key Vault

Store production secrets in Azure Key Vault.

### Secrets that may belong in Key Vault

- UI BFF Microsoft Entra client secret
- Third-party email credentials, if required
- Certificate-generation secrets, if any
- External API keys
- Legacy connection strings that cannot yet use Managed Identity

### App Service integration

Use App Service Key Vault references.

Example:

```text
@Microsoft.KeyVault(
  VaultName=learningapp-kv;
  SecretName=UiClientSecret
)
```

### Important rule

The application should continue reading configuration through the standard .NET configuration system.

Application code should not be tightly coupled to secret retrieval unless direct Key Vault access is specifically required.

---

## 4.7 Azure Monitor and Application Insights

Add Azure Monitor and Application Insights to:

- LearningApp.Api
- LearningApp.UI.Server
- LearningApp.Functions

### Telemetry to capture

- HTTP request count
- HTTP request duration
- Failed requests
- Exceptions
- SQL dependency calls
- BFF-to-API calls
- Service Bus publish operations
- Service Bus processing duration
- Function execution duration
- Blob Storage upload duration
- Certificate generation duration
- Email delivery result
- Dead-letter queue activity
- Authentication failures
- Authorization failures

### Recommended custom dimensions

Include:

```text
UserId
CourseId
EnrollmentId
CertificateId
EventId
CorrelationId
EventType
MessageDeliveryCount
FunctionName
Environment
```

Avoid including passwords, tokens, secrets, or unnecessary personal data.

### Recommended alerts

Create alerts for:

```text
API failure rate greater than 5%
Function failures greater than 0
Dead-letter queue message count greater than 0
Certificate generation taking more than 30 seconds
Azure SQL dependency failures
Blob Storage upload failures
Email delivery failures
API /health/ready unavailable
UI health endpoint unavailable
```

### Seq strategy

Seq can remain useful for local development.

Recommended setup:

```text
Local development:
Serilog -> Console + Seq

Azure:
OpenTelemetry / Application Insights
Optional Serilog structured logs
```

Avoid relying only on a locally hosted Seq instance for production monitoring.

---

## 5. New Application Components

## 5.1 New API components

Suggested folders and files:

```text
LearningApp.Api
|-- Messaging
|   |-- IEventPublisher.cs
|   |-- ServiceBusEventPublisher.cs
|   `-- Events
|       `-- CourseCompletedEvent.cs
|
|-- Controllers
|   `-- CertificatesController.cs
|
|-- Services
|   |-- ICertificateService.cs
|   `-- CertificateService.cs
|
|-- Repositories
|   |-- ICertificateRepository.cs
|   `-- CertificateRepository.cs
|
|-- Entities
|   `-- Certificate.cs
|
|-- Configurations
|   `-- CertificateConfiguration.cs
|
|-- Options
|   |-- ServiceBusOptions.cs
|   `-- BlobStorageOptions.cs
|
`-- Extensions
    `-- AzureServiceCollectionExtensions.cs
```

### Suggested API endpoints

```text
GET /api/certificates
GET /api/certificates/{certificateId}
GET /api/certificates/{certificateId}/download
POST /api/certificates/{certificateId}/regenerate
```

The regenerate endpoint should be restricted to administrators.

---

## 5.2 New Function project structure

```text
LearningApp.Functions
|-- Functions
|   `-- GenerateCourseCompletionCertificate.cs
|
|-- Contracts
|   |-- CourseCompletedEvent.cs
|   `-- CertificateGeneratedEvent.cs
|
|-- Services
|   |-- ICertificateGenerator.cs
|   |-- PdfCertificateGenerator.cs
|   |-- ICertificateStorage.cs
|   |-- BlobCertificateStorage.cs
|   |-- ICertificateRepository.cs
|   |-- CertificateRepository.cs
|   |-- IEmailNotificationService.cs
|   `-- AzureCommunicationEmailService.cs
|
|-- Options
|   |-- BlobStorageOptions.cs
|   |-- EmailOptions.cs
|   `-- ServiceBusOptions.cs
|
|-- Telemetry
|   `-- TelemetryExtensions.cs
|
|-- Program.cs
|-- host.json
|-- local.settings.json.example
`-- LearningApp.Functions.csproj
```

Do not commit actual values in `local.settings.json`.

---

## 5.3 New Blazor UI components

Suggested pages:

```text
LearningApp.UI.Client
|-- Pages
|   |-- Certificates
|   |   |-- CertificateList.razor
|   |   `-- CertificateDetails.razor
|
|-- Services
|   |-- ICertificateApiService.cs
|   `-- CertificateApiService.cs
|
`-- Components
    |-- CertificateCard.razor
    |-- CertificateStatusBadge.razor
    `-- DownloadCertificateButton.razor
```

### Suggested routes

```text
/certificates
/certificates/{certificateId:guid}
```

### Certificate-list fields

Display:

- Course name
- Learner name, for administrators
- Certificate number
- Completion date
- Issue date
- Status
- Download action

---

## 6. Updated Data Flow

### Course completion flow

```text
1. Blazor UI submits progress update.
2. UI Server proxies the request through /bff/api/...
3. API validates Microsoft Entra access token.
4. LearningProgressService validates the progress update.
5. Progress is changed to 100%.
6. Enrollment status is changed to Completed.
7. SQL changes are committed.
8. API publishes CourseCompletedEvent.
9. API returns success to the UI.
10. Azure Function receives the event.
11. Function checks whether the event was already processed.
12. Function generates the certificate.
13. Function uploads the PDF to Blob Storage.
14. Function saves certificate metadata in Azure SQL.
15. Function sends a completion notification.
16. Function writes telemetry.
17. Blazor UI displays the generated certificate.
```

---

## 7. Transaction and Messaging Consistency

A failure can occur after the database transaction commits but before the Service Bus event is published.

For a robust production implementation, consider the Outbox Pattern.

### Suggested outbox table

```sql
CREATE TABLE learning.OutboxMessages
(
    OutboxMessageId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT PK_OutboxMessages PRIMARY KEY,

    EventType NVARCHAR(200) NOT NULL,
    Payload NVARCHAR(MAX) NOT NULL,
    CorrelationId NVARCHAR(100) NULL,

    CreatedUtc DATETIME2 NOT NULL,
    ProcessedUtc DATETIME2 NULL,
    RetryCount INT NOT NULL
        CONSTRAINT DF_OutboxMessages_RetryCount DEFAULT 0,

    LastError NVARCHAR(MAX) NULL
);
```

### Outbox flow

```text
1. Update learning progress.
2. Mark enrollment complete.
3. Insert OutboxMessages record.
4. Commit all SQL changes in one transaction.
5. Background publisher reads pending outbox records.
6. Publish events to Service Bus.
7. Mark outbox records as processed.
```

This prevents a completed course from being saved without a corresponding event.

For the first learning version, direct Service Bus publishing is acceptable. The Outbox Pattern should be the next reliability improvement.

---

## 8. Idempotency Design

Service Bus messages can be delivered more than once.

The function must not create duplicate certificates.

### Suggested strategies

- Use `EventId` as a unique processing key.
- Keep one certificate per enrollment.
- Add a unique constraint on `EnrollmentId`.
- Store processed event IDs.
- Check for an existing certificate before generation.
- Treat repeated delivery as a successful no-op.

### Suggested processed-events table

```sql
CREATE TABLE learning.ProcessedEvents
(
    EventId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT PK_ProcessedEvents PRIMARY KEY,

    EventType NVARCHAR(200) NOT NULL,
    ProcessedUtc DATETIME2 NOT NULL,
    CorrelationId NVARCHAR(100) NULL
);
```

---

## 9. Error and Retry Handling

### Transient failures

Retry:

- Temporary Azure SQL connectivity failures
- Temporary Blob Storage failures
- Temporary Service Bus failures
- Temporary email-service failures
- Network timeouts

### Permanent failures

Do not endlessly retry:

- Invalid message schema
- Missing user
- Missing course
- Missing enrollment
- Unsupported event version
- Corrupt message data
- Authorization or configuration errors requiring intervention

### Dead-letter queue metadata

Include:

```text
DeadLetterReason
DeadLetterErrorDescription
EventId
CorrelationId
EnrollmentId
DeliveryCount
```

### Operational process

1. Monitor dead-letter queue count.
2. Inspect failed message and telemetry.
3. Correct the underlying issue.
4. Replay the message safely.
5. Confirm idempotency prevents duplicates.

---

## 10. Security Improvements Required

The following issues should be addressed before or during the Azure-service expansion.

## 10.1 Enforce API role policies

Current role policies must enforce actual roles rather than only requiring authentication.

Suggested configuration:

```csharp
options.AddPolicy(
    "AdminOnly",
    policy => policy
        .RequireAuthenticatedUser()
        .RequireRole("Admin"));

options.AddPolicy(
    "LearnerOrAdmin",
    policy => policy
        .RequireAuthenticatedUser()
        .RequireRole("Learner", "Admin"));
```

Role claim mapping must match the claims issued by Microsoft Entra ID.

---

## 10.2 Add antiforgery protection

State-changing requests through the BFF should be protected against cross-site request forgery.

Review:

```text
POST
PUT
PATCH
DELETE
```

Use an antiforgery-token pattern suitable for the hosted Blazor WASM and BFF architecture.

---

## 10.3 Persist Data Protection keys

The UI Server owns the authentication cookie.

For Azure deployment, persist ASP.NET Core Data Protection keys in durable storage.

Possible storage:

- Azure Blob Storage

Protect keys using:

- Azure Key Vault

This is important when:

- App Service restarts
- Deployment slots are swapped
- Multiple App Service instances are used
- Horizontal scaling is enabled

---

## 10.4 Keep secrets out of configuration files

Do not commit:

- Microsoft Entra client secrets
- Storage-account keys
- Service Bus connection strings
- Azure Communication Services credentials
- Production SQL passwords
- SAS tokens

Use:

- Managed Identity
- Azure RBAC
- Key Vault
- User Secrets for local development

---

## 11. Suggested Azure Resources

Suggested resource naming:

```text
learningapp-api
learningapp-ui
learningapp-functions
learningapp-sql
learningapp-storage
learningapp-servicebus
learningapp-keyvault
learningapp-appinsights
learningapp-monitor-workspace
learningapp-communication
```

Apply environment suffixes where necessary:

```text
-dev
-test
-prod
```

Example:

```text
learningapp-servicebus-dev
learningapp-storage-prod
```

---

## 12. Recommended Implementation Phases

| Phase | Feature | Azure Services | Difficulty |
|---|---|---|---|
| 1 | Production monitoring | Application Insights, Azure Monitor | Easy |
| 2 | Secure configuration | Managed Identity, Key Vault | Easy to Medium |
| 3 | Certificate metadata and UI | Azure SQL, existing API and Blazor UI | Medium |
| 4 | Certificate file storage | Azure Blob Storage | Medium |
| 5 | Course-completion events | Azure Service Bus | Medium |
| 6 | Background certificate generation | Azure Functions | Medium to Advanced |
| 7 | Completion notification | Azure Communication Services Email | Medium |
| 8 | Reliability hardening | Outbox Pattern, DLQ handling, idempotency | Advanced |
| 9 | Automated delivery | GitHub Actions or Azure DevOps | Medium |
| 10 | Performance optimization | Azure Managed Redis | Medium |

---

## 13. Recommended Starting Scope

The first implementation should include:

```text
Course completion
+ Azure Service Bus
+ Azure Functions
+ Azure Blob Storage
+ Application Insights
```

Email should be added only after certificate generation is working reliably.

### First milestone

A learner reaches 100% progress and:

1. A Service Bus message is published.
2. A Function processes the message.
3. A PDF certificate is generated.
4. The certificate is stored in Blob Storage.
5. Metadata is saved in Azure SQL.
6. The learner can view and download it from Blazor.
7. All operations are visible in Application Insights.

---

## 14. Future Azure Services

After completing the certificate workflow, consider the following additions.

## 14.1 Azure Managed Redis

Use Redis for:

- Dashboard caching
- Category and course caching
- Distributed session or token-cache scenarios
- Rate-limit coordination across scaled instances

Do not add Redis until a measurable caching or scale requirement exists.

## 14.2 Azure App Configuration

Use App Configuration for:

- Feature flags
- Centralized non-secret settings
- Gradual feature rollout
- Certificate-feature enablement
- Environment-specific behavior

Suggested feature flags:

```text
CertificatesEnabled
CompletionEmailEnabled
UseServiceBusTopic
EnableCertificateRegeneration
```

## 14.3 Azure AI Search

A future learning feature could provide:

- Full-text course search
- Filtering by category and difficulty
- Semantic search
- Search suggestions

This should be considered only after the event-driven workflow is complete.

## 14.4 Azure OpenAI

Possible future features:

- Course-description generation
- Learning-path recommendations
- Course-content summarization
- AI-generated quizzes
- Personalized learner assistance

This should be treated as a separate future phase, not part of the first Azure-services implementation.

---

## 15. Testing Requirements

Add automated test projects.

Suggested structure:

```text
LearningApp.Api.Tests
LearningApp.Functions.Tests
LearningApp.IntegrationTests
```

### API tests

Test:

- Progress update to less than 100%
- Progress update to exactly 100%
- Duplicate completion attempts
- Authorization rules
- Certificate-list access
- Certificate-download authorization
- Event-publishing failures

### Function tests

Test:

- Valid Service Bus message
- Invalid message
- Duplicate event
- Existing certificate
- Blob upload failure
- SQL failure
- Email failure
- Dead-letter behavior
- Correlation ID propagation

### Integration tests

Test:

```text
API
-> Azure Service Bus emulator or test namespace
-> Azure Function
-> Blob Storage emulator or test account
-> SQL test database
```

---

## 16. Acceptance Criteria

The feature is complete when:

- A course-completion event is published only when progress reaches 100%.
- Duplicate messages do not create duplicate certificates.
- The certificate is stored in a private Blob container.
- Certificate metadata is stored in Azure SQL.
- Learners can see only their own certificates.
- Administrators can see certificates according to the defined policy.
- Downloads require authentication and authorization.
- The browser never stores Microsoft access tokens.
- Azure resources are accessed using Managed Identity where supported.
- Secrets are stored in Key Vault or secure local development storage.
- Failures are visible in Application Insights.
- Dead-letter messages create alerts.
- Correlation IDs are preserved across API, Service Bus, Function, Blob, SQL, and email operations.
- The existing BFF authentication flow continues to work.
- Existing CRUD functionality remains unaffected.

---

## 17. Final Target Architecture

```text
Browser
   |
   | HttpOnly authentication cookie
   v
LearningApp.UI.Server
   |
   | Microsoft Entra delegated access token
   v
LearningApp.Api
   |
   |-- Azure SQL Database
   |-- Azure Service Bus
   |-- Azure Blob Storage
   |-- Azure Key Vault
   |-- Application Insights
   |
   v
CourseCompletedEvent
   |
   v
Azure Service Bus
   |
   v
LearningApp.Functions
   |
   |-- Generate PDF certificate
   |-- Upload to Blob Storage
   |-- Save metadata to Azure SQL
   |-- Send email notification
   |-- Log telemetry
   |
   v
Azure Monitor Alerts
```

---

## 18. Final Recommendation

The most suitable next feature for LearningApp is an asynchronous course-completion certificate workflow using:

- Azure Service Bus
- Azure Functions
- Azure Blob Storage
- Managed Identity
- Azure Key Vault
- Application Insights
- Azure Monitor
- Azure Communication Services Email

This extension fits the current learning-progress domain naturally and introduces the most valuable Azure concepts for a .NET developer:

- Event-driven architecture
- Asynchronous messaging
- Serverless processing
- Cloud object storage
- Distributed tracing
- Managed identities
- Secret management
- Retry handling
- Dead-letter queues
- Idempotency
- Production monitoring
