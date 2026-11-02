using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Testihc;
using System.Diagnostics;

namespace Ihc
{
    /// <summary>
    /// The controller's factory-test service: versions and identities of its boards, and tests of its LEDs, SD card,
    /// I/O board, USB host and RS-485 port. Do not use it on an installation in use: some of its operations may be
    /// dangerous, so those deemed most dangerous are disabled unless
    /// <see cref="IhcSettings.AllowDangerousInternTestCalls"/> is <c>true</c>.
    /// </summary>
    public interface IInternalTestService : IIHCApiService
    {
        // Each version/identity getter below returns null when the controller reports none: their
        // wire elements are declared nillable and the generated layer is oblivious, so a non-null
        // return type here would be a promise this tier cannot keep.
        /// <summary>
        /// Gets the version string of the Airlink radio, the controller's wireless module.
        /// </summary>
        /// <returns>The version, or <c>null</c> when the reply carries none.</returns>
        Task<string?> GetAirlinkVersion();

        /// <summary>
        /// Gets the version string of the I/O board, the controller's dataline board.
        /// </summary>
        /// <returns>The version, or <c>null</c> when the reply carries none.</returns>
        Task<string?> GetIOBoardVersion();

        /// <summary>
        /// Gets the version string of the Wiser board, probably the controller's main board.
        /// </summary>
        /// <returns>The version, or <c>null</c> when the reply carries none.</returns>
        Task<string?> GetWiserBoardVersion();

        /// <summary>
        /// Gets the MAC address of the Wiser board.
        /// </summary>
        /// <returns>The MAC address, or <c>null</c> when the reply carries none.</returns>
        Task<string?> GetWiserBoardMACAddress();

        /// <summary>
        /// Gets the hardware version string of the Wiser board.
        /// </summary>
        /// <returns>The version, or <c>null</c> when the reply carries none.</returns>
        Task<string?> GetWiserBoardHWVersion();

        /// <summary>
        /// Gets the serial number of the Wiser board.
        /// </summary>
        /// <returns>The serial number, or <c>null</c> when the reply carries none.</returns>
        Task<string?> GetWiserBoardSerialNumber();

        /// <summary>
        /// Probably makes the controller send a packet over its Airlink radio; the request carries no data.
        /// </summary>
        Task SendAirlinkPacket();

        /// <summary>
        /// Marks the production test as passed.
        /// </summary>
        /// <remarks>
        /// WARNING: This operation may be dangerous and is disabled by default.
        /// Set <see cref="IhcSettings.AllowDangerousInternTestCalls"/> to <c>true</c> to enable this method.
        /// </remarks>
        Task ProductionTestPassed();

        /// <summary>
        /// Gets the controller's time and date through the test service. The SDK takes the wire's <c>long</c> as
        /// milliseconds since 1970 UTC, an unconfirmed unit, and returns <c>DateTimeOffset.MinValue</c> when the
        /// reply carries no value.
        /// </summary>
        Task<DateTimeOffset> GetTimeAndDate();

        /// <summary>
        /// Sets the controller's time and date through the test service. The SDK sends the value as milliseconds since
        /// 1970 UTC, an unconfirmed unit.
        /// </summary>
        /// <param name="dateTime">The date and time to set; any offset is converted to UTC.</param>
        Task SetTimeAndDate(DateTimeOffset dateTime);

        /// <summary>
        /// Probably writes (burns) the I/O board's firmware; the request carries no image.
        /// </summary>
        /// <remarks>
        /// WARNING: This operation may be dangerous and is disabled by default.
        /// Set <see cref="IhcSettings.AllowDangerousInternTestCalls"/> to <c>true</c> to enable this method.
        /// </remarks>
        Task<bool> BurnIO();

        /// <summary>
        /// Tests the SD card.
        /// </summary>
        /// <remarks>
        /// WARNING: This operation may be dangerous and is disabled by default.
        /// Set <see cref="IhcSettings.AllowDangerousInternTestCalls"/> to <c>true</c> to enable this method.
        /// </remarks>
        Task<bool> TestSdCard();

        /// <summary>
        /// Turns on the red LED.
        /// </summary>
        Task TurnOnRedLed();

        /// <summary>
        /// Turns off the red LED.
        /// </summary>
        Task TurnOffRedLed();

        /// <summary>
        /// Turns on the green LED.
        /// </summary>
        Task TurnOnGreenLed();

        /// <summary>
        /// Turns off the green LED.
        /// </summary>
        Task TurnOffGreenLed();

        /// <summary>
        /// Turns on the blue LED.
        /// </summary>
        Task TurnOnBlueLed();

        /// <summary>
        /// Turns off the blue LED.
        /// </summary>
        Task TurnOffBlueLed();

