using UnityEngine;

namespace DLLTest
{
    public class Test : MonoBehaviour
    {
        [ContextMenu("test0")]
        public void Test0()
        {
            Debug.Log("DLLabcabc");
            Debug.LogError("DLLabcabc22222222222222");
            Test1();
        }

        private void Test1()
        {
            Test2();
        }

        private void Test2()
        {
            Debug.Log("DLLmore");
        }
    }
}
