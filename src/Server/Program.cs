using CommandLine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    class Program
    {
        private static string _originalTitle;
        private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        private static void SetTitle(string newTitle)
        {
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
            var result = Parser.Default.ParseArguments<ServerOptions>(args).MapResult(r =>
            {
                s = r;
                return 0;
            }, _ => 1);

            if (result != 0)
                return result;

            var cts = new CancellationTokenSource();
            SetTitle($"System.IO.Pipelines.Server[{s.Host}:{s.Port}]");
            try
            {
                return await RunServer(s, cts.Token);
            }
            finally
            {
                ResetTitle();
            }

        }

        private static async Task<int> RunServer(ServerOptions options, CancellationToken cancel)
        {
            ILoggerProvider cp = new ConsoleLoggerProvider((s, level) => level >= (options.Verbose ? LogLevel.Debug : LogLevel.Information), false);

            var serverLogger = cp.CreateLogger($"Server[{options.Host}:{options.Port}]");

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { Blocking = false };
            try
            {
                serverLogger.LogDebug("Binding to {0}:{1}", options.Host, options.Port);
                socket.Bind(new DnsEndPoint(options.Host, options.Port));
                socket.Listen(20);
                serverLogger.LogInformation("Successfully bound to {0}:{1}", options.Host, options.Port);
                while (!cancel.IsCancellationRequested)
                {
                    var clientSocket = await socket.AcceptAsync();
                    serverLogger.LogInformation("Accepted connection from {0}", clientSocket.RemoteEndPoint.ToString());
                }
                serverLogger.LogInformation("Received cancellation signal - shutting down.");
                return 0;
            }
            catch(Exception ex)
            {
                serverLogger.LogError(ex, "received error during processing.");
                return -1;
            }
            finally
            {
                try
                {
                    socket.Close(5);
                    socket.Dispose();
                    serverLogger.LogInformation("Shutdown complete.");
                    cp.Dispose();
                }
                catch
                {
                    // don't care about shutdown exceptions
                }
            }
        }


        private static async Task HandleConnection(Socket conn, ILogger logger, CancellationToken cancel)
        {           
            var pipe = new Pipe();
            var writer = pipe.Writer;
            var reader = pipe.Reader;

            await Task.WhenAll(ReadFromSocket(conn, writer, logger, cancel), WriteToSocket(conn, reader, logger, cancel));
        }

        const int minimumBufferSize = 512;
        const int frameLength = 4;
        const long maxFrameSize = 128000;

        private static async Task ReadFromSocket(Socket conn, PipeWriter writer, ILogger logger, CancellationToken cancel)
        {
           
            while (!cancel.IsCancellationRequested)
            {
                var memory = writer.GetMemory(minimumBufferSize);
                try
                {
                    var bytesRead = await conn.ReceiveAsync(memory, SocketFlags.None, cancel);
                    if(bytesRead == 0)
                    {
                        break;
                    }

                    writer.Advance(bytesRead);
                }
                catch(Exception ex)
                {
                    logger.LogError(ex, "Error on socket read");
                    break;
                }

                var flushResult = await writer.FlushAsync();

                if (flushResult.IsCompleted)
                {
                    break; // no more data coming
                }
            }

            writer.Complete();
        }

        private static async Task WriteToSocket(Socket conn, PipeReader reader, ILogger logger, CancellationToken cancel)
        {
            var discardingTooLongFrame = false;
            long bytesToDiscard = 0;
            long tooLongFrameLength = 0;

            while (!cancel.IsCancellationRequested)
            {
                var result = await reader.ReadAsync();
                var buffer = result.Buffer;
                SequencePosition? position = null;

                // check to see if we're already discarding an illegally large frame
                if (discardingTooLongFrame)
                {
                    var localBytesToDiscard = Math.Min(buffer.Length, bytesToDiscard);
                    buffer = buffer.Slice(localBytesToDiscard);
                    bytesToDiscard -= localBytesToDiscard;
                    if(bytesToDiscard == 0)
                    {
                        discardingTooLongFrame = false;
                        logger.LogDebug("No longer discarding too long frame of size [{0}] bytes.", tooLongFrameLength);
                        tooLongFrameLength = 0;
                    }
                    else
                    {
                        continue; // can't do any further processing - still have more to discard
                    }
                }

                do
                {
                    var frameLength = Convert.ToInt64(buffer.Slice(4));
                    if(frameLength > maxFrameSize)
                } while (position != null);
            }
        }
    }
}
