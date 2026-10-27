using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Script.Serialization;

namespace Krabbz.Blynclight
{
    // Reads the user's Teams presence, and shows them as Busy / In a call during phone calls, through Microsoft Graph.
    // Signs in with the system browser (authorization code + PKCE, loopback redirect) as a public client, so there's
    // no client secret. Only a refresh token is kept, encrypted to the Windows user with DPAPI.
    public class TeamsPresence : IDisposable
    {
        const string Scopes = "openid profile offline_access https://graph.microsoft.com/Presence.ReadWrite";
        const string GraphUsers = "https://graph.microsoft.com/v1.0/users/";
        // Microsoft drops the Busy status on its own after this, so a crash or sleep never leaves it stuck.
        const string BusyExpiration = "PT5M";
        // Renew before the 5-minute expiration while the call continues.
        const int RenewMs = 4 * 60 * 1000;
        const int SignInTimeoutMs = 5 * 60 * 1000;

        static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        readonly string clientId;
        readonly string tenantId;
        readonly Action<string> reportProblem;
        readonly string tokenPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Krabbz9999 Blynclight", "teams-presence.dat");
        readonly object sync = new object();
        // Serializes Graph calls so a renewal can't land after the call has been cleared.
        readonly object graphGate = new object();
        // Serializes token refreshes so two threads can't race on a rotated refresh token.
        readonly object tokenGate = new object();
        readonly Timer renewTimer;
        string refreshToken;
        string userId;
        string account;
        string accessToken;
        DateTime accessTokenExpires;
        bool onCall;
        bool problemReported;

        // reportProblem is called on a background thread with a message the user should see.
        public TeamsPresence(string clientId, string tenantId, Action<string> reportProblem)
        {
            this.clientId = clientId;
            this.tenantId = tenantId;
            this.reportProblem = reportProblem;
            Load();
            renewTimer = new Timer(delegate { Renew(); }, null, Timeout.Infinite, Timeout.Infinite);
        }

        public bool SignedIn { get { lock (sync) return refreshToken != null; } }
        public string Account { get { lock (sync) return account; } }

        string Authority { get { return "https://login.microsoftonline.com/" + tenantId + "/oauth2/v2.0/"; } }

        // Opens the browser for Microsoft sign-in and blocks until it finishes. Throws with a readable message on failure.
        public void SignIn()
        {
            string verifier = Base64Url(RandomBytes(32));
            string challenge;
            using (var sha = SHA256.Create()) challenge = Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
            string state = Base64Url(RandomBytes(16));

            // Browsers may resolve localhost to either loopback address, so listen on both.
            var listeners = new List<TcpListener> { new TcpListener(IPAddress.Loopback, 0) };
            listeners[0].Start();
            int port = ((IPEndPoint)listeners[0].LocalEndpoint).Port;
            try
            {
                try
                {
                    var v6 = new TcpListener(IPAddress.IPv6Loopback, port);
                    v6.Start();
                    listeners.Add(v6);
                }
                catch (SocketException) { } // No IPv6, or port taken there; IPv4 is enough.

                string redirectUri = "http://localhost:" + port;
                Process.Start(Authority + "authorize" +
                    "?client_id=" + Uri.EscapeDataString(clientId) +
                    "&response_type=code&response_mode=query" +
                    "&redirect_uri=" + Uri.EscapeDataString(redirectUri) +
                    "&scope=" + Uri.EscapeDataString(Scopes) +
                    "&state=" + state +
                    "&code_challenge=" + challenge + "&code_challenge_method=S256" +
                    "&prompt=select_account");

                NameValueCollection reply = WaitForRedirect(listeners, SignInTimeoutMs);
                if (reply == null) throw new InvalidOperationException("Sign-in timed out.");
                if (reply["state"] != state) throw new InvalidOperationException("Sign-in reply didn't match this request.");
                if (reply["error"] != null) throw new InvalidOperationException("Sign-in failed: " + (reply["error_description"] ?? reply["error"]));

                var tokens = RequestTokens(new NameValueCollection
                {
                    { "grant_type", "authorization_code" },
                    { "code", reply["code"] },
                    { "redirect_uri", redirectUri },
                    { "code_verifier", verifier },
                });
                var claims = JwtPayload((string)tokens["id_token"]);
                lock (sync)
                {
                    userId = (string)claims["oid"];
                    object name;
                    account = claims.TryGetValue("preferred_username", out name) ? name as string : null;
                }
                StoreTokens(tokens);
                problemReported = false;
            }
            finally
            {
                foreach (var listener in listeners) listener.Stop();
            }
        }

