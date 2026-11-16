# Personal Microsoft account authorization

> Historical design, superseded by [Entra role authorization](entra-role-authorization.md).
> The current API uses Entra access-token roles, never database roles, for Microsoft
> sign-in. It no longer grants Learner automatically. Do not use the consumer
> rollout instructions below for the current single-tenant deployment.

Personal-account-only app registrations cannot define Entra app roles. LearningApp
therefore keeps permissions in `learning.Users.Role`. Microsoft proves the external
identity; the API resolves that identity to a local user before applying the existing
`LearnerOrAdmin` and `AdminOnly` policies.

## Flow

1. The UI signs in with Microsoft using the authorization code flow and requests
   the API's `access_as_user` delegated scope.
2. The API validates the access token's signature, issuer, audience and expiry.
3. The bearer token validation event requires `access_as_user` and resolves the
   validated `(iss, sub)` through `learning.ExternalLogins`. On first use it creates
   a user with role `Learner` and no usable password, plus the external login link.
4. The API reads the user's current role and active status on each request.
   Incoming Microsoft role claims are replaced with the database role. Disabled
   users fail authentication. Administrators must grant `Admin` explicitly in the
   database or through the existing admin user-management API.
5. `/api/auth/me` supplies the local user ID and role to `/bff/user` so Blazor can
   apply the same permissions. Enrollments use this internal ID, not Microsoft's subject.

Email is contact/display data, never the account linking key. An existing password
account with the same email stays separate; it cannot be taken over through Microsoft
login. If email is missing or already used, the external user's email field receives
a unique reserved `@identity.invalid` address. No email is sent to that address.
Changing the API registration can change its pairwise `sub`, requiring an explicitly
verified account relinking process. Automatic email-based account linking is not provided.

## Azure rollout

1. Run [Database/003_external_logins.sql](../Database/003_external_logins.sql) against
   the deployed LearningApp SQL database **before** deploying the updated API. This
   additive script also needs to run after scripts 001 and 002 on a fresh database.
2. Publish the updated API and UI Server applications.
3. Verify App Service environment settings. Keep `consumers` for personal accounts:

   | Application | Setting | Value |
   | --- | --- | --- |
   | API | `AzureAd__TenantId` | `consumers` |
   | API | `AzureAd__ClientId` | API registration's application ID |
   | API | `AzureAd__Audience` | API's configured Application ID URI |
   | UI Server | `AzureAd__Authority` | `https://login.microsoftonline.com/consumers/v2.0` |
   | UI Server | `AzureAd__ApiScope` | API's exact full scope, usually `api://<API-application-id>/access_as_user` |
   | UI Server | `AzureAd__ClientId` | UI registration's application ID |
   | UI Server | `AzureAd__ClientSecret` | UI registration's current secret value |
   | UI Server | `Api__BaseUrl` | Deployed API HTTPS base URL, with trailing slash |

   The repository's API and UI scope defaults currently reference different API IDs;
   deployed overrides must point to the same API registration. Keep secrets in App
   Service settings/Key Vault, outside source control.
4. Sign out and sign back in. `/bff/user` should return the internal `userId` and
   `roles: ["Learner"]`; the dashboard's categories GET should succeed. Admin writes
   must still return 403 for a Learner.

If requests fail, inspect API logs and verify that the schema script ran, the DB is
reachable, and the access token is for this API with scope `access_as_user`. The ID
token's claims are not evidence of API permissions. Keep access tokens server-side.

## Verification

```powershell
dotnet run --project tests/LearningApp.AuthChecks/LearningApp.AuthChecks.csproj -p:BuildInParallel=false
```

The regression checks use SQLite and exercise the registered bearer token validation
hook, provisioning, identity reuse, scope rejection, database role changes, disabled
accounts, email collisions, dashboard/admin policies and internal user IDs. They
start after token cryptographic validation and do not replace an Azure sign-in test
or SQL Server deployment verification.

Microsoft reference: [Secure applications and APIs by validating claims](https://learn.microsoft.com/en-us/entra/identity-platform/claims-validation).
