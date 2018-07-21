using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.UI
{
    public class MenuController : MonoBehaviour
    {
        private bool isPaused = false;

        [SerializeField]
        private GameObject menuGraphic;

        private void Start()
        {
            menuGraphic.SetActive(false);
        }

        public void WhenEscPressed()
        {
            if (isPaused)
                Resume();
            else
                Pause();
        }

        private void Pause()
        {
            if (isPaused)
                throw new System.Exception();

            isPaused = true;

            menuGraphic.SetActive(true);

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.Confined;
        }

        private void Resume()
        {
            if (!isPaused)
                throw new System.Exception();

            isPaused = false;

            menuGraphic.SetActive(false);

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Confined;
        }
    }
}
