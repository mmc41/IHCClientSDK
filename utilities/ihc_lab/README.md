# Test project (C#)

This is an in cross-platform GUI utility to experiment with calling the IHC API.

**IMPORTANT**: Please create a ihcsettings.json in the root dir of the repo before running this app. Refer to the ihcsettings_template.json for required format.

# Running against a controller stub

To explore the API without a controller, start the controller stub and then Lab with the switch the stub prints first:

```bash
dotnet run --project ihcclient_stub/ihcclient_stub.csproj
dotnet run --project utilities/ihc_lab/ihc_lab.csproj -- --controller-stub=http://127.0.0.1:<port>/<token>/
```

Lab still reads `ihcsettings.json` for everything but the endpoint and login. The title then reads `IHC Lab : CONTROLLER STUB (no real controller) : Endpoint set to …`. A real endpoint entered in the Setup dialog during that run is refused until Lab restarts, and the title reads `IHC Lab : CONTROLLER STUB (real endpoints refused until restart) : Endpoint set to …`. Without the switch, a `mock://` endpoint left in `ihcsettings.json` is refused at start: the error names both ways out, a controller's address in the file or the switch, and Setup stays disabled, because the file is where it is repaired. See [Running against a controller stub](../../README.md#running-against-a-controller-stub) for the rest.

# NOTES
Consider using
https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Exporter.InMemory