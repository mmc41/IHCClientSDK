using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using Ihc.Envelope;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Encodings.Web;

namespace Ihc.App
{
    /// <summary>
    /// Base class for IHC Application services
    /// </summary>
    public abstract class AppServiceBase
    {
        protected AppServiceBase()
        {
        }

        protected Activity StartActivity(string operationName)
        {
            Activity activity = Telemetry.ActivitySource.StartActivity(this.GetType().Name + "." + operationName, ActivityKind.Internal);
            activity?.SetTag("service.name", this.GetType().Name); // Set name of IHC webservice highlevel wrapper as telemetry service name.
            activity?.SetTag("service.operation", operationName); // Set name of IHC webservice highlevel wrapper operation.          
            return activity;
        }
    }
}