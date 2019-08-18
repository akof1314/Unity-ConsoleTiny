using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
#if UNITY_2017_1_OR_NEWER
using CoreLog = UnityEditor;
#else
using CoreLog = UnityEditorInternal;
#endif

namespace ConsoleTiny
{
    public class LogEntries
    {
        public static void Clear()
        {
            CoreLog.LogEntries.Clear();
        }

        internal static EntryWrapped wrapped = new EntryWrapped();

        internal class EntryWrapped
        {
            private class EntryInfo
            {
                public int row;
                public string lines;
                public string text;
                public string pure; // remove tag
                public string lower;
                public int entryCount;
                public int searchIndex;
                public int searchEndIndex;
                public ConsoleFlags flags;
                public CoreLog.LogEntry entry;
                public List<StacktraceLineInfo> stacktraceLineInfos;
                public List<int> tagPosInfos;
            }

            [Flags]
            enum Mode
            {
                Error = 1 << 0,
                Assert = 1 << 1,
                Log = 1 << 2,
                Fatal = 1 << 4,
                DontPreprocessCondition = 1 << 5,
                AssetImportError = 1 << 6,
                AssetImportWarning = 1 << 7,
                ScriptingError = 1 << 8,
                ScriptingWarning = 1 << 9,
                ScriptingLog = 1 << 10,
                ScriptCompileError = 1 << 11,
                ScriptCompileWarning = 1 << 12,
                StickyError = 1 << 13,
                MayIgnoreLineNumber = 1 << 14,
                ReportBug = 1 << 15,
                DisplayPreviousErrorInStatusBar = 1 << 16,
                ScriptingException = 1 << 17,
                DontExtractStacktrace = 1 << 18,
                ShouldClearOnPlay = 1 << 19,
                GraphCompileError = 1 << 20,
                ScriptingAssertion = 1 << 21,
                VisualScriptingError = 1 << 22
            };

            enum ConsoleFlags
            {
                Collapse = 1 << 0,
                LogLevelLog = 1 << 7,
                LogLevelWarning = 1 << 8,
                LogLevelError = 1 << 9,
                ShowTimestamp = 1 << 10,
            };

            public int numberOfLines
            {
                private get { return m_NumberOfLines; }
                set { m_NumberOfLines = value; ResetEntriesForNumberLines(); }
            }

            public bool showTimestamp
            {
                get { return m_ShowTimestamp; }
                set
                {
                    m_ShowTimestamp = value;
                    EditorPrefs.SetBool(kPrefShowTimestamp, value);
                    ResetEntriesForNumberLines();
                }
            }

            public bool collapse
            {
                get { return m_Collapse; }
                set
                {
                    if (m_Collapse != value)
                    {
                        m_Collapse = value;
                        EditorPrefs.SetBool(kPrefCollapse, value);
                        ClearEntries();
                        UpdateEntries();
                    }
                }
            }

            public string searchString
            {
                get { return m_SearchString; }
                set { m_SearchStringComing = value; }
            }

            public string[] searchHistory = new[] { "" };

            public bool searchFrame { get; set; }

            private int m_ConsoleFlags;
            private int m_ConsoleFlagsComing;
            private string m_SearchString;
            private string m_SearchStringComing;
            private double m_LastSearchStringTime;

            private bool m_Init;
            private int m_NumberOfLines;
            private bool m_ShowTimestamp;
            private bool m_Collapse;
            private int[] m_TypeCounts = new[] { 0, 0, 0 };
            private int m_LastEntryCount = -1;
            private EntryInfo m_SelectedInfo;
            private readonly List<EntryInfo> m_EntryInfos = new List<EntryInfo>();
            private readonly List<EntryInfo> m_FilteredInfos = new List<EntryInfo>();
            private readonly CustomFiltersGroup m_CustomFilters = new CustomFiltersGroup();

            private const string kPrefConsoleFlags = "ConsoleTiny_ConsoleFlags";
            private const string kPrefShowTimestamp = "ConsoleTiny_ShowTimestamp";
            private const string kPrefCollapse = "ConsoleTiny_Collapse";
            private const string kPrefCustomFilters = "ConsoleTiny_CustomFilters";

            public bool HasFlag(int flags) { return (m_ConsoleFlags & flags) != 0; }

            public void SetFlag(int flags, bool val) { SetConsoleFlag(flags, val); }

            private void SetConsoleFlag(int bit, bool value)
            {
                if (value)
                {
                    m_ConsoleFlagsComing |= bit;
                }
                else
                {
                    m_ConsoleFlagsComing &= ~bit;
                }
            }

            public int GetCount()
            {
                return m_FilteredInfos.Count;
            }

