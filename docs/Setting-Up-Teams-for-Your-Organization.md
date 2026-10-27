# Setting Up Teams for Your Organization

*For IT admins. Once per organization, about 3 minutes.*

To read and set Teams status, the app signs users in with a **Microsoft Entra app registration** in your organization's Microsoft 365. An admin creates it once, and everyone in the organization uses it.

- **Permissions:** one delegated Microsoft Graph permission, `Presence.ReadWrite`, plus the basic sign-in scopes (`openid`, `profile`, `offline_access`). Each user can only read and set their own presence.
- **No client secret or certificate.** The app is a public client that signs in through the user's browser.
- **Check first:** if an app registration named **Krabbz9999 Blynclight** already exists in your tenant, use it. Don't create a second one. The script below finds and reuses an existing one automatically.

## Option A: Entra admin center

1. Go to **[Entra admin center > App registrations](https://entra.microsoft.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade)**. Search for *Krabbz9999 Blynclight*; if it exists, skip to step 6.
2. **New registration**:
   - **Name:** `Krabbz9999 Blynclight`
   - **Supported account types:** *Accounts in this organizational directory only* (single tenant)
   - **Redirect URI:** platform **Public client/native (mobile & desktop)**, value `http://localhost`
3. **API permissions > Add a permission > Microsoft Graph > Delegated permissions > `Presence.ReadWrite`**. You can remove the default `User.Read`.
4. Click **Grant admin consent for (your organization)**.
5. Under **Authentication**, leave **Allow public client flows** set to *No*. Don't create any secrets or certificates.
6. On the app's **Overview** page, copy the **Application (client) ID** and **Directory (tenant) ID**.

## Option B: PowerShell script

[`scripts/New-EntraAppRegistration.ps1`](https://github.com/showtechclove1/Krabbz9999-BusyLightz2.0/blob/main/scripts/New-EntraAppRegistration.ps1) does all of the above, or reuses and updates an existing registration, then prints the two IDs.

Needs the Microsoft Graph PowerShell modules (`Microsoft.Graph.Authentication`, `.Applications`, `.Identity.SignIns`) and an account that can create app registrations and grant consent, such as Global Administrator or Cloud Application Administrator. From a folder containing the script:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\New-EntraAppRegistration.ps1 -OutFile .\entra-ids.json
```

Sign in with the admin account when the Microsoft window opens. It may be behind other windows. Signing in with a regular account fails with *Insufficient privileges* and changes nothing.

## Give the IDs to the app

Pick one:

- **Each user:** tray menu > **Setup checklist...** > *Your organization's Teams app* > **Set up...**, paste both IDs, then click **Save**. The app checks the tenant ID exists. The directory ID is pre-filled when the PC is joined to Entra.
- **Deploy to everyone:** pass them to the installer:
  ```
  msiexec /i Krabbz9999-BusyLightz2.0.msi /qn TEAMSCLIENTID=<client ID> TEAMSTENANTID=<tenant ID>
  ```

Then each user clicks **Connect** and signs in with their own work account.

## Helping users ask you

In the **Set up...** dialog, **Copy admin instructions** copies a short version of this page that users can paste into an email or ticket for IT.

---
[Guide home](README.md) · [Installing and Updating](Installing-and-Updating.md) · [Using the Light](Using-the-Light.md) · [Teams Status](Teams-Status.md) · [Call Detection](Call-Detection.md) · [Setup Checklist](Setup-Checklist.md) · [Setting Up Teams for Your Organization](Setting-Up-Teams-for-Your-Organization.md) · [Troubleshooting and FAQ](Troubleshooting-and-FAQ.md) · [For Developers](For-Developers.md)