        // Clears any Busy status this app set and forgets the stored token.
        public void SignOut()
        {
            bool wasOnCall;
            lock (sync) { wasOnCall = onCall; onCall = false; }
            renewTimer.Change(Timeout.Infinite, Timeout.Infinite);
            if (wasOnCall) TryClear();
            lock (sync) { refreshToken = null; accessToken = null; userId = null; account = null; }
            try { File.Delete(tokenPath); } catch (IOException) { }
        }

        public void CallStarted()
        {
            lock (sync)
            {
                if (refreshToken == null || onCall) return;
                onCall = true;
            }
            renewTimer.Change(0, RenewMs);
        }

        public void CallEnded()
        {
            lock (sync)
            {
                if (!onCall) return;
                onCall = false;
            }
            renewTimer.Change(Timeout.Infinite, Timeout.Infinite);
            ThreadPool.QueueUserWorkItem(delegate { TryClear(); });
        }

        void Renew()
        {
            lock (graphGate)
            {
                lock (sync) if (!onCall) return;
                try
                {
                    PostPresence("setPresence", new Dictionary<string, object>
                    {
                        { "sessionId", clientId },
                        { "availability", "Busy" },
                        { "activity", "InACall" },
                        { "expirationDuration", BusyExpiration },
                    });
                    problemReported = false;
                }
                catch (Exception ex) { Report("Couldn't set Teams to Busy: " + ex.Message); }
            }
        }

        void TryClear()
        {
            lock (graphGate)
            {
                try { PostPresence("clearPresence", new Dictionary<string, object> { { "sessionId", clientId } }); }
                catch (WebException ex)
                {
                    var response = ex.Response as HttpWebResponse;
                    if (response == null || response.StatusCode != HttpStatusCode.NotFound) // 404: already expired.
                        Report("Couldn't clear Teams Busy status (it expires on its own within 5 minutes): " + ex.Message);
                }
                catch (Exception ex) { Report("Couldn't clear Teams Busy status (it expires on its own within 5 minutes): " + ex.Message); }
            }
        }

        // Shows one message per run of failures, so a dropped network doesn't pop up a balloon every renewal.
        void Report(string message)
        {
            if (problemReported) return;
            problemReported = true;
            reportProblem(message);
        }

        void PostPresence(string action, Dictionary<string, object> body)
        {
            string token = AccessToken();
            string user;
            lock (sync) user = userId;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using (var web = new WebClient())
            {
                web.Headers[HttpRequestHeader.Authorization] = "Bearer " + token;
                web.Headers[HttpRequestHeader.ContentType] = "application/json";
                web.UploadString(GraphUsers + user + "/presence/" + action, "POST", Json.Serialize(body));
            }
        }

        string AccessToken()
        {
            lock (tokenGate)
            {
                string refresh;
                lock (sync)
                {
                    if (accessToken != null && DateTime.UtcNow < accessTokenExpires) return accessToken;
                    if (refreshToken == null) throw new InvalidOperationException("Not signed in to Microsoft.");
                    refresh = refreshToken;
                }
                try
                {
                    StoreTokens(RequestTokens(new NameValueCollection
                    {
                        { "grant_type", "refresh_token" },
                        { "refresh_token", refresh },
                    }));
                }
                catch (TokenException ex)
                {
                    // The sign-in was revoked or expired; the user has to sign in again from the tray menu.
                    if (ex.Error == "invalid_grant") SignOut();
                    throw;
                }
                lock (sync) return accessToken;
            }
        }

        // Returns the user's Teams availability (e.g. "Available", "Busy", "DoNotDisturb", "Away"),
        // or null if it couldn't be read (the problem is reported once).
        public string ReadAvailability()
        {
            try
            {
                string token = AccessToken();
                string user;
                lock (sync) user = userId;
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var web = new WebClient())
                {
                    web.Headers[HttpRequestHeader.Authorization] = "Bearer " + token;
                    var result = (Dictionary<string, object>)Json.DeserializeObject(web.DownloadString(GraphUsers + user + "/presence"));
                    problemReported = false;
                    return result["availability"] as string;
                }
            }
            catch (Exception ex)
            {
                Report("Couldn't read your Teams status: " + ex.Message);
                return null;
            }
        }

        Dictionary<string, object> RequestTokens(NameValueCollection form)
        {
            form["client_id"] = clientId;
            form["scope"] = Scopes;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            try
            {
                using (var web = new WebClient())
                    return (Dictionary<string, object>)Json.DeserializeObject(Encoding.UTF8.GetString(web.UploadValues(Authority + "token", form)));
            }
            catch (WebException ex)
            {
                if (ex.Response == null) throw;
                string body;
                using (var reader = new StreamReader(ex.Response.GetResponseStream())) body = reader.ReadToEnd();
                var error = Json.DeserializeObject(body) as Dictionary<string, object>;
                if (error == null || !error.ContainsKey("error")) throw;
                throw new TokenException((string)error["error"],
                    error.ContainsKey("error_description") ? (string)error["error_description"] : (string)error["error"]);
            }
        }

