using ihc_openvisual.Services;

namespace safe_visual_tests;

/// <summary>US-012: the data-line terminal address encode/decode (Avalonia-free logic).</summary>
public class DatalineAddressingTests
{
    [TestCase(1, 1, 16, "_0x1")]
    [TestCase(2, 3, 16, "_0x13")]   // (2-1)*16 + 3 = 19 = 0x13
    [TestCase(1, 2, 8, "_0x2")]
    [TestCase(3, 8, 8, "_0x18")]    // (3-1)*8 + 8 = 24 = 0x18
    public void Encode_ProducesToken(int line, int terminal, int perLine, string expected) =>
        Assert.That(DatalineAddressing.Encode(line, terminal, perLine), Is.EqualTo(expected));

    [TestCase(0, 1, 16)]    // data line < 1
    [TestCase(1, 0, 16)]    // terminal < 1
    [TestCase(1, 17, 16)]   // terminal beyond the line
    public void Encode_OutOfRange_ReturnsUnaddressedDefault(int line, int terminal, int perLine) =>
        Assert.That(DatalineAddressing.Encode(line, terminal, perLine), Is.EqualTo("_0x0"));

    [Test]
    public void EncodeDecode_RoundTrips()
    {
        (int line, int term)[] cases = { (1, 1), (2, 5), (3, 16), (4, 8) };
        Assert.Multiple(() =>
        {
            foreach (var (line, term) in cases)
            {
                string token = DatalineAddressing.Encode(line, term, 16);
                Assert.That(DatalineAddressing.TryDecode(token, 16, out int l, out int t), Is.True);
                Assert.That((l, t), Is.EqualTo((line, term)));
            }
        });
    }

    [Test]
    public void TryDecode_BlankOrUnaddressed_IsFalse()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DatalineAddressing.TryDecode("_0x0", 16, out _, out _), Is.False);
            Assert.That(DatalineAddressing.TryDecode("", 16, out _, out _), Is.False);
            Assert.That(DatalineAddressing.TryDecode(null, 16, out _, out _), Is.False);
        });
    }
}