        /// <summary>
        /// Turns on all LEDs.
        /// </summary>
        Task TurnOnLeds();

        /// <summary>
        /// Tests the I/O board.
        /// </summary>
        /// <remarks>
        /// WARNING: This operation may be dangerous and is disabled by default.
        /// Set <see cref="IhcSettings.AllowDangerousInternTestCalls"/> to <c>true</c> to enable this method.
        /// </remarks>
        Task<bool> TestIOBoard();

        /// <summary>
        /// Reads the USB host status.
        /// </summary>
        Task<bool> ReadUsbHost();

        /// <summary>
        /// Sends data over the RS-485 port.
        /// </summary>
        /// <param name="data">The data to send.</param>
        /// <remarks>
        /// WARNING: This operation may be dangerous and is disabled by default.
        /// Set <see cref="IhcSettings.AllowDangerousInternTestCalls"/> to <c>true</c> to enable this method.
        /// </remarks>
        Task<bool> SendRS485Data(string data);

        /// <summary>
        /// Reads data from the RS-485 port.
        /// </summary>
        /// <param name="data">Passed to the controller as-is; its meaning is not documented.</param>
        /// <remarks>
        /// WARNING: This operation may be dangerous and is disabled by default.
        /// Set <see cref="IhcSettings.AllowDangerousInternTestCalls"/> to <c>true</c> to enable this method.
        /// </remarks>
        Task<bool> ReadRS485Data(string data);
    }

    public class InternalTestService : ServiceBase, IInternalTestService
    {
        private class SoapImpl : ServiceBaseImpl, TestIhcService
        {
            public SoapImpl(ICookieHandler cookieHandler, IhcSettings settings) : base(cookieHandler, settings, "TestIhcService") { }

            public Task<outputMessageName1> getAirlinkVersionAsync(inputMessageName1 request)
            {
                return soapPost<outputMessageName1, inputMessageName1>("getAirlinkVersion", request);
            }

            public Task<outputMessageName2> getIOBoardVersionAsync(inputMessageName2 request)
            {
                return soapPost<outputMessageName2, inputMessageName2>("getIOBoardVersion", request);
            }

            public Task<outputMessageName3> getWiserBoardVersionAsync(inputMessageName3 request)
            {
                return soapPost<outputMessageName3, inputMessageName3>("getWiserBoardVersion", request);
            }

            public Task<outputMessageName4> getWiserBoardMACAddressAsync(inputMessageName4 request)
            {
                return soapPost<outputMessageName4, inputMessageName4>("getWiserBoardMACAddress", request);
            }

            public Task<outputMessageName5> getWiserBoardHWVersionAsync(inputMessageName5 request)
            {
                return soapPost<outputMessageName5, inputMessageName5>("getWiserBoardHWVersion", request);
            }

            public Task<outputMessageName6> getWiserBoardSerialNumberAsync(inputMessageName6 request)
            {
                return soapPost<outputMessageName6, inputMessageName6>("getWiserBoardSerialNumber", request);
            }

            public Task<outputMessageName7> sendAirlinkPacketAsync(inputMessageName7 request)
            {
                return soapPost<outputMessageName7, inputMessageName7>("sendAirlinkPacket", request);
            }

            public Task<outputMessageName8> productionTestPassedAsync(inputMessageName8 request)
            {
                return soapPost<outputMessageName8, inputMessageName8>("productionTestPassed", request);
            }

            public Task<outputMessageName9> getTimeAndDateAsync(inputMessageName9 request)
            {
                return soapPost<outputMessageName9, inputMessageName9>("getTimeAndDate", request);
            }

            public Task<outputMessageName10> setTimeAndDateAsync(inputMessageName10 request)
            {
                return soapPost<outputMessageName10, inputMessageName10>("setTimeAndDate", request);
            }

            public Task<outputMessageName11> burnIOAsync(inputMessageName11 request)
            {
                return soapPost<outputMessageName11, inputMessageName11>("burnIO", request);
            }

            public Task<outputMessageName12> testSdCardAsync(inputMessageName12 request)
            {
                return soapPost<outputMessageName12, inputMessageName12>("testSdCard", request);
            }

            public Task<outputMessageName13> turnOnRedLedAsync(inputMessageName13 request)
            {
                return soapPost<outputMessageName13, inputMessageName13>("turnOnRedLed", request);
            }

            public Task<outputMessageName14> turnOffRedLedAsync(inputMessageName14 request)
            {
                return soapPost<outputMessageName14, inputMessageName14>("turnOffRedLed", request);
            }

            public Task<outputMessageName15> turnOnGreenLedAsync(inputMessageName15 request)
            {
                return soapPost<outputMessageName15, inputMessageName15>("turnOnGreenLed", request);
            }