            public string GetEntryLinesAndFlagAndCount(int row, ref int consoleFlag, ref int entryCount, ref int searchIndex, ref int searchEndIndex)
            {
                if (row < 0 || row >= m_FilteredInfos.Count)
                {
                    return String.Empty;
                }

                EntryInfo entryInfo = m_FilteredInfos[row];
                consoleFlag = (int)entryInfo.flags;
                entryCount = entryInfo.entryCount;
                searchIndex = entryInfo.searchIndex;
                searchEndIndex = entryInfo.searchEndIndex;
                return entryInfo.text;
            }

            public void GetCountsByType(ref int errorCount, ref int warningCount, ref int logCount)
            {
                errorCount = m_TypeCounts[0];
                warningCount = m_TypeCounts[1];
                logCount = m_TypeCounts[2];
            }

            public int SetSelectedEntry(int row)
            {
                m_SelectedInfo = null;
                if (row < 0 || row >= m_FilteredInfos.Count)
                {
                    return 0;
                }

                m_SelectedInfo = m_FilteredInfos[row];
                return m_SelectedInfo.entry.instanceID;
            }

            public bool IsEntrySelected(int row)
            {
                if (row < 0 || row >= m_FilteredInfos.Count)
                {
                    return false;
                }

                return m_FilteredInfos[row] == m_SelectedInfo;
            }

            public bool IsSelectedEntryShow()
            {
                if (m_SelectedInfo != null)
                {
                    return m_FilteredInfos.Contains(m_SelectedInfo);
                }
                return false;
            }

            public int GetSelectedEntryIndex()
            {
                if (m_SelectedInfo != null)
                {
                    for (int i = 0; i < m_FilteredInfos.Count; i++)
                    {
                        if (m_FilteredInfos[i] == m_SelectedInfo)
                        {
                            return i;
                        }
                    }
                }
                return -1;
            }

            public int GetFirstErrorEntryIndex()
            {
                for (int i = 0; i < m_FilteredInfos.Count; i++)
                {
                    if (m_FilteredInfos[i].flags == ConsoleFlags.LogLevelError)
                    {
                        return i;
                    }
                }
                return -1;
            }

            public void UpdateEntries()
            {
                CheckInit();
                int flags = CoreLog.LogEntries.consoleFlags;
                CoreLog.LogEntries.SetConsoleFlag((int)ConsoleFlags.LogLevelLog, true);
                CoreLog.LogEntries.SetConsoleFlag((int)ConsoleFlags.LogLevelWarning, true);
                CoreLog.LogEntries.SetConsoleFlag((int)ConsoleFlags.LogLevelError, true);
                CoreLog.LogEntries.SetConsoleFlag((int)ConsoleFlags.Collapse, collapse);
                int count = CoreLog.LogEntries.GetCount();
                if (count == m_LastEntryCount)
                {
                    CoreLog.LogEntries.consoleFlags = flags;
                    CheckRepaint(CheckSearchStringChanged());
                    return;
                }

                if (m_LastEntryCount > count)
                {
                    ClearEntries();
                }

                CoreLog.LogEntries.SetConsoleFlag((int)ConsoleFlags.ShowTimestamp, true);
                CoreLog.LogEntries.StartGettingEntries();
                for (int i = m_LastEntryCount; i < count; i++)
                {
                    CoreLog.LogEntry entry = new CoreLog.LogEntry();
                    if (!CoreLog.LogEntries.GetEntryInternal(i, entry))
                    {
                        continue;
                    }

                    int mode = 0;
                    string text = null;
#if UNITY_2017_1_OR_NEWER
                    CoreLog.LogEntries.GetLinesAndModeFromEntryInternal(i, 10, ref mode, ref text);
#else
                    CoreLog.LogEntries.GetFirstTwoLinesEntryTextAndModeInternal(i, ref mode, ref text);
#endif

                    int entryCount = 0;
                    if (collapse)
                    {
                        entryCount = CoreLog.LogEntries.GetEntryCount(i);
                    }
                    AddEntry(i, entry, text, entryCount);
                }
                CoreLog.LogEntries.EndGettingEntries();
                CoreLog.LogEntries.consoleFlags = flags;
                m_LastEntryCount = count;

                CheckSearchStringChanged();
                CheckRepaint(true);
            }

            private void ClearEntries()
            {
                m_SelectedInfo = null;
                m_EntryInfos.Clear();
                m_FilteredInfos.Clear();
                m_LastEntryCount = -1;
                m_TypeCounts = new[] { 0, 0, 0 };
            }

