using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Threax.Extensions.DependencyInjection;

namespace Threax.Steps.Tests
{
    [TestClass]
    public class DeriveTypeTests
    {
        class DeriveType
        {
            public string DerivedValue { get; set; }

            public void Derive() 
            {
                DerivedValue = DerivedValue ?? "Derived";
            }
        }

        [TestMethod]
        public void TestDerivedRegistration()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.TryAddScopedDeriveType(typeof(DeriveType));

            using var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();

            var config = scope.ServiceProvider.GetRequiredService<DeriveType>();

            Assert.AreEqual("Derived", config.DerivedValue);
        }

        [TestMethod]
        public void TestDerivedRegistrationGeneric()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.TryAddScopedDeriveType<DeriveType>();

            using var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();

            var config = scope.ServiceProvider.GetRequiredService<DeriveType>();

            Assert.AreEqual("Derived", config.DerivedValue);
        }

        [TestMethod]
        public void TestOverrideRegistration()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.TryAddScopedDeriveType<DeriveType>(() => new DeriveType
            {
                DerivedValue = "Override"
            });

            using var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();

            var config = scope.ServiceProvider.GetRequiredService<DeriveType>();

            Assert.AreEqual("Override", config.DerivedValue);
        }

        [TestMethod]
        public void TestOverrideWithServicesRegistration()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.TryAddScopedDeriveType<DeriveType>((s) => new DeriveType
            {
                DerivedValue = "Override"
            });

            using var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();

            var config = scope.ServiceProvider.GetRequiredService<DeriveType>();

            Assert.AreEqual("Override", config.DerivedValue);
        }
    }
}