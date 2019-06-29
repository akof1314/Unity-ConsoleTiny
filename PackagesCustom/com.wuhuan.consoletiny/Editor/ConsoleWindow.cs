using System;
using UnityEngine;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine.Scripting;
using UnityEngine.Experimental.Networking.PlayerConnection;
using UnityEditor.Experimental.Networking.PlayerConnection;
using ConnectionGUILayout = UnityEditor.Experimental.Networking.PlayerConnection.EditorGUILayout;
using EditorGUI = UnityEditor.EditorGUI;
using EditorGUILayout = UnityEditor.EditorGUILayout;
using EditorGUIUtility = UnityEditor.EditorGUIUtility;

namespace ConsoleTiny
{
    [EditorWindowTitle(title = "Console", useTypeNameAsIconName = true)]
    public class ConsoleWindow : EditorWindow, IHasCustomMenu
    {
        [MenuItem("Window/General/ConsoleT %#t", false, 7)]
        static void ShowConsole()
        {
            EditorWindow.GetWindow<ConsoleWindow>();
        }

        internal delegate void EntryDoubleClickedDelegate(LogEntry entry);

        //TODO: move this out of here
        internal class Constants
        {
            private static bool ms_Loaded;
            private static int ms_logStyleLineCount;
            public static GUIStyle Box;
            public static GUIStyle MiniButton;
            public static GUIStyle LogStyle;
            public static GUIStyle WarningStyle;
            public static GUIStyle ErrorStyle;
            public static GUIStyle IconLogStyle;
            public static GUIStyle IconWarningStyle;
            public static GUIStyle IconErrorStyle;
            public static GUIStyle EvenBackground;
            public static GUIStyle OddBackground;
            public static GUIStyle MessageStyle;
            public static GUIStyle StatusError;
            public static GUIStyle StatusWarn;
            public static GUIStyle StatusLog;
            public static GUIStyle Toolbar;
            public static GUIStyle CountBadge;
            public static GUIStyle LogSmallStyle;
            public static GUIStyle WarningSmallStyle;
            public static GUIStyle ErrorSmallStyle;
            public static GUIStyle IconLogSmallStyle;
            public static GUIStyle IconWarningSmallStyle;
            public static GUIStyle IconErrorSmallStyle;
            public static readonly string ClearLabel = L10n.Tr("Clear");
            public static readonly string ClearOnPlayLabel = L10n.Tr("Clear on Play");
            public static readonly string ErrorPauseLabel = L10n.Tr("Error Pause");
            public static readonly string CollapseLabel = L10n.Tr("Collapse");
            public static readonly string StopForAssertLabel = L10n.Tr("Stop for Assert");
            public static readonly string StopForErrorLabel = L10n.Tr("Stop for Error");

            public static int LogStyleLineCount
            {
                get { return ms_logStyleLineCount; }
                set
                {
                    ms_logStyleLineCount = value;

                    // If Constants hasn't been initialized yet we just skip this for now
                    // and let Init() call this for us in a bit.
                    if (!ms_Loaded)
                        return;
                    UpdateLogStyleFixedHeights();
                }
            }

            public static void Init()
            {
                if (ms_Loaded)
                    return;
                ms_Loaded = true;
                Box = "CN Box";


                MiniButton = "ToolbarButton";
                Toolbar = "Toolbar";
                LogStyle = "CN EntryInfo";
                LogSmallStyle = "CN EntryInfoSmall";
                WarningStyle = "CN EntryWarn";
                WarningSmallStyle = "CN EntryWarnSmall";
                ErrorStyle = "CN EntryError";
                ErrorSmallStyle = "CN EntryErrorSmall";
                IconLogStyle = "CN EntryInfoIcon";
                IconLogSmallStyle = "CN EntryInfoIconSmall";
                IconWarningStyle = "CN EntryWarnIcon";
                IconWarningSmallStyle = "CN EntryWarnIconSmall";
                IconErrorStyle = "CN EntryErrorIcon";
                IconErrorSmallStyle = "CN EntryErrorIconSmall";
                EvenBackground = "CN EntryBackEven";
                OddBackground = "CN EntryBackodd";
                MessageStyle = "CN Message";
                StatusError = "CN StatusError";
                StatusWarn = "CN StatusWarn";
                StatusLog = "CN StatusInfo";
                CountBadge = "CN CountBadge";
                MessageStyle = new GUIStyle(MessageStyle);
                MessageStyle.onNormal.textColor = MessageStyle.active.textColor;
                MessageStyle.padding.top = 0;
                MessageStyle.padding.bottom = 0;
                var selectedStyle = new GUIStyle("MeTransitionSelect");
                MessageStyle.onNormal.background = selectedStyle.normal.background;

                // If the console window isn't open OnEnable() won't trigger so it will end up with 0 lines,
                // so we always make sure we read it up when we initialize here.
                LogStyleLineCount = EditorPrefs.GetInt("ConsoleWindowLogLineCount", 2);
            }

            private static void UpdateLogStyleFixedHeights()
            {
                // Whenever we change the line height count or the styles are set we need to update the fixed height
                // of the following GuiStyles so the entries do not get cropped incorrectly.
                ErrorStyle.fixedHeight = (LogStyleLineCount * ErrorStyle.lineHeight) + ErrorStyle.border.top;
                WarningStyle.fixedHeight = (LogStyleLineCount * WarningStyle.lineHeight) + WarningStyle.border.top;
                LogStyle.fixedHeight = (LogStyleLineCount * LogStyle.lineHeight) + LogStyle.border.top;
            }
        }

