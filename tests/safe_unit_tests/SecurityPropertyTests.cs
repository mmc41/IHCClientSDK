using System;
using System.Linq;
using CsCheck;
using Ihc;

namespace Ihc.Tests
{
    /// <summary>
    /// Property-based tests for <see cref="SimpleSecret"/>, using CsCheck.
    ///
    /// The example-based <see cref="SecurityUnitTest"/> checks fixed unicode/long/special-char inputs;
    /// this generalizes the two algebraic laws: DecryptString(EncryptString(s)) == s for any string,
    /// and FromBase64Url(ToBase64Url(b)) == b for any byte array (isolating the codec's padding edges).
    /// </summary>
    [TestFixture]
    public class SecurityPropertyTests
    {
        private const string Passphrase = "correct horse battery staple";

        // Full-Unicode text (incl. astral planes and the empty string), built from Unicode scalar
        // values so it never contains a lone surrogate - which UTF-8 encoding would replace, breaking
        // the round-trip for reasons unrelated to SimpleSecret.
        private static readonly Gen<string> UnicodeText =
            Gen.Int[0, 0x10FFFF]
               .Where(cp => cp < 0xD800 || cp > 0xDFFF)
               .Select(char.ConvertFromUtf32)
               .Array[0, 40]
               .Select(parts => string.Concat(parts));

        /// <summary>
        /// Law: decrypting an encrypted string recovers the original, for any text - and the
        /// ciphertext is never empty and never the plaintext itself (so a round-trip that "passes"
        /// by not encrypting at all, or by producing nothing for the empty input, still fails).
        /// </summary>
        [Test]
        public void EncryptString_DecryptString_RoundTripsAnyText()
        {
            var cipher = new SimpleSecret(Passphrase);
            UnicodeText.Sample(plaintext =>
            {
                string encrypted = cipher.EncryptString(plaintext);
                return cipher.DecryptString(encrypted) == plaintext
                    && encrypted.Length > 0
                    && encrypted != plaintext;
            });
        }

        /// <summary>
        /// Law: the Base64URL codec recovers the original bytes exactly, for any length (exercising
        /// the length mod 3 = 0/1/2 padding boundaries).
        /// </summary>
        [Test]
        public void FromBase64Url_ToBase64Url_RoundTripsAnyBytes()
        {
            Gen.Byte.Array[0, 64].Sample(bytes =>
                SimpleSecret.FromBase64Url(SimpleSecret.ToBase64Url(bytes)).SequenceEqual(bytes));
        }
    }
}
