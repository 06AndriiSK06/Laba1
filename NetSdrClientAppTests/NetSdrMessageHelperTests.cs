using NetSdrClientApp.Messages;

namespace NetSdrClientAppTests
{
    public class NetSdrMessageHelperTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void GetControlItemMessageTest()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.Ack;
            var code = NetSdrMessageHelper.ControlItemCodes.ReceiverState;
            int parametersLength = 7500;

            //Act
            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, new byte[parametersLength]);

            var headerBytes = msg.Take(2);
            var codeBytes = msg.Skip(2).Take(2);
            var parametersBytes = msg.Skip(4);

            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);
            var actualCode = BitConverter.ToInt16(codeBytes.ToArray());

            //Assert
            Assert.That(headerBytes.Count(), Is.EqualTo(2));
            Assert.That(msg.Length, Is.EqualTo(actualLength));
            Assert.That(type, Is.EqualTo(actualType));

            Assert.That(actualCode, Is.EqualTo((short)code));

            Assert.That(parametersBytes.Count(), Is.EqualTo(parametersLength));
        }

        [Test]
        public void GetDataItemMessageTest()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.DataItem2;
            int parametersLength = 7500;

            //Act
            byte[] msg = NetSdrMessageHelper.GetDataItemMessage(type, new byte[parametersLength]);

            var headerBytes = msg.Take(2);
            var parametersBytes = msg.Skip(2);

            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);

            //Assert
            Assert.That(headerBytes.Count(), Is.EqualTo(2));
            Assert.That(msg.Length, Is.EqualTo(actualLength));
            Assert.That(type, Is.EqualTo(actualType));

            Assert.That(parametersBytes.Count(), Is.EqualTo(parametersLength));
        }

        //TODO: add more NetSdrMessageHelper tests



        [Test]
        public void GetControlItemMessage_EmptyParameters_ShouldHaveCorrectLength()
        {
            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(
                NetSdrMessageHelper.MsgTypes.Ack,
                NetSdrMessageHelper.ControlItemCodes.ReceiverState,
                new byte[0]
            );

            Assert.That(msg.Length, Is.EqualTo(4)); // header + code
        }

        [Test]
        public void GetDataItemMessage_EmptyParameters_ShouldHaveCorrectLength()
        {
            byte[] msg = NetSdrMessageHelper.GetDataItemMessage(
                NetSdrMessageHelper.MsgTypes.DataItem2,
                new byte[0]
            );

            Assert.That(msg.Length, Is.EqualTo(2)); // тільки header
        }





        [Test]
        public void TranslateMessage_ControlItem_CorrectlyParses()
        {
            // Arrange: SetControlItem (type 0), ReceiverState (code 0x0018), Body { 0xAA, 0xBB }
            // Header (2 байти): 0x18 (ControlItem: 0), 0x06 (Length: 2 (Header) + 2 (Code) + 2 (Body) = 6)
            // Код (2 байти): 0x18, 0x00
            byte[] msg = { 0x06, 0x00, 0x18, 0x00, 0xAA, 0xBB };

            // Act
            NetSdrMessageHelper.TranslateMessage(msg, out var type, out var itemCode, out var sequenceNumber, out var body);

            // Assert
            Assert.That(type, Is.EqualTo(NetSdrMessageHelper.MsgTypes.SetControlItem));
            Assert.That(itemCode, Is.EqualTo(NetSdrMessageHelper.ControlItemCodes.ReceiverState));
            Assert.That(sequenceNumber, Is.EqualTo(0)); // Для ControlItem має бути 0
            CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB }, body);
        }

        [Test]
        public void TranslateMessage_DataItem_CorrectlyParses()
        {
            // Arrange: DataItem2 (type 6), Sequence: 0x1234, Body { 0xCC, 0xDD }
            // Header (2 байти): 0x06 (DataItem2: 6), 0x06 (Length: 2 (Header) + 2 (SeqNum) + 2 (Body) = 6)
            // SeqNum (2 байти): 0x34, 0x12 (Little Endian)
            byte[] msg = { 0x06, 0xC0, 0x34, 0x12, 0xCC, 0xDD }; // 0xC006 (Type=6, Length=6)

            // Act
            NetSdrMessageHelper.TranslateMessage(msg, out var type, out var itemCode, out var sequenceNumber, out var body);

            // Assert
            Assert.That(type, Is.EqualTo(NetSdrMessageHelper.MsgTypes.DataItem2));
            Assert.That(itemCode, Is.EqualTo(NetSdrMessageHelper.ControlItemCodes.None)); // Для DataItem має бути None
            Assert.That(sequenceNumber, Is.EqualTo(0x1234));
            CollectionAssert.AreEqual(new byte[] { 0xCC, 0xDD }, body);
        }

        [Test]
        public void TranslateMessage_InvalidControlItemCode_ReturnsFailure()
        {
            // Arrange: ControlItem з неіснуючим кодом 0xFFFF
            byte[] msg = { 0x04, 0x00, 0xFF, 0xFF }; // Header (4 байти)

            // Act
            var result = NetSdrMessageHelper.TranslateMessage(msg, out var type, out var itemCode, out var sequenceNumber, out var body);

            // Assert
            Assert.IsFalse(result, "Має повернутися false, оскільки код не визначено.");
            Assert.That(itemCode, Is.EqualTo(NetSdrMessageHelper.ControlItemCodes.None)); // Повинно бути None, або значення за замовчуванням
        }

        [Test]
        public void TranslateMessage_InvalidLength_ReturnsFailure()
        {
            // Arrange: Довжина в заголовку 6, але фактична довжина масиву 5
            byte[] msg = { 0x06, 0x00, 0x18, 0x00, 0xAA }; // Очікувана довжина 6, фактична 5

            // Act
            var result = NetSdrMessageHelper.TranslateMessage(msg, out var type, out var itemCode, out var sequenceNumber, out var body);

            // Assert
            Assert.IsFalse(result, "Має повернутися false, оскільки фактична довжина не відповідає заголовку.");
        }

        // --- Тести для GetSamples ---

        [Test]
        public void GetSamples_16BitSamples_CorrectlyParses()
        {
            // Arrange: 16-бітні вибірки (2 байти на вибірку). {I1, Q1, I2, Q2, ...}
            // I1=0x01, Q1=0x02 -> Sample 1: 0x0201 (Little Endian)
            // I2=0x03, Q2=0x04 -> Sample 2: 0x0403 (Little Endian)
            byte[] body = { 0x01, 0x02, 0x03, 0x04 };

            // Act
            var samples = NetSdrMessageHelper.GetSamples(16, body).ToArray();

            // Assert
            Assert.That(samples.Length, Is.EqualTo(2));
            Assert.That(samples[0], Is.EqualTo(0x0201));
            Assert.That(samples[1], Is.EqualTo(0x0403));
        }

        [Test]
        public void GetSamples_8BitSamples_CorrectlyParses()
        {
            // Arrange: 8-бітні вибірки (1 байт на вибірку). {I1, Q1, ...}
            // I1=0x10, Q1=0x20
            byte[] body = { 0x10, 0x20 };

            // Act
            var samples = NetSdrMessageHelper.GetSamples(8, body).ToArray();

            // Assert
            Assert.That(samples.Length, Is.EqualTo(2));
            // 8-бітні вибірки розширюються до 32-біт (Int32)
            Assert.That(samples[0], Is.EqualTo(0x10));
            Assert.That(samples[1], Is.EqualTo(0x20));
        }

        [Test]
        public void GetSamples_4BytesSamples_CorrectlyParses()
        {
            // Arrange: 32-бітні вибірки (4 байти на вибірку). {I1, Q1, ...}
            // I1 = 0x11223344 (Little Endian)
            byte[] body = { 0x44, 0x33, 0x22, 0x11 };

            // Act
            var samples = NetSdrMessageHelper.GetSamples(32, body).ToArray();

            // Assert
            Assert.That(samples.Length, Is.EqualTo(1));
            Assert.That(samples[0], Is.EqualTo(0x11223344));
        }

        [Test]
        public void GetSamples_InvalidSampleSize_ThrowsArgumentOutOfRangeException()
        {
            // Arrange: 64-біт (8 байт), що > 4 байти
            ushort sampleSize = 64;
            byte[] body = new byte[8];

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => NetSdrMessageHelper.GetSamples(sampleSize, body).ToArray());
        }

        [Test]
        public void GetSamples_PartialSampleLeftover_IgnoresLeftover()
        {
            // Arrange: 16-бітні вибірки (2 байти). 3 байти -> 1 повна вибірка + 1 байт залишку
            byte[] body = { 0x01, 0x02, 0x03 };

            // Act
            var samples = NetSdrMessageHelper.GetSamples(16, body).ToArray();

            // Assert
            Assert.That(samples.Length, Is.EqualTo(1), "Повинна бути тільки одна повна вибірка.");
            Assert.That(samples[0], Is.EqualTo(0x0201));
        }







    }
}