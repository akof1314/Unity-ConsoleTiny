using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEditorInternal;
#if UNITY_2017_1_OR_NEWER
using UnityEditor.PackageManager;
#endif

namespace ConsoleTiny
{
    public class ScriptAssetOpener
    {
        private Assembly m_Assembly;
        private Type m_ScriptOpenerType;
        private MethodInfo m_WaitForPongFromVisualStudioMi;
        private MethodInfo m_TryOpenFileMi;
        private MethodInfo m_VisualStudioProcessesMi;
        private FieldInfo m_VsProcessFi;
        private MethodInfo m_GetWindowText;
        private MethodInfo m_VisualStudioExecutable;
        //private MethodInfo m_AllowSetForegroundWindow;
        private object m_ScriptOpener;
        private string m_SolutionFile;

        public bool initialized { get { return m_Assembly != null; } }
        public bool alreadyInitialized { get; private set; }

        public void Init(Assembly assembly)
        {
            alreadyInitialized = true;
            if (assembly == null)
            {
                return;
            }

            m_Assembly = assembly;
            if (m_Assembly == null)
            {
                return;
            }
            m_ScriptOpenerType = m_Assembly.GetType("SyntaxTree.VisualStudio.Unity.Bridge.ScriptOpener");
            if (m_ScriptOpenerType == null)
            {
                return;
            }
            m_WaitForPongFromVisualStudioMi = m_ScriptOpenerType.GetMethod("WaitForPongFromVisualStudio", BindingFlags.Public | BindingFlags.Instance);
            m_TryOpenFileMi = m_ScriptOpenerType.GetMethod("TryOpenFile", BindingFlags.Public | BindingFlags.Instance);
            m_VisualStudioProcessesMi = m_ScriptOpenerType.GetMethod("VisualStudioProcesses", BindingFlags.NonPublic | BindingFlags.Static);
            m_VsProcessFi = m_ScriptOpenerType.GetField("_vsProcess", BindingFlags.NonPublic | BindingFlags.Instance);

            Type win32Type = m_Assembly.GetType("SyntaxTree.VisualStudio.Unity.Bridge.Win32");
            m_GetWindowText = win32Type.GetMethod("GetWindowText", BindingFlags.Public | BindingFlags.Static, null,
                new Type[] { typeof(int) }, null);
            //m_AllowSetForegroundWindow = win32Type.GetMethod("AllowSetForegroundWindow", BindingFlags.Public | BindingFlags.Static);

            Type productInfoType = m_Assembly.GetType("SyntaxTree.VisualStudio.Unity.ProductInfo");
            m_VisualStudioExecutable = productInfoType.GetMethod("VisualStudioExecutable", BindingFlags.Public | BindingFlags.Static);
        }

        private bool TryAcquireVisualStudioProcess()
        {
            IEnumerable<Process> processes = (IEnumerable<Process>)m_VisualStudioProcessesMi.Invoke(null, null);
            Process process = processes.FirstOrDefault(IsTargetVisualStudio);
            m_VsProcessFi.SetValue(m_ScriptOpener, process);
            return process != null;
        }

        private bool IsTargetVisualStudio(Process process)
        {
            string title = (string)m_GetWindowText.Invoke(null, new object[] { process.Id });
            if (!string.IsNullOrEmpty(title) && title.StartsWith(Path.GetFileNameWithoutExtension(SolutionFile()), StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        private void StartVisualStudioProcess()
        {
            string path = (string)m_VisualStudioExecutable.Invoke(null, null);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string arguments = QuotePathIfNeeded(SolutionFile());
            Process process = Process.Start(new ProcessStartInfo
            {
                WorkingDirectory = Path.GetFullPath(Path.GetDirectoryName(SolutionFile())),
                UseShellExecute = true,
                CreateNoWindow = true,
                Arguments = arguments,
                FileName = Path.GetFullPath(path)
            });
            m_VsProcessFi.SetValue(m_ScriptOpener, process);
        }

        /// <summary>
        /// sln完整路径
        /// </summary>
        /// <returns></returns>
        private string SolutionFile()
        {
            return m_SolutionFile;
        }

        private static string QuotePathIfNeeded(string path)
        {
            if (!path.Contains(" "))
            {
                return path;
            }
            return "\"" + path + "\"";
        }

        public void OpenEditor(string projectPath, string file, int line)
        {
            m_SolutionFile = projectPath;

            if (m_ScriptOpenerType != null)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    OpenEditorInter(projectPath, file, line);
                });
            }
            else
            {
#if UNITY_2017_1_OR_NEWER
                string vsPath = ScriptEditorUtility.GetExternalScriptEditor();
#else
                string vsPath = InternalEditorUtility.GetExternalScriptEditor();
#endif
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
                    exePath = exePath + "\\Editor\\VisualStudioFileOpenTool.exe";

                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        OpenEditorInter2(exePath, vsPath, projectPath, file, line);
                    });
                }
            }
        }

        private void OpenEditorInter(string projectPath, string file, int line)
        {
            m_ScriptOpener = Activator.CreateInstance(m_ScriptOpenerType, projectPath, file, line - 1);
            try
            {
                if (!TryAcquireVisualStudioProcess())
                {
                    StartVisualStudioProcess();
                }
                if ((bool)m_WaitForPongFromVisualStudioMi.Invoke(m_ScriptOpener, null))
                {
                    m_TryOpenFileMi.Invoke(m_ScriptOpener, null);
                }
            }
            finally
            {
                if (m_ScriptOpener != null)
                {
                    ((IDisposable)m_ScriptOpener).Dispose();
                }
                m_ScriptOpener = null;
            }
        }

        private void OpenEditorInter2(string exePath, string vsPath, string projectPath, string file, int line)
        {
            if (!File.Exists(exePath))
            {
                return;
            }
            
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = String.Format("{0} {1} {2} {3}",
                    QuotePathIfNeeded(vsPath), QuotePathIfNeeded(projectPath), QuotePathIfNeeded(file), line),
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }

        private static ScriptAssetOpener sao;

        private static void LoadScriptAssetOpener()
        {
            if (sao == null)
            {
                sao = new ScriptAssetOpener();
            }
            if (sao.initialized || sao.alreadyInitialized)
            {
                return;
            }

            sao.Init(null);
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (a.FullName.StartsWith("SyntaxTree.VisualStudio.Unity.Bridge"))
                {
                    sao.Init(a);
                    break;
                }
            }
        }

        public static bool OpenAsset(string file, int line)
        {
            if (string.IsNullOrEmpty(file) || file == "None")
            {
                return false;
            }
            if (file.StartsWith("Assets/"))
            {
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(file);
                if (obj)
                {
                    AssetDatabase.OpenAsset(obj, line);
                }
                return false;
            }

            string fileFullPath = Path.GetFullPath(file.Replace('/', '\\'));

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

            LoadScriptAssetOpener();
            if (!sao.initialized)
            {
                return false;
            }

            string dirPath = fileFullPath;

            do
            {
                dirPath = Path.GetDirectoryName(dirPath);
                if (!string.IsNullOrEmpty(dirPath) && Directory.Exists(dirPath))
                {
                    var files = Directory.GetFiles(dirPath, "*.sln", SearchOption.TopDirectoryOnly);
                    if (files.Length > 0)
                    {
                        sao.OpenEditor(files[0], fileFullPath, line);
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
    }
}