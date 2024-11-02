using NUnit.Framework;
using System;

namespace Unity.Entities.Editor.Tests
{
    class CooldownTests
    {
        [Test]
        public void CooldownUpdate_ReturnsFalseWhenCalledToSoon()
        {
            var cd = new Cooldown(TimeSpan.FromMilliseconds(100));
            var now = DateTime.Now;
            Assert.That(cd.Update(now), Is.True);
            Assert.That(cd.Update(now), Is.False);
            Assert.That(cd.Update(now.AddMilliseconds(99)), Is.False);
            Assert.That(cd.Update(now.AddMilliseconds(100)), Is.True);
        }
    }
}
