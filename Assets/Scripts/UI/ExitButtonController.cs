using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    [RequireComponent(typeof(Button))]
    public class ExitButtonController : MonoBehaviour
    {
        private void Start()
        {
            var b = GetComponent<Button>();
            b.onClick.AddListener(Exit);
        }

        private void Exit()
        {
            Application.Quit();
        }
    }
}