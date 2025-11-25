using NUnit.Framework;
using NSubstitute;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace EchoServer.Tests
{
    public class EchoServerTests
    {
        [Test]
        public async Task StartAsync_ShouldCallAcceptTcpClientAsync()
        {
            // Arrange
            var mockListener = Substitute.For<ITcpListener>();

            var fakeClient = new TcpClient();
            mockListener.AcceptTcpClientAsync().Returns(Task.FromResult(fakeClient));

            var server = new EchoServer(5000, mockListener);

            // Act
            var serverTask = Task.Run(() => server.StartAsync());

            await Task.Delay(200); // give loop time to run

            // Assert
            await mockListener.Received().AcceptTcpClientAsync();

            // Cleanup
            server.Stop();
        }

        [Test]
        public void Stop_ShouldCallListenerStop()
        {
           
            var mockListener = Substitute.For<ITcpListener>();
            var server = new EchoServer(5000, mockListener);

         
            server.Stop();

            mockListener.Received().Stop();
        }

        [Test]
        public async Task HandleClientAsync_ShouldEchoData()
        {
            var server = new EchoServer(5000);
            using var clientA = new TcpClient();
            using var clientB = new TcpClient();
            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            clientA.Connect(listener.LocalEndpoint as System.Net.IPEndPoint);
            var accepted = listener.AcceptTcpClient();

            var streamClient = clientA.GetStream();
            var streamServer = accepted.GetStream();

            var handleTask = server.HandleClientAsync(accepted, CancellationToken.None);

            byte[] message = { 10, 20, 30, 40 };
            await streamClient.WriteAsync(message, 0, message.Length);
            byte[] buffer = new byte[4];
            await streamClient.ReadAsync(buffer, 0, buffer.Length);

            CollectionAssert.AreEqual(message, buffer);

            accepted.Close();
            clientA.Close();
            listener.Stop();
        }
    }
}
