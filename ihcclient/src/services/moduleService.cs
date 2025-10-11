using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.Module;
using System.Diagnostics;

namespace Ihc {
    /**
    * A highlevel client interface for the IHC ModuleService without any of the soap distractions.
    */
    public interface IModuleService : IIHCService
    {
        /// <summary>
        /// Get information about the scene project on the controller.
        /// </summary>
        public Task<SceneProjectInfo> GetSceneProjectInfo();

        /// <summary>
        /// Get a scene project by name.
        /// </summary>
        /// <param name="name">The name of the scene project to retrieve</param>
        public Task<SceneProject> GetSceneProject(string name);

        /// <summary>
        /// Store a scene project on the controller.
        /// </summary>
        /// <param name="project">The scene project to store</param>
        public Task StoreSceneProject(SceneProject project);

        /// <summary>
        /// Get a specific segment of a scene project (for large projects that need to be downloaded in parts).
        /// </summary>
        /// <param name="name">The name of the scene project</param>
        /// <param name="segmentNumber">The segment number to retrieve</param>
        public Task<SceneProject> GetSceneProjectSegment(string name, int segmentNumber);

        /// <summary>
        /// Store a segment of a scene project (for large projects that need to be uploaded in parts).
        /// </summary>
        /// <param name="projectSegment">The project segment to store</param>
        /// <param name="isFirstSegment">True if this is the first segment</param>
        /// <param name="isLastSegment">True if this is the last segment</param>
        /// <returns>True if the operation was successful</returns>
        public Task<bool> StoreSceneProjectSegment(SceneProject projectSegment, bool isFirstSegment, bool isLastSegment);

        /// <summary>
        /// Clear all scene projects from the controller.
        /// </summary>
        public Task ClearAll();

        /// <summary>
        /// Get the segmentation size used for splitting large scene projects into segments.
        /// </summary>
        /// <returns>The segment size in bytes</returns>
        public Task<int> GetSceneProjectSegmentationSize();
    }

    /**
    * A highlevel implementation of a client to the IHC ModuleService without exposing any of the soap distractions.
    */
    public class ModuleService : ServiceBase, IModuleService {
        private readonly IAuthenticationService authService;

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Module.ModuleService
        {
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, string endpoint, bool logSensitiveData, bool asyncContinueOnCapturedContext) : base(logger, cookieHandler, endpoint, "ModuleService", logSensitiveData, asyncContinueOnCapturedContext) {}

            public Task<outputMessageName6> clearAllAsync(inputMessageName6 request)
            {
                return soapPost<outputMessageName6, inputMessageName6>("clearAll", request);
            }

            public Task<outputMessageName5> getSceneProjectAsync(inputMessageName5 request)
            {
                return soapPost<outputMessageName5, inputMessageName5>("getSceneProject", request);
            }

            public Task<outputMessageName1> getSceneProjectInfoAsync(inputMessageName1 request)
            {
                return soapPost<outputMessageName1, inputMessageName1>("getSceneProjectInfo", request);
            }

            public Task<outputMessageName3> getSceneProjectSegmentAsync(inputMessageName3 request)
            {
                return soapPost<outputMessageName3, inputMessageName3>("getSceneProjectSegment", request);
            }

            public Task<outputMessageName7> getSceneProjectSegmentationSizeAsync(inputMessageName7 request)
            {
                return soapPost<outputMessageName7, inputMessageName7>("getSceneProjectSegmentationSize", request);
            }

            public Task<outputMessageName2> storeSceneProjectAsync(inputMessageName2 request)
            {
                return soapPost<outputMessageName2, inputMessageName2>("storeSceneProject", request);
            }

            public Task<outputMessageName4> storeSceneProjectSegmentAsync(inputMessageName4 request)
            {
                return soapPost<outputMessageName4, inputMessageName4>("storeSceneProjectSegment", request);
            }
        }

        private readonly SoapImpl impl;

