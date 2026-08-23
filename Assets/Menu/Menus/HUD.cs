using TMPro;
using UnityEngine;

namespace Menu.Menus
{
    public class HUD : MonoBehaviour
    {
        TMP_Text cursorText;
    
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            cursorText = transform.Find("CursorText").GetComponent<TMP_Text>();
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
}
