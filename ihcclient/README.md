# About ihcclient

This project contains an unoffical dotnet IHC client API for  [IHC (Intelligent House Concept)](https://www.lk.dk/professionel/produktoversigt/intelligente-systemer/ihc/). The project is NOT affiliated with- or supported by LK / Schneider Electric.

Since dotnet core (and later dot net v5.0) did not officially support
SOAP webservices, the SDK API is based on a combination of
code generated by [dotnet-svcutil](https://docs.microsoft.com/en-us/dotnet/core/additional-tools/dotnet-svcutil-guide?tabs=dotnetsvcutil2x) from downloaded WSDL files in combination with custom serialization and
a higher-level wrapper for easier usage.

The Ihc namespace contains API service classes for all IHC controller web services such as `AuthenticationService`, `ConfigurationService`, `ControllerService`, `MessagecontrollogService`, `ModuleService`, `NotificationManagerService`, `ResourceInteractionService`, `TimeManagerService`, `UserManagerService` and `OpenAPIService` (*). The API service classes have a 1-1 relationship
with backing soap services and soap artifacts but are on a higher level to make the API easy to use and to better isolate developers against IHC SOAP contract changes. SOAP details are thus not exposed. Instead, methods in these service API classes use their own high-level data models declared in the same Ihc namespace.

*\*) The new OpenAPIService is for v3.0+ controllers only. I am unsure of the quality of it. Usage is not recommend it at the time of writing (2021).*

Each service API class must be instantiated with `new`. Provide a logger and a IHC controller IP address in the constructor. Unless you are using the `OpenAPIService`, you generally need
to login using `AuthenticationService` before using the other services.
You can use a `Microsoft.Extensions.Logging.Abstractions.NullLogger<YourClass>.Instance` as a dummy logger if you want.

The provided API is by design free of SOAP inhertiage and completly async. For long pooling the API use async enumerables are used instead of traditional events/callbacks (see `ResourceInteractionService.GetResourceValueChanges`).

Each service class is internally wrapping a `SoapImpl` class based on a specific Ihc.Soap.\*.\* interface generated from the WSDL. These and
the related low level SOAP data structures in Ihc.Soap.*.* namespaces are for internal use (should not be exposed).

## Status

The `AuthenticationService` and `ResourceInteractionService` services are essentially feature complete. The remaining servies are currently only partically implemented.

All low-level wiring within `SoapImpl` ought to work though, so in most cases it should be relatively easy to add a missing high-level function (possibly needing a new high-level data structure as well).

## Getting started

Unless you are using the new OpenAPIService (not recommended, see above comment), Your code needs to Authenticate before calling IHC API's and Disconnect when finished as shown below. Setting the IHC application name is important for controllrs before v3.0 as the default is unlikely to work.

```csharp
var endpoint = "http://<YOUR CONTROLLER IP ADDRESS>";
var noLog = NullLogger<Program>.Instance;

var authService = new AuthenticationService(noLog, endpoint);

var login = await authService.Authenticate("<MY IHC USERNAME>", "<MY IHC PASSWORD>", "administrator");

// Instantiate additional IHC Services and invoke them here.

var logoff = authService.Disconnect(); 
```

## .NET usage notes 

While it is possible to use the client in both simple console apps
and in AspNetCore apps, it is generally recommended to use
Microsoft host services in order to ensure orderly shutdown, such
as `Disconnect` being called when aborting with CTRL-C etc. AspNetCore apps all use hosting. For command line apps you can use [.NET Generic Host](
https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host?view=aspnetcore-2.2).

## Updating WSDL / generated src.
In the rare event that WSDL must be changed and new generated code is needed, one can use the `download_wsdl.sh` and `generate.sh` files for this on MacOS (no scripts currently avilable for Windows/Linux). The output is placed in the `generatedsrc` folder. These files should not be changed manually. Note that `wget` and the [dotnet-svcutil](https://docs.microsoft.com/en-us/dotnet/core/additional-tools/dotnet-svcutil-guide?tabs=dotnetsvcutil2x) tool must be installed for the above scripts to work.