        int m_LineHeight;
        int m_BorderHeight;

        bool m_HasUpdatedGuiStyles;

        ListViewState m_ListView;
        ListViewState m_ListViewMessage;
        private List<StacktraceLineInfo> m_StacktraceLineInfos;
        private int m_StacktraceLineContextClickRow;
        string m_ActiveText = "";
        private int m_ActiveInstanceID = 0;
        bool m_DevBuild;

        Vector2 m_TextScroll = Vector2.zero;

        SplitterState spl = new SplitterState(new float[] { 70, 30 }, new int[] { 32, 32 }, null);

        static bool ms_LoadedIcons = false;
        static internal Texture2D iconInfo, iconWarn, iconError;
        static internal Texture2D iconInfoSmall, iconWarnSmall, iconErrorSmall;
        static internal Texture2D iconInfoMono, iconWarnMono, iconErrorMono;

        int ms_LVHeight = 0;

        class ConsoleAttachToPlayerState : GeneralConnectionState
        {
            static class Content
            {
                public static GUIContent PlayerLogging = EditorGUIUtility.TrTextContent("Player Logging");
                public static GUIContent FullLog = EditorGUIUtility.TrTextContent("Full Log (Developer Mode Only)");
            }

            public ConsoleAttachToPlayerState(EditorWindow parentWindow, Action<string> connectedCallback = null) : base(parentWindow, connectedCallback)
            {
            }

            bool IsConnected()
            {
                return PlayerConnectionLogReceiver.instance.State != PlayerConnectionLogReceiver.ConnectionState.Disconnected;
            }

            void PlayerLoggingOptionSelected()
            {
                PlayerConnectionLogReceiver.instance.State = IsConnected() ? PlayerConnectionLogReceiver.ConnectionState.Disconnected : PlayerConnectionLogReceiver.ConnectionState.CleanLog;
            }

            bool IsLoggingFullLog()
            {
                return PlayerConnectionLogReceiver.instance.State == PlayerConnectionLogReceiver.ConnectionState.FullLog;
            }

            void FullLogOptionSelected()
            {
                PlayerConnectionLogReceiver.instance.State = IsLoggingFullLog() ? PlayerConnectionLogReceiver.ConnectionState.CleanLog : PlayerConnectionLogReceiver.ConnectionState.FullLog;
            }

            public override void AddItemsToMenu(GenericMenu menu, Rect position)
            {
                // option to turn logging and the connection on or of
                menu.AddItem(Content.PlayerLogging, IsConnected(), PlayerLoggingOptionSelected);
                if (IsConnected())
                {
                    // All other options but the first are only available if logging is enabled
                    menu.AddItem(Content.FullLog, IsLoggingFullLog(), FullLogOptionSelected);
                    menu.AddSeparator("");
                    base.AddItemsToMenu(menu, position);
                }
            }
        }

        IConnectionState m_ConsoleAttachToPlayerState;

        [Flags]
        internal enum Mode
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
            ClearOnPlay = 1 << 1,
            ErrorPause = 1 << 2,
            Verbose = 1 << 3,
            StopForAssert = 1 << 4,
            StopForError = 1 << 5,
            Autoscroll = 1 << 6,
            LogLevelLog = 1 << 7,
            LogLevelWarning = 1 << 8,
            LogLevelError = 1 << 9,
            ShowTimestamp = 1 << 10,
        };

        static ConsoleWindow ms_ConsoleWindow = null;
        private string m_SearchText;

        static void ShowConsoleWindowImmediate()
        {
            ShowConsoleWindow(true);
        }

        public static void ShowConsoleWindow(bool immediate)
        {
            if (ms_ConsoleWindow == null)
            {
                ms_ConsoleWindow = ScriptableObject.CreateInstance<ConsoleWindow>();
                ms_ConsoleWindow.Show(immediate);
                ms_ConsoleWindow.Focus();
            }
            else
            {
                ms_ConsoleWindow.Show(immediate);
                ms_ConsoleWindow.Focus();
            }
        }

        static internal void LoadIcons()
        {
            if (ms_LoadedIcons)
                return;

            ms_LoadedIcons = true;
            iconInfo = EditorGUIUtility.LoadIcon("console.infoicon");
            iconWarn = EditorGUIUtility.LoadIcon("console.warnicon");
            iconError = EditorGUIUtility.LoadIcon("console.erroricon");
            iconInfoSmall = EditorGUIUtility.LoadIcon("console.infoicon.sml");
            iconWarnSmall = EditorGUIUtility.LoadIcon("console.warnicon.sml");
            iconErrorSmall = EditorGUIUtility.LoadIcon("console.erroricon.sml");

            // TODO: Once we get the proper monochrome images put them here.
            /*iconInfoMono = EditorGUIUtility.LoadIcon("console.infoicon.mono");
            iconWarnMono = EditorGUIUtility.LoadIcon("console.warnicon.mono");
            iconErrorMono = EditorGUIUtility.LoadIcon("console.erroricon.mono");*/
            iconInfoMono = EditorGUIUtility.LoadIcon("console.infoicon.sml");
            iconWarnMono = EditorGUIUtility.LoadIcon("console.warnicon.inactive.sml");
            iconErrorMono = EditorGUIUtility.LoadIcon("console.erroricon.inactive.sml");
            Constants.Init();
        }

        //[RequiredByNativeCode]
        public static void LogChanged()
        {
            if (ms_ConsoleWindow == null)
                return;

            ms_ConsoleWindow.DoLogChanged();
        }

