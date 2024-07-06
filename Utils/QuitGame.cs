using UnityEngine;

namespace ECS_Common.Utils
{
    public class QuitGame : MonoBehaviour
    {
        void Update()
        {
            if (Input.GetKey("escape"))
            {
                Application.Quit();
            }
        }
    }
}
