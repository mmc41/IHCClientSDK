using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Module;
using System.Diagnostics;

namespace Ihc {
    /// <summary>
    /// Transfers the scene project of IHC SceneDesign and IHC SceneView (the graphical pages and the e-mail and SMS
    /// messages and controls) to and from the controller, which holds one. It is separate from the IHC project, and
    /// sending an IHC project leaves it in place.
    /// </summary>
    public interface IModuleService : IIHCApiService
    {
        /// <summary>
        /// Describes the scene project the controller holds: its name, size, file path, dates and checksum.
        /// </summary>
        /// <returns>The information, or <c>null</c> when the reply carries none.</returns>
        public Task<SceneProjectInfo?> GetSceneProjectInfo();

        /// <summary>
        /// Downloads the stored scene project in one call.
        /// </summary>
        /// <param name="name">Probably the name <see cref="GetSceneProjectSegment"/> takes: the stored file's name
        /// without its <c>.icz</c> suffix.</param>
        /// <returns>The scene project, or <c>null</c> when the reply carries no file.</returns>
        public Task<SceneProject?> GetSceneProject(string name);

        /// <summary>
        /// Uploads a whole scene project in one call.
        /// </summary>
        /// <param name="project">The scene project file (.icz) to store.</param>
        public Task StoreSceneProject(SceneProject project);

        /// <summary>
        /// Downloads one segment of the stored scene project. Ask for segments 0, 1, 2 and so on, joining their data in
        /// order, until the reply is <c>null</c>; the controller gives no segment count.
        /// </summary>
        /// <param name="name">The file name in <see cref="SceneProjectInfo.Filepath"/> without its <c>.icz</c>
        /// suffix.</param>
        /// <param name="segmentNumber">Zero-based segment index.</param>
        /// <returns>The segment, or <c>null</c> past the last one.</returns>
        public Task<SceneProject?> GetSceneProjectSegment(string name, int segmentNumber);

        /// <summary>
        /// Uploads one segment of a scene project; the upload replaces the project the controller holds. Send slices of
        /// <see cref="GetSceneProjectSegmentationSize"/> bytes in order, the last carrying the remainder.
        /// </summary>
        /// <param name="projectSegment">The segment's bytes, with the project's name as
        /// <see cref="SceneProject.Filename"/>; the controller stores its copy under a fixed file name
        /// regardless.</param>
        /// <param name="isFirstSegment"><c>true</c> for the first segment.</param>
        /// <param name="isLastSegment"><c>true</c> for the last segment; a project that fits in one segment sets both
        /// flags.</param>
        /// <returns><c>true</c> when the controller took the segment; <c>false</c> when it did not or the reply carries
        /// no value; stop the upload at the first <c>false</c>.</returns>
        public Task<bool> StoreSceneProjectSegment(SceneProject projectSegment, bool isFirstSegment, bool isLastSegment);

        /// <summary>
        /// Probably deletes the scene project the controller holds. Unverified: no vendor client calls it, so treat it
        /// as destructive.
        /// </summary>
        public Task ClearAll();

        /// <summary>
        /// Gets the segment size for the scene-project transfers: slice an upload into segments of this many bytes
        /// (5,000 on firmware 3.3.44).
        /// </summary>
        /// <returns>The segment size in bytes; 0 when the reply carries no value.</returns>
        public Task<int> GetSceneProjectSegmentationSize();
    }

