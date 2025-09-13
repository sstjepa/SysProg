namespace Projekat1SYS
{
    class Program
    {
        public static void Main(string[] args)
        {
            var server = new WebServer("http://localhost:5050/", "root");

            Thread serverThread = new Thread(server.Start);

            serverThread.Start();

            Console.WriteLine("\nPritisnite Enter za zaustavljanje servera...");
            Console.ReadLine();

            server.Stop();
            Console.WriteLine("Server se zaustavlja...");
        }
    }
}