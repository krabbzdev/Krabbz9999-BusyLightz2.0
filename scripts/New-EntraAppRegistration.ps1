<#
.SYNOPSIS
  Creates (or updates) the Entra app registration that "Connect to Microsoft Teams" uses.

.DESCRIPTION
  - Single-tenant public client, redirect URI http://localhost, no secret or certificate.
  - One Microsoft Graph delegated permission, Presence.ReadWrite (plus the openid, profile and
    offline_access sign-in scopes), with admin consent for the organization.
  Safe to run again: an existing "Krabbz9999 Blynclight" registration is reused and brought up to date.
  Needs the Microsoft.Graph.Authentication, .Applications and .Identity.SignIns modules, and an admin
  sign-in (Global Administrator or Cloud Application Administrator) in the browser.

.EXAMPLE
  .\scripts\New-EntraAppRegistration.ps1 -OutFile .\build\entra-ids.json
#>
param(
    [string]$DisplayName = 'Krabbz9999 Blynclight',
    # Where to write the client and tenant IDs (JSON) for build.ps1.
    [string]$OutFile
)

$ErrorActionPreference = 'Stop'
$graphAppId = '00000003-0000-0000-c000-000000000000' # Microsoft Graph
$scopes = 'openid', 'profile', 'offline_access', 'Presence.ReadWrite'

Import-Module Microsoft.Graph.Authentication, Microsoft.Graph.Applications, Microsoft.Graph.Identity.SignIns
Write-Host 'Opening Microsoft sign-in. Use an account that can manage app registrations and grant consent.'
Connect-MgGraph -Scopes 'Application.ReadWrite.All', 'DelegatedPermissionGrant.ReadWrite.All' -ContextScope Process -NoWelcome
try {
    $context = Get-MgContext
    Write-Host "Signed in as $($context.Account), tenant $($context.TenantId)"

    $graph = Get-MgServicePrincipal -Filter "appId eq '$graphAppId'"
    $access = foreach ($name in $scopes) {
        $scope = $graph.Oauth2PermissionScopes | Where-Object Value -eq $name
        if (-not $scope) { throw "Microsoft Graph has no delegated permission named $name." }
        @{ Id = $scope.Id; Type = 'Scope' }
    }
    $settings = @{
        SignInAudience         = 'AzureADMyOrg'
        PublicClient           = @{ RedirectUris = @('http://localhost') }
        IsFallbackPublicClient = $false
        RequiredResourceAccess = @(@{ ResourceAppId = $graphAppId; ResourceAccess = @($access) })
    }

    $app = Get-MgApplication -Filter "displayName eq '$DisplayName'" | Select-Object -First 1
    if ($app) {
        Update-MgApplication -ApplicationId $app.Id @settings
        Write-Host "Updated existing app registration '$DisplayName'."
    }
    else {
        $app = New-MgApplication -DisplayName $DisplayName @settings
        Write-Host "Created app registration '$DisplayName'."
    }
    $current = Get-MgApplication -ApplicationId $app.Id
    if ($current.PasswordCredentials -or $current.KeyCredentials) {
        Write-Warning "This app registration has secrets or certificates. The app doesn't use them; consider removing them."
    }

    $sp = Get-MgServicePrincipal -Filter "appId eq '$($app.AppId)'"
    if (-not $sp) {
        # A new registration can take a few seconds to be usable for creating its service principal.
        for ($i = 0; -not $sp; $i++) {
            try { $sp = New-MgServicePrincipal -AppId $app.AppId }
            catch { if ($i -ge 5) { throw }; Start-Sleep -Seconds 5 }
        }
    }

    # Admin consent for the organization: one tenant-wide delegated grant to Microsoft Graph.
    $grantScope = $scopes -join ' '
    $grant = Get-MgOauth2PermissionGrant -Filter "clientId eq '$($sp.Id)'" | Where-Object { $_.ResourceId -eq $graph.Id -and $_.ConsentType -eq 'AllPrincipals' }
    if ($grant) { Update-MgOauth2PermissionGrant -OAuth2PermissionGrantId $grant.Id -Scope $grantScope }
    else { New-MgOauth2PermissionGrant -ClientId $sp.Id -ConsentType AllPrincipals -ResourceId $graph.Id -Scope $grantScope | Out-Null }
    Write-Host "Granted admin consent: $grantScope"

    $ids = [ordered]@{ ClientId = $app.AppId; TenantId = $context.TenantId }
    Write-Host "Application (client) ID: $($ids.ClientId)"
    Write-Host "Directory (tenant) ID:   $($ids.TenantId)"
    if ($OutFile) { $ids | ConvertTo-Json | Set-Content -Path $OutFile -Encoding ascii }
}
finally {
    Disconnect-MgGraph | Out-Null
    Write-Host 'Signed out of Microsoft Graph.'
}
