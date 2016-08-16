using System;

namespace Multiplex
{
    public class Program
    {

        public static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                ShowUsage();
                Environment.Exit(1);
            }

            if (args[0] == "server")
            {
                Test.Multiplex.Server.TestServer.Execute();
            }
            else if (args[0] == "client")
            {
                Test.Multiplex.Client.TestClient.Execute();
            }
            else
            {
                ShowUsage();
            }
        }

        private static void ShowUsage()
        {
            Console.WriteLine("You must specify either 'client' or 'server'");
        }
    }
}
