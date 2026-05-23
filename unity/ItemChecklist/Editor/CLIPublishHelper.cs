#if PUG_USE_MODIO
using System;
using System.IO;
using System.Text.RegularExpressions;
using ModIO;
using PugMod;
using PugMod.ModIO;
using UnityEditor;
using UnityEngine;

namespace ItemChecklist.Editor
{
    /// <summary>
    /// Editor-only mod.io publish helper, invoked via
    /// <c>unity -batchmode -executeMethod ItemChecklist.Editor.CLIPublishHelper.Publish</c>.
    /// Builds the mod and drives the mod.io plugin to create/update the
    /// profile and upload a modfile. mod.io calls are async, so upload.sh
    /// omits -quit and this class calls EditorApplication.Exit on every path.
    /// </summary>
    public static class CLIPublishHelper
    {
        private const string ModName = "ItemChecklist";
        private const string SettingsPath = "Assets/" + ModName + ".asset";
        // In the mod's Editor/ folder: versioned via the directory symlink,
        // and excluded from builds so ModBuilder never bundles it.
        private const string ModIoSettingsPath =
            "Assets/" + ModName + "/Editor/" + ModName + "_modio.asset";
        private const string LogoAssetPath =
            "Assets/" + ModName + "/Editor/logo.png";

        private static bool _dryRun;
        private static string _version;
        private static string _changelog;
        private static string _buildDir;

        public static void Publish()
        {
            try
            {
                _dryRun = Environment.GetEnvironmentVariable("PUBLISH_DRY_RUN") == "1";

                var repoRoot = Environment.GetEnvironmentVariable("MOD_REPO_ROOT");
                if (string.IsNullOrEmpty(repoRoot)) { Fail("MOD_REPO_ROOT not set"); return; }

                var changelogPath = Path.Combine(repoRoot, "CHANGELOG.md");
                if (!File.Exists(changelogPath))
                {
                    Fail($"No CHANGELOG.md at {changelogPath}"); return;
                }
                if (!TryParseChangelog(File.ReadAllText(changelogPath),
                        out _version, out _changelog))
                {
                    Fail("CHANGELOG.md has no '## [x.y.z]' entry"); return;
                }
                Debug.Log($"[CLIPublishHelper] {ModName} v{_version}"
                          + (_dryRun ? " (dry run)" : ""));

                var settings = AssetDatabase.LoadAssetAtPath<ModBuilderSettings>(SettingsPath);
                if (settings == null) { Fail($"No ModBuilderSettings at {SettingsPath}"); return; }

                _buildDir = Path.Combine(Application.temporaryCachePath,
                    Guid.NewGuid().ToString());
                Directory.CreateDirectory(_buildDir);

                ModBuilder.BuildMod(settings, _buildDir, buildOk =>
                {
                    if (!buildOk) { Fail("Build failed"); return; }
                    OnBuilt();
                }, installInSubDirectory: false);
            }
            catch (Exception e) { Fail($"Exception: {e}"); }
        }

        /// <summary>
        /// Extracts the topmost "## [x.y.z]" entry of a Keep-a-Changelog file:
        /// its version and the body text up to the next "## " header.
        /// Pure — unit-testable in isolation.
        /// </summary>
        public static bool TryParseChangelog(string content, out string version,
            out string changelog)
        {
            version = null;
            changelog = null;
            var header = Regex.Match(content,
                @"^##\s*\[(\d+\.\d+\.\d+)\].*$", RegexOptions.Multiline);
            if (!header.Success) return false;
            version = header.Groups[1].Value;

            int bodyStart = header.Index + header.Length;
            var next = Regex.Match(content.Substring(bodyStart),
                @"^##\s", RegexOptions.Multiline);
            changelog = (next.Success
                ? content.Substring(bodyStart, next.Index)
                : content.Substring(bodyStart)).Trim();
            return true;
        }

        private static void OnBuilt()
        {
            if (!ModIOUnity.IsInitialized())
            {
                var init = ModIOUnity.InitializeForUser("PugModSDKUser");
                if (!init.Succeeded()) { Fail("mod.io init failed"); return; }
            }
            ModIOUnity.IsAuthenticated(auth =>
            {
                if (!auth.Succeeded())
                {
                    Fail("Not authenticated. Log in once via the SDK window's "
                         + "'Log in' tab.");
                    return;
                }
                ResolveSettingsAndPublish();
            });
        }

