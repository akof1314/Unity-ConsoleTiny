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
        Debug.LogError("Opens asset in an external editor, \ntexture application or modelling tool depending on what type of asset it is. \nIf it is a text file, lineNumber instructs the text editor to go to that line. Returns true if asset opened successfully.");
        Debug.Log("abcabc");
        Debug.LogWarning("Description");
        Test1("aaaa1");
    }

    private void Test1(string ta)
    {
        Test2();
    }

    private void Test2()
    {
        Debug.Log("more");
    }
}