        public void DoLogChanged()
        {
            ms_ConsoleWindow.Repaint();
        }

        public ConsoleWindow()
        {
            position = new Rect(200, 200, 800, 400);
            m_ListView = new ListViewState(0, 0);
            m_ListViewMessage = new ListViewState(0, 14);
            m_SearchText = string.Empty;
            m_StacktraceLineInfos = new List<StacktraceLineInfo>();
            m_StacktraceLineContextClickRow = -1;
        }

        void OnEnable()
        {
            if (m_ConsoleAttachToPlayerState == null)
                m_ConsoleAttachToPlayerState = new ConsoleAttachToPlayerState(this);

            MakeSureConsoleAlwaysOnlyOne();

            titleContent = EditorGUIUtility.TrTextContentWithIcon("Console", "UnityEditor.ConsoleWindow");
            titleContent = new GUIContent(titleContent) { text = "ConsoleT" };
            ms_ConsoleWindow = this;
            m_DevBuild = Unsupported.IsDeveloperMode();

            Constants.LogStyleLineCount = EditorPrefs.GetInt("ConsoleWindowLogLineCount", 2);
        }

        void MakeSureConsoleAlwaysOnlyOne()
        {
            // make sure that console window is always open as only one.
            if (ms_ConsoleWindow != null)
            {
                // get the container window of this console window.
                ContainerWindow cw = ms_ConsoleWindow.m_Parent.window;

                // the container window must not be main view(prevent from quitting editor).
                if (cw.rootView.GetType() != typeof(MainView))
                    cw.Close();
            }
        }

        void OnDisable()
        {
            m_ConsoleAttachToPlayerState?.Dispose();
            m_ConsoleAttachToPlayerState = null;

            if (ms_ConsoleWindow == this)
                ms_ConsoleWindow = null;
        }

        private int RowHeight
        {
            get
            {
                return (Constants.LogStyleLineCount * m_LineHeight) + m_BorderHeight;
            }
        }

        private static bool HasMode(int mode, Mode modeToCheck) { return (mode & (int)modeToCheck) != 0; }
        private static bool HasFlag(ConsoleFlags flags) { return (LogEntries.consoleFlags & (int)flags) != 0; }
        private static void SetFlag(ConsoleFlags flags, bool val) { LogEntries.SetConsoleFlag((int)flags, val); }

        static internal Texture2D GetIconForErrorMode(int mode, bool large)
        {
            // Errors
            if (HasMode(mode, Mode.Fatal | Mode.Assert |
                Mode.Error | Mode.ScriptingError |
                Mode.AssetImportError | Mode.ScriptCompileError |
                Mode.GraphCompileError | Mode.ScriptingAssertion))
                return large ? iconError : iconErrorSmall;
            // Warnings
            if (HasMode(mode, Mode.ScriptCompileWarning | Mode.ScriptingWarning | Mode.AssetImportWarning))
                return large ? iconWarn : iconWarnSmall;
            // Logs
            if (HasMode(mode, Mode.Log | Mode.ScriptingLog))
                return large ? iconInfo : iconInfoSmall;

            // Nothing
            return null;
        }

        static internal GUIStyle GetStyleForErrorMode(int mode, bool isIcon, bool isSmall)
        {
            // Errors
            if (HasMode(mode, Mode.Fatal | Mode.Assert |
                Mode.Error | Mode.ScriptingError |
                Mode.AssetImportError | Mode.ScriptCompileError |
                Mode.GraphCompileError | Mode.ScriptingAssertion))
            {
                if (isIcon)
                {
                    if (isSmall)
                    {
                        return Constants.IconErrorSmallStyle;
                    }
                    return Constants.IconErrorStyle;
                }

                if (isSmall)
                {
                    return Constants.ErrorSmallStyle;
                }
                return Constants.ErrorStyle;
            }
            // Warnings
            if (HasMode(mode, Mode.ScriptCompileWarning | Mode.ScriptingWarning | Mode.AssetImportWarning))
            {
                if (isIcon)
                {
                    if (isSmall)
                    {
                        return Constants.IconWarningSmallStyle;
                    }
                    return Constants.IconWarningStyle;
                }

                if (isSmall)
                {
                    return Constants.WarningSmallStyle;
                }
                return Constants.WarningStyle;
            }
            // Logs
            if (isIcon)
            {
                if (isSmall)
                {
                    return Constants.IconLogSmallStyle;
                }
                return Constants.IconLogStyle;
            }

            if (isSmall)
            {
                return Constants.LogSmallStyle;
            }
            return Constants.LogStyle;
        }

        static internal GUIStyle GetStatusStyleForErrorMode(int mode)
        {
            // Errors
            if (HasMode(mode, Mode.Fatal | Mode.Assert |
                Mode.Error | Mode.ScriptingError |
                Mode.AssetImportError | Mode.ScriptCompileError |
                Mode.GraphCompileError | Mode.ScriptingAssertion))
                return Constants.StatusError;
            // Warnings
            if (HasMode(mode, Mode.ScriptCompileWarning | Mode.ScriptingWarning | Mode.AssetImportWarning))
                return Constants.StatusWarn;
            // Logs
            return Constants.StatusLog;
        }

        static string ContextString(LogEntry entry)
        {
            StringBuilder context = new StringBuilder();

            if (HasMode(entry.mode, Mode.Error))
                context.Append("Error ");
            else if (HasMode(entry.mode, Mode.Log))
                context.Append("Log ");
            else
                context.Append("Assert ");

            context.Append("in file: ");
            context.Append(entry.file);
            context.Append(" at line: ");
            context.Append(entry.line);

            if (entry.errorNum != 0)
            {
                context.Append(" and errorNum: ");
                context.Append(entry.errorNum);
            }

            return context.ToString();
        }

