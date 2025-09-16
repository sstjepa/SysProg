using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;

public class ReactiveWebServer : IDisposable
{
    private readonly HttpListener _listener = new HttpListener();
    private IDisposable _serverSubscription;
    private static readonly HttpClient _httpClient = new HttpClient();

    public void Start(string url)
    {
        _listener.Prefixes.Add(url);
        _listener.Start();
        Console.WriteLine($"[INFO] Web server pokrenut na adresi: {url}");

        IObservable<HttpListenerContext> requestStream = Observable
            .FromAsync(() => _listener.GetContextAsync())
            .Repeat()
            .Retry();

        _serverSubscription = requestStream
            .SubscribeOn(Scheduler.ThreadPool)
            .ObserveOn(Scheduler.ThreadPool)
            .Do(ctx => Console.WriteLine($"[LOG] Primljen zahtev: {ctx.Request.HttpMethod} {ctx.Request.Url}"))
            .Subscribe(
                async context => await ProcessRequest(context),
                ex => Console.WriteLine($"[FATAL_ERROR] Greška u osluškivanju servera: {ex.Message}")
            );
    }

    private async Task ProcessRequest(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;

            if (request.Url.AbsolutePath.ToLower() != "/nobel")
            {
                await SendResponse(response, "Putanja nije pronađena. Pokušajte sa /nobel?fromYear=YYYY&toYear=YYYY", HttpStatusCode.NotFound);
                Console.WriteLine($"[WARN] Zahtev ka nepostojećoj putanji: {request.Url.AbsolutePath}");
                return;
            }

            var fromYearStr = request.QueryString["fromYear"];
            var toYearStr = request.QueryString["toYear"];

            if (string.IsNullOrEmpty(fromYearStr) || string.IsNullOrEmpty(toYearStr))
            {
                await SendResponse(response, "Molimo Vas unesite 'fromYear' i 'toYear' parametre.", HttpStatusCode.BadRequest);
                Console.WriteLine("[ERROR] Nedostaju query parametri.");
                return;
            }

            string apiUrl = $"https://api.nobelprize.org/2.1/nobelPrizes?nobelPrizeYear={fromYearStr}&yearTo={toYearStr}";
            Console.WriteLine($"[INFO] Pozivam Nobel API: {apiUrl}");

            var apiResponse = await _httpClient.GetAsync(apiUrl);
            apiResponse.EnsureSuccessStatusCode();

            string jsonResponse = await apiResponse.Content.ReadAsStringAsync();

            // ========== PROMENA 1: Deserijalizacija u korenski objekat ==========
            var apiRootObject = JsonSerializer.Deserialize<NobelApiResponse>(jsonResponse);
            var nobelPrizes = apiRootObject?.NobelPrizes;

            // ========== PROMENA 2: Ažurirana provera da li ima podataka ==========
            if (nobelPrizes == null || nobelPrizes.Count == 0)
            {
                await SendResponse(response, "Nema podataka za traženi opseg godina.", HttpStatusCode.OK);
                Console.WriteLine("[INFO] Nema rezultata za dati opseg.");
                return;
            }

            double averagePrize = nobelPrizes.Average(p => p.PrizeAmountAdjusted);

            var resultBuilder = new StringBuilder();
            resultBuilder.AppendLine("<html><body><h1>Nobelove Nagrade</h1>");
            resultBuilder.AppendLine($"<h2>Prosecna korigovana novcana nagrada: ${averagePrize:N2}</h2>");
            resultBuilder.AppendLine("<hr/><ul>");

            foreach (var prize in nobelPrizes)
            {
                if (prize.Laureates != null)
                {
                    foreach (var laureate in prize.Laureates)
                    {
                        resultBuilder.AppendLine(
                            $"<li><b>Dobitnik:</b> {laureate.KnownName?.En ?? "Nepoznato"}, " +
                            $"<b>Kategorija:</b> {prize.Category.En}, " +
                            $"<b>Godina:</b> {prize.AwardYear}</li>");
                    }
                }
            }
            resultBuilder.AppendLine("</ul></body></html>");

            await SendResponse(response, resultBuilder.ToString(), HttpStatusCode.OK, "text/html");
            Console.WriteLine($"[SUCCESS] Zahtev uspešno obrađen. Vraćeno {nobelPrizes.Count} nagrada.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Greška pri obradi zahteva: {ex.Message}");
            if (!context.Response.OutputStream.CanWrite) return;
            await SendResponse(context.Response, $"Došlo je do greške na serveru: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    private async Task SendResponse(HttpListenerResponse response, string content, HttpStatusCode statusCode, string contentType = "text/plain")
    {
        response.StatusCode = (int)statusCode;
        response.ContentType = contentType;
        byte[] buffer = Encoding.UTF8.GetBytes(content);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        response.OutputStream.Close();
    }

    public void Dispose()
    {
        Console.WriteLine("\n[INFO] Zaustavljanje servera...");
        _serverSubscription?.Dispose();
        _listener.Stop();
        _listener.Close();
    }
}