            private void AddEntry(int row, CoreLog.LogEntry entry, string text, int entryCount)
            {
                EntryInfo entryInfo = new EntryInfo
                {
                    row = row,
                    lines = text,
                    text = GetNumberLines(text),
                    entryCount = entryCount,
                    flags = GetConsoleFlagFromMode(entry.mode),
                    entry = entry
                };
                entryInfo.pure = GetPureLines(entryInfo.text, out entryInfo.tagPosInfos);
                entryInfo.lower = entryInfo.pure.ToLower();
                m_EntryInfos.Add(entryInfo);

                bool hasSearchString = !string.IsNullOrEmpty(m_SearchString);
                string searchStringValue = null;
                int searchStringLen = 0;
                if (hasSearchString)
                {
                    searchStringValue = m_SearchString.ToLower();
                    searchStringLen = searchStringValue.Length;
                }

                // 没有将堆栈都进行搜索，以免信息太杂，只根据行数，但是变化行数时不会重新搜索
                if (HasFlag((int)entryInfo.flags) && m_CustomFilters.HasFilters(entryInfo.lower) && (!hasSearchString ||
                      (entryInfo.searchIndex = entryInfo.lower.IndexOf(searchStringValue, StringComparison.Ordinal)) != -1))
                {
                    SearchIndexToTagIndex(entryInfo, searchStringLen);
                    m_FilteredInfos.Add(entryInfo);
                }

                if (entryInfo.flags == ConsoleFlags.LogLevelError)
                {
                    m_TypeCounts[0]++;
                }
                else if (entryInfo.flags == ConsoleFlags.LogLevelWarning)
                {
                    m_TypeCounts[1]++;
                }
                else
                {
                    m_TypeCounts[2]++;
                }
            }

            private void ResetEntriesForNumberLines()
            {
                foreach (var entryInfo in m_EntryInfos)
                {
                    entryInfo.text = GetNumberLines(entryInfo.lines);
                    entryInfo.pure = GetPureLines(entryInfo.text, out entryInfo.tagPosInfos);
                    entryInfo.lower = entryInfo.pure.ToLower();
                }
            }

            private void CheckInit()
            {
                if (m_Init)
                {
                    return;
                }

                m_Init = true;
                m_ConsoleFlagsComing = EditorPrefs.GetInt(kPrefConsoleFlags, 896);
                m_ShowTimestamp = EditorPrefs.GetBool(kPrefShowTimestamp, false);
                m_Collapse = EditorPrefs.GetBool(kPrefCollapse, false);
                m_CustomFilters.Load();
            }

            private bool CheckSearchStringChanged()
            {
                if (m_LastSearchStringTime > 1f && m_LastSearchStringTime < EditorApplication.timeSinceStartup)
                {
                    m_LastSearchStringTime = -1f;
                    if (!string.IsNullOrEmpty(m_SearchString))
                    {
                        if (searchHistory[0].Length == 0)
                        {
                            ArrayUtility.RemoveAt(ref searchHistory, 0);
                        }
                        else
                        {
                            ArrayUtility.Remove(ref searchHistory, m_SearchString);
                        }
                        ArrayUtility.Insert(ref searchHistory, 0, m_SearchString);
                        if (searchHistory.Length > 10)
                        {
                            ArrayUtility.RemoveAt(ref searchHistory, 10);
                        }
                    }
                }

                bool customFiltersChangedValue = m_CustomFilters.IsChanged();
                if (m_SearchString == m_SearchStringComing && m_ConsoleFlags == m_ConsoleFlagsComing && !customFiltersChangedValue)
                {
                    return false;
                }

                bool hasSearchString = !string.IsNullOrEmpty(m_SearchStringComing);
                bool startsWithValue = hasSearchString && !string.IsNullOrEmpty(m_SearchString)
                                       && m_SearchStringComing.StartsWith(m_SearchString, StringComparison.Ordinal);
                bool flagsChangedValue = m_ConsoleFlags != m_ConsoleFlagsComing;
                m_ConsoleFlags = m_ConsoleFlagsComing;
                string searchStringValue = null;
                int searchStringLen = 0;
                if (hasSearchString)
                {
                    searchStringValue = m_SearchStringComing.ToLower();
                    searchStringLen = searchStringValue.Length;
                }

                if (flagsChangedValue || !startsWithValue || customFiltersChangedValue)
                {
                    m_FilteredInfos.Clear();

                    foreach (var entryInfo in m_EntryInfos)
                    {
                        if (HasFlag((int)entryInfo.flags) && m_CustomFilters.HasFilters(entryInfo.lower) && (!hasSearchString
                            || (entryInfo.searchIndex = entryInfo.lower.IndexOf(searchStringValue, StringComparison.Ordinal)) != -1))
                        {
                            SearchIndexToTagIndex(entryInfo, searchStringLen);
                            m_FilteredInfos.Add(entryInfo);
                        }
                    }
                }
                else
                {
                    for (int i = m_FilteredInfos.Count - 1; i >= 0; i--)
                    {
                        if ((m_FilteredInfos[i].searchIndex = m_FilteredInfos[i].lower.IndexOf(searchStringValue, StringComparison.Ordinal)) == -1)
                        {
                            m_FilteredInfos.RemoveAt(i);
                        }
                        else
                        {
                            SearchIndexToTagIndex(m_FilteredInfos[i], searchStringLen);
                        }
                    }
                }

                m_SearchString = m_SearchStringComing;
                if (hasSearchString)
                {
                    m_LastSearchStringTime = EditorApplication.timeSinceStartup + 3f;
                }

                if (flagsChangedValue)
                {
                    EditorPrefs.SetInt(kPrefConsoleFlags, m_ConsoleFlags);
                }

                if (customFiltersChangedValue)
                {
                    m_CustomFilters.ClearChanged();
                    m_CustomFilters.Save();
                }

                searchFrame = IsSelectedEntryShow();
                return true;
            }

