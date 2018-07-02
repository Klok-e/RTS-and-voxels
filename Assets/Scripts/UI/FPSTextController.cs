using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class FPSTextController : MonoBehaviour
{
    private Text fpsText;

    private float prevFps = 0;
    private float prevprevFps = 0;

    private void Start()
    {
        fpsText = GetComponent<Text>();
    }

    private void Update()
    {
        float currFps = 1f / Time.deltaTime;

        fpsText.text = Math.Round((currFps + prevFps + prevprevFps) / 3f, 2).ToString();

        prevprevFps = prevFps;
        prevFps = currFps;
    }
}
