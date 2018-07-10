using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Scripts.UI
{
    public class ButtonPressedEventInvokerController : MonoBehaviour
    {
        public KeyCode button;
        public UnityEvent toCallWhenButtonPressed;

        private void Update()
        {
            if (Input.GetKeyDown(button))
            {
                toCallWhenButtonPressed.Invoke();
            }
        }
    }
}