        private static void ResolveSettingsAndPublish()
        {
            var modIo = AssetDatabase.LoadAssetAtPath<ModSettings>(ModIoSettingsPath);
            var builder = AssetDatabase.LoadAssetAtPath<ModBuilderSettings>(SettingsPath);
            if (builder == null) { Fail($"No ModBuilderSettings at {SettingsPath}"); return; }
            if (modIo == null)
            {
                modIo = ScriptableObject.CreateInstance<ModSettings>();
                modIo.modSettings = builder;
                AssetDatabase.CreateAsset(modIo, ModIoSettingsPath);
                AssetDatabase.SaveAssets();
            }

            var logo = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoAssetPath);
            var summary = Environment.GetEnvironmentVariable("MOD_SUMMARY") ?? "";

            if (modIo.modId == 0)
            {
                if (_dryRun)
                {
                    Debug.Log("[CLIPublishHelper] dry run: would create a new "
                              + "mod profile."
                              + (logo == null ? " (no logo asset yet)" : ""));
                    Succeed();
                    return;
                }
                if (logo == null)
                {
                    Fail($"No logo for new profile — add {LogoAssetPath}");
                    return;
                }
                var token = ModIOUnity.GenerateCreationToken();
                var details = new ModProfileDetails
                {
                    name = builder.metadata.name,
                    summary = summary,
                    logo = logo,
                    visible = false,
                };
                ModIOUnity.CreateModProfile(token, details, created =>
                {
                    if (!created.result.Succeeded())
                    {
                        Fail($"CreateModProfile failed: {created.result.message}");
                        return;
                    }
                    modIo.modId = created.value;
                    EditorUtility.SetDirty(modIo);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[CLIPublishHelper] Created mod.io profile, "
                              + $"id={modIo.modId}");
                    EnsureTagThenUpload(modIo);
                });
            }
            else
            {
                if (_dryRun)
                {
                    Debug.Log($"[CLIPublishHelper] dry run: would update profile "
                              + $"{modIo.modId} and upload v{_version}.");
                    Succeed();
                    return;
                }
                var details = new ModProfileDetails
                {
                    modId = new ModId(modIo.modId),
                    name = builder.metadata.name,
                    summary = summary,
                };
                if (logo != null) details.logo = logo;
                ModIOUnity.EditModProfile(details, edited =>
                {
                    if (!edited.Succeeded())
                    {
                        Fail($"EditModProfile failed: {edited.message}");
                        return;
                    }
                    EnsureTagThenUpload(modIo);
                });
            }
        }

        private static void EnsureTagThenUpload(ModSettings modIo)
        {
            var gameVersion = Environment.GetEnvironmentVariable("CK_GAME_VERSION");
            if (string.IsNullOrEmpty(gameVersion))
            {
                Fail("CK_GAME_VERSION not set"); return;
            }
            ModIOUnity.AddTags(new ModId(modIo.modId), new[] { gameVersion }, tagRes =>
            {
                if (!tagRes.Succeeded())
                {
                    Debug.LogWarning($"[CLIPublishHelper] Could not add version "
                        + $"tag '{gameVersion}': {tagRes.message}. Verify the "
                        + "exact tag value on the mod.io website.");
                }
                Upload(modIo);
            });
        }

        private static void Upload(ModSettings modIo)
        {
            var file = new ModfileDetails
            {
                modId = new ModId(modIo.modId),
                directory = _buildDir,
                version = _version,
                changelog = _changelog,
            };
            ModIOUnity.UploadModfile(file, uploaded =>
            {
                if (!uploaded.Succeeded())
                {
                    Fail($"UploadModfile failed: {uploaded.message}");
                    return;
                }
                Debug.Log($"[CLIPublishHelper] Uploaded {ModName} v{_version}. "
                          + "Review and set the profile visible on mod.io.");
                Succeed();
            });
        }

        private static void Succeed()
        {
            Debug.Log("[CLIPublishHelper] Done.");
            EditorApplication.Exit(0);
        }

        private static void Fail(string message)
        {
            Debug.LogError($"[CLIPublishHelper] {message}");
            EditorApplication.Exit(1);
        }
    }
}
#endif
