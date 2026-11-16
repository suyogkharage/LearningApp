# Single-tenant Entra role authorization

Microsoft Entra API access-token roles are the authority for Microsoft users.
The database supplies an internal user ID, profile and active status so enrollments
and progress continue to reference `learning.Users`.

## Request flow

1. The UI Server signs in with a tenant-specific OpenID Connect authority and
   requests the API's delegated `access_as_user` scope.
2. The API validates the token signature, issuer, audience and lifetime. Its
   `MicrosoftAccountService` additionally requires the delegated scope and links
   the validated `(iss, sub)` to a local user via `learning.ExternalLogins`.
3. On first sign-in, a local user and external login link are created. The user
   has no usable local password. The existing `Users.Role` default is retained for
   schema compatibility but is never used to authorize a Microsoft token.
4. API policies use the token's roles: `AdminOnly` requires `Admin`, while
   `LearnerOrAdmin` accepts `Learner` or `Admin`. Raw `roles` and mapped role claims
   are normalized, including multiple assigned roles. No role is inferred from
   successful authentication. An unassigned user receives 403 from protected APIs.
5. `/api/auth/me` returns the internal user ID, profile and effective token roles.
   `/bff/user` supplies those values to Blazor. This call remains in this
   implementation because API roles need not appear in the UI application's ID
   token, and the UI also needs the database ID for enrollment forms.

Local password login continues to use local JWT roles derived from `Users.Role`.
Changing a database role does not change Microsoft user permissions. Likewise,
the role displayed by the existing Users management page is the local password
role, not a live Entra assignment. Assign Microsoft permissions in Entra.
Local `IsActive = false` still blocks Microsoft authentication.

Entra role changes apply when a newly issued access token contains them. Existing
tokens retain their claims until expiry; sign out and sign back in when testing.
The database is still queried to resolve identity and check active status, not to
decide the Microsoft user's role.

## Identity and existing data

Accounts are linked by validated issuer and subject, never by email. An existing
password account with the same email remains separate. Missing or duplicate email
uses a unique reserved `@identity.invalid` address for the external user record.

Repeated sign-in with the same `(iss, sub)` reuses the same user ID and preserves
enrollment/progress ownership. Switching from consumer sign-in to tenant sign-in
can change that identity key, including for a personal account invited as a guest.
In that case, the old records remain, but a separate user is created. Retaining
ownership across that switch requires an explicitly verified relink to the old
user; automatic email-based merging is not implemented.

## Azure and local configuration

Both registrations must be single tenant (`signInAudience: AzureADMyOrg`). On the
API registration define enabled `Users/Groups` app roles with exact values `Admin`
and `Learner`. Assign users through the API's Enterprise application. The UI must
have delegated `access_as_user` API permission and appropriate consent.

Supply these settings through App Service environment variables (or local .NET
user secrets). Checked-in `consumers` settings are legacy and must be overridden.
The tenant ID was not supplied in this change and is not guessed in source code.

| Application | Setting | Value |
| --- | --- | --- |
| API | `AzureAd__Instance` | `https://login.microsoftonline.com/` |
| API | `AzureAd__TenantId` | Your directory's tenant GUID |
| API | `AzureAd__ClientId` | API registration application ID |
| API | `AzureAd__Audience` | Exact API Application ID URI |
| UI Server | `AzureAd__Authority` | `https://login.microsoftonline.com/<tenant-id>/v2.0` |
| UI Server | `AzureAd__ClientId` | UI registration application ID |
| UI Server | `AzureAd__ClientSecret` | Current UI secret value |
| UI Server | `AzureAd__ApiScope` | API's full delegated scope, typically `api://<api-client-id>/access_as_user` |
| UI Server | `Api__BaseUrl` | Deployed API HTTPS base URL ending in `/` |

Verify the scope identifies the same registration as the API client ID/audience;
the repository's production API and UI defaults reference different API IDs.
Use Web callbacks `/signin-oidc` and `/signout-callback-oidc` on the UI registration.
Keep secrets outside source control.

Run [Database/003_external_logins.sql](../Database/003_external_logins.sql) before
deploying if it has not already run. Existing installations using the external
login table need no additional schema change. Publish both API and UI Server,
then sign out and sign back in.

## Verification

```powershell
dotnet run --project tests/LearningApp.AuthChecks/LearningApp.AuthChecks.csproj -p:BuildInParallel=false
```

Checks exercise the registered bearer-token validation hook after cryptographic
validation, identity provisioning/reuse, scope rejection, mapped/raw/multiple roles,
database-role independence, unassigned users, local active status, API profiles and
internal user IDs. They use SQLite, not Azure SQL or live Entra sign-in.

Microsoft references: [API app roles](https://learn.microsoft.com/en-us/entra/identity-platform/howto-add-app-roles-in-apps)
and [validating claims](https://learn.microsoft.com/en-us/entra/identity-platform/claims-validation).
