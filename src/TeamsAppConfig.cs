using System;
using System.IO;
using System.Net;
using Microsoft.Win32;

namespace Krabbz.Blynclight
{
    // Which Entra app registration "Connect to Microsoft Teams" signs in with. IDs saved for the user (entered in
    // setup, or deployed by the installer's TEAMSCLIENTID / TEAMSTENANTID) win over the registration built into the app.
    public static class TeamsAppConfig
    {
        const string SettingsKey = @"Software\Krabbz9999\Blynclight App Control";
        const string JoinInfoKey = @"SYSTEM\CurrentControlSet\Control\CloudDomainJoin\JoinInfo";

        public static string ClientId { get { return Read("TeamsClientId") ?? Program.GraphClientId; } }
        public static string TenantId { get { return Read("TeamsTenantId") ?? Program.GraphTenantId; } }
        public static bool Configured { get { return ClientId != null && TenantId != null; } }
        public static bool UsingOwnApp { get { return Read("TeamsClientId") != null && Read("TeamsTenantId") != null; } }

        public static void Save(string clientId, string tenantId)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(SettingsKey))
            {
                key.SetValue("TeamsClientId", clientId.Trim());
                key.SetValue("TeamsTenantId", tenantId.Trim());
            }
        }

        // Goes back to the registration built into the app.
        public static void UseBuiltIn()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(SettingsKey, true))
            {
                if (key == null) return;
                key.DeleteValue("TeamsClientId", false);
                key.DeleteValue("TeamsTenantId", false);
            }
        }

        // The Microsoft 365 organization this PC is joined to (Entra join), or null if it isn't joined.
        public static string PcTenantId
        {
            get
            {
                using (var joins = Registry.LocalMachine.OpenSubKey(JoinInfoKey))
                {
                    if (joins == null) return null;
                    foreach (string name in joins.GetSubKeyNames())
                        using (var join = joins.OpenSubKey(name))
                        {
                            string tenant = join == null ? null : join.GetValue("TenantId") as string;
                            if (IsGuid(tenant)) return tenant;
                        }
                }
                return null;
            }
        }

        // True when the app's built-in registration belongs to this PC's organization, so nothing needs setting up.
        public static bool BuiltInCoversThisPc
        {
            get
            {
                string pc = PcTenantId;
                return Program.GraphTenantId != null && pc != null && string.Equals(pc, Program.GraphTenantId, StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool IsGuid(string value)
        {
            Guid parsed;
            return Guid.TryParse((value ?? "").Trim(), out parsed);
        }

        // Asks Microsoft's sign-in service whether the tenant ID exists, without signing in. (Whether an app is
        // registered there can only be confirmed by signing in.) Returns null when Microsoft couldn't be reached.
        public static bool? TenantExists(string tenantId)
        {
            string url = "https://login.microsoftonline.com/" + Uri.EscapeDataString(tenantId.Trim()) + "/v2.0/.well-known/openid-configuration";
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            try
            {
                using (var web = new WebClient()) web.DownloadString(url);
                return true;
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                if (response != null && (int)response.StatusCode >= 400 && (int)response.StatusCode < 500) return false;
                return null;
            }
        }

        static string Read(string name)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(SettingsKey))
            {
                string value = key == null ? null : key.GetValue(name) as string;
                return IsGuid(value) ? value.Trim() : null;
            }
        }
    }
}
