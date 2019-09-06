using CommandLine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Server
{
    class Program
    {
        private static string _originalTitle;
        private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        private static void SetTitle(string newTitle)
        {
            Console.WriteLine(newTitle);
            if (IsWindows) // changing console title is not supported on OS X or Linux
            {
                _originalTitle = Console.Title;
                Console.Title = newTitle;
            }
        }

        private static void ResetTitle()
        {
            if (IsWindows)
                Console.Title = _originalTitle; // reset the console window title back
        }

        static async Task<int> Main(string[] args)
        {
            ServerOptions s = null;
            var r = Parser.Default.ParseArguments<ServerOptions>(args).MapResult(r =>
            {
                s = r;
                return 0;
            }, _ => 1);

            if (r != 0)
                return r;

            SetTitle($"System.IO.Pipelines.Server[{s.Host}:{s.Port}]");
            try
            {
                return await RunServer(s);
            }
            finally
            {
                ResetTitle();
            }

        }

        private static async Task<int> RunServer(ServerOptions s)
        {
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { Blocking = false };
            socket.Bind(new DnsEndPoint(s.Host, s.Port));
        }
    }
}
