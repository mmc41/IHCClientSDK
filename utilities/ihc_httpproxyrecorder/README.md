# HTTP/HTTPS Proxy Recorder for IHC.

A simple HTTP/HTTPS proxy server used to capture traffic to a USB connected IHC controller. It is useful to investigate how existing applications
use the undocumented IHC controller API's. Mostly of use for internal SDK development.

Nb. This recorder works with Windows PC only because it assumes the interface 'http://usb' which in turn requires a Windows USB HTTP driver that Schneider Electric has released only for Windows.

To use:
1) Connect the IHC controller to a USB port on the Windows PC that runs this proxy.
2) Configure the IHC software that you want to capture trafic to/from to http://localhost:5083
3) Run this proxy

## Prerequisites

- .NET 9.0 SDK

## Installation

1. Clone the repository
2. Navigate to the project directory
3. Restore dependencies:
   ```bash
   dotnet restore
   ```
4. Trust the development certificate for HTTPS !!
   ```bash
   dotnet dev-certs https --trust
   ```

## Usage

### Basic Usage

Run the proxy server with default settings:
```bash
dotnet run
```

The proxy will start on:
- HTTP: `http://localhost:5082`
- HTTPS: `https://localhost:5083` (with SSL termination)

Both endpoints forward all requests to `http://usb`.

All captured traffic is automatically logged to a `capture.log` file with timestamps. Use Ctrl+C to stop the proxy, which will ensure the log file is properly closed and flushed.

### Enable Header Logging

To include HTTP headers in the console output, you can either:

1. Set the environment variable:
   ```bash
   LOG_HEADERS=true dotnet run
   ```

2. Or modify `appsettings.json`:
   ```json
   {
     "LogHeaders": true
   }
   ```

## Console Output

The proxy logs all traffic to both console and `capture.log` file with:
- **Correlation ID**: Sequential integer for matching requests with responses
- **Request details**: HTTP method, URL, and body
- **Response details**: Status code, reason phrase, and body
- **Headers**: Optionally displayed when header logging is enabled
- **Timestamps**: Added to log file entries for precise timing

Example output:
```
HTTP/HTTPS Proxy Server starting...
Proxy endpoints:
  HTTP:  http://localhost:5082
  HTTPS: https://localhost:5083 (SSL termination)
Forwarding all traffic to: http://usb
Header logging: DISABLED (set LOG_HEADERS=true to enable)
Logging to file: capture.log
----------------------------------------
Ready to receive requests...

[1] ====== REQUEST ======
[1] POST http://usb/api/data
[1] Body: {"test": "data"}

[1] ====== RESPONSE ======
[1] Status: 200 OK
[1] Body: {"result": "success"}
```

## Configuration

- **Ports**:
  - HTTP: 5082
  - HTTPS: 5083 (with SSL termination)
- **Target**: http://usb (all requests are forwarded here)
- **Environment**: Development mode by default
- **SSL Certificate**: Uses ASP.NET Core development certificate

## Build

To build the project:
```bash
dotnet build
```

To clean build artifacts:
```bash
dotnet clean
```