        static string GetFirstLine(string s)
        {
            int i = s.IndexOf("\n");
            return (i != -1) ? s.Substring(0, i) : s;
        }

        static string GetFirstTwoLines(string s)
        {
            int i = s.IndexOf("\n");
            if (i != -1)
            {
                i = s.IndexOf("\n", i + 1);
                if (i != -1)
                    return s.Substring(0, i);
            }

            return s;
        }

        void SetActiveEntry(LogEntry entry)
        {
            if (entry != null)
            {
                StacktraceListView_Parse(m_ActiveText, entry.condition);
                m_ActiveText = entry.condition;
                // ping object referred by the log entry
                if (m_ActiveInstanceID != entry.instanceID)
                {
                    m_ActiveInstanceID = entry.instanceID;
                    if (entry.instanceID != 0)
                        EditorGUIUtility.PingObject(entry.instanceID);
                }
            }
            else
            {
                StacktraceListView_Parse(m_ActiveText, string.Empty);
                m_ActiveText = string.Empty;
                m_ActiveInstanceID = 0;
                m_ListView.row = -1;
                m_ListViewMessage.row = -1;
            }
        }

        // Used implicitly with CallStaticMonoMethod("ConsoleWindow", "ShowConsoleRow", param);
        static void ShowConsoleRow(int row)
        {
            ShowConsoleWindow(false);

            if (ms_ConsoleWindow)
            {
                ms_ConsoleWindow.m_ListView.row = row;
                ms_ConsoleWindow.m_ListView.selectionChanged = true;
                ms_ConsoleWindow.Repaint();
            }
        }

        void UpdateListView()
        {
            m_HasUpdatedGuiStyles = true;
            int newRowHeight = RowHeight;

            // We reset the scroll list to auto scrolling whenever the log entry count is modified
            m_ListView.rowHeight = newRowHeight;
            m_ListView.row = -1;
            m_ListView.scrollPos.y = LogEntries.GetCount() * newRowHeight;
        }

