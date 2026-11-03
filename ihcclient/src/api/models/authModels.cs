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
    /// IHC Application names for login: the name tells the controller which client is logging in. There is no rule on
    /// which APIs an application can use, but access can differ.
    ///
    /// A login is admitted only if the account's group permits the application and the application's switch in
    /// <see cref="WebAccessControl"/> (see <see cref="IConfigurationService.GetWebAccessControl"/>) is on for the route
    /// the client comes by: USB, the local network (the controller's own IP subnet) or the Internet (any other
    /// address).
    ///
    /// The member names are sent as the application name, so renaming one breaks its login. <c>administrator</c>,
    /// <c>treeview</c>, <c>sceneview</c>, <c>scenedesign</c> and <c>ihcvisual</c> have each logged in on a captured
    /// controller; <c>openapi</c>, the default, appears in no capture or vendor client as an application name, only in
    /// the OpenAPI access-control switches.
    /// </summary>
    public enum Application
    {
        /// <summary>
        /// OpenAPI application. Default. Use this if in doubt. Its access-control switches are
        /// <see cref="WebAccessControl.OpenapiUsb"/>, <see cref="WebAccessControl.OpenapiInternal"/> and
        /// <see cref="WebAccessControl.OpenapiExternal"/>.
        /// </summary>
        openapi,
        /// <summary>
        /// IHC Administrator's application name. Accounts in the <see cref="IhcUserGroup.Users"/> group are refused.
        /// </summary>
        administrator,

        /// <summary>
        /// IHC ServiceView's application name. Accounts in the <see cref="IhcUserGroup.Users"/> group are refused.
        /// </summary>
        treeview,

        /// <summary>
        /// IHC SceneView's application name; accounts of both groups may use it.
        /// </summary>
        sceneview,

        /// <summary>
        /// IHC SceneDesign's application name. Accounts in the <see cref="IhcUserGroup.Users"/> group are refused.
        /// </summary>
        scenedesign,
        /// <summary>
        /// IHC Visual's application name: the project programming tool.
        /// </summary>
        ihcvisual
    }

}