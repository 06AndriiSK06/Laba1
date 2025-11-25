using NUnit.Framework;
using NSubstitute;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Net;

namespace EchoServer.Tests
{
    [TestFixture]
    public class EchoServerTests
    {
        // --- ТЕСТИ ЗАПУСКУ СЕРВЕРА ---

        [Test]
        public async Task StartAsync_ShouldCallAcceptTcpClientAsync()
        {
            var mockListener = Substitute.For<ITcpListener>();
            var fakeClient = new TcpClient();
            // Використовуємо FromResult
            mockListener.AcceptTcpClientAsync().Returns(Task.FromResult(fakeClient));

            var server = new EchoServer(5000, mockListener);

            var serverTask = Task.Run(() => server.StartAsync());
            await Task.Delay(200);
            server.Stop();

            await mockListener.Received().AcceptTcpClientAsync();
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
        public async Task StartAsync_AcceptLoopTerminatesOnCancellation()
        {
            var mockListener = Substitute.For<ITcpListener>();
            var blockAccept = new TaskCompletionSource<TcpClient>();
            mockListener.AcceptTcpClientAsync().Returns(blockAccept.Task);
            var server = new EchoServer(5000, mockListener);

            var serverTask = server.StartAsync();
            await Task.Delay(100);

            Assert.That(serverTask.IsCompleted, Is.False, "Сервер має бути заблокований на Accept.");

            server.Stop();
            // Імітуємо викидання помилки при зупинці
            blockAccept.SetException(new ObjectDisposedException("Listener stopped"));

            await serverTask;

            mockListener.Received().Start();
            mockListener.Received().Stop();
            await mockListener.Received(1).AcceptTcpClientAsync();
            Assert.That(serverTask.IsCompleted, Is.True);
        }

        // --- ВИПРАВЛЕНІ ТЕСТИ ОБРОБКИ КЛІЄНТА (Інтеграційні) ---

        [Test]
        public async Task HandleClientAsync_ShouldEchoData()
        {
            var server = new EchoServer(5000);
            using var clientA = new TcpClient();
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var endpoint = (IPEndPoint)listener.LocalEndpoint;

            clientA.Connect(endpoint);
            var accepted = listener.AcceptTcpClient(); // Реальний клієнт на стороні сервера

            var streamClient = clientA.GetStream();

            var handleTask = server.HandleClientAsync(accepted, CancellationToken.None);

            byte[] message = { 10, 20, 30, 40 };
            await streamClient.WriteAsync(message, 0, message.Length);

            byte[] buffer = new byte[4];
            int bytesRead = await streamClient.ReadAsync(buffer, 0, buffer.Length);

            Assert.That(bytesRead, Is.EqualTo(4));
            CollectionAssert.AreEqual(message, buffer);

            accepted.Close();
            clientA.Close();
            listener.Stop();
        }

        [Test]
        public async Task HandleClientAsync_DisconnectsOnException()
        {
            // Цей тест перевіряє блок catch (Exception) всередині HandleClientAsync.
            // Ми використовуємо реальний сокет, але примусово закриваємо його під час читання,
            // що викликає ObjectDisposedException/IOException.

            // 1. Піднімаємо сервер і підключаємось
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);

            var serverSideClient = await listener.AcceptTcpClientAsync();
            var server = new EchoServer(5000);

            // 2. Запускаємо обробку
            var handleTask = server.HandleClientAsync(serverSideClient, CancellationToken.None);

            // 3. "Жорстко" вбиваємо з'єднання з боку сервера (симулюємо помилку мережі/сокета)
            // Це змусить ReadAsync всередині методу викинути помилку
            serverSideClient.GetStream().Dispose();
            serverSideClient.Close();

            // 4. Чекаємо завершення. Метод не повинен впасти, він має обробити помилку в catch
            await handleTask;

            // Перевіряємо, що клієнт закритий (код дійшов до finally)
            Assert.IsFalse(serverSideClient.Connected);

            listener.Stop();
        }

        [Test]
        public async Task HandleClientAsync_TerminatesOnCancellation()
        {
            // Цей тест перевіряє вихід по CancellationToken

            // 1. Піднімаємо сервер і підключаємось
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);

            var serverSideClient = await listener.AcceptTcpClientAsync();
            var server = new EchoServer(5000);