            private void CheckRepaint(bool repaint)
            {

            }

            private bool HasMode(int mode, Mode modeToCheck) { return (mode & (int)modeToCheck) != 0; }

            private ConsoleFlags GetConsoleFlagFromMode(int mode)
            {
                // Errors
                if (HasMode(mode, Mode.Fatal | Mode.Assert |
                                  Mode.Error | Mode.ScriptingError |
                                  Mode.AssetImportError | Mode.ScriptCompileError |
                                  Mode.GraphCompileError | Mode.ScriptingAssertion))
                {
                    return ConsoleFlags.LogLevelError;
                }
                // Warnings
                if (HasMode(mode, Mode.ScriptCompileWarning | Mode.ScriptingWarning | Mode.AssetImportWarning))
                {
                    return ConsoleFlags.LogLevelWarning;
                }
                // Logs
                return ConsoleFlags.LogLevelLog;
            }

            private string GetNumberLines(string s)
            {
                int num = numberOfLines;
                int i = -1;
                for (int j = 1, k = 0; j <= num; j++)
                {
                    i = s.IndexOf('\n', i + 1);
                    if (i == -1)
                    {
                        if (k < num)
                        {
                            i = s.Length;
                        }
                        break;
                    }
                    k++;
                }

                if (i != -1)
                {
                    int startIndex = 0;
#if UNITY_2018_1_OR_NEWER
                    if (!showTimestamp)
                    {
                        startIndex = 11;
                    }
#endif
                    return s.Substring(startIndex, i - startIndex);
                }
                return s;
            }

            public void ExportLog()
            {
                string filePath = EditorUtility.SaveFilePanel("Export Log", "",
                    "Console Log " + string.Format("{0:HHmm}", DateTime.Now) + ".txt", "txt");
                if (string.IsNullOrEmpty(filePath))
                {
                    return;
                }

                StringBuilder sb = new StringBuilder();
                foreach (var entryInfo in m_FilteredInfos)
                {
                    sb.AppendLine(entryInfo.entry.condition);
                }
                File.WriteAllText(filePath, sb.ToString());
            }

            #region HTMLTag

            private const int kTagQuadIndex = 5;

            private readonly string[] m_TagStrings = new string[]
            {
                "b",
                "i",
                "color",
                "size",
                "material",
                "quad",
                "x",
                "y",
                "width",
                "height",
            };

            private readonly StringBuilder m_StringBuilder = new StringBuilder();
            private readonly Stack<int> m_TagStack = new Stack<int>();

            private int GetTagIndex(string input, ref int pos, out bool closing)
            {
                closing = false;
                if (input[pos] == '<')
                {
                    int inputLen = input.Length;
                    int nextPos = pos + 1;
                    if (nextPos == inputLen)
                    {
                        return -1;
                    }

                    closing = input[nextPos] == '/';
                    if (closing)
                    {
                        nextPos++;
                    }

                    for (int i = 0; i < m_TagStrings.Length; i++)
                    {
                        var tagString = m_TagStrings[i];
                        bool find = true;

                        for (int j = 0; j < tagString.Length; j++)
                        {
                            int pingPos = nextPos + j;
                            if (pingPos == inputLen || char.ToLower(input[pingPos]) != tagString[j])
                            {
                                find = false;
                                break;
                            }
                        }

                        if (find)
                        {
                            int endPos = nextPos + tagString.Length;
                            if (endPos == inputLen)
                            {
                                continue;
                            }

                            if ((!closing && input[endPos] == '=') || (input[endPos] == ' ' && i == kTagQuadIndex))
                            {
                                while (input[endPos] != '>' && endPos < inputLen)
                                {
                                    endPos++;
                                }
                            }

                            if (input[endPos] != '>')
                            {
                                continue;
                            }

                            pos = endPos;
                            return i;
                        }
                    }
                }
                return -1;
            }

