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











}