            public Task<outputMessageName16> turnOffGreenLedAsync(inputMessageName16 request)
            {
                return soapPost<outputMessageName16, inputMessageName16>("turnOffGreenLed", request);
            }

            public Task<outputMessageName17> turnOnBlueLedAsync(inputMessageName17 request)
            {
                return soapPost<outputMessageName17, inputMessageName17>("turnOnBlueLed", request);
            }

            public Task<outputMessageName18> turnOffBlueLedAsync(inputMessageName18 request)
            {
                return soapPost<outputMessageName18, inputMessageName18>("turnOffBlueLed", request);
            }

            public Task<outputMessageName19> turnOnLedsAsync(inputMessageName19 request)
            {
                return soapPost<outputMessageName19, inputMessageName19>("turnOnLeds", request);
            }

            public Task<outputMessageName20> testIOBoardAsync(inputMessageName20 request)
            {
                return soapPost<outputMessageName20, inputMessageName20>("testIOBoard", request);
            }

            public Task<outputMessageName21> readUsbHostAsync(inputMessageName21 request)
            {
                return soapPost<outputMessageName21, inputMessageName21>("readUsbHost", request);
            }

            public Task<outputMessageName22> sendRS485DataAsync(inputMessageName22 request)
            {
                return soapPost<outputMessageName22, inputMessageName22>("sendRS485Data", request);
            }

            public Task<outputMessageName23> readRS485DataAsync(inputMessageName23 request)
            {
                return soapPost<outputMessageName23, inputMessageName23>("readRS485Data", request);
            }
        }

        private readonly TestIhcService impl;

        /// <summary>
        /// Create an InternalTestService instance for access to the IHC API related to internal testing.
        /// </summary>
        /// <param name="authService">AuthenticationService instance</param>
        public InternalTestService(IAuthenticationService authService)
            : base(SettingsOf(authService))
        {
            this.impl = new SoapImpl(authService.GetCookieHandler(), settings);
        }

        /// <summary>Test seam: inject a fake SOAP layer (used by unit tests only).</summary>
        internal InternalTestService(IAuthenticationService authService, TestIhcService impl)
            : base(SettingsOf(authService))
        {
            this.impl = impl;
        }

