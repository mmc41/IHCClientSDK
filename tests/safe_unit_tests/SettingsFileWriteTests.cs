using NUnit.Framework;
using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json.Nodes;

namespace Ihc.Tests
{
    /// <summary>
    /// The protection <c>ihc_settings_encrypt</c> owes the file it rewrites.
    /// </summary>
    /// <remarks>
    /// The utility swaps a temporary file into place rather than truncating the original, and a swap is
    /// exactly where a credentials file can quietly lose the protection it was given: a newly created file
    /// carries the DIRECTORY's defaults, so an owner-only settings file can come back world-readable while
    /// the run reports nothing but success. Each platform's answer to "protected" is pinned by its own test
    /// and skipped on the other.
    /// </remarks>
    public class SettingsFileWriteTests
    {
        private string directory = string.Empty;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "ihc-settings-write-" + Path.GetRandomFileName());
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(directory, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        /// <summary>Writes a settings file holding a plaintext password and returns its path.</summary>
        private string GivenSettingsFile()
        {
            string path = Path.Combine(directory, "ihcsettings.json");
            File.WriteAllText(path, "{\"ihcclient\":{\"password\":\"before\"},\"encryption\":{\"isEncrypted\":false}}");
            return path;
        }

        /// <summary>Rewrites <paramref name="path"/> with a changed password, the way either operation does.</summary>
        private static void WhenRewritten(string path)
        {
            JsonNode root = JsonNode.Parse(File.ReadAllText(path))!;
            root["ihcclient"]!["password"] = "after";
            Ihc.Utility.Program.WriteSettingsFile(root, path);
        }

        [Test]
        public void WriteSettingsFile_KeepsTheUnixModeOfTheFileItReplaces()
        {
            if (OperatingSystem.IsWindows())
            {
                Assert.Ignore("Protection on Windows is an ACL rather than a mode; pinned by the ACL test.");
            }
            else
            {
                string path = GivenSettingsFile();
                const UnixFileMode ownerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                File.SetUnixFileMode(path, ownerOnly);

                WhenRewritten(path);

                Assert.That(File.GetUnixFileMode(path), Is.EqualTo(ownerOnly),
                    "the rewritten settings file took the temporary file's default mode instead of its own");
            }
        }

        [Test]
        public void WriteSettingsFile_KeepsTheAclOfTheFileItReplaces()
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.Ignore("Protection on Unix is a mode rather than an ACL; pinned by the mode test.");
            }
            else
            {
                string path = GivenSettingsFile();
                var file = new FileInfo(path);
                FileSecurity security = file.GetAccessControl();
                // Inheritance off, one explicit ACE: the shape a file gets when someone deliberately locks it
                // down, and the shape nothing a fresh file in this directory would be born with.
                security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                security.AddAccessRule(new FileSystemAccessRule(
                    WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl, AccessControlType.Allow));
                file.SetAccessControl(security);

                WhenRewritten(path);

                FileSecurity after = new FileInfo(path).GetAccessControl();
                Assert.That(after.AreAccessRulesProtected, Is.True,
                    "the rewritten settings file re-inherited the directory's rules instead of keeping its own");
                Assert.That(
                    after.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier)),
                    Has.Count.EqualTo(1),
                    "the explicit rule the settings file was given did not survive the rewrite");
            }
        }

        // The decrypt path puts a plaintext password in the temporary file, so a successful run leaving one
        // behind would be a second copy of the credential nobody knows to delete.
        [Test]
        public void WriteSettingsFile_LeavesNoTemporaryFileBehind()
        {
            string path = GivenSettingsFile();

            WhenRewritten(path);

            Assert.That(File.Exists(path + ".tmp"), Is.False);
            Assert.That(File.ReadAllText(path), Does.Contain("after"));
        }
    }
}
