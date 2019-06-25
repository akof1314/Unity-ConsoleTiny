using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEditor;

namespace ConsoleTiny
{
    public class EditorGUILayoutTiny
    {
        public static void SelectableLabel(string text, GUIStyle style, params GUILayoutOption[] options)
        {
            var lines = text.Split(new string[] { "\n" }, StringSplitOptions.None);
            float y = 0;
            Rect rect = GUILayoutUtility.GetRect(new GUIContent(text), style, options);
            for (int i = 0; i < lines.Length; ++i)
            {
                float height = style.CalcHeight(new GUIContent(lines[i]), rect.width);
                string textBeforeFilePath = ") (at ";
                int filePathIndex = lines[i].IndexOf(textBeforeFilePath, StringComparison.Ordinal);
                if (filePathIndex > 0)
                {
                    filePathIndex += textBeforeFilePath.Length;
                    if (lines[i][filePathIndex] != '<') // sometimes no url is given, just an id between <>, we can't do an hyperlink
                    {
                        string filePathPart = lines[i].Substring(filePathIndex);
                        int lineIndex = filePathPart.LastIndexOf(":", StringComparison.Ordinal); // LastIndex because the url can contain ':' ex:"C:"
                        if (lineIndex > 0)
                        {
                            int endLineIndex = filePathPart.LastIndexOf(")", StringComparison.Ordinal); // LastIndex because files or folder in the url can contain ')'
                            if (endLineIndex > 0)
                            {
                                string lineString =
                                    filePathPart.Substring(lineIndex + 1, (endLineIndex) - (lineIndex + 1));
                                string filePath = filePathPart.Substring(0, lineIndex);

                                string labelText = lines[i].Substring(0, filePathIndex);
                                Vector2 size = style.CalcSize(new GUIContent(labelText));
                                EditorGUI.SelectableLabel(new Rect(0, y, rect.width, height), labelText, style);

                                string fileText = string.Format("<color=#4F80F8>{0}:{1}</color>)", filePath, lineString);
                                GUIContent fileTextContent = new GUIContent(fileText);
                                float fileTextWidth = style.CalcSize(fileTextContent).x;
                                Rect fileTextRect = new Rect(size.x - 6, y, fileTextWidth, height);
                                if (GUI.Button(fileTextRect, fileText, style))
                                {
                                    Debug.Log(fileText);
                                    GUIUtility.ExitGUI();
                                }
                                EditorGUIUtility.AddCursorRect(fileTextRect, MouseCursor.Link);
                                y += height - 4;

                                continue; // continue to evade the default case
                            }
                        }
                    }
                }
                EditorGUI.SelectableLabel(new Rect(0, y, rect.width, height), lines[i], style);
                y += height - 4;
            }
        }

        internal class HyperLinkClickedEventArgs : EventArgs
        {
            public Dictionary<string, string> hyperlinkInfos { get; private set; }

            public HyperLinkClickedEventArgs(Dictionary<string, string> hyperlinkInfos)
            {
                this.hyperlinkInfos = hyperlinkInfos;
            }
        }
    }
}