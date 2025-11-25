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
        // EchoServerTests.cs - ДОДАЙТЕ ЦЕЙ КЛАС В КІНЕЦЬ ФАЙЛУ (всередині namespace EchoServer.Tests)

        [TestFixture]
        public class UdpTimedSenderTests
        {
            [Test]
            public async Task UdpTimedSender_SendsDataPeriodically()
            {
                // Цей тест реально відправляє UDP пакети, щоб покрити код UdpTimedSender
                int port = 55000;
                using var receiver = new System.Net.Sockets.UdpClient(port);
                using var sender = new UdpTimedSender("127.0.0.1", port);

                // Act
                sender.StartSending(10); // Відправляти кожні 10 мс

                // Чекаємо трохи, щоб таймер спрацював кілька разів
                await Task.Delay(100);

                sender.StopSending();

                // Assert
                // Перевіряємо, що хоч щось прийшло (значить код таймера виконався)
                Assert.That(receiver.Available, Is.GreaterThan(0));

                // Перевіряємо повторний старт (покриття перевірки на _timer != null)
                Assert.Throws<InvalidOperationException>(() => sender.StartSending(10));
            }

            [Test]
            public void UdpTimedSender_Stop_SafeToCallMultipleTimes()
            {
                using var sender = new UdpTimedSender("127.0.0.1", 55001);
                sender.StartSending(100);
                sender.StopSending();
                Assert.DoesNotThrow(() => sender.StopSending());
            }
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



        [Test]
        public async Task StartAsync_AcceptLoopTerminatesOnCancellation()
        {
            // Arrange
            var mockListener = Substitute.For<ITcpListener>();

            // Створюємо TaskCompletionSource, який буде блокувати AcceptTcpClientAsync, 
            // поки ми не викличемо Stop. Це усуває помилку CS0121.
            var blockAccept = new TaskCompletionSource<TcpClient>();

            
            mockListener.AcceptTcpClientAsync().ReturnsForAnyArgs(
                blockAccept.Task,                                        
                Task.FromException<TcpClient>(new ObjectDisposedException("Listener stopped")) // 2. Кидаємо виняток після Stop()
            );

            var server = new EchoServer(5000, mockListener);

            // Act
            var serverTask = server.StartAsync(); // Запускаємо сервер (викликається AcceptTcpClientAsync 1 раз)

            // Даємо циклу час на запуск і блокування
            await Task.Delay(100);

            // Перевіряємо, що сервер працює і заблокований
            Assert.That(serverTask.IsCompleted, Is.False, "Сервер має бути заблокований на Accept.");
            server.Stop();

            blockAccept.SetResult(new TcpClient());
            await serverTask;
            mockListener.Received().Start();
            mockListener.Received().Stop();
            await mockListener.Received(2).AcceptTcpClientAsync();
            Assert.That(serverTask.IsCompleted, Is.True, "Серверна таска має завершитися після Stop.");
        }

        [Test]
        public async Task HandleClientAsync_DisconnectsOnException()
        {
            // Arrange
            // Створення Mock NetworkStream, який видає виняток при читанні
            var mockStream = Substitute.For<NetworkStream>(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), true);
            mockStream.CanRead.Returns(true);
            mockStream.ReadAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<int>(new IOException("Simulated network failure")));

            var mockClient = Substitute.For<TcpClient>();
            mockClient.GetStream().Returns(mockStream);

            var server = new EchoServer(5000);

            // Act & Assert
            // Викликаємо HandleClientAsync і перевіряємо, що він завершується і викликає Close на клієнті
            await server.HandleClientAsync(mockClient, CancellationToken.None);

            mockClient.Received().Close();
            // Перевіряємо, що stream.ReadAsync було викликано (виняток виник при першому читанні)
            await mockStream.Received(1).ReadAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task HandleClientAsync_TerminatesOnCancellation()
        {
            // Arrange
            var mockStream = Substitute.For<NetworkStream>(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), true);
            mockStream.CanRead.Returns(true);
            // Імітація блокування читання
            var cts = new CancellationTokenSource();
            mockStream.ReadAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(x => Task.Delay(Timeout.Infinite, cts.Token).ContinueWith(_ => 0)); // Чекаємо на скасування

            var mockClient = Substitute.For<TcpClient>();
            mockClient.GetStream().Returns(mockStream);

            var server = new EchoServer(5000);

            // Act
            var handleTask = server.HandleClientAsync(mockClient, cts.Token);

            // Скасовуємо токен, щоб розблокувати ReadAsync
            cts.Cancel();

            // Assert
            await handleTask; // Дочекаємося завершення таски

            mockClient.Received().Close();
            Assert.That(handleTask.IsCompleted, Is.True);
        }
    }
}
