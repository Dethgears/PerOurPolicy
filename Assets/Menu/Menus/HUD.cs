using UnityEngine;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    Text cursorText;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        cursorText = transform.Find("CursorText").GetComponent<Text>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    public void SetCursorText(string text)
    {
        cursorText.text = text;
    }
}