        public async Task<string?> GetAirlinkVersion()
        {
            using (var activity = StartActivity(nameof(GetAirlinkVersion)))
            {
                try
                {
                    var resp = await impl.getAirlinkVersionAsync(new inputMessageName1()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.getAirlinkVersion1;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<string?> GetIOBoardVersion()
        {
            using (var activity = StartActivity(nameof(GetIOBoardVersion)))
            {
                try
                {
                    var resp = await impl.getIOBoardVersionAsync(new inputMessageName2()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.getIOBoardVersion1;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<string?> GetWiserBoardVersion()
        {
            using (var activity = StartActivity(nameof(GetWiserBoardVersion)))
            {
                try
                {
                    var resp = await impl.getWiserBoardVersionAsync(new inputMessageName3()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.getWiserBoardVersion1;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<string?> GetWiserBoardMACAddress()
        {
            using (var activity = StartActivity(nameof(GetWiserBoardMACAddress)))
            {
                try
                {
                    var resp = await impl.getWiserBoardMACAddressAsync(new inputMessageName4()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.getWiserBoardMACAddress1;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<string?> GetWiserBoardHWVersion()
        {
            using (var activity = StartActivity(nameof(GetWiserBoardHWVersion)))
            {
                try
                {
                    var resp = await impl.getWiserBoardHWVersionAsync(new inputMessageName5()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.getWiserBoardHWVersion1;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<string?> GetWiserBoardSerialNumber()
        {
            using (var activity = StartActivity(nameof(GetWiserBoardSerialNumber)))
            {
                try
                {
                    var resp = await impl.getWiserBoardSerialNumberAsync(new inputMessageName6()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.getWiserBoardSerialNumber1;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task SendAirlinkPacket()
        {
            using (var activity = StartActivity(nameof(SendAirlinkPacket)))
            {
                try
                {
                    await impl.sendAirlinkPacketAsync(new inputMessageName7()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task ProductionTestPassed()
        {
            using (var activity = StartActivity(nameof(ProductionTestPassed)))
            {                
                try
                {
                    if (!settings.AllowDangerousInternTestCalls)
                        throw new InvalidOperationException("Internal call is disabled in settings. Enable 'allowDangerousInternTestCalls' to use this method.");
                
                    await impl.productionTestPassedAsync(new inputMessageName8()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<DateTimeOffset> GetTimeAndDate()
        {
            using (var activity = StartActivity(nameof(GetTimeAndDate)))
            {
                try
                {
                    var resp = await impl.getTimeAndDateAsync(new inputMessageName9()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var result = resp.getTimeAndDate1;
                    var retv = DateHelper.OrAbsentSentinel(
                        result.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(result.Value) : null,
                        nameof(GetTimeAndDate));

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task SetTimeAndDate(DateTimeOffset dateTime)
        {
            using (var activity = StartActivity(nameof(SetTimeAndDate)))
            {
                try
                {
                    activity?.SetParameters((nameof(dateTime), dateTime));

                    var timestamp = dateTime.ToUnixTimeMilliseconds();
                    await impl.setTimeAndDateAsync(new inputMessageName10 { setTimeAndDate1 = timestamp }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<bool> BurnIO()
        {
            using (var activity = StartActivity(nameof(BurnIO)))
            {
                try
                {
                    if (!settings.AllowDangerousInternTestCalls)
                        throw new InvalidOperationException("Internal call is disabled in settings. Enable 'allowDangerousInternTestCalls' to use this method.");
                        
                    var resp = await impl.burnIOAsync(new inputMessageName11()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.burnIO1 ?? false;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<bool> TestSdCard()
        {
            using (var activity = StartActivity(nameof(TestSdCard)))
            {
                try
                {
                    if (!settings.AllowDangerousInternTestCalls)
                        throw new InvalidOperationException("Internal call is disabled in settings. Enable 'allowDangerousInternTestCalls' to use this method.");
                        
                    var resp = await impl.testSdCardAsync(new inputMessageName12()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.testSdCard1 ?? false;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task TurnOnRedLed()
        {
            using (var activity = StartActivity(nameof(TurnOnRedLed)))
            {
                try
                {
                    await impl.turnOnRedLedAsync(new inputMessageName13()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task TurnOffRedLed()
        {
            using (var activity = StartActivity(nameof(TurnOffRedLed)))
            {
                try
                {
                    await impl.turnOffRedLedAsync(new inputMessageName14()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task TurnOnGreenLed()
        {
            using (var activity = StartActivity(nameof(TurnOnGreenLed)))
            {
                try
                {
                    await impl.turnOnGreenLedAsync(new inputMessageName15()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task TurnOffGreenLed()
        {
            using (var activity = StartActivity(nameof(TurnOffGreenLed)))
            {
                try
                {
                    await impl.turnOffGreenLedAsync(new inputMessageName16()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task TurnOnBlueLed()
        {
            using (var activity = StartActivity(nameof(TurnOnBlueLed)))
            {
                try
                {
                    await impl.turnOnBlueLedAsync(new inputMessageName17()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task TurnOffBlueLed()
        {
            using (var activity = StartActivity(nameof(TurnOffBlueLed)))
            {
                try
                {
                    await impl.turnOffBlueLedAsync(new inputMessageName18()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task TurnOnLeds()
        {
            using (var activity = StartActivity(nameof(TurnOnLeds)))
            {
                try
                {
                    await impl.turnOnLedsAsync(new inputMessageName19()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<bool> TestIOBoard()
        {
            using (var activity = StartActivity(nameof(TestIOBoard)))
            {
                try
                {
                    if (!settings.AllowDangerousInternTestCalls)
                        throw new InvalidOperationException("Internal call is disabled in settings. Enable 'allowDangerousInternTestCalls' to use this method.");
                        
                    var resp = await impl.testIOBoardAsync(new inputMessageName20()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.testIOBoard1 ?? false;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<bool> ReadUsbHost()
        {
            using (var activity = StartActivity(nameof(ReadUsbHost)))
            {
                try
                {
                    var resp = await impl.readUsbHostAsync(new inputMessageName21()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.readUsbHost1 ?? false;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<bool> SendRS485Data(string data)
        {
            using (var activity = StartActivity(nameof(SendRS485Data)))
            {
                try
                {
                    if (!settings.AllowDangerousInternTestCalls)
                        throw new InvalidOperationException("Internal call is disabled in settings. Enable 'allowDangerousInternTestCalls' to use this method.");
                        
                    activity?.SetParameters((nameof(data), data));

                    var resp = await impl.sendRS485DataAsync(new inputMessageName22 { sendRS485Data1 = data }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.sendRS485Data2 ?? false;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<bool> ReadRS485Data(string data)
        {
            using (var activity = StartActivity(nameof(ReadRS485Data)))
            {
                try
                {
                    if (!settings.AllowDangerousInternTestCalls)
                        throw new InvalidOperationException("Internal call is disabled in settings. Enable 'allowDangerousInternTestCalls' to use this method.");
                        
                    activity?.SetParameters((nameof(data), data));

                    var resp = await impl.readRS485DataAsync(new inputMessageName23 { readRS485Data1 = data }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.readRS485Data2 ?? false;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }
    }
}