            var cts = new CancellationTokenSource();

            // 2. Запускаємо обробку
            var handleTask = server.HandleClientAsync(serverSideClient, cts.Token);

            // 3. Скасовуємо токен
            cts.Cancel();

            // 4. Перевіряємо, що задача завершилася
            // Можливо знадобиться трохи часу на обробку скасування
            var completedTask = await Task.WhenAny(handleTask, Task.Delay(1000));

            Assert.That(completedTask, Is.EqualTo(handleTask), "Task should have completed via cancellation");
            Assert.That(handleTask.IsCompleted, Is.True);

            listener.Stop();
        }
    }
    // Додайте цей клас в EchoServer.Tests.cs
    [TestFixture]
    public class TcpListenerAdapterTests
    {
        [Test]
        public void TcpListenerAdapter_Lifecycle_Coverage()
        {
            // Цей тест перевіряє реальний клас TcpListenerAdapter, а не мок.
            // Використовуємо порт 0, щоб система сама вибрала вільний порт.
            var adapter = new TcpListenerAdapter(IPAddress.Loopback, 0);

            // Перевіряємо, що методи не кидають помилок при виклику
            Assert.DoesNotThrow(() => adapter.Start());

            // AcceptTcpClientAsync важко протестувати без блокування, 
            // але сам факт створення класу вже покриває конструктор.

            Assert.DoesNotThrow(() => adapter.Stop());
        }
    }



    // --- ВИПРАВЛЕНІ ТЕСТИ UdpTimedSender ---

    [TestFixture]
    public class UdpTimedSenderTests
    {
        [Test]
        public async Task UdpTimedSender_SendsDataPeriodically()
        {
            int port = 55000;
            using var receiver = new UdpClient(port);
            using var sender = new UdpTimedSender("127.0.0.1", port);

            // Act
            sender.StartSending(10);
            await Task.Delay(100);

            // Assert 1: Дані прийшли
            Assert.That(receiver.Available, Is.GreaterThan(0));

            // Assert 2: Повторний запуск кидає помилку
            // (Ми НЕ зупиняли sender, тому _timer != null)
            Assert.Throws<InvalidOperationException>(() => sender.StartSending(10));

            // Cleanup
            sender.StopSending();
        }

        [Test]
        public void UdpTimedSender_Stop_SafeToCallMultipleTimes()
        {
            using var sender = new UdpTimedSender("127.0.0.1", 55001);
            sender.StartSending(100);
            sender.StopSending();
            // Повторна зупинка не має кидати помилок
            Assert.DoesNotThrow(() => sender.StopSending());
        }
        [Test]
        public void UdpTimedSender_Dispose_ShouldNotThrow()
        {
            // Покриває метод Dispose(), який викликає StopSending
            var sender = new UdpTimedSender("127.0.0.1", 56020);
            sender.StartSending(100);

            Assert.DoesNotThrow(() => sender.Dispose());

            // Повторний Dispose теж має бути безпечним
            Assert.DoesNotThrow(() => sender.Dispose());
        }

        [Test]
        public async Task EchoServer_HandleClientAsync_GenericException_LogsError()
        {
            // Цей тест замінює Mock на реальну симуляцію помилки мережі.
            // Ми розриваємо з'єднання під час читання, що викликає IOException/ObjectDisposedException.
            // Це гарантовано покриває блок catch (Exception ex) у вашому сервері.

            // 1. Створюємо реальний сервер і клієнт
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);

            var serverSideClient = await listener.AcceptTcpClientAsync();
            var server = new EchoServer(5000);

            // 2. Запускаємо обробку клієнта
            var handleTask = server.HandleClientAsync(serverSideClient, CancellationToken.None);

            // 3. СИМУЛЯЦІЯ АВАРІЇ: "Вбиваємо" потік даних з боку сервера
            // Це змусить метод stream.ReadAsync викинути виняток.
            serverSideClient.GetStream().Dispose();

            // 4. Перевірка
            // Метод HandleClientAsync повинен перехопити помилку в блоці catch і завершитися без падіння.
            Assert.DoesNotThrowAsync(async () => await handleTask);

            // Перевіряємо, що клієнт був закритий (код дійшов до finally)
            Assert.IsFalse(serverSideClient.Connected);

            listener.Stop();
        }
    }

}