            private string GetPureLines(string input, out List<int> posList)
            {
                m_StringBuilder.Length = 0;
                m_TagStack.Clear();
                posList = null;

                int preStrPos = 0;
                int pos = 0;
                while (pos < input.Length)
                {
                    int oldPos = pos;
                    bool closing;
                    int tagIndex = GetTagIndex(input, ref pos, out closing);
                    if (tagIndex != -1)
                    {
                        if (closing)
                        {
                            if (m_TagStack.Count == 0 || m_TagStack.Pop() != tagIndex)
                            {
                                posList = null;
                                return input;
                            }
                        }

                        if (posList == null)
                        {
                            posList = new List<int>();
                        }
                        posList.Add(oldPos);
                        posList.Add(pos);

                        if (preStrPos != oldPos)
                        {
                            m_StringBuilder.Append(input, preStrPos, oldPos - preStrPos);
                        }
                        preStrPos = pos + 1;

                        if (closing || tagIndex == kTagQuadIndex)
                        {
                            continue;
                        }

                        m_TagStack.Push(tagIndex);
                    }
                    pos++;
                }

                if (m_TagStack.Count > 0)
                {
                    posList = null;
                    return input;
                }

                if (preStrPos > 0 && preStrPos < input.Length)
                {
                    m_StringBuilder.Append(input, preStrPos, input.Length - preStrPos);
                }
                if (m_StringBuilder.Length > 0)
                {
                    return m_StringBuilder.ToString();
                }

                return input;
            }

            private int GetOriginalCharIndex(int idx, List<int> posList)
            {
                if (posList == null || posList.Count == 0)
                {
                    return idx;
                }

                int idx2 = 0;
                for (int i = 0; i < posList.Count && (i + 1) < posList.Count;)
                {
                    int idx1 = idx2;
                    if ((i - 1) > 0)
                    {
                        idx2 += posList[i] - posList[i - 1] - 1;
                    }
                    else
                    {
                        idx2 = posList[i] - 1;
                    }

                    if (idx >= idx1 && idx <= idx2)
                    {
                        if ((i - 1) > 0)
                        {
                            return posList[i - 1] + idx - idx1;
                        }

                        return idx;
                    }

                    i += 2;
                }

                return posList[posList.Count - 1] + idx - idx2;
            }

            private void SearchIndexToTagIndex(EntryInfo entryInfo, int searchLength)
            {
                if (entryInfo.searchIndex == -1)
                {
                    return;
                }

                entryInfo.searchEndIndex = GetOriginalCharIndex(entryInfo.searchIndex + searchLength,
                    entryInfo.tagPosInfos);
                entryInfo.searchIndex = GetOriginalCharIndex(entryInfo.searchIndex, entryInfo.tagPosInfos);
            }

            #endregion

            #region CustomFilters

            public class CustomFiltersItem
            {
                private string m_Filter;
                private bool m_Toggle;

                public bool changed { get; set; }

                public string filter
                {
                    get { return m_Filter; }
                    set
                    {
                        if (value != m_Filter)
                        {
                            m_Filter = value.ToLower();
                            if (toggle)
                            {
                                changed = true;
                            }
                        }
                    }
                }

                public bool toggle
                {
                    get { return m_Toggle; }
                    set
                    {
                        if (value != m_Toggle)
                        {
                            m_Toggle = value;
                            changed = true;
                        }
                    }
                }
            }

            public class CustomFiltersGroup
            {
                public readonly List<CustomFiltersItem> filters = new List<CustomFiltersItem>();
                public bool changed { get; set; }

                public bool IsChanged()
                {
                    foreach (var filter in filters)
                    {
                        if (filter.changed)
                        {
                            return true;
                        }
                    }
                    return changed;
                }

                public void ClearChanged()
                {
                    changed = false;
                    foreach (var filter in filters)
                    {
                        filter.changed = false;
                    }
                }

                public bool HasFilters(string input)
                {
                    foreach (var filter in filters)
                    {
                        if (filter.toggle && !input.Contains(filter.filter))
                        {
                            return false;
                        }
                    }

                    return true;
                }

                public void Load()
                {
                    filters.Clear();
                    var val = EditorPrefs.GetString(kPrefCustomFilters, String.Empty);
                    if (string.IsNullOrEmpty(val))
                    {
                        return;
                    }

                    var vals = val.Split('\n');
                    try
                    {
                        for (int i = 0; i < vals.Length && (i + 1) < vals.Length; i++)
                        {
                            var item = new CustomFiltersItem { filter = vals[i], toggle = bool.Parse(vals[i + 1]) };
                            filters.Add(item);
                            i++;
                        }
                    }
                    catch
                    {
                        // ignored
                    }
                }

                public void Save()
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (var filter in filters)
                    {
                        sb.Append(filter.filter);
                        sb.Append('\n');
                        sb.Append(filter.toggle.ToString());
                        sb.Append('\n');
                    }
                    EditorPrefs.SetString(kPrefCustomFilters, sb.ToString());
                }
            }

