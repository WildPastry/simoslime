using NUnit.Framework;

namespace SimoSlime.Rules.Tests
{
    public class SmokeTests
    {
        [Test]
        public void Rules_assembly_compiles()
        {
            Assert.That(typeof(CardKind).Assembly.GetName().Name, Is.EqualTo("SimoSlime.Rules"));
        }
    }
}
