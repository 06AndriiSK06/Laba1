using NetArchTest.Rules;
using NUnit.Framework;

namespace NetSdrClientAppTests
{
    [TestFixture]
    public class ArchitectureTests
    {
        [Test]
        public void Core_Should_Not_Depend_On_UI()
        {
            // Перевіряємо, що основна частина програми не залежить від UI
            var result = Types
                .InAssembly(typeof(NetSdrClientApp.Networking.UdpClientWrapper).Assembly)
                .ShouldNot()
                .HaveDependencyOn("NetSdrClientApp.UI")
                .GetResult();

            Assert.That(result.IsSuccessful, Is.True, "Core не повинен напряму залежати від UI!");
        }






        [Test]
        public void UI_Should_Not_Depend_On_Infrastructure()
        {
            // Якщо колись з’явиться Infrastructure — теж перевірка
            var result = Types
                .InAssembly(typeof(NetSdrClientApp.Program).Assembly)
                .ShouldNot()
                .HaveDependencyOn("NetSdrClientApp.Infrastructure")
                .GetResult();

            Assert.That(result.IsSuccessful, Is.True, "UI не повинен напряму залежати від Infrastructure!");
        }
    }
}
