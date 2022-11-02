using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Win32;
using UnityEditor;
using UnityEditorInternal;
#if UNITY_2017_1_OR_NEWER
using UnityEditor.PackageManager;
#endif

namespace ConsoleTiny
{
    public class ScriptAssetOpener
    {
        private static bool IsNotWindowsEditor()
        {
            return UnityEngine.Application.platform != UnityEngine.RuntimePlatform.WindowsEditor;
        }

        private static string QuotePathIfNeeded(string path)
        {
            if (!path.Contains(" "))
            {
                return path;
            }
            return "\"" + path + "\"";
        }

        public static bool OpenAsset(string file, int line)
        {
            if (string.IsNullOrEmpty(file) || file == "None")
            {
                return false;
            }
            if (file.StartsWith("Assets/", StringComparison.Ordinal))
            {
                var ext = Path.GetExtension(file).ToLower();
                if (ext == ".lua" && TryOpenLuaFile(file, line))
                {
                    return true;
                }

                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(file);
                if (obj)
                {
                    AssetDatabase.OpenAsset(obj, line);
                    return true;
                }

                return false;
            }

            char separatorChar = '\\';
            string fileFullPath;
            if (IsNotWindowsEditor())
            {
                separatorChar = '/';
                fileFullPath = Path.GetFullPath(file);
            }
            else
            {
                fileFullPath = Path.GetFullPath(file.Replace('/', separatorChar));
            }

#if UNITY_2018_1_OR_NEWER
            var packageInfos = Packages.GetAll();
            foreach (var packageInfo in packageInfos)
            {
                if (fileFullPath.StartsWith(packageInfo.resolvedPath, StringComparison.Ordinal))
                {
                    InternalEditorUtility.OpenFileAtLineExternal(fileFullPath, line);
                    return true;
                }
            }
#elif UNITY_2017_1_OR_NEWER
            // TODO
#endif

            // 别人编译的DLL，不存在文件路径，那么就以工程路径拼接组装来尝试获取本地路径
            if (!File.Exists(fileFullPath))
            {
                string directoryName = Directory.GetCurrentDirectory();
                while (true)
                {
                    if (string.IsNullOrEmpty(directoryName) || !Directory.Exists(directoryName))
                    {
                        return false;
                    }

                    int pos = fileFullPath.IndexOf(separatorChar);
                    while (pos != -1)
                    {
                        string testFullPath = Path.Combine(directoryName, fileFullPath.Substring(pos + 1));
                        if (File.Exists(testFullPath) && TryOpenVisualStudioFile(testFullPath, line))
                        {
                            return true;
                        }

                        pos = fileFullPath.IndexOf(separatorChar, pos + 1);
                    }

                    directoryName = Path.GetDirectoryName(directoryName);
                }
            }

            return TryOpenVisualStudioFile(fileFullPath, line);
        }

        private static bool TryOpenVisualStudioFile(string file, int line)
        {
            string dirPath = file;

            do
            {
                dirPath = Path.GetDirectoryName(dirPath);
                if (!string.IsNullOrEmpty(dirPath) && Directory.Exists(dirPath))
                {
                    var files = Directory.GetFiles(dirPath, "*.sln", SearchOption.TopDirectoryOnly);
                    if (files.Length > 0)
                    {
                        OpenVisualStudioFile(files[0], file, line);
                        return true;
                    }
                }
                else
                {
                    break;
                }
            } while (true);

            return false;
        }

