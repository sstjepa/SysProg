using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Projekat1SYS
{
    public class WebServer
    {
        private readonly HttpListener _listener = new HttpListener();
        private readonly string _rootDirectory;
        private readonly ConcurrentDictionary<string, string> _cache = new ConcurrentDictionary<string, string>();
        private static readonly object _consoleLock = new object();

        public WebServer(string prefix, string rootDirectory)
        {
            if (!HttpListener.IsSupported)
                throw new NotSupportedException("HttpListener nije podržan na ovom sistemu.");

            _listener.Prefixes.Add(prefix);
            _rootDirectory = Path.GetFullPath(rootDirectory);

            if (!Directory.Exists(_rootDirectory))
            {
                Directory.CreateDirectory(_rootDirectory);
                Log($"Kreiran root direktorijum: {_rootDirectory}");
            }
        }

        public void Start()
        {
            _listener.Start();
            Log($"Server pokrenut. Očekujem zahteve na: {_listener.Prefixes.First()}");
            Log($"Root direktorijum za pretragu fajlova: {_rootDirectory}");
            while (_listener.IsListening)
            {
                try
                {
                    var context = _listener.GetContext();

                    ThreadPool.QueueUserWorkItem(ProcessRequest, context);
                }
                catch (HttpListenerException)
                {
                    Log("Server se zaustavlja.");
                    break;
                }
                catch (Exception ex)
                {
                    Log($"Neočekivana greška u glavnoj petlji: {ex.Message}");
                }
            }
        }

        public void Stop()
        {
            _listener.Stop();
            _listener.Close();
        }

        private void ProcessRequest(object? state)
        {
            if (state is not HttpListenerContext context) return;

            try
            {
                var request = context.Request;
                string fileName = request.Url?.AbsolutePath.TrimStart('/') ?? string.Empty;

                Log($"Primljen zahtev: {request.HttpMethod} {request.Url} od {request.RemoteEndPoint}");

                if (request.HttpMethod != "GET" || string.IsNullOrWhiteSpace(fileName))
                {
                    SendResponse(context, "Neispravan zahtev. Koristiti GET metodu sa nazivom fajla.", HttpStatusCode.BadRequest);
                    return;
                }

                // 1. Provera keša
                if (_cache.TryGetValue(fileName, out string? cachedResponse))
                {
                    Log($"[CACHE HIT] Za fajl '{fileName}'. Vraćam keširani odgovor.");
                    SendResponse(context, cachedResponse, HttpStatusCode.OK);
                    return;
                }

                Log($"[CACHE MISS] Za fajl '{fileName}'. Krećem u pretragu i obradu.");


                // 2. Pretraga fajla
                var foundFiles = Directory.GetFiles(_rootDirectory, fileName, SearchOption.AllDirectories);

                if (foundFiles.Length == 0)
                {
                    Log($"[GREŠKA] Fajl '{fileName}' nije pronađen.");
                    SendResponse(context, $"Greška: Fajl '{fileName}' nije pronađen.", HttpStatusCode.NotFound);
                    return;
                }

                string filePath = foundFiles.First();
                Log($"Fajl pronađen na putanji: {filePath}");

                // 3. Čitanje fajla i brojanje palindroma
                string content = File.ReadAllText(filePath);

                var words = Regex.Split(content.ToLower(), @"\W+").Where(w => !string.IsNullOrWhiteSpace(w));

                int palindromeCount = words.Count(IsPalindrome);

                string responseMessage;
                if (palindromeCount > 0)
                {
                    responseMessage = $"Broj reči koje su palindromi u fajlu '{fileName}': {palindromeCount}";
                }
                else
                {
                    responseMessage = $"Fajl '{fileName}' ne sadrži reči koje su palindromi.";
                }

                // 4. Keširanje rezultata i slanje odgovora
                _cache[fileName] = responseMessage;
                Log($"[KEŠIRANO] Rezultat za '{fileName}'.");
                SendResponse(context, responseMessage, HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                Log($"[GREŠKA] Došlo je do greške prilikom obrade fajla: {ex.Message}");
                if (context.Response.OutputStream.CanWrite)
                {
                    SendResponse(context, "Greška na serveru prilikom obrade zahteva.", HttpStatusCode.InternalServerError);
                }
            }
        }

        private bool IsPalindrome(string word)
        {
            if (string.IsNullOrEmpty(word) || word.Length <= 1)
            {
                return false;
            }

            string cleanedWord = new string(word.Where(char.IsLetterOrDigit).ToArray()).ToLower();
            string reversedWord = new string(cleanedWord.Reverse().ToArray());

            return cleanedWord == reversedWord;
        }

        private void SendResponse(HttpListenerContext context, string content, HttpStatusCode statusCode)
        {
            var response = context.Response;
            byte[] buffer = Encoding.UTF8.GetBytes(content);

            response.ContentLength64 = buffer.Length;
            response.StatusCode = (int)statusCode;
            response.ContentType = "text/plain; charset=utf-8";

            try
            {
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                Log($"Greška prilikom slanja odgovora: {ex.Message}");
            }
            finally
            {
                response.OutputStream.Close();
            }
            Log($"Odgovor poslat klijentu {context.Request.RemoteEndPoint} sa statusom {statusCode}.");
        }

        private void Log(string message)
        {
            lock (_consoleLock)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Thread: {Thread.CurrentThread.ManagedThreadId:00}] {message}");
            }
        }
    }
}