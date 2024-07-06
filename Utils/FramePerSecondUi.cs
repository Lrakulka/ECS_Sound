using UnityEngine;
using UnityEngine.UI;
using static ECS_Common.Utils.CommonConstants;
using static ECS_Common.Utils.CommonUtils;

namespace ECS_Common.Utils
{
    public class FramePerSecondUi : MonoBehaviour
    {
        public Text text;

        private float deltaTime;

        void Update()
        {
            deltaTime += (Time.deltaTime - deltaTime) * 0.1f;
            var fps = 1.0f / deltaTime;

            if (IsNotUpdate(this, UI_UPDATE_500_MS)) return;
            text.text = $"{fps:0.} FPS";
        }
    }
}