        void OnGUI()
        {
            Event e = Event.current;
            LoadIcons();
            LogEntries.TestFilteringText(m_SearchText);

            if (!m_HasUpdatedGuiStyles)
            {
                m_LineHeight = Mathf.RoundToInt(Constants.ErrorStyle.lineHeight);
                m_BorderHeight = Constants.ErrorStyle.border.top + Constants.ErrorStyle.border.bottom;
                UpdateListView();
            }

            GUILayout.BeginHorizontal(Constants.Toolbar);

            if (GUILayout.Button(Constants.ClearLabel, Constants.MiniButton))
            {
                LogEntries.Clear();
                GUIUtility.keyboardControl = 0;
            }

            int currCount = LogEntries.GetCount();

            if (m_ListView.totalRows != currCount && m_ListView.totalRows > 0)
            {
                // scroll bar was at the bottom?
                if (m_ListView.scrollPos.y >= m_ListView.rowHeight * m_ListView.totalRows - ms_LVHeight)
                {
                    m_ListView.scrollPos.y = currCount * RowHeight - ms_LVHeight;
                }
            }

            EditorGUILayout.Space();

            bool wasCollapsed = HasFlag(ConsoleFlags.Collapse);
            SetFlag(ConsoleFlags.Collapse, GUILayout.Toggle(wasCollapsed, Constants.CollapseLabel, Constants.MiniButton));

            bool collapsedChanged = (wasCollapsed != HasFlag(ConsoleFlags.Collapse));
            if (collapsedChanged)
            {
                // unselect if collapsed flag changed
                m_ListView.row = -1;

                // scroll to bottom
                m_ListView.scrollPos.y = LogEntries.GetCount() * RowHeight;
            }

            SetFlag(ConsoleFlags.ClearOnPlay, GUILayout.Toggle(HasFlag(ConsoleFlags.ClearOnPlay), Constants.ClearOnPlayLabel, Constants.MiniButton));
            SetFlag(ConsoleFlags.ErrorPause, GUILayout.Toggle(HasFlag(ConsoleFlags.ErrorPause), Constants.ErrorPauseLabel, Constants.MiniButton));

            ConnectionGUILayout.AttachToPlayerDropdown(m_ConsoleAttachToPlayerState, EditorStyles.toolbarDropDown);

            EditorGUILayout.Space();

            if (m_DevBuild)
            {
                GUILayout.FlexibleSpace();
                SetFlag(ConsoleFlags.StopForAssert, GUILayout.Toggle(HasFlag(ConsoleFlags.StopForAssert), Constants.StopForAssertLabel, Constants.MiniButton));
                SetFlag(ConsoleFlags.StopForError, GUILayout.Toggle(HasFlag(ConsoleFlags.StopForError), Constants.StopForErrorLabel, Constants.MiniButton));
            }

            GUILayout.FlexibleSpace();

            // Search bar
            GUILayout.Space(4f);
            SearchField(e);

            int errorCount = 0, warningCount = 0, logCount = 0;
            LogEntries.GetCountsByType(ref errorCount, ref warningCount, ref logCount);
            EditorGUI.BeginChangeCheck();
            bool setLogFlag = GUILayout.Toggle(HasFlag(ConsoleFlags.LogLevelLog), new GUIContent((logCount <= 999 ? logCount.ToString() : "999+"), logCount > 0 ? iconInfoSmall : iconInfoMono), Constants.MiniButton);
            bool setWarningFlag = GUILayout.Toggle(HasFlag(ConsoleFlags.LogLevelWarning), new GUIContent((warningCount <= 999 ? warningCount.ToString() : "999+"), warningCount > 0 ? iconWarnSmall : iconWarnMono), Constants.MiniButton);
            bool setErrorFlag = GUILayout.Toggle(HasFlag(ConsoleFlags.LogLevelError), new GUIContent((errorCount <= 999 ? errorCount.ToString() : "999+"), errorCount > 0 ? iconErrorSmall : iconErrorMono), Constants.MiniButton);
            // Active entry index may no longer be valid
            if (EditorGUI.EndChangeCheck())
                SetActiveEntry(null);

            SetFlag(ConsoleFlags.LogLevelLog, setLogFlag);
            SetFlag(ConsoleFlags.LogLevelWarning, setWarningFlag);
            SetFlag(ConsoleFlags.LogLevelError, setErrorFlag);

            GUILayout.EndHorizontal();

            SplitterGUILayout.BeginVerticalSplit(spl);
            int rowHeight = RowHeight;
            EditorGUIUtility.SetIconSize(new Vector2(rowHeight, rowHeight));
            GUIContent tempContent = new GUIContent();
            int id = GUIUtility.GetControlID(0);
            int rowDoubleClicked = -1;

            /////@TODO: Make Frame selected work with ListViewState
            using (new GettingLogEntriesScope(m_ListView))
            {
                int selectedRow = -1;
                bool openSelectedItem = false;
                bool collapsed = HasFlag(ConsoleFlags.Collapse);
                foreach (ListViewElement el in ListViewGUI.ListView(m_ListView, Constants.Box))
                {
                    if (e.type == EventType.MouseDown && e.button == 0 && el.position.Contains(e.mousePosition))
                    {
                        selectedRow = m_ListView.row;
                        if (e.clickCount == 2)
                            openSelectedItem = true;
                    }
                    else if (e.type == EventType.Repaint)
                    {
                        int mode = 0;
                        string text = null;
                        LogEntries.GetLinesAndModeFromEntryInternal(el.row, Constants.LogStyleLineCount, ref mode, ref text);

                        // Draw the background
                        GUIStyle s = el.row % 2 == 0 ? Constants.OddBackground : Constants.EvenBackground;
                        s.Draw(el.position, false, false, m_ListView.row == el.row, false);

                        // Draw the icon
                        GUIStyle iconStyle = GetStyleForErrorMode(mode, true, Constants.LogStyleLineCount == 1);
                        iconStyle.Draw(el.position, false, false, m_ListView.row == el.row, false);

                        // Draw the text
                        tempContent.text = text;
                        GUIStyle errorModeStyle = GetStyleForErrorMode(mode, false, Constants.LogStyleLineCount == 1);
                        errorModeStyle.Draw(el.position, tempContent, id, m_ListView.row == el.row);

                        if (collapsed)
                        {
                            Rect badgeRect = el.position;
                            tempContent.text = LogEntries.GetEntryCount(el.row).ToString(CultureInfo.InvariantCulture);
                            Vector2 badgeSize = Constants.CountBadge.CalcSize(tempContent);
                            badgeRect.xMin = badgeRect.xMax - badgeSize.x;
                            badgeRect.yMin += ((badgeRect.yMax - badgeRect.yMin) - badgeSize.y) * 0.5f;
                            badgeRect.x -= 5f;
                            GUI.Label(badgeRect, tempContent, Constants.CountBadge);
                        }
                    }
                }

                if (selectedRow != -1)
                {
                    if (m_ListView.scrollPos.y >= m_ListView.rowHeight * m_ListView.totalRows - ms_LVHeight)
                        m_ListView.scrollPos.y = m_ListView.rowHeight * m_ListView.totalRows - ms_LVHeight - 1;
                }

                // Make sure the selected entry is up to date
                if (m_ListView.totalRows == 0 || m_ListView.row >= m_ListView.totalRows || m_ListView.row < 0)
                {
                    if (m_ActiveText.Length != 0)
                        SetActiveEntry(null);
                }
                else
                {
                    LogEntry entry = new LogEntry();
                    LogEntries.GetEntryInternal(m_ListView.row, entry);
                    SetActiveEntry(entry);

                    // see if selected entry changed. if so - clear additional info
                    LogEntries.GetEntryInternal(m_ListView.row, entry);
                    if (m_ListView.selectionChanged || !m_ActiveText.Equals(entry.condition))
                    {
                        SetActiveEntry(entry);
                    }
                }

                // Open entry using return key
                if ((GUIUtility.keyboardControl == m_ListView.ID) && (e.type == EventType.KeyDown) && (e.keyCode == KeyCode.Return) && (m_ListView.row != 0))
                {
                    selectedRow = m_ListView.row;
                    openSelectedItem = true;
                }

                if (e.type != EventType.Layout && ListViewGUI.ilvState.rectHeight != 1)
                    ms_LVHeight = ListViewGUI.ilvState.rectHeight;

                if (openSelectedItem)
                {
                    rowDoubleClicked = selectedRow;
                    e.Use();
                }

                if (selectedRow != -1)
                {
                    m_ListViewMessage.row = -1;
                }
            }

            // Prevent dead locking in EditorMonoConsole by delaying callbacks (which can log to the console) until after LogEntries.EndGettingEntries() has been
            // called (this releases the mutex in EditorMonoConsole so logging again is allowed). Fix for case 1081060.
            if (rowDoubleClicked != -1)
                StacktraceListView_RowGotDoubleClicked();

            EditorGUIUtility.SetIconSize(Vector2.zero);

            // Display active text (We want word wrapped text with a vertical scrollbar)
            //m_TextScroll = GUILayout.BeginScrollView(m_TextScroll, Constants.Box);
            //float height = Constants.MessageStyle.CalcHeight(new GUIContent(m_ActiveText), position.width);
            //EditorGUILayoutTiny.SelectableLabel(m_ActiveText, Constants.MessageStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true), GUILayout.MinHeight(height));
            //GUILayout.EndScrollView();
            StacktraceListView(e, tempContent);

            SplitterGUILayout.EndVerticalSplit();

            // Copy & Paste selected item
            if ((e.type == EventType.ValidateCommand || e.type == EventType.ExecuteCommand) && e.commandName == "Copy" && m_ActiveText != string.Empty)
            {
                if (e.type == EventType.ExecuteCommand)
                    EditorGUIUtility.systemCopyBuffer = m_ActiveText;
                e.Use();
            }
        }

