# About This project

This project is an **unofficial** community provided client software development kit for [IHC (Intelligent House Concept)](https://www.lk.dk/professionel/produktoversigt/intelligente-systemer/ihc/) with clients running Microsoft .NET on Windows, Mac and Linux variants (incl. raspberry pi).

This project is released as open source. Please supply pull requests with tested changes. New contributors are welcome!

*NOTE: Before running any tests, tools or examples in this repo, please create and configure a private **ihcsettings.json** file with information on your IHC installation. Refer to the ihcsettings_template.json for details on what is needed. While the file is needed for tests/tools/examples located here, the file is NOT needed in your own projects if you simply consume this SDK. Finally, you also need to enable network access and thirdparty access in the **IHC administrator** interface app from LK.*

## Important disclaimers:

* Please notice that this project is **not** in any way affiliated with- or supported by LK / Schneider Electric. It exist only because Schneider Electric has not yet released a public SDK themselves.
* The project is unofficial, unfinished and may contain serious bugs. Use at your own risk!
* The project is only partially supported. You are welcome to report bugs and feature requests but don't expect quick solutions unless you supply tested pull-requests as well (or offer to pay the author(s) for support).
* The project is intended for experienced .NET developers using C# and/or F#.
* The IHC has been tested against v3.0 controllers using both Mac and Windows (but Linux ought to work too). More testing from users are needed. Feedback is welcome.

## Status

[![build](https://github.com/mmc41/IHCClientSDK/actions/workflows/build-validation.yml/badge.svg)](https://github.com/mmc41/IHCClientSDK/actions/workflows/build-validation.yml)

The project is an early preview/beta.

The SDK currently supports v3.0 IHC controller's only. Support with pre-3.0 controllers is possible but require contributors interested in this. See [this Issue](https://github.com/mmc41/IHCClientSDK/issues/1) to discuss this subject and to keep track of future support.

Definitely missing is an easy to consume nuget package for the client. I expect to publish a package if there is interest. For now you will have to build the client yourself. 

See here [ihcclient](ihcclient/README.md#Status) for more details on IHC API implementation status.

## Content

This project is hosted in a mono-repo containing the following sub-projects:

* SDK:
    * [ihcclient](ihcclient/README.md) This is the main project that contains the code for the IHC client API. This is the project you will need to reference in your own solutions.
* SDK usage examples:
    * [ihcclient_example1](examples/ihcclient_example1/README.md) contains code for simple command line client console program in c#. Use this for inspiration on how to get started.
    * [ihcclient_example2](examples/ihcclient_example2/README.md) contains code for simple command line client console program in c#. Use this for inspiration on how to get started.
* SDK utilities:
    * [Ihc Lab](utilities/ihc_lab/README.md) contains an experimental cross-platform GUI for calling individual API's.
    * [Program code extractor ](utilities/ihc_project_io_extractor/README.md) contains an optional command line utility for software developers that can generate constant definitions of IO addresses in a concrete IHC installation. Use this approach in your projects if you don't want to lookup and hardcode IO addresses yourself.
    * [IHC Http Proxy recorder](utilities/ihc_httpproxyrecorder/README.md) contains a simple http proxy useful for software (sdk) developers to investigate undocumented IHC controller API's.
    * [IHC Project download/upload](utilities/ihc_project_download_upload/README.md) contains a tool to download/upload project files.
* SDK and utilitty tests:
    * [Safe Lab tests](tests/safe_lab_tests/README.md) contains headless gui tests for Ihc Lab utility. Does not access a controller.
    * [Safe integration tests](tests/safe_integration_tests/README.md) contains system integration tests written in c# that can be safely run aginst a controller in use.
    * [Safe unit tests](tests/safe_unit_tests/README.md) contains  unit tests written in c# that does not access a controller.


## Configuration

The SDK use a ihc_settings.json file to configure IHC controller, logging/telemetry, application setup and tests. Before using the SDK or any utilties/tests/examples 
take a copy the [ihcsettings_template.json](ihcsettings_template.json) into ihcsettings.json in the same directory and fill-in the details of your installation such as endpoint, username, password etc. See also [ihcsettings_example.json](ihcsettings_example.json). 

```json
"ihcclient": {
        "endpoint" : "http://192.100.1.10",
        "userName" : "johndoe",
        "password" : "mypassword",
        "application" : "administrator",
        "logSensitiveData": false,
        "asyncContinueOnCapturedContext": false
},
```

Note: 
* Endpoint should be the http/https baseurl for the controller. If connecting to controller over usb, use endpoint set to "http://usb".
* Username and password should match user setup by controller. Ignored by controller if logging in over usb.
* Application name is can be set to 'treeview', 'openapi', 'administrator'".
* Keep logSensitiveData and asyncContinueOnCapturedContext set to false unless you know what you are doing.

### OpenTelemtry as a logging replacement

As a more powerfull alternative to log files, the SDK (optionally) supports [OpenTelemetry](https://opentelemetry.io/) to view traces. To enable this change ```telemetry``` settings in the config file. The SDK should work with any OpenTel solutions. Below is listed one example.

Note, while the SDK use OpenTelemtry instead of logging. Some utilities/applications using the SDK may still use logging. Therefor the example/template specification for ```ihcssettings.json``` files retains a logging configuration.

#### OpenTelemetry using OpenObserve details

[OpenObserve.ai](https://openobserve.ai) provides a free, self-hosted easy to install OpenTelemetry solution. A single executable be downloaded from [OpenObserve.ai](https://openobserve.ai/downloads/). Select "Open source" version, your OS and download. Once installed and run you should be able to access OpenObserve from [http://localhost:5080](http://localhost:5080). From the menu select Datasource menu, Select Traces and note the Authorization key. Then update ```ihsettings.json``` with the following information but with the placeholder ```<Authorization Key here>``` replaced with your key.

OpenObserver setup: 
```json
 "telemetry": {
      "Host": "http://localhost:5080",
      "Traces": "http://localhost:5080/api/default/v1/traces",
      "Logs": "http://localhost:5080/api/default/v1/logs",
      "Headers": "Authorization=Basic <Authorization Key here>, stream-name=Ihc, organization=default"
    },
```    



## FAQ

**Q**: Do I need to configure my IHC before running the examples or using the API from my own code?  
**A**: Open the standard IHC administrator app from LK, login, click access control and enable localnet or internet access + "Open for thirdparty products". Only enable internet access if you have a firewall and know how to use it securely. 

**Q**: Why does I get a error message 'The configuration file 'ihcsettings.json' was not found and is not optional' when running examples/tests/applications.
**A**: You must create a "ihcsettings.json" file in root folder. Copy the 'ihcsettings_example.json' file to 'ihcsettings.json' and fill out your information.

**Q**: Is there a NuGet package available ?  
**A**: Not yet but might happen later if there is demand. For now just clone to repo and use the sdk project directly.

**Q**: Can I contribute to this project?
**A**: Yes please. See https://docs.github.com/en/get-started/quickstart/contributing-to-projects and https://github.com/mmc41/IHCClientSDK

**Q**: How do I get support?  
**A**: There is no official support for this SDK but you can post issues on the github site. https://github.com/mmc41/IHCClientSDK.
