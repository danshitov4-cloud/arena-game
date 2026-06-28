using UnityEngine;
using UnityEngine.InputSystem;

public class KeyboardDebugOverlay : MonoBehaviour
{
    private string lastKey = "none";
    private string inputSystem = "checking...";

    private void Update()
    {
        var keyboard = Keyboard.current;

        if (keyboard != null)
        {
            inputSystem = "NEW Input System OK";
            if (keyboard.wKey.wasPressedThisFrame)         lastKey = "W";
            if (keyboard.aKey.wasPressedThisFrame)         lastKey = "A";
            if (keyboard.sKey.wasPressedThisFrame)         lastKey = "S";
            if (keyboard.dKey.wasPressedThisFrame)         lastKey = "D";
            if (keyboard.upArrowKey.wasPressedThisFrame)   lastKey = "UpArrow";
            if (keyboard.downArrowKey.wasPressedThisFrame) lastKey = "DownArrow";
            if (keyboard.leftArrowKey.wasPressedThisFrame) lastKey = "LeftArrow";
            if (keyboard.rightArrowKey.wasPressedThisFrame)lastKey = "RightArrow";
            if (keyboard.rKey.wasPressedThisFrame)         lastKey = "R";
        }
        else
        {
            inputSystem = "Keyboard.current == NULL";
        }

        if (Input.anyKeyDown)
            lastKey += " (legacy sees it too)";
    }

    private void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 22 };

        style.normal.textColor = Keyboard.current != null ? Color.green : Color.red;
        GUI.Label(new Rect(20, 55, 600, 35), $"Input System: {inputSystem}", style);

        style.normal.textColor = Color.yellow;
        GUI.Label(new Rect(20, 90, 600, 35), $"Last key: {lastKey}", style);
    }
}