        private void SearchField(Event e)
        {
            string searchBarName = "SearchFilter";
            if (e.commandName == "Find")
            {
                if (e.type == EventType.ExecuteCommand)
                {
                    EditorGUI.FocusTextInControl(searchBarName);
                }

                if (e.type != EventType.Layout)
                    e.Use();
            }

            string searchText = m_SearchText;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Escape)
                {
                    searchText = string.Empty;
                    GUIUtility.keyboardControl = m_ListView.ID;
                    Repaint();
                }
                else if ((e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.DownArrow) &&
                         GUI.GetNameOfFocusedControl() == searchBarName)
                {
                    GUIUtility.keyboardControl = m_ListView.ID;
                }
            }

            GUI.SetNextControlName(searchBarName);
            Rect rect = GUILayoutUtility.GetRect(0, EditorGUILayout.kLabelFloatMaxW * 1.5f, EditorGUI.kSingleLineHeight,
                EditorGUI.kSingleLineHeight, EditorStyles.toolbarSearchField, GUILayout.MinWidth(100),
                GUILayout.MaxWidth(300));
            var filteringText = EditorGUI.ToolbarSearchField(rect, searchText, false);
            if (m_SearchText != filteringText)
            {
                m_SearchText = filteringText;
                LogEntries.SetFilteringText(filteringText);
                // Reset the active entry when we change the filtering text
                SetActiveEntry(null);
            }
        }

        #region Stacktrace

        private class StacktraceLineInfo
        {
            public string plain;
            public string text;
            public string filePath;
            public int lineNum;
        }

        private void StacktraceListView_Parse(string preActiveText, string nowActiveText)
        {
            if (preActiveText == nowActiveText)
            {
                if (!(nowActiveText.Length > 0 && m_StacktraceLineInfos.Count == 0))
                {
                    return;
                }
            }
            var lines = nowActiveText.Split(new string[] { "\n" }, StringSplitOptions.None);
            m_StacktraceLineInfos.Clear();
            m_ListViewMessage.scrollPos.y = 0;

            string rootDirectory = System.IO.Path.Combine(Application.dataPath, "..");
            Uri uriRoot = new Uri(rootDirectory);
            string textBeforeFilePath = ") (at ";
            string textUnityEngineDebug = "UnityEngine.Debug";
            string fileInBuildSlave = "C:/buildslave/unity/";
            string luaCFunction = "[C]";
            string luaMethodBefore = ": in function ";
            string luaFileExt = ".lua";
            string luaAssetPath = "Assets/Lua/";
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrEmpty(line))
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
                m_StacktraceLineInfos.Add(info);

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

                            if (!filePath.StartsWith(fileInBuildSlave, StringComparison.Ordinal))
                            {
                                alphaColor = false;
                            }

                            info.filePath = filePath;
                            info.lineNum = int.Parse(lineString);

                            if (filePath.Length > 2 && filePath[1] == ':')
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
                info.text = string.Format("<color=#4E5B6A>{0}</color>" +
                                          "<color=#2A577B>{1}</color>" +
                                          "<color=#246581>{2}</color>" +
                                          "<color=#425766>{3}</color>" +
                                          "<color=#375860>{4}</color>" +
                                          "<color=#4A6E8A>{5}</color>" +
                                          "<color=#375860>{6}</color>",
                    namespaceString, classString, methodString, argsString, fileString, fileNameString, fileLineString);
            }
            else
            {
                info.text = string.Format("<color=#6A87A7>{0}</color>" +
                                          "<color=#1A7ECD>{1}</color>" +
                                          "<color=#0D9DDC>{2}</color>" +
                                          "<color=#4F7F9F>{3}</color>" +
                                          "<color=#375860>{4}</color>" +
                                          "<color=#4A6E8A>{5}</color>" +
                                          "<color=#375860>{6}</color>",
                    namespaceString, classString, methodString, argsString, fileString, fileNameString, fileLineString);
            }

            return true;
        }

        private bool StacktraceListView_Parse_Lua(string line, StacktraceLineInfo info, 
            string luaCFunction, string luaMethodBefore, string luaFileExt, string luaAssetPath)
        {
            if (line[0] != '	')
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

                        info.filePath = luaAssetPath + filePath;
                        info.lineNum = int.Parse(lineString);

                        string namespaceString = String.Empty;
                        int classFirstIndex = filePath.LastIndexOf('/');
                        if (classFirstIndex > 0)
                        {
                            namespaceString = filePath.Substring(0, classFirstIndex + 1);
                        }

                        string classString = filePath.Substring(classFirstIndex + 1,
                            filePath.Length - namespaceString.Length - luaFileExt.Length);


                        info.text = string.Format("	<color=#6A87A7>{0}</color>" +
                                                  "<color=#1A7ECD>{1}</color>" +
                                                  "<color=#375860>:{2}</color>" +
                                                  "<color=#375860>{3}</color>" +
                                                  "<color=#0D9DDC>{4}</color>",
                            namespaceString, classString, lineString, luaMethodBefore, methodString);
                    }
                }
            }
            else
            {
                info.text = string.Format("<color=#375860>{0}</color>" +
                                          "<color=#246581>{1}</color>",
                    preMethodString, methodString);
            }

            return true;
        }

        private void StacktraceListView(Event e, GUIContent tempContent)
        {
            var maxLine = -1;
            var maxLineLen = -1;
            for (int i = 0; i < m_StacktraceLineInfos.Count; i++)
            {
                if (maxLineLen < m_StacktraceLineInfos[i].plain.Length)
                {
                    maxLineLen = m_StacktraceLineInfos[i].plain.Length;
                    maxLine = i;
                }
            }

            float maxWidth = 1f;
            if (maxLine != -1)
            {
                tempContent.text = m_StacktraceLineInfos[maxLine].plain;
                maxWidth = Constants.MessageStyle.CalcSize(tempContent).x;
            }

            if (m_StacktraceLineContextClickRow != -1)
            {
                var stacktraceLineInfo = m_StacktraceLineInfos[m_StacktraceLineContextClickRow];
                m_StacktraceLineContextClickRow = -1;
                GenericMenu menu = new GenericMenu();
                if (!string.IsNullOrEmpty(stacktraceLineInfo.filePath))
                {
                    menu.AddItem(new GUIContent("Open"), false, StacktraceListView_Open, stacktraceLineInfo);
                    menu.AddSeparator("");
                }
                menu.AddItem(new GUIContent("Copy"), false, StacktraceListView_Copy, stacktraceLineInfo);
                menu.AddItem(new GUIContent("Copy All"), false, StacktraceListView_CopyAll);
                menu.ShowAsContext();
            }

            int id = GUIUtility.GetControlID(0);
            int rowDoubleClicked = -1;
            int selectedRow = -1;
            bool openSelectedItem = false;
            m_ListViewMessage.totalRows = m_StacktraceLineInfos.Count;
            GUILayout.BeginHorizontal(Constants.Box);
            m_ListViewMessage.scrollPos = EditorGUILayout.BeginScrollView(m_ListViewMessage.scrollPos);
            ListViewGUI.ilvState.beganHorizontal = true;
            m_ListViewMessage.draggedFrom = -1;
            m_ListViewMessage.draggedTo = -1;
            m_ListViewMessage.fileNames = (string[])null;
            Rect rect = GUILayoutUtility.GetRect(maxWidth,
                (float)(m_ListViewMessage.totalRows * m_ListViewMessage.rowHeight + 3));
            foreach (ListViewElement el in ListViewGUI.DoListView(rect, m_ListViewMessage, null, string.Empty))
            {
                if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1) && el.position.Contains(e.mousePosition))
                {
                    if (e.button == 1)
                    {
                        m_ListViewMessage.row = el.row;
                        selectedRow = m_ListViewMessage.row;
                        m_StacktraceLineContextClickRow = selectedRow;
                        continue;
                    }

                    selectedRow = m_ListViewMessage.row;
                    if (e.clickCount == 2)
                        openSelectedItem = true;
                }
                else if (e.type == EventType.Repaint)
                {
                    tempContent.text = m_StacktraceLineInfos[el.row].text;
                    rect = el.position;
                    if (rect.width < maxWidth)
                    {
                        rect.width = maxWidth;
                    }
                    Constants.MessageStyle.Draw(rect, tempContent, id, m_ListViewMessage.row == el.row);
                }
            }

            // Open entry using return key
            if ((GUIUtility.keyboardControl == m_ListViewMessage.ID) && (e.type == EventType.KeyDown) && (e.keyCode == KeyCode.Return) && (m_ListViewMessage.row != 0))
            {
                selectedRow = m_ListViewMessage.row;
                openSelectedItem = true;
            }

            if (openSelectedItem)
            {
                rowDoubleClicked = selectedRow;
                e.Use();
            }

            if (m_StacktraceLineContextClickRow != -1)
            {
                Repaint();
            }

            if (rowDoubleClicked != -1)
            {
                StacktraceListView_Open(m_StacktraceLineInfos[rowDoubleClicked]);
            }
        }

        private void StacktraceListView_RowGotDoubleClicked()
        {
            foreach (var stacktraceLineInfo in m_StacktraceLineInfos)
            {
                if (!string.IsNullOrEmpty(stacktraceLineInfo.filePath))
                {
                    StacktraceListView_Open(stacktraceLineInfo);
                    break;
                }
            }
        }

        private void StacktraceListView_Open(object userData)
        {
            var stacktraceLineInfo = userData as StacktraceLineInfo;
            if (stacktraceLineInfo != null)
            {
                var filePath = stacktraceLineInfo.filePath;
                var lineNum = stacktraceLineInfo.lineNum;
                ScriptAssetOpener.OpenAsset(filePath, lineNum);
            }
        }

        private void StacktraceListView_Copy(object userData)
        {
            var stacktraceLineInfo = userData as StacktraceLineInfo;
            if (stacktraceLineInfo != null)
            {
                EditorGUIUtility.systemCopyBuffer = stacktraceLineInfo.plain;
            }
        }

        private void StacktraceListView_CopyAll()
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var stacktraceLineInfo in m_StacktraceLineInfos)
            {
                stringBuilder.AppendLine(stacktraceLineInfo.plain);
            }

            EditorGUIUtility.systemCopyBuffer = stringBuilder.ToString();
        }

