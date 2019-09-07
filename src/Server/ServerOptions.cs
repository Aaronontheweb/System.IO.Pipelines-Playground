using CommandLine;

namespace Server
{
    public sealed class ServerOptions
    {
        [Option('p', "port", Required = false, HelpText = "Port number to host server on. Defaults to 9000.", Default = 9000)]
        public int Port { get; set; }

        [Option('h', "hostname", Required = false, HelpText = "Host name to host server on. Defaults to 'localhost'.", Default = "localhost")]
        public string Host { get; set; }

        [Option(
            Default = false,
            HelpText = "Prints out extensive debug logs during operation.")]
        public bool Verbose { get; set; }
    }
}