        /**
        * Create an ModuleService instance for access to the IHC API related to projects.
        * <param name="authService">AuthenticationService instance</param>
        * <param name="logSensitiveData">If true, log sensitive data. If false (default), redact sensitive values in logs.</param>
        * <param name="asyncContinueOnCapturedContext">If true, continue on captured context after await. If false (default), use ConfigureAwait(false) for better library performance.</param>
        */
        public ModuleService(IAuthenticationService authService, bool logSensitiveData = false, bool asyncContinueOnCapturedContext = false)
            : base(authService.Logger, logSensitiveData, asyncContinueOnCapturedContext)
        {
            this.authService = authService;
            this.impl = new SoapImpl(logger, authService.GetCookieHandler(), authService.Endpoint, logSensitiveData, asyncContinueOnCapturedContext);
        }

        private SceneProject mapSceneProject(Ihc.Soap.Module.WSFile proj)
        {
            return new SceneProject()
            {
                Filename = proj?.filename,
                Data = proj?.data // TODO: Check if binary data can be converet/decompressed to something useful?
            };
        }

        private SceneProjectInfo mapSceneProjectInfo(Ihc.Soap.Module.WSSceneProjectInfo info)
        {
            return info != null ? new SceneProjectInfo()
            {
                Name = info.name,
                Size = info.size,
                Filepath = info.filepath,
                Remote = info.remote,
                Version = info.version,
                Created = info.created?.ToDateTimeOffset().DateTime,
                LastModified = info.lastmodified?.ToDateTimeOffset().DateTime,
                Description = info.description,
                Crc = info.crc
            } : null;
        }

        private Ihc.Soap.Module.WSFile unmapSceneProject(SceneProject proj)
        {
            return new Ihc.Soap.Module.WSFile()
            {
                filename = proj.Filename,
                data = proj.Data
            };
        }

        public async Task<SceneProjectInfo> GetSceneProjectInfo()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var resp = await impl.getSceneProjectInfoAsync(new inputMessageName1() {}).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = mapSceneProjectInfo(resp.getSceneProjectInfo1);

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<SceneProject> GetSceneProject(string name)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(("name", name));

            var resp = await impl.getSceneProjectAsync(new inputMessageName5(name) {}).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = mapSceneProject(resp.getSceneProject2);

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task StoreSceneProject(SceneProject project)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(("project", project));

            await impl.storeSceneProjectAsync(new inputMessageName2(unmapSceneProject(project))).ConfigureAwait(asyncContinueOnCapturedContext);
        }

        public async Task<SceneProject> GetSceneProjectSegment(string name, int segmentNumber)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(("name", name), ("segmentNumber", segmentNumber));

            var resp = await impl.getSceneProjectSegmentAsync(new inputMessageName3(name, segmentNumber)).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = mapSceneProject(resp.getSceneProjectSegment3);

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<bool> StoreSceneProjectSegment(SceneProject projectSegment, bool isFirstSegment, bool isLastSegment)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(("projectSegment", projectSegment), ("isFirstSegment", isFirstSegment), ("isLastSegment", isLastSegment));

            var resp = await impl.storeSceneProjectSegmentAsync(new inputMessageName4(unmapSceneProject(projectSegment), isFirstSegment, isLastSegment)).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = resp.storeSceneProjectSegment4.HasValue ? resp.storeSceneProjectSegment4.Value : false;

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task ClearAll()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            await impl.clearAllAsync(new inputMessageName6() {}).ConfigureAwait(asyncContinueOnCapturedContext);
        }

        public async Task<int> GetSceneProjectSegmentationSize()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var resp = await impl.getSceneProjectSegmentationSizeAsync(new inputMessageName7() {}).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = resp.getSceneProjectSegmentationSize1.HasValue ? resp.getSceneProjectSegmentationSize1.Value : 0;

            activity?.SetReturnValue(retv);
            return retv;
        }

    }
}