#endregion

        public static bool GetConsoleErrorPause()
        {
            return HasFlag(ConsoleFlags.ErrorPause);
        }

        public static void SetConsoleErrorPause(bool enabled)
        {
            SetFlag(ConsoleFlags.ErrorPause, enabled);
        }

        public struct StackTraceLogTypeData
        {
            public LogType logType;
            public StackTraceLogType stackTraceLogType;
        }

        public void ToggleLogStackTraces(object userData)
        {
            StackTraceLogTypeData data = (StackTraceLogTypeData)userData;
            PlayerSettings.SetStackTraceLogType(data.logType, data.stackTraceLogType);
        }

        public void ToggleLogStackTracesForAll(object userData)
        {
            foreach (LogType logType in Enum.GetValues(typeof(LogType)))
                PlayerSettings.SetStackTraceLogType(logType, (StackTraceLogType)userData);
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            if (Application.platform == RuntimePlatform.OSXEditor)
                menu.AddItem(EditorGUIUtility.TrTextContent("Open Player Log"), false, UnityEditorInternal.InternalEditorUtility.OpenPlayerConsole);
            menu.AddItem(EditorGUIUtility.TrTextContent("Open Editor Log"), false, UnityEditorInternal.InternalEditorUtility.OpenEditorConsole);

            menu.AddItem(EditorGUIUtility.TrTextContent("Show Timestamp"), HasFlag(ConsoleFlags.ShowTimestamp), SetTimestamp);

            for (int i = 1; i <= 10; ++i)
            {
                var lineString = i == 1 ? "Line" : "Lines";
                menu.AddItem(new GUIContent(string.Format("Log Entry/{0} {1}", i, lineString)), i == Constants.LogStyleLineCount, SetLogLineCount, i);
            }

            AddStackTraceLoggingMenu(menu);
        }

        private void SetTimestamp()
        {
            SetFlag(ConsoleFlags.ShowTimestamp, !HasFlag(ConsoleFlags.ShowTimestamp));
        }

        private void SetLogLineCount(object obj)
        {
            int count = (int)obj;
            EditorPrefs.SetInt("ConsoleWindowLogLineCount", count);
            Constants.LogStyleLineCount = count;

            UpdateListView();
        }

        private void AddStackTraceLoggingMenu(GenericMenu menu)
        {
            // TODO: Maybe remove this, because it basically duplicates UI in PlayerSettings
            foreach (LogType logType in Enum.GetValues(typeof(LogType)))
            {
                foreach (StackTraceLogType stackTraceLogType in Enum.GetValues(typeof(StackTraceLogType)))
                {
                    StackTraceLogTypeData data;
                    data.logType = logType;
                    data.stackTraceLogType = stackTraceLogType;

                    menu.AddItem(EditorGUIUtility.TrTextContent("Stack Trace Logging/" + logType + "/" + stackTraceLogType), PlayerSettings.GetStackTraceLogType(logType) == stackTraceLogType,
                        ToggleLogStackTraces, data);
                }
            }

            int stackTraceLogTypeForAll = (int)PlayerSettings.GetStackTraceLogType(LogType.Log);
            foreach (LogType logType in Enum.GetValues(typeof(LogType)))
            {
                if (PlayerSettings.GetStackTraceLogType(logType) != (StackTraceLogType)stackTraceLogTypeForAll)
                {
                    stackTraceLogTypeForAll = -1;
                    break;
                }
            }

            foreach (StackTraceLogType stackTraceLogType in Enum.GetValues(typeof(StackTraceLogType)))
            {
                menu.AddItem(EditorGUIUtility.TrTextContent("Stack Trace Logging/All/" + stackTraceLogType), (StackTraceLogType)stackTraceLogTypeForAll == stackTraceLogType,
                    ToggleLogStackTracesForAll, stackTraceLogType);
            }
        }

        private static event EntryDoubleClickedDelegate entryWithManagedCallbackDoubleClicked;

        //[RequiredByNativeCode]
        private static void SendEntryDoubleClicked(LogEntry entry)
        {
            if (ConsoleWindow.entryWithManagedCallbackDoubleClicked != null)
                ConsoleWindow.entryWithManagedCallbackDoubleClicked(entry);
        }

        // This method is used by the Visual Scripting project. Please do not delete. Contact @husseink for more information.
        internal void AddMessageWithDoubleClickCallback(string condition, string file, int mode, int instanceID)
        {
            var outputEntry = new LogEntry { condition = condition, file = file, mode = mode, instanceID = instanceID };
            LogEntries.AddMessageWithDoubleClickCallback(outputEntry);
        }
    }

    internal class GettingLogEntriesScope : IDisposable
    {
        private bool m_Disposed;

        public GettingLogEntriesScope(ListViewState listView)
        {
            listView.totalRows = LogEntries.StartGettingEntries();
        }

        public void Dispose()
        {
            if (m_Disposed)
                return;
            LogEntries.EndGettingEntries();
            m_Disposed = true;
        }

        ~GettingLogEntriesScope()
        {
            if (!m_Disposed)
                Debug.LogError("Scope was not disposed! You should use the 'using' keyword or manually call Dispose.");
        }
    }
}