            public CustomFiltersGroup customFilters
            {
                get { return m_CustomFilters; }
            }

            #endregion

            #region Stacktrace

            internal class Constants
            {
                public static string colorNamespace, colorNamespaceAlpha;
                public static string colorClass, colorClassAlpha;
                public static string colorMethod, colorMethodAlpha;
                public static string colorParameters, colorParametersAlpha;
                public static string colorPath, colorPathAlpha;
                public static string colorFilename, colorFilenameAlpha;
            }

            private class StacktraceLineInfo
            {
                public string plain;
                public string text;
                public string filePath;
                public int lineNum;
            }

            public bool StacktraceListView_IsExist()
            {
                if (m_SelectedInfo == null || m_SelectedInfo.stacktraceLineInfos == null)
                {
                    return false;
                }

                return true;
            }

            public int StacktraceListView_GetCount()
            {
                if (StacktraceListView_IsExist() && IsSelectedEntryShow())
                {
                    return m_SelectedInfo.stacktraceLineInfos.Count;
                }

                return 0;
            }

            public string StacktraceListView_GetLine(int row)
            {
                if (StacktraceListView_IsExist())
                {
                    return m_SelectedInfo.stacktraceLineInfos[row].text;
                }

                return String.Empty;
            }

            public float StacktraceListView_GetMaxWidth(GUIContent tempContent, GUIStyle tempStyle)
            {
                if (m_SelectedInfo == null || !IsSelectedEntryShow())
                {
                    return 1f;
                }

                if (!StacktraceListView_IsExist())
                {
                    StacktraceListView_Parse(m_SelectedInfo);
                }

                var maxLine = -1;
                var maxLineLen = -1;
                for (int i = 0; i < m_SelectedInfo.stacktraceLineInfos.Count; i++)
                {
                    if (maxLineLen < m_SelectedInfo.stacktraceLineInfos[i].plain.Length)
                    {
                        maxLineLen = m_SelectedInfo.stacktraceLineInfos[i].plain.Length;
                        maxLine = i;
                    }
                }

                float maxWidth = 1f;
                if (maxLine != -1)
                {
                    tempContent.text = m_SelectedInfo.stacktraceLineInfos[maxLine].plain;
                    maxWidth = tempStyle.CalcSize(tempContent).x;
                }

                return maxWidth;
            }

            private void StacktraceListView_Parse(EntryInfo entryInfo)
            {
                var lines = entryInfo.entry.condition.Split(new char[] { '\n' }, StringSplitOptions.None);
                entryInfo.stacktraceLineInfos = new List<StacktraceLineInfo>(lines.Length);

                string rootDirectory = System.IO.Path.Combine(Application.dataPath, "..");
                Uri uriRoot = new Uri(rootDirectory);
                string textBeforeFilePath = ") (at ";
                string textUnityEngineDebug = "UnityEngine.Debug";
#if UNITY_2019_1_OR_NEWER
                string fileInBuildSlave = "D:/unity/";
#else
                string fileInBuildSlave = "C:/buildslave/unity/";
#endif
                string luaCFunction = "[C]";
                string luaMethodBefore = ": in function ";
                string luaFileExt = ".lua";
                string luaAssetPath = "Assets/Lua/";
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (i == lines.Length - 1 && string.IsNullOrEmpty(line))
                    {
                        continue;
                    }
                    if (line.StartsWith(textUnityEngineDebug))
                    {
                        continue;
                    }

                    StacktraceLineInfo info = new StacktraceLineInfo();
                    info.plain = line;
                    info.text = info.plain;
                    entryInfo.stacktraceLineInfos.Add(info);

                    if (i == 0)
                    {
                        continue;
                    }

                    if (!StacktraceListView_Parse_CSharp(line, info, textBeforeFilePath, fileInBuildSlave, uriRoot))
                    {
                        StacktraceListView_Parse_Lua(line, info, luaCFunction, luaMethodBefore, luaFileExt, luaAssetPath);
                    }
                }
            }

