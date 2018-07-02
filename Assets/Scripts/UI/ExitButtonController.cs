using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
