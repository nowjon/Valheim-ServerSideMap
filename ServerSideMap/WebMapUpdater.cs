using UnityEngine;

namespace ServerSideMap
{
    public class WebMapUpdater : MonoBehaviour
    {
        public void Initialize()
        {
            // Initialization if needed
        }

        void Update()
        {
            if (_ZNet.IsServer(_ZNet._instance))
            {
                PlayerTracker.Update();
            }
        }
    }
}

