﻿using UnityEngine;
using UnityEngine.EventSystems;

public class OnClickMsgClickedSpace : MonoBehaviour {
    [HideInInspector] public int x, z;

    private void OnMouseDown() {
        if (!EventSystem.current.IsPointerOverGameObject() && MouseHoverHighlight.isEffectActive) {
            GameManager.instance.SendMessage("MessageClickedSpace", new Vector2(x, z)); // Using a Vector2 to hold an X,Z because SendMessage can only handle ONE param
        }
    }
}
