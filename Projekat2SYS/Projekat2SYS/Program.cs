namespace Projekat2SYS
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            var server = new WebServer("http://localhost:5050/", "root");

            try
            {
                await server.Start();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Došlo je do kritične greške pri pokretanju servera: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine("\nPritisnite Enter za zaustavljanje servera...");
            Console.ReadLine();

            server.Stop();
            Console.WriteLine("Server se zaustavlja...");
        }
    }
}