        void StoreTokens(Dictionary<string, object> tokens)
        {
            object rotated, expiresIn;
            lock (sync)
            {
                accessToken = (string)tokens["access_token"];
                int seconds = tokens.TryGetValue("expires_in", out expiresIn) ? Convert.ToInt32(expiresIn) : 3600;
                // Refresh a couple of minutes early so a request never goes out with a token about to expire.
                accessTokenExpires = DateTime.UtcNow.AddSeconds(Math.Max(60, seconds - 120));
                if (tokens.TryGetValue("refresh_token", out rotated)) refreshToken = (string)rotated;
            }
            Save();
        }

        // Saves the refresh token and user ID, encrypted so only this Windows user can read them.
        void Save()
        {
            string json;
            lock (sync)
            {
                if (refreshToken == null) return;
                json = Json.Serialize(new Dictionary<string, object>
                {
                    { "refresh_token", refreshToken }, { "oid", userId }, { "account", account },
                    { "client_id", clientId }, { "tenant_id", tenantId },
                });
            }
            Directory.CreateDirectory(Path.GetDirectoryName(tokenPath));
            File.WriteAllBytes(tokenPath, ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser));
        }

        void Load()
        {
            try
            {
                if (!File.Exists(tokenPath)) return;
                string json = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(tokenPath), null, DataProtectionScope.CurrentUser));
                var saved = (Dictionary<string, object>)Json.DeserializeObject(json);
                // A sign-in made with a different organization's app doesn't carry over; sign in again.
                object savedClient, savedTenant;
                if (saved.TryGetValue("client_id", out savedClient) && !string.Equals(savedClient as string, clientId, StringComparison.OrdinalIgnoreCase)) return;
                if (saved.TryGetValue("tenant_id", out savedTenant) && !string.Equals(savedTenant as string, tenantId, StringComparison.OrdinalIgnoreCase)) return;
                refreshToken = saved["refresh_token"] as string;
                userId = saved["oid"] as string;
                account = saved["account"] as string;
            }
            catch (Exception) { refreshToken = null; } // Unreadable (e.g. copied from another user); sign in again.
        }

        // Accepts connections on the loopback listeners until the browser arrives with the sign-in reply.
        // Returns the reply's query parameters, or null on timeout.
        internal static NameValueCollection WaitForRedirect(List<TcpListener> listeners, int timeoutMs)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                foreach (var listener in listeners)
                {
                    if (!listener.Pending()) continue;
                    using (var client = listener.AcceptTcpClient())
                    using (var stream = client.GetStream())
                    {
                        stream.ReadTimeout = 5000;
                        string requestLine;
                        try { requestLine = new StreamReader(stream, Encoding.ASCII).ReadLine() ?? ""; }
                        catch (IOException) { continue; }
                        // "GET /?code=...&state=... HTTP/1.1"
                        string[] parts = requestLine.Split(' ');
                        string target = parts.Length > 1 ? parts[1] : "";
                        int q = target.IndexOf('?');
                        var query = HttpUtility.ParseQueryString(q >= 0 ? target.Substring(q + 1) : "");
                        bool isReply = query["state"] != null;
                        Respond(stream, isReply);
                        if (isReply) return query;
                    }
                }
                Thread.Sleep(100);
            }
            return null;
        }

        static void Respond(Stream stream, bool isReply)
        {
            string html = isReply
                ? "<html><body style=\"font-family:Segoe UI,sans-serif;padding:40px\"><h2>Krabbz9999-BusyLightz2.0</h2>" +
                  "<p>Sign-in finished. You can close this tab.</p></body></html>"
                : "";
            byte[] body = Encoding.UTF8.GetBytes(html);
            byte[] head = Encoding.ASCII.GetBytes((isReply ? "HTTP/1.1 200 OK" : "HTTP/1.1 404 Not Found") +
                "\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
            try
            {
                stream.Write(head, 0, head.Length);
                stream.Write(body, 0, body.Length);
            }
            catch (IOException) { } // Browser closed the connection early.
        }

        static Dictionary<string, object> JwtPayload(string jwt)
        {
            string payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            return (Dictionary<string, object>)Json.DeserializeObject(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
        }

        static byte[] RandomBytes(int count)
        {
            var bytes = new byte[count];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return bytes;
        }

        static string Base64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        public void Dispose()
        {
            renewTimer.Dispose();
            bool wasOnCall;
            lock (sync) { wasOnCall = onCall; onCall = false; }
            if (wasOnCall) TryClear();
        }

        class TokenException : Exception
        {
            public readonly string Error;
            public TokenException(string error, string description) : base(description) { Error = error; }
        }
    }
}
