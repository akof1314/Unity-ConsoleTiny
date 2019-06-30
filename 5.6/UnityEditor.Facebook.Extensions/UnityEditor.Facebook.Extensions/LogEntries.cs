using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ConsoleTiny
{
    public class LogEntries
    {
        private static string filteredText;
        private static List<int> filteredIndex = new List<int>();

        public static int consoleFlags
        {
            get { return UnityEditorInternal.LogEntries.consoleFlags; }
        }

        public static void SetConsoleFlag(int bit, bool value)
        {
            UnityEditorInternal.LogEntries.SetConsoleFlag(bit, value);
        }

        public static void Clear()
        {
            UnityEditorInternal.LogEntries.Clear();
        }

        public static void GetCountsByType(ref int errorCount, ref int warningCount, ref int logCount)
        {
            UnityEditorInternal.LogEntries.GetCountsByType(ref errorCount, ref warningCount, ref logCount);
        }

        public static int GetCount()
        {
            MatchFilteringText();
            return GetFilteredIndexCount();
        }

        public static void GetLinesAndModeFromEntryInternal(int row, int numberOfLines, ref int mask, [In, Out] ref string outString)
        {
            UnityEditorInternal.LogEntries.GetFirstTwoLinesEntryTextAndModeInternal(GetRowByFilteredIndex(row), ref mask, ref outString);
            if (numberOfLines == 1)
            {
                var index = outString.IndexOf('\n');
                if (index > 0)
                {
                    outString = outString.Substring(0, index);
                }
            }
        }

        internal static bool GetEntryInternal(int row, [Out] UnityEditorInternal.LogEntry outputEntry)
        {
            return UnityEditorInternal.LogEntries.GetEntryInternal(GetRowByFilteredIndex(row), outputEntry);
        }

        public static int GetEntryCount(int row)
        {
            return UnityEditorInternal.LogEntries.GetEntryCount(GetRowByFilteredIndex(row));
        }

        internal static void AddMessageWithDoubleClickCallback(UnityEditorInternal.LogEntry outputEntry)
        {
            //UnityEditorInternal.LogEntries.AddMessageWithDoubleClickCallback(outputEntry);
        }

        public static int StartGettingEntries()
        {
            UnityEditorInternal.LogEntries.StartGettingEntries();
            return GetFilteredIndexCount();
        }

        public static void EndGettingEntries()
        {
            UnityEditorInternal.LogEntries.EndGettingEntries();
        }

        public static void SetFilteringText(string filteringText)
        {
            filteredText = filteringText;
            MatchFilteringText();
        }

        public static void TestFilteringText(string filteringText)
        {
            if (filteredText != filteringText)
            {
                SetFilteringText(filteringText);
            }
        }

        private static int GetFilteredIndexCount()
        {
            return filteredIndex.Count;
        }

        private static int GetRowByFilteredIndex(int index)
        {
            return filteredIndex[index];
        }

        private static void MatchFilteringText()
        {
            filteredIndex.Clear();
            int count = UnityEditorInternal.LogEntries.GetCount();
            if (string.IsNullOrEmpty(filteredText))
            {
                for (int i = 0; i < count; i++)
                {
                    filteredIndex.Add(i);
                }
                return;
            }

            UnityEditorInternal.LogEntries.StartGettingEntries();
            for (int i = 0; i < count; i++)
            {
                int mask = 0;
                string text = null;
                UnityEditorInternal.LogEntries.GetFirstTwoLinesEntryTextAndModeInternal(i, ref mask, ref text);

                if (!string.IsNullOrEmpty(text) && text.Contains(filteredText))
                {
                    filteredIndex.Add(i);
                }
            }
            UnityEditorInternal.LogEntries.EndGettingEntries();
        }
    }
}