using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace EchoServer
{
    public class TcpListenerAdapter : ITcpListener
    {
        private readonly TcpListener _listener;

        public TcpListenerAdapter(IPAddress address, int port)
        {
            _listener = new TcpListener(address, port);
        }

        public void Start() => _listener.Start();

        public void Stop() => _listener.Stop();

        public Task<TcpClient> AcceptTcpClientAsync() =>
            _listener.AcceptTcpClientAsync();
    }
}
