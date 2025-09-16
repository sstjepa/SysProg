class Program
{
    static void Main(string[] args)
    {
        string url = "http://localhost:8080/";

        using (var server = new ReactiveWebServer())
        {
            server.Start(url);

            Console.WriteLine("Pritisnite ENTER za zaustavljanje servera.");
            Console.ReadLine();
        }
    }
}