            private bool StacktraceListView_Parse_CSharp(string line, StacktraceLineInfo info,
                string textBeforeFilePath, string fileInBuildSlave, Uri uriRoot)
            {
                int methodLastIndex = line.IndexOf('(');
                if (methodLastIndex <= 0)
                {
                    return false;
                }
                int argsLastIndex = line.IndexOf(')', methodLastIndex);
                if (argsLastIndex <= 0)
                {
                    return false;
                }
                int methodFirstIndex = line.LastIndexOf(':', methodLastIndex);
                if (methodFirstIndex <= 0)
                {
                    methodFirstIndex = line.LastIndexOf('.', methodLastIndex);
                    if (methodFirstIndex <= 0)
                    {
                        return false;
                    }
                }
                string methodString = line.Substring(methodFirstIndex + 1, methodLastIndex - methodFirstIndex - 1);

                string classString;
                string namespaceString = String.Empty;
                int classFirstIndex = line.LastIndexOf('.', methodFirstIndex - 1);
                if (classFirstIndex <= 0)
                {
                    classString = line.Substring(0, methodFirstIndex + 1);
                }
                else
                {
                    classString = line.Substring(classFirstIndex + 1, methodFirstIndex - classFirstIndex);
                    namespaceString = line.Substring(0, classFirstIndex + 1);
                }

                string argsString = line.Substring(methodLastIndex, argsLastIndex - methodLastIndex + 1);
                string fileString = String.Empty;
                string fileNameString = String.Empty;
                string fileLineString = String.Empty;
                bool alphaColor = true;

                int filePathIndex = line.IndexOf(textBeforeFilePath, argsLastIndex, StringComparison.Ordinal);
                if (filePathIndex > 0)
                {
                    filePathIndex += textBeforeFilePath.Length;
                    if (line[filePathIndex] != '<') // sometimes no url is given, just an id between <>, we can't do an hyperlink
                    {
                        string filePathPart = line.Substring(filePathIndex);
                        int lineIndex = filePathPart.LastIndexOf(":", StringComparison.Ordinal); // LastIndex because the url can contain ':' ex:"C:"
                        if (lineIndex > 0)
                        {
                            int endLineIndex = filePathPart.LastIndexOf(")", StringComparison.Ordinal); // LastIndex because files or folder in the url can contain ')'
                            if (endLineIndex > 0)
                            {
                                string lineString =
                                    filePathPart.Substring(lineIndex + 1, (endLineIndex) - (lineIndex + 1));
                                string filePath = filePathPart.Substring(0, lineIndex);

                                bool isInBuildSlave = filePath.StartsWith(fileInBuildSlave, StringComparison.Ordinal);
                                if (!isInBuildSlave)
                                {
                                    alphaColor = false;
                                }

                                info.filePath = filePath;
                                info.lineNum = int.Parse(lineString);

                                if (filePath.Length > 2 && filePath[1] == ':' && !isInBuildSlave)
                                {
                                    Uri uriFile = new Uri(filePath);
                                    Uri relativeUri = uriRoot.MakeRelativeUri(uriFile);
                                    string relativePath = relativeUri.ToString();
                                    if (!string.IsNullOrEmpty(relativePath))
                                    {
                                        info.plain = info.plain.Replace(filePath, relativePath);
                                        filePath = relativePath;
                                    }
                                }

                                fileNameString = System.IO.Path.GetFileName(filePath);
                                fileString = textBeforeFilePath.Substring(1) + filePath.Substring(0, filePath.Length - fileNameString.Length);
                                fileLineString = filePathPart.Substring(lineIndex, endLineIndex - lineIndex + 1);
                            }
                        }
                    }
                    else
                    {
                        fileString = line.Substring(argsLastIndex + 1);
                    }
                }

                if (alphaColor)
                {
                    info.text =
                        string.Format("<color=#{0}>{1}</color>", Constants.colorNamespaceAlpha, namespaceString) +
                        string.Format("<color=#{0}>{1}</color>", Constants.colorClassAlpha, classString) +
                        string.Format("<color=#{0}>{1}</color>", Constants.colorMethodAlpha, methodString) +
                        string.Format("<color=#{0}>{1}</color>", Constants.colorParametersAlpha, argsString) +
                        string.Format("<color=#{0}>{1}</color>", Constants.colorPathAlpha, fileString) +
                        string.Format("<color=#{0}>{1}</color>", Constants.colorFilenameAlpha, fileNameString) +
                        string.Format("<color=#{0}>{1}</color>", Constants.colorPathAlpha, fileLineString);
                }
                else
                {
                    info.text = string.Format("<color=#{0}>{1}</color>", Constants.colorNamespace, namespaceString) +
                                string.Format("<color=#{0}>{1}</color>", Constants.colorClass, classString) +
                                string.Format("<color=#{0}>{1}</color>", Constants.colorMethod, methodString) +
                                string.Format("<color=#{0}>{1}</color>", Constants.colorParameters, argsString) +
                                string.Format("<color=#{0}>{1}</color>", Constants.colorPath, fileString) +
                                string.Format("<color=#{0}>{1}</color>", Constants.colorFilename, fileNameString) +
                                string.Format("<color=#{0}>{1}</color>", Constants.colorPath, fileLineString);
                }

                return true;
            }