    /// <summary>
    /// A highlevel implementation of a client to the IHC ModuleService without exposing any of the soap distractions.
    /// </summary>
    public class ModuleService : ServiceBase, IModuleService {
        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Module.ModuleService
        {
            public SoapImpl(ICookieHandler cookieHandler, IhcSettings settings) : base(cookieHandler, settings, "ModuleService") {}

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

        private readonly Ihc.Soap.Module.ModuleService impl;

        /// <summary>
        /// Create a ModuleService instance for transferring the scene project of IHC SceneDesign and IHC SceneView,
        /// which is separate from the IHC project.
        /// </summary>
        /// <param name="authService">AuthenticationService instance</param>
        public ModuleService(IAuthenticationService authService)
            : base(SettingsOf(authService))
        {
            this.impl = new SoapImpl(authService.GetCookieHandler(), settings);
        }

        /// <summary>Test seam: inject a fake SOAP layer (used by unit tests only).</summary>
        internal ModuleService(IAuthenticationService authService, Ihc.Soap.Module.ModuleService impl)
            : base(SettingsOf(authService))
        {
            this.impl = impl;
        }

        private static SceneProject? mapSceneProject(Ihc.Soap.Module.WSFile? proj)
        {
            if (proj == null)
                return null;

            if (proj.data == null)
            {
                // Substituting an empty array here would be indistinguishable from a genuinely empty scene
                // project, and would travel back to the controller as an empty <data> element on the next
                // store — WSFile.data is not IsNullable, so null omits the element and byte[0] emits it.
                throw new InvalidOperationException(
                    $"The controller returned a scene project entry ('{proj.filename}') carrying no data.");
            }

            // TODO: Check if binary data can be converted/decompressed to something useful?
            return new SceneProject(proj.filename, proj.data);
        }

        private static SceneProjectInfo? mapSceneProjectInfo(Ihc.Soap.Module.WSSceneProjectInfo? info)
        {
            return info != null ? new SceneProjectInfo()
            {
                Name = info.name,
                Size = info.size,
                Filepath = info.filepath,
                Remote = info.remote,
                Version = info.version,
                Created = DateHelper.OrAbsentSentinel(info.created?.ToDateTimeOffset(), nameof(SceneProjectInfo.Created)),
                LastModified = DateHelper.OrAbsentSentinel(info.lastmodified?.ToDateTimeOffset(), nameof(SceneProjectInfo.LastModified)),
                Description = info.description,
                Crc = info.crc
            } : null;
        }

        private static Ihc.Soap.Module.WSFile unmapSceneProject(SceneProject proj)
        {
            return new Ihc.Soap.Module.WSFile()
            {
                filename = proj.Filename,
                data = proj.Data
            };
        }

        public async Task<SceneProjectInfo?> GetSceneProjectInfo()
        {
            using (var activity = StartActivity(nameof(GetSceneProjectInfo)))
            {
                try
                {
                    var resp = await impl.getSceneProjectInfoAsync(new inputMessageName1() {}).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = mapSceneProjectInfo(resp.getSceneProjectInfo1);

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

        public async Task<SceneProject?> GetSceneProject(string name)
        {
            using (var activity = StartActivity(nameof(GetSceneProject)))
            {
                try
                {
                    activity?.SetParameters((nameof(name), name));

                    var resp = await impl.getSceneProjectAsync(new inputMessageName5(name) {}).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = mapSceneProject(resp.getSceneProject2);

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

        public async Task StoreSceneProject(SceneProject project)
        {
            using (var activity = StartActivity(nameof(StoreSceneProject)))
            {
                try
                {
                    activity?.SetParameters((nameof(project), project));

                    ValidationHelper.ValidateDataAnnotations(project, nameof(project));

                    await impl.storeSceneProjectAsync(new inputMessageName2(unmapSceneProject(project))).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<SceneProject?> GetSceneProjectSegment(string name, int segmentNumber)
        {
            using (var activity = StartActivity(nameof(GetSceneProjectSegment)))
            {
                try
                {
                    activity?.SetParameters((nameof(name), name), (nameof(segmentNumber), segmentNumber));

                    var resp = await impl.getSceneProjectSegmentAsync(new inputMessageName3(name, segmentNumber)).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = mapSceneProject(resp.getSceneProjectSegment3);

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

        public async Task<bool> StoreSceneProjectSegment(SceneProject projectSegment, bool isFirstSegment, bool isLastSegment)
        {
            using (var activity = StartActivity(nameof(StoreSceneProjectSegment)))
            {
                try
                {
                    activity?.SetParameters((nameof(projectSegment), projectSegment), (nameof(isFirstSegment), isFirstSegment), (nameof(isLastSegment), isLastSegment));

                    ValidationHelper.ValidateDataAnnotations(projectSegment, nameof(projectSegment));

                    var resp = await impl.storeSceneProjectSegmentAsync(new inputMessageName4(unmapSceneProject(projectSegment), isFirstSegment, isLastSegment)).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.storeSceneProjectSegment4.HasValue ? resp.storeSceneProjectSegment4.Value : false;

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

        public async Task ClearAll()
        {
            using (var activity = StartActivity(nameof(ClearAll)))
            {
                try
                {
                    await impl.clearAllAsync(new inputMessageName6() {}).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<int> GetSceneProjectSegmentationSize()
        {
            using (var activity = StartActivity(nameof(GetSceneProjectSegmentationSize)))
            {
                try
                {
                    var resp = await impl.getSceneProjectSegmentationSizeAsync(new inputMessageName7() {}).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.getSceneProjectSegmentationSize1.HasValue ? resp.getSceneProjectSegmentationSize1.Value : 0;

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