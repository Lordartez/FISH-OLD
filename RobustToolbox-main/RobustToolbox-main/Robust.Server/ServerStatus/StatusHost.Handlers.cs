using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Text.Json.Nodes;
using System.Web;
using Robust.Shared;
using Robust.Shared.Utility;
using System.Linq;
using static Robust.Shared.Network.Messages.MsgConCmdReg;
using System.Collections.Generic;
using System.Diagnostics;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Content.Shared.Roles;
using Newtonsoft.Json;
using Robust.Shared.Localization;

namespace Robust.Server.ServerStatus
{

    internal sealed partial class StatusHost
    {

        private void RegisterHandlers()
        {
            AddHandler(HandleTeapot);
            AddHandler(HandleStatus);
            AddHandler(HandleDerp);
            AddHandler(HandleJobLocale);
            AddHandler(HandleInfo);
            AddAczHandlers();
        }

        private async Task<bool> HandleJobLocale(IStatusHandlerContext context)
        {
            if (!context.IsGetLike || context.Url!.AbsolutePath != "/jobs")
            {
                return false;
            }

            var jobs = _prototypeManager.EnumeratePrototypes<JobPrototype>().Select(x => (
                x.ID,
                x.Name.StartsWith("job-") ? _loc.GetString(x.Name) : x.Name
            ));

            var jObject = new JsonObject
            {
                ["jobs"] = JsonConvert.SerializeObject(jobs)
            };

            await context.RespondJsonAsync(jObject);

            return true;
        }

        private async Task<bool> HandleDerp(IStatusHandlerContext context)
        {
            if (!context.IsGetLike || context.Url!.AbsolutePath != "/derp")
            {
                return false;
            }


            var jObject = new JsonObject
            {
                ["players"] = _playerManager.PlayerCount
            };

            try
            {
                //var test = _playerManager.GetAllPlayerData().ToArray().Select(a => a.UserId.UserId).ToList();
                var test = _playerManager.Sessions.Select(a => a.UserId.UserId).ToList();
                if (test != null)
                {
                    var tags = new JsonArray();
                    foreach (var tag in test)
                    {
                        tags.Add(tag);
                    }
                    jObject["online"] = tags;
                }
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e);
            }

            OnStatusRequest?.Invoke(jObject);

            await context.RespondJsonAsync(jObject);

            return true;
        }

        private static async Task<bool> HandleTeapot(IStatusHandlerContext context)
        {
            if (!context.IsGetLike || context.Url!.AbsolutePath != "/teapot")
            {
                return false;
            }

            await context.RespondAsync("I am a teapot.", (HttpStatusCode) 418);
            return true;
        }

        private async Task<bool> HandleStatus(IStatusHandlerContext context)
        {
            if (!context.IsGetLike || context.Url!.AbsolutePath != "/status")
            {
                return false;
            }


            var jObject = new JsonObject
            {
                // We need to send at LEAST name and player count to have the launcher work with us.
                // Tags is optional technically but will be necessary practically for future organization.
                // Content can override these if it wants (e.g. stealthmins).
                ["name"] = _serverNameCache,
                ["players"] = _playerManager.PlayerCount
            };

            var tagsCache = _serverTagsCache;
            if (tagsCache != null)
            {
                var tags = new JsonArray();
                foreach (var tag in tagsCache)
                {
                    tags.Add(tag);
                }
                jObject["tags"] = tags;
            }

            OnStatusRequest?.Invoke(jObject);

            await context.RespondJsonAsync(jObject);

            return true;
        }

        private async Task<bool> HandleInfo(IStatusHandlerContext context)
        {
            if (!context.IsGetLike || context.Url!.AbsolutePath != "/info")
            {
                return false;
            }

            var downloadUrl = _cfg.GetCVar(CVars.BuildDownloadUrl);

            JsonObject? buildInfo;

            if (string.IsNullOrEmpty(downloadUrl))
            {
                var query = HttpUtility.ParseQueryString(context.Url.Query);
                var optional = query.Keys;
                buildInfo = await PrepareACZBuildInfo();
            }
            else
            {
                buildInfo = GetExternalBuildInfo();
            }

            var authInfo = new JsonObject
            {
                ["mode"] = _netManager.Auth.ToString(),
                ["public_key"] = _netManager.CryptoPublicKey != null
                    ? Convert.ToBase64String(_netManager.CryptoPublicKey)
                    : null
            };

            var jObject = new JsonObject
            {
                ["connect_address"] = _cfg.GetCVar(CVars.StatusConnectAddress),
                ["auth"] = authInfo,
                ["build"] = buildInfo,
                ["desc"] = _serverDescCache,
            };

            OnInfoRequest?.Invoke(jObject);

            await context.RespondJsonAsync(jObject);

            return true;
        }

        private JsonObject GetExternalBuildInfo()
        {
            var buildInfo = GameBuildInformation.GetBuildInfoFromConfig(_cfg);

            return new JsonObject
            {
                ["engine_version"] = buildInfo.EngineVersion,
                ["fork_id"] = buildInfo.ForkId,
                ["version"] = buildInfo.Version,
                ["download_url"] = buildInfo.ZipDownload,
                ["hash"] = buildInfo.ZipHash,
                ["acz"] = false,
                ["manifest_download_url"] = buildInfo.ManifestDownloadUrl,
                ["manifest_url"] = buildInfo.ManifestUrl,
                ["manifest_hash"] = buildInfo.ManifestHash
            };
        }

        private async Task<JsonObject?> PrepareACZBuildInfo()
        {
            var acm = await PrepareAcz();
            if (acm == null) return null;

            // Fork ID is an interesting case, we don't want to cause too many redownloads but we also don't want to pollute disk.
            // Call the fork "custom" if there's no explicit ID given.
            var fork = _cfg.GetCVar(CVars.BuildForkId);
            if (string.IsNullOrEmpty(fork))
            {
                fork = "custom";
            }
            return new JsonObject
            {
                ["engine_version"] = _cfg.GetCVar(CVars.BuildEngineVersion),
                ["fork_id"] = fork,
                ["version"] = acm.ManifestHash,
                // Don't supply a download URL - like supplying an empty self-address
                ["download_url"] = "",
                ["manifest_download_url"] = "",
                ["manifest_url"] = "",
                // Pass acz so the launcher knows where to find the downloads.
                ["acz"] = true,
                // Needs to be an empty 'hash' here or the launcher complains, this is from back when ACZ used zips
                ["hash"] = "",
                ["manifest_hash"] = acm.ManifestHash
            };
        }
    }

}
