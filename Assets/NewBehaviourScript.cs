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
        Test1("aaaa11");
    }

    [ContextMenu("test2")]
    public void Test22()
    {
        Debug.Log("abcabc");
        Debug.Log("a2220bcabc");
        Debug.Log("abcabc");
    }

    private void Test1(string ta)
    {
        Test2();
        Test3();
    }

    private void Test2()
    {
        Debug.Log("more");
        //string a = "aaaa";
        //a.LastIndexOf('.', 8, 20);
    }

    private void Test3()
    {
        for (int i = 0; i < 9999; i++)
        {
            Debug.LogError("A variant of Debug.Log that logs an error message to the console.");
            Debug.Log("Assert a condition and logs an error message to the Unity console on failure");
            Debug.LogWarning("Class containing methods to ease debugging while developing a game");
        }
    }
}
