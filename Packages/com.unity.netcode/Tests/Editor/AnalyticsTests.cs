using NUnit.Framework;
using Unity.NetCode.Analytics;

namespace Unity.NetCode.Tests
{
    namespace AnalyticsTests
    {
        class SessionState
        {
            [TearDown]
            public void SetUp()
            {
                NetCodeAnalytics.ClearGhostComponents();
            }

            [Test]
            public void StoredValueCanBeRetrieved()
            {
                var x = new GhostConfigurationAnalyticsData { id = "42", };

                NetCodeAnalytics.StoreGhostComponent(x);

                Assert.That(x.id, Is.EqualTo(NetCodeAnalytics.RetrieveGhostComponents()[0].id));
            }

            [Test]
            public void ChangedValueCanBeRetrieved()
            {
                var x = new GhostConfigurationAnalyticsData();
                NetCodeAnalytics.StoreGhostComponent(x);
                x.importance = 123;
                NetCodeAnalytics.StoreGhostComponent(x);
                var res = NetCodeAnalytics.RetrieveGhostComponents();
                Assert.That(x.importance, Is.EqualTo(res[0].importance));
            }

            [Test]
            public void ChangedSecondValue()
            {
                var x = new GhostConfigurationAnalyticsData { id = "43" };
                NetCodeAnalytics.StoreGhostComponent(x);
                var y = new GhostConfigurationAnalyticsData { id = "42" };
                NetCodeAnalytics.StoreGhostComponent(y);
                y.importance = 100;
                NetCodeAnalytics.StoreGhostComponent(y);
                var res = NetCodeAnalytics.RetrieveGhostComponents();
                Assert.That(y.importance, Is.EqualTo(res[1].importance));
            }

            [Test]
            public void ChangedBothValues()
            {
                var x = new GhostConfigurationAnalyticsData { id = "43" };
                NetCodeAnalytics.StoreGhostComponent(x);
                var y = new GhostConfigurationAnalyticsData { id = "42" };
                NetCodeAnalytics.StoreGhostComponent(y);
                y.importance = 100;
                NetCodeAnalytics.StoreGhostComponent(y);
                x.importance = 42;
                NetCodeAnalytics.StoreGhostComponent(x);
                var res = NetCodeAnalytics.RetrieveGhostComponents();
                Assert.That(x.importance, Is.EqualTo(res[0].importance));
                Assert.That(y.importance, Is.EqualTo(res[1].importance));
            }

            [Test]
            public void MultipleValueCanBeRetrieved()
            {
                var x = new GhostConfigurationAnalyticsData { id = "42", };
                NetCodeAnalytics.StoreGhostComponent(x);
                var y = new GhostConfigurationAnalyticsData { id = "43", };
                NetCodeAnalytics.StoreGhostComponent(y);
                var res = NetCodeAnalytics.RetrieveGhostComponents();
                Assert.That(res.Length, Is.EqualTo(2));
                Assert.That(x.id, Is.EqualTo(res[0].id));
                Assert.That(y.id, Is.EqualTo(res[1].id));
            }
        }
    }
}
