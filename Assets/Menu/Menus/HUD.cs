using TMPro;
using UnityEngine;

namespace Menu.Menus
{
    public class HUD : MonoBehaviour
    {
        [SerializeField] TMP_Text objectiveText;
        [SerializeField] TMP_Text specialOfferText;
        [SerializeField] TMP_Text statusText;
        [SerializeField] TMP_Text cursorText;
    
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            
        }

        // Update is called once per frame
        void Update()
        {
        
        }
        
        public void SetObjectiveText(string text)
        {
            objectiveText.text = text;
        }
        
        public void SetSpecialOfferText(string text)
        {
            specialOfferText.text = text;
        }

        public void SetStatusText(string text)
        {
            statusText.text = text;
        }
    
        public void SetCursorText(string text)
        {
            cursorText.text = text;
        }
    }
}
