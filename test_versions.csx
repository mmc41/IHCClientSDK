// Quick test script to verify version methods
#r "ihcclient/bin/Debug/net9.0/ihcclient.dll"

using Ihc;

Console.WriteLine($"SDK Version: {VersionInfo.GetSdkVersion()}");
