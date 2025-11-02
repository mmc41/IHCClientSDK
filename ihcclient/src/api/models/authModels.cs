using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Authentication;
using System.Text;
using System.Runtime.Serialization;
using System.ComponentModel.DataAnnotations;

namespace Ihc
{
    /// <summary>
    /// IHC Application names for login. There is no rule on which API's applications can use but access can differ.
    /// 
    /// Depending on connection method (usb/ethernet) and network (intranet/internet), applications access are allowed/disallowed 
    /// according to WebAccessControl (See IConfigurationService for details).
    ///
    /// Nb. The names are identical to the string representations accepted by the IHC controller. Do not rename!
    /// </summary>
    public enum Application
    {
        /// <summary>
        /// OpenAPI application. Default. Use this if in doubt.
        /// </summary>
        openapi,
        /// <summary>
        /// Administrator application.
        /// </summary>
        administrator,

        /// <summary>
        /// IHC project view application.
        /// </summary>
        treeview,

        /// <summary>
        /// IHC scene view application.
        /// </summary>
        sceneview,

        /// <summary>
        /// IHC scene design project application.
        /// </summary>
        scenedesign,
        /// <summary>
        /// IHC project application.
        /// </summary>
        ihcvisual
    }

}