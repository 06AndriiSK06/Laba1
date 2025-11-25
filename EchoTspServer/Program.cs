using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EchoServer
{
    public class EchoServer
    {
        private readonly int _port;
        private readonly ITcpListener _listener;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public EchoServer(int port, ITcpListener listener = null)
        {
            _port = port;
            _listener = listener ?? new TcpListenerAdapter(IPAddress.Any, port);
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task StartAsync()
        {
            _listener.Start();
            Console.WriteLine($"Server started on port {_port}.");

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    Console.WriteLine("Client connected.");

                    _ = Task.Run(() => HandleClientAsync(client, _cancellationTokenSource.Token));
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }

            Console.WriteLine("Server stopped listening.");
        }

        public async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            using (NetworkStream stream = client.GetStream())
            {
                try
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead;

                    while (!token.IsCancellationRequested &&
                           (bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                    {
                        await stream.WriteAsync(buffer, 0, bytesRead, token);
                        Console.WriteLine($"Echoed {bytesRead} bytes.");
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
                finally
                {
                    client.Close();
                    Console.WriteLine("Client disconnected.");
                }
            }
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _listener.Stop();
            _cancellationTokenSource.Dispose();
            Console.WriteLine("Server stopped.");
        }

        public static async Task Main(string[] args)
        {
            var server = new EchoServer(5000);

            // Start server
            _ = Task.Run(() => server.StartAsync());

            // Simple loop to keep application running
            Console.WriteLine("Press Q to quit.");
            while (Console.ReadKey(true).Key != ConsoleKey.Q) { }

            server.Stop();
        }
    }

    public class UdpTimedSender : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly UdpClient _udpClient;
        private Timer _timer;

        public UdpTimedSender(string host, int port)
        {
            _host = host;
            _port = port;
            _udpClient = new UdpClient();
        }

        public void StartSending(int intervalMilliseconds)
        {
            if (_timer != null)
                throw new InvalidOperationException("Sender already started.");

            _timer = new Timer(SendMessageCallback, null, 0, intervalMilliseconds);
        }

        ushort i = 0;

        private void SendMessageCallback(object state)
        {
            try
            {
                Random rnd = new Random();
                byte[] samples = new byte[1024];
                rnd.NextBytes(samples);
                i++;

                byte[] msg = (new byte[] { 0x04, 0x84 })
                                .Concat(BitConverter.GetBytes(i))
                                .Concat(samples)
                                .ToArray();

                var endpoint = new IPEndPoint(IPAddress.Parse(_host), _port);

                _udpClient.Send(msg, msg.Length, endpoint);
                Console.WriteLine($"Message sent to {_host}:{_port}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Send error: {ex.Message}");
            }
        }

        public void StopSending()
        {
            _timer?.Dispose();
            _timer = null;
        }

        public void Dispose()
        {
            StopSending();
            _udpClient.Dispose();
        }
    }
}