            private bool StacktraceListView_Parse_Lua(string line, StacktraceLineInfo info,
                string luaCFunction, string luaMethodBefore, string luaFileExt, string luaAssetPath)
            {
                if (string.IsNullOrEmpty(line) || line[0] != '	')
                {
                    return false;
                }

                string preMethodString = line;
                string methodString = String.Empty;
                int methodFirstIndex = line.IndexOf(luaMethodBefore, StringComparison.Ordinal);
                if (methodFirstIndex > 0)
                {
                    methodString = line.Substring(methodFirstIndex + luaMethodBefore.Length);
                    preMethodString = preMethodString.Remove(methodFirstIndex + luaMethodBefore.Length);
                }

                bool cFunction = line.IndexOf(luaCFunction, 1, StringComparison.Ordinal) == 1;
                if (!cFunction)
                {
                    int lineIndex = line.IndexOf(':');
                    if (lineIndex > 0)
                    {
                        int endLineIndex = line.IndexOf(':', lineIndex + 1);
                        if (endLineIndex > 0)
                        {
                            string lineString =
                                line.Substring(lineIndex + 1, (endLineIndex) - (lineIndex + 1));
                            string filePath = line.Substring(1, lineIndex - 1);
                            if (!filePath.EndsWith(luaFileExt, StringComparison.Ordinal))
                            {
                                filePath += luaFileExt;
                            }

                            if (!int.TryParse(lineString, out info.lineNum))
                            {
                                return false;
                            }
                            info.filePath = luaAssetPath + filePath;

                            string namespaceString = String.Empty;
                            int classFirstIndex = filePath.LastIndexOf('/');
                            if (classFirstIndex > 0)
                            {
                                namespaceString = filePath.Substring(0, classFirstIndex + 1);
                            }

                            string classString = filePath.Substring(classFirstIndex + 1,
                                filePath.Length - namespaceString.Length - luaFileExt.Length);


                            info.text = string.Format("	<color=#{0}>{1}</color>", Constants.colorNamespace,
                                            namespaceString) +
                                        string.Format("<color=#{0}>{1}</color>", Constants.colorClass, classString) +
                                        string.Format("<color=#{0}>:{1}</color>", Constants.colorPath, lineString) +
                                        string.Format("<color=#{0}>{1}</color>", Constants.colorPath, luaMethodBefore) +
                                        string.Format("<color=#{0}>{1}</color>", Constants.colorMethod, methodString);
                        }
                    }
                }
                else
                {
                    info.text = string.Format("<color=#{0}>{1}</color>", Constants.colorPathAlpha, preMethodString) +
                                string.Format("<color=#{0}>{1}</color>", Constants.colorMethodAlpha, methodString);
                }

                return true;
            }

            public bool StacktraceListView_CanOpen(int stacktraceLineInfoIndex)
            {
                if (!StacktraceListView_IsExist())
                {
                    return false;
                }

                if (stacktraceLineInfoIndex < m_SelectedInfo.stacktraceLineInfos.Count)
                {
                    return !string.IsNullOrEmpty(m_SelectedInfo.stacktraceLineInfos[stacktraceLineInfoIndex].filePath);
                }
                return false;
            }

            public void StacktraceListView_RowGotDoubleClicked()
            {
                if (!StacktraceListView_IsExist())
                {
                    return;
                }

                for (var i = 0; i < m_SelectedInfo.stacktraceLineInfos.Count; i++)
                {
                    var stacktraceLineInfo = m_SelectedInfo.stacktraceLineInfos[i];
                    if (!string.IsNullOrEmpty(stacktraceLineInfo.filePath))
                    {
                        StacktraceListView_Open(i);
                        break;
                    }
                }
            }

            public void StacktraceListView_Open(object userData)
            {
                if (!StacktraceListView_IsExist())
                {
                    return;
                }

                var stacktraceLineInfoIndex = (int)userData;
                if (stacktraceLineInfoIndex < m_SelectedInfo.stacktraceLineInfos.Count)
                {
                    var filePath = m_SelectedInfo.stacktraceLineInfos[stacktraceLineInfoIndex].filePath;
                    var lineNum = m_SelectedInfo.stacktraceLineInfos[stacktraceLineInfoIndex].lineNum;
                    ScriptAssetOpener.OpenAsset(filePath, lineNum);
                }
            }

            public void StacktraceListView_Copy(object userData)
            {
                if (!StacktraceListView_IsExist())
                {
                    return;
                }

                var stacktraceLineInfoIndex = (int)userData;
                if (stacktraceLineInfoIndex < m_SelectedInfo.stacktraceLineInfos.Count)
                {
                    EditorGUIUtility.systemCopyBuffer = m_SelectedInfo.stacktraceLineInfos[stacktraceLineInfoIndex].plain;
                }
            }

            public void StacktraceListView_CopyAll()
            {
                if (!StacktraceListView_IsExist() || !IsSelectedEntryShow())
                {
                    return;
                }

                EditorGUIUtility.systemCopyBuffer = m_SelectedInfo.entry.condition;
            }

            #endregion
        }
    }
}