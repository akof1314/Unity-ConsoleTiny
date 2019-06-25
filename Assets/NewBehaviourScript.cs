using System.Collections;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    [ContextMenu("test")]
    public void Test()
    {
        Debug.Log("abcabc");
        Debug.Log("a222bcabc");
        Debug.Log("abcabc");
        Debug.LogError("EditorMonoConsoleEditorMonoConsole");
        Debug.Log("abcabc");
        Debug.LogWarning("EditorMonoConsoleEditorMonoConsole");
    }
}
