using Core.Diagram;
using NUnit.Framework;

namespace Tests.EditMode
{
    [TestFixture]
    public class ClassNameRulesTests
    {
        [Test]
        public void Sanitize_NullEmptyOrWhitespace_ReturnsNull()
        {
            Assert.IsNull(ClassNameRules.Sanitize(null));
            Assert.IsNull(ClassNameRules.Sanitize(""));
            Assert.IsNull(ClassNameRules.Sanitize("   "));
        }

        [Test]
        public void Sanitize_AllInvalidCharacters_ReturnsNull()
        {
            Assert.IsNull(ClassNameRules.Sanitize("###"));
            Assert.IsNull(ClassNameRules.Sanitize("!@#$%"));
        }

        [Test]
        public void Sanitize_KeepsLettersDigitsSpaceHyphenUnderscore()
        {
            Assert.AreEqual("Classe A-1", ClassNameRules.Sanitize("Classe A-1"));
            Assert.AreEqual("Leitor_Livros", ClassNameRules.Sanitize("Leitor_Livros"));
        }

        [Test]
        public void Sanitize_StripsInvalidCharacters()
        {
            Assert.AreEqual("Cliente", ClassNameRules.Sanitize("Cliente!!!"));
            Assert.AreEqual("Usuario1", ClassNameRules.Sanitize("Usuario@1"));
            Assert.AreEqual("bSalab", ClassNameRules.Sanitize("<b>Sala</b>"));
        }

        [Test]
        public void Sanitize_CollapsesConsecutiveSpaces()
        {
            Assert.AreEqual("Classe A", ClassNameRules.Sanitize("Classe     A"));
        }

        [Test]
        public void Sanitize_TrimsOuterWhitespace()
        {
            Assert.AreEqual("Classe", ClassNameRules.Sanitize("   Classe   "));
        }

        [Test]
        public void Sanitize_TruncatesToMaxLength()
        {
            string result = ClassNameRules.Sanitize(new string('a', 100));
            Assert.IsNotNull(result);
            Assert.AreEqual(ClassNameRules.MaxLength, result.Length);
        }
    }
}
