using Moq;
using NetSdrClientApp;
using NetSdrClientApp.Messages;
using NetSdrClientApp.Networking;

namespace NetSdrClientAppTests;

public class NetSdrClientTests
{
    NetSdrClient _client;
    Mock<ITcpClient> _tcpMock;
    Mock<IUdpClient> _updMock;

    public NetSdrClientTests() { }

    [SetUp]
    public void Setup()
    {
        _tcpMock = new Mock<ITcpClient>();
        _tcpMock.Setup(tcp => tcp.Connect()).Callback(() =>
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(true);
        });

        _tcpMock.Setup(tcp => tcp.Disconnect()).Callback(() =>
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(false);
        });

        _tcpMock.Setup(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>())).Callback<byte[]>((bytes) =>
        {
            _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, bytes);
        });

        _updMock = new Mock<IUdpClient>();

        _client = new NetSdrClient(_tcpMock.Object, _updMock.Object);
    }

    [Test]
    public async Task ConnectAsyncTest()
    {
        //act
        await _client.ConnectAsync();

        //assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(3));
    }

    [Test]
    public async Task DisconnectWithNoConnectionTest()
    {
        //act
        _client.Disconect();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task DisconnectTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        _client.Disconect();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task StartIQNoConnectionTest()
    {

        //act
        await _client.StartIQAsync();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce);
    }

    [Test]
    public async Task StartIQTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        await _client.StartIQAsync();

        //assert
        //No exception thrown
        _updMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
        Assert.That(_client.IQStarted, Is.True);
    }

    [Test]
    public async Task StopIQTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        await _client.StopIQAsync();

        //assert
        //No exception thrown
        _updMock.Verify(tcp => tcp.StopListening(), Times.Once);
        Assert.That(_client.IQStarted, Is.False);
    }

    //TODO: cover the rest of the NetSdrClient code here



    [Test]
    public void IQStarted_ShouldBeFalseInitially()
    {
        // перевірка початкового стану IQStarted
        Assert.IsFalse(_client.IQStarted);
    }

    [Test]
    public void Disconnect_WithoutConnecting_ShouldNotThrow()
    {
        // перевіряєм, що виклик Disconnect без з'єднання не викликає винятків
        Assert.DoesNotThrow(() => _client.Disconect());
    }

    [Test]
    public void StopIQ_WithoutStarting_ShouldNotThrow()
    {
        // перевіряєм, що зупинка IQ без старта не падає
        Assert.DoesNotThrowAsync(async () => await _client.StopIQAsync());
    }


    // Додайте ці нові тести у файл NetSdrClientTests.cs

    [Test]
    
    public async Task ChangeFrequencyAsync_SendsCorrectMessage()
    {
        
        await _client.ConnectAsync();

        long expectedFrequency = 123456789;
        int channel = 1;

       
        await _client.ChangeFrequencyAsync(expectedFrequency, channel);

        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(4));

       
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.Is<byte[]>(bytes =>
            bytes[4] == channel &&
            bytes[5] == (byte)(expectedFrequency & 0xFF)
        )), Times.Exactly(1)); 
    }
    [Test]
    public async Task ChangeFrequencyAsync_NoConnection_DoesNotSend()
    {
        // Arrange
        // Клієнт НЕ підключений за замовчуванням у Setup

        // Act
        await _client.ChangeFrequencyAsync(1000000, 1);

        // Assert
        // Перевіряємо, що SendMessageAsync жодного разу не був викликаний
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
    }

    [Test]
    public async Task StartIQAsync_SetsIQStartedToTrue_IfConnected()
    {
        // Arrange
        await ConnectAsyncTest(); // Підключаємося

        // Act
        await _client.StartIQAsync();

        // Assert
        Assert.That(_client.IQStarted, Is.True);
    }

    [Test]
    public async Task StopIQAsync_SetsIQStartedToFalse_IfConnected()
    {
        // Arrange
        await ConnectAsyncTest();
        await _client.StartIQAsync(); // Спочатку запускаємо

        // Act
        await _client.StopIQAsync();

        // Assert
        Assert.That(_client.IQStarted, Is.False);
    }

    // Тестування приватної логіки SendTcpRequest та обробки відповіді
    // NetSdrClientTests.cs - ДОДАЙТЕ ЦІ ТЕСТИ В КЛАС NetSdrClientTests

    [Test]
    public void UdpMessageReceived_WritesSamplesToFile()
    {
        // Цей тест покриває приватний метод _udpClient_MessageReceived та запис у файл

        // Arrange
        _tcpMock.SetupGet(t => t.Connected).Returns(true);

        // Створюємо валідне повідомлення з даними (16-біт семпли)
        // Header (DataItem2) + Seq + Body
        byte[] validDataMessage = {
                0x06, 0xC0, // Header (Length 6, Type DataItem2)
                0x00, 0x00, // Sequence
                0x01, 0x02  // Sample (0x0201)
            };

        // Видаляємо файл, якщо він існує, щоб тест був чистим
        if (File.Exists("samples.bin")) File.Delete("samples.bin");

        // Act
        // Імітуємо прихід UDP пакету
        _updMock.Raise(u => u.MessageReceived += null, _updMock.Object, validDataMessage);

        // Assert
        Assert.IsTrue(File.Exists("samples.bin"), "Файл samples.bin мав бути створений");

        var fileInfo = new FileInfo("samples.bin");
        Assert.That(fileInfo.Length, Is.GreaterThan(0));

        // Cleanup
        if (File.Exists("samples.bin")) File.Delete("samples.bin");
    }

    // --- НОВИЙ КЛАС ДЛЯ ТЕСТУВАННЯ WRAPPERS (Додайте його в кінець файлу NetSdrClientTests.cs, але за межами класу NetSdrClientTests) ---

    [TestFixture]
    public class WrapperIntegrationTests
    {
        // Ці тести реально використовують мережу (localhost), щоб покрити TcpClientWrapper та UdpClientWrapper
        // Це дасть величезний приріст покриття.

        [Test]
        public async Task TcpClientWrapper_RealConnectionTest()
        {
            int port = 56000;
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
            listener.Start();

            var wrapper = new NetSdrClientApp.Networking.TcpClientWrapper("127.0.0.1", port);
            bool messageReceived = false;

            wrapper.MessageReceived += (s, data) => { messageReceived = true; };

            try
            {
                // 1. Connect
                wrapper.Connect();
                Assert.IsTrue(wrapper.Connected);

                // Приймаємо клієнта на стороні "сервера"
                var serverClient = await listener.AcceptTcpClientAsync();
                var serverStream = serverClient.GetStream();

                // 2. Send (Client -> Server)
                string testMsg = "Hello";
                await wrapper.SendMessageAsync(testMsg);

                byte[] buffer = new byte[1024];
                int read = await serverStream.ReadAsync(buffer, 0, buffer.Length);
                Assert.That(read, Is.GreaterThan(0));

                // 3. Receive (Server -> Client)
                byte[] response = { 0xAA, 0xBB };
                await serverStream.WriteAsync(response, 0, response.Length);

                // Чекаємо поки wrapper отримає дані
                await Task.Delay(100);
                Assert.IsTrue(messageReceived);

                // 4. Disconnect
                wrapper.Disconnect();
                Assert.IsFalse(wrapper.Connected);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public async Task UdpClientWrapper_RealListeningTest()
        {
            int port = 56001;
            var wrapper = new NetSdrClientApp.Networking.UdpClientWrapper(port);
            bool messageReceived = false;

            wrapper.MessageReceived += (s, e) => { messageReceived = true; };

            // Запускаємо прослуховування
            var listenTask = wrapper.StartListeningAsync();

            // Відправляємо дані на цей порт
            using var sender = new System.Net.Sockets.UdpClient();
            byte[] data = { 1, 2, 3 };
            await sender.SendAsync(data, data.Length, "127.0.0.1", port);

            // Чекаємо обробки
            await Task.Delay(100);

            Assert.IsTrue(messageReceived);

            // Зупиняємо
            wrapper.StopListening();

            // Чекаємо завершення таска
            await Task.WhenAny(listenTask, Task.Delay(500));
            Assert.IsTrue(listenTask.IsCompleted);
        }

        [Test]
        public void TcpClientWrapper_ConnectToInvalidPort_ShouldNotCrash()
        {
            // Тест на обробку помилок (try-catch всередині Connect)
            var wrapper = new NetSdrClientApp.Networking.TcpClientWrapper("127.0.0.1", 9999); // Неіснуючий порт
            Assert.DoesNotThrow(() => wrapper.Connect());
            Assert.IsFalse(wrapper.Connected);
        }

        [Test]
        public async Task TcpClientWrapper_SendMessage_WhenNotConnected_Throws()
        {
            var wrapper = new NetSdrClientApp.Networking.TcpClientWrapper("127.0.0.1", 56002);
            // Не викликаємо Connect

            Assert.ThrowsAsync<InvalidOperationException>(async () => await wrapper.SendMessageAsync(new byte[] { 1 }));
        }
    }
    [Test]
    public async Task SendTcpRequest_NoConnection_ReturnsNullAndDoesNotThrow()
    {
        // Arrange
        _tcpMock.SetupGet(tcp => tcp.Connected).Returns(false); // Забезпечуємо, що _tcpClient.Connected = false
        byte[] requestMessage = { 0x01, 0x02 };

        // Act
        var task = _client.GetType()
            .GetMethod("SendTcpRequest", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(_client, new object[] { requestMessage }) as Task<byte[]>;

        // Assert
        Assert.IsNotNull(task);
        var result = await task;
        Assert.IsNull(result, "При відсутності підключення має повертатися null.");
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
    }



    // --- ДОДАТКОВІ ТЕСТИ ДЛЯ ПОКРИТТЯ ГІЛОК IF/ELSE ---

    [Test]
    public async Task TcpClientWrapper_EdgeCases_Coverage()
    {
        int port = 56005;
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
        listener.Start();

        var wrapper = new NetSdrClientApp.Networking.TcpClientWrapper("127.0.0.1", port);

        // 1. Test Connect
        wrapper.Connect();
        Assert.IsTrue(wrapper.Connected);

        // 2. Test "Already Connected" branch (покриваємо if (Connected) return;)
        // Має написати в консоль "Already connected...", але не впасти
        wrapper.Connect();
        Assert.IsTrue(wrapper.Connected);

        // 3. Test SendMessageAsync(string) overload (ми тестували тільки byte[])
        var acceptTask = listener.AcceptTcpClientAsync();
        await wrapper.SendMessageAsync("Hello String");
        var serverClient = await acceptTask;
        Assert.IsTrue(serverClient.Connected);

        // 4. Test Disconnect
        wrapper.Disconnect();
        Assert.IsFalse(wrapper.Connected);

        // 5. Test "Already Disconnected" branch (покриваємо if (!Connected)...)
        // Має написати "No active connection...", але не впасти
        wrapper.Disconnect();

        // 6. Test SendMessageAsync when disconnected (Exception branch)
        Assert.ThrowsAsync<InvalidOperationException>(async () => await wrapper.SendMessageAsync("Fail"));

        listener.Stop();
    }

    [Test]
    public async Task UdpClientWrapper_EdgeCases_Coverage()
    {
        int port = 56006;
        var wrapper = new NetSdrClientApp.Networking.UdpClientWrapper(port);

        // 1. Test GetHashCode (Sonar часто вимагає покриття перевизначених методів)
        var hash = wrapper.GetHashCode();
        Assert.That(hash, Is.Not.Zero);

        // 2. Test Exit method (дублює StopListening, але треба покрити)
        var task = wrapper.StartListeningAsync();
        await Task.Delay(50);

        wrapper.Exit(); // Це має зупинити прослуховування

        await Task.WhenAny(task, Task.Delay(500));
        Assert.IsTrue(task.IsCompleted);
    }

    [Test]
    public async Task UdpClientWrapper_ErrorHandling_Coverage()
    {
        // Тест на випадок, коли порт зайнятий (покриваємо try/catch у StartListeningAsync)
        int port = 56007;
        using var blocker = new System.Net.Sockets.UdpClient(port); // Займаємо порт

        var wrapper = new NetSdrClientApp.Networking.UdpClientWrapper(port);

        // Це має впасти всередині і вивести помилку в консоль, але не обвалити тест
        // (або завершити таск миттєво)
        await wrapper.StartListeningAsync();

        // Якщо ми тут, значить exception був оброблений всередині (catch (Exception ex))
        Assert.Pass();
    }







}
