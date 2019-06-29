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
            get { return UnityEditor.LogEntries.consoleFlags; }
        }

        public static void SetConsoleFlag(int bit, bool value)
        {
            UnityEditor.LogEntries.SetConsoleFlag(bit, value);
        }

        public static void Clear()
        {
            UnityEditor.LogEntries.Clear();
        }

        public static void GetCountsByType(ref int errorCount, ref int warningCount, ref int logCount)
        {
            UnityEditor.LogEntries.GetCountsByType(ref errorCount, ref warningCount, ref logCount);
        }

        public static int GetCount()
        {
            MatchFilteringText();
            return GetFilteredIndexCount();
        }

        public static void GetLinesAndModeFromEntryInternal(int row, int numberOfLines, ref int mask, [In, Out] ref string outString)
        {
            UnityEditor.LogEntries.GetLinesAndModeFromEntryInternal(GetRowByFilteredIndex(row), numberOfLines, ref mask, ref outString);
        }

        internal static bool GetEntryInternal(int row, [Out] UnityEditor.LogEntry outputEntry)
        {
            return UnityEditor.LogEntries.GetEntryInternal(GetRowByFilteredIndex(row), outputEntry);
        }

        public static int GetEntryCount(int row)
        {
            return UnityEditor.LogEntries.GetEntryCount(GetRowByFilteredIndex(row));
        }

        internal static void AddMessageWithDoubleClickCallback(UnityEditor.LogEntry outputEntry)
        {
            UnityEditor.LogEntries.AddMessageWithDoubleClickCallback(outputEntry);
        }

        public static int StartGettingEntries()
        {
            UnityEditor.LogEntries.StartGettingEntries();
            return GetFilteredIndexCount();
        }

        public static void EndGettingEntries()
        {
            UnityEditor.LogEntries.EndGettingEntries();
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
            int count = UnityEditor.LogEntries.GetCount();
            if (string.IsNullOrEmpty(filteredText))
            {
                for (int i = 0; i < count; i++)
                {
                    filteredIndex.Add(i);
                }
                return;
            }

            UnityEditor.LogEntries.StartGettingEntries();
            for (int i = 0; i < count; i++)
            {
                int mode = 0;
                string text = null;
                UnityEditor.LogEntries.GetLinesAndModeFromEntryInternal(i, 1, ref mode, ref text);

                if (!string.IsNullOrEmpty(text) && text.Contains(filteredText))
                {
                    filteredIndex.Add(i);
                }
            }
            UnityEditor.LogEntries.EndGettingEntries();
        }
    }
}