        private static void OpenVisualStudioFile(string projectPath, string file, int line)
        {
#if UNITY_2017_1_OR_NEWER
            string vsPath = ScriptEditorUtility.GetExternalScriptEditor();
#else
            string vsPath = InternalEditorUtility.GetExternalScriptEditor();
#endif
            if (IsNotWindowsEditor())
            {
                Process.Start("open", "-a " + QuotePathIfNeeded(vsPath) + " " + QuotePathIfNeeded(file));
                return;
            }

            if (string.IsNullOrEmpty(vsPath) || !File.Exists(vsPath))
            {
                return;
            }
            string exePath = String.Empty;

#if UNITY_2018_1_OR_NEWER
            var packageInfos = Packages.GetAll();
            foreach (var packageInfo in packageInfos)
            {
                if (packageInfo.name == "com.wuhuan.consoletiny")
                {
                    exePath = packageInfo.resolvedPath;
                    break;
                }
            }

#elif UNITY_2017_1_OR_NEWER
            // TODO
            exePath = "../../PackagesCustom/com.wuhuan.consoletiny";
#endif

            if (!string.IsNullOrEmpty(exePath))
            {
                // https://github.com/akof1314/VisualStudioFileOpenTool
                exePath = exePath + "\\Editor\\VisualStudioFileOpenTool.exe";
                if (!File.Exists(exePath))
                {
                    return;
                }

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    OpenVisualStudioFileInter(exePath, vsPath, projectPath, file, line);
                });
            }
        }

        private static void OpenVisualStudioFileInter(string exePath, string vsPath, string projectPath, string file, int line)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = String.Format("{0} {1} {2} {3}",
                    QuotePathIfNeeded(vsPath), QuotePathIfNeeded(projectPath), QuotePathIfNeeded(file), line),
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }

        private const string kLuaScriptEditor = "ConsoleTiny_LuaScriptEditor";

        public static void SetLuaScriptEditor()
        {
            string filePath = EditorUtility.OpenFilePanel("Lua Script Editor", "", InternalEditorUtility.GetApplicationExtensionForRuntimePlatform(UnityEngine.Application.platform));
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            if (IsNotWindowsEditor())
            {
                if (filePath.EndsWith(".app", StringComparison.Ordinal))
                {
                    filePath = Path.Combine(filePath, "Contents/MacOS");
                    var filePaths = Directory.GetFiles(filePath);
                    if (filePaths.Length > 0)
                    {
                        filePath = filePaths[0];
                    }
                }
            }

            EditorPrefs.SetString(kLuaScriptEditor, filePath);
        }

        private static bool TryOpenLuaFile(string file, int line)
        {
            string luaPath = LuaExecutablePath();
            if (string.IsNullOrEmpty(luaPath) || !File.Exists(luaPath))
            {
                return false;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                OpenLuaFileInter(luaPath, file, line);
            });
            return true;
        }

        private static void OpenLuaFileInter(string exePath, string file, int line)
        {
            string arg = string.Format("{0}:{1}", QuotePathIfNeeded(file), line);
            if (exePath.EndsWith("idea.exe", StringComparison.Ordinal) ||
                exePath.EndsWith("idea64.exe", StringComparison.Ordinal) ||
                exePath.EndsWith("idea", StringComparison.Ordinal))
            {
                arg = String.Format("--line {1} {0}", QuotePathIfNeeded(file), line);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arg,
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }

        private static string LuaExecutablePath()
        {
            string path = EditorPrefs.GetString(kLuaScriptEditor, string.Empty);
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }
            if (IsNotWindowsEditor())
            {
                return String.Empty;
            }

            using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FileExts\\.lua\\UserChoice"))
            {
                if (registryKey != null)
                {
                    string val = registryKey.GetValue("Progid") as string;
                    if (!string.IsNullOrEmpty(val))
                    {
                        val = "Software\\Classes\\" + val + "\\shell\\open\\command";
                        using (RegistryKey registryKey2 = Registry.CurrentUser.OpenSubKey(val))
                        {
                            string val3 = LuaExecutablePathInter(registryKey2);
                            if (!string.IsNullOrEmpty(val3))
                            {
                                return val3;
                            }
                        }
                    }
                }
            }

            using (RegistryKey registryKey = Registry.ClassesRoot.OpenSubKey(".lua"))
            {
                if (registryKey != null)
                {
                    string val = registryKey.GetValue(null) as string;
                    if (val != null)
                    {
                        val += "\\shell\\open\\command";
                        using (RegistryKey registryKey2 = Registry.ClassesRoot.OpenSubKey(val))
                        {
                            string val3 = LuaExecutablePathInter(registryKey2);
                            if (!string.IsNullOrEmpty(val3))
                            {
                                return val3;
                            }
                        }
                    }
                }
            }
            return String.Empty;
        }

        private static string LuaExecutablePathInter(RegistryKey registryKey2)
        {
            if (registryKey2 != null)
            {
                string val2 = registryKey2.GetValue(null) as string;
                if (!string.IsNullOrEmpty(val2))
                {
                    string val3 = val2;
                    int pos = val2.IndexOf(" \"", StringComparison.Ordinal);
                    if (pos != -1)
                    {
                        val3 = val2.Substring(0, pos);
                    }

                    val3 = val3.Trim('"');
                    return val3;
                }
            }

            return String.Empty;
        }
    }
}