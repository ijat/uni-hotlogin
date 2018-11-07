using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace httpClient_2
{
    class Program
    {
        private const string UserAgent = "Mozilla/5.0 (Linux; Android 7.0; SM-G930V Build/NRD90M) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/59.0.3071.125 Mobile Safari/537.36";
        private static CookieContainer CookieJar = new CookieContainer();
        private static HttpClient Client;
        private static HttpClientHandler Handler = new HttpClientHandler();
        
        public static SecureString getPasswordFromConsole(String displayMessage) {
            SecureString pass = new SecureString();
            Console.Write(displayMessage);
            ConsoleKeyInfo key;

            do {
                key = Console.ReadKey(true);

                // Backspace Should Not Work
                if (!char.IsControl(key.KeyChar)) {
                    pass.AppendChar(key.KeyChar);
                    Console.Write("*");
                } else {
                    if (key.Key == ConsoleKey.Backspace && pass.Length > 0) {
                        pass.RemoveAt(pass.Length - 1);
                        Console.Write("\b \b");
                    }
                }
            }
            // Stops Receving Keys Once Enter is Pressed
            while (key.Key != ConsoleKey.Enter);
            return pass;
        }
        
        static String SecureStringToString(SecureString value) {
            IntPtr valuePtr = IntPtr.Zero;
            try {
                valuePtr = Marshal.SecureStringToGlobalAllocUnicode(value);
                return Marshal.PtrToStringUni(valuePtr);
            } finally {
                Marshal.ZeroFreeGlobalAllocUnicode(valuePtr);
            }
        }
        
        static void Main(string[] args)
        {
            SecureString password, matrixnum;
            
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Debug, theme: AnsiConsoleTheme.Code)
                .WriteTo.RollingFile("log-{Date}.txt")
                .CreateLogger();
            
            Log.Information("=== Start ===");

            Console.Out.Flush();
            matrixnum = getPasswordFromConsole("Matrix Number: ");
            Console.WriteLine();
            password = getPasswordFromConsole("Password: ");
            Console.WriteLine();
            
            // SET COOKIE
            try
            {
                Handler.CookieContainer = CookieJar;
                Client = new HttpClient(Handler);
            }
            catch
            {
                Log.Error("Failed to set cookies");
            }

            // USER AGENT
            try
            {
                Log.Information("Setting up User-Agent...");
                Client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
                Log.Information($"User-Agent set as: {UserAgent}");
            }
            catch
            {
                Log.Error("Failed to set User-Agent");
            }

            string[] websites = {
                "https://authenticate.upm.edu.my/",
                "https://authenticate.upm.edu.my/a70.htm",
                "https://authenticate.upm.edu.my/b82.css",
                "https://authenticate.upm.edu.my:801",
                "https://authenticate.upm.edu.my:801/eportal/"
            };
            
            Dictionary<string, string> CookieDict = new Dictionary<string, string>();

            while (true)
            {
                if (!TestPing())
                {
                    Log.Information("Internet not available. Trying to automatically login...");
                    foreach (string website in websites)
                    {
                        // CHECK FORWARDER
                        try
                        {
                            Uri uri = new Uri(website);
                            Log.Information($"Opening {uri.AbsoluteUri}...");
                            HttpResponseMessage response = Client.GetAsync(uri).Result;
                            string googleString = JsonConvert.SerializeObject(response);
                            Log.Debug(googleString);
                            IEnumerable<Cookie> responseCookies = CookieJar.GetCookies(uri).Cast<Cookie>();
                            foreach (Cookie cookie in responseCookies)
                            {
                                CookieDict[cookie.Name] = cookie.Value;
                                Log.Verbose($"Cookie {cookie.Name}: {cookie.Value}");
                            }

                            Log.Verbose($"Request URI: {response.RequestMessage.RequestUri.AbsoluteUri}");
                            Log.Verbose($"Request Query: {response.RequestMessage.RequestUri.Query}");
                            using (HttpContent content = response.Content)
                            {
                                Log.Verbose("Page source:");
                                string pageSource = content.ReadAsStringAsync().Result;
                                Log.Verbose(pageSource);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex.ToString());
                        }
                    }
                    
                    // Authenticate
                    try
                    {
                        Log.Information("Trying to login...");
                        Uri authUrl = new Uri("https://authenticate.upm.edu.my:801/eportal/?c=ACSetting&a=Login&wlanuserip=null&wlanacip=null&wlanacname=BRAS-02&port=&iTermType=1&mac=000000000000&ip=172.18.76.70&redirect=null&session=null");
                        
                        Log.Information($"Login into {authUrl}...");
                        
                        var localCookieJar = new CookieContainer();
                        var localHandler = new HttpClientHandler() {CookieContainer = localCookieJar};
                        var localClient = new HttpClient(localHandler) {BaseAddress = authUrl};
                        
                        // Set-up header
                        localClient.DefaultRequestHeaders.Add("Referer","https://authenticate.upm.edu.my/a70.htm?wlanacname=BRAS-02");
                        localClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
                        
                        // Set up cookie
                        localCookieJar.Add(authUrl, new Cookie("program", "UPM180207"));
                        localCookieJar.Add(authUrl, new Cookie("ssid", "null"));
                        localCookieJar.Add(authUrl, new Cookie("vlan", "0"));
                        localCookieJar.Add(authUrl, new Cookie("ip", "172.18.76.70"));
                        if (CookieDict.ContainsKey("PHPSESSID"))
                            localCookieJar.Add(authUrl, new Cookie("PHPSESSID", CookieDict["PHPSESSID"]));
                        
                        // set up form data
                        var dict = new Dictionary<string, string>();
                        dict.Add("DDDDD", SecureStringToString(matrixnum));
                        dict.Add("upass", SecureStringToString(password));
                        dict.Add("R1", "0");
                        dict.Add("R2", "0");
                        dict.Add("R6", "0");
                        dict.Add("para", "00");
                        dict.Add("0MKKey", "123456");
                        dict.Add("buttonClicked", "");
                        dict.Add("redirect_url", "");
                        dict.Add("err_flag", "");
                        dict.Add("username", "");
                        dict.Add("password", "");
                        dict.Add("user", "");
                        
                        var req = new HttpRequestMessage(HttpMethod.Post, authUrl)
                        {
                            Content = new FormUrlEncodedContent(dict),
                        };
                        var res = localClient.SendAsync(req).Result;
                        Log.Information(JsonConvert.SerializeObject(res));
                    }
                    catch(Exception ex)
                    {
                        Log.Error(ex.ToString());
                    }
                }
                else
                {
                    Log.Information("Ping success. Skipping login.");
                }

                Thread.Sleep(20000);
            }       
        }

        private static bool TestPing()
        {
            string targetHost = "bing.com";
            string data = "a quick brown fox jumped over the lazy dog";

            Ping pingSender = new Ping();
            PingOptions options = new PingOptions
            {
                DontFragment = true
            };
    
            byte[] buffer = Encoding.ASCII.GetBytes(data);
            int timeout = 10240;

            Log.Debug($"Pinging {targetHost}");
            PingReply reply = pingSender.Send(targetHost, timeout, buffer, options);
            if (reply.Status == IPStatus.Success)
            {
                Log.Debug($"Address: {reply.Address}");
                Log.Debug($"RoundTrip time: {reply.RoundtripTime}");
                return true;
            }
            else
            {
                Log.Debug(reply.Status.ToString());
                return false;
            }
        }
    }
}