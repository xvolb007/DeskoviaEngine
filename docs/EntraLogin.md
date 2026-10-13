# Microsoft Entra ID sign-in

Deskovia now uses genuine single-tenant OpenID Connect authorization code flow with PKCE. The backend redeems the code and validates signature, issuer, audience, expiry, state, correlation, nonce and the configured tenant before creating a normal Deskovia session. The email-only demo endpoint and five-second timer have been removed. No migration is needed: the existing user model already stores nullable MicrosoftTenantId and MicrosoftObjectId.

## Configuration

The non-secret registration values are in WebApi/appsettings.json:

- Tenant ID: 0fc379c2-9aac-4a1c-9fbd-f4dc20999e9c.
- Application/client ID: 5f93d70d-3656-4603-9ad7-144d6bd1e74c.
- Enabled: true.
- Web redirect URI: https://localhost:7095/signin-oidc.
- Frontend origin: https://localhost:7178.

In the Entra registration, Authentication must contain this exact redirect URI under **Web**, not SPA. Keep the app single tenant. Authorization code flow does not require implicit ID/access token grants. Only openid, profile and email are requested; Graph permissions and an application role setup are unnecessary for sign-in. University policy may require administrator consent or assigned users.

Local demo configuration is kept in WebApi/appsettings.json at the user's request, including Entra:ClientSecret. Use the secret Value, never Secret ID. The matching User Secrets override was removed so changes in this file take effect. TenantId, ClientId, SQL Server connection, frontend origin, demo directory and billing settings are also in this file. Keep the credential-bearing local file out of commits; use protected configuration for deployment. Client secrets must never be placed in frontend configuration.

Rebuild and restart both applications. Open https://localhost:7178/login and click **Sign in with Microsoft**. The browser navigates to Microsoft's authorization endpoint, then returns through the backend callback to Deskovia after successful sign-in. This is a real redirect-based login; closing/cancelling Microsoft cannot create an account or authenticate a user. If the secret is missing or Entra is disabled, local password login remains available and Microsoft cannot issue a Deskovia session.

## Account identity and roles

Accounts are matched by verified tenant ID + object ID. Email is contact data, not authorization or identity. New identities create an ABC membership atomically with the user, session and audit: first membership is Owner, all later memberships are Employee. Existing permissions always come from Deskovia's database, not Microsoft's role/name/email claims. Disabled memberships/users cannot sign in, and session revocation and fixed eight-hour expiry apply to both login methods. Deskovia sign-out revokes its session; it does not sign out of all Microsoft applications.

Account linking and the Sign-in settings entry are removed. Microsoft sign-in matches the verified tenant ID + object ID pair or creates a new account. It never attaches a Microsoft identity to an existing local/demo account by email. If the email already belongs to another account, sign-in returns email_exists and asks the user to use that account's existing sign-in method. Identities previously stored in the database remain valid; users, roles, credentials and reservations are not rewritten.

The authorization-code callback uses query response mode and the existing SameSite=Lax cookie policy. Callback responses disable caching and referrer forwarding.

## Reproducible acceptance

1. Restart the API in Development and the frontend. Verify /api/auth/options advertises microsoftEnabled=true and microsoftConfigured=true.
2. On a fresh demonstration database, sign in through Microsoft. The first member is Owner. Sign out and repeat with the same Microsoft identity; no duplicate member appears. A second university identity becomes Employee.
3. Cancel Microsoft sign-in, use an invalid/expired secret, or try another tenant: no Deskovia session is created. The frontend shows a retryable error. Check tenant consent/assignment policy if Microsoft refuses the account.
4. Sign in with an existing Microsoft identity and verify its role and history remain unchanged. If a new Microsoft identity uses a local account's email, sign-in must reject the collision without attaching the identity or modifying that account.
5. Try a forged callback/state and a challenge without antiforgery token. Neither creates a session. Token issuer/audience/signature/nonce checks are handled by ASP.NET Core's OIDC middleware.
6. Disable an Employee, then repeat Microsoft sign-in and use its old cookie. Both are denied. Check that Employee cannot call administrative/Owner endpoints.
7. Verify /api/auth/demo-login is absent even if an old DemoEmailLoginEnabled setting survives in local configuration.

The file-based runner checks identity creation, roles, mutable/missing email, email collisions, expired/revoked sessions, disabled members, concurrency and archived ABC without a database. Verify-Host.ps1 checks HTTP security without touching the database. Verify-EntraHost.ps1 additionally checks the real Microsoft challenge, state/cancellation failures and absence of premature sessions; it requires a configured secret and network access to public Microsoft metadata. Run this script before exhausting credential rate limits. A real successful Microsoft sign-in and SQL persistence require your local running database and interactive university authentication; do not treat host-only checks as successful tenant authentication.

Microsoft Graph daily employee checks and SMTP remain separate integrations. The existing directory simulation is explicitly labelled. Application payment functionality is removed; no subscription or payment is required.

References: [ASP.NET Core OIDC configuration](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-oidc-web-authentication?view=aspnetcore-10.0), [Microsoft identity claims](https://learn.microsoft.com/en-us/entra/identity-platform/id-token-claims-reference).
