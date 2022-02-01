using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bulldozer
{
    public class CheckboxControl : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private GameObject _hoverText;
        public string HoverText = "";
        public event Action<PointerEventData> onClick;
        public Text textObject;

        public void Start()
        {
            if (textObject != null)
            {
                _hoverText = textObject.gameObject;
                _hoverText.SetActive(false);
                textObject.text = HoverText;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_hoverText == null)
            {
                Console.WriteLine("hovertext not initialized");
                Start();
            }
            else
            {
                _hoverText.SetActive(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_hoverText == null)
            {
                Console.WriteLine("hovertext not initialized");
                Start();
            }
            else
            {
                _hoverText.SetActive(false);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            onClick?.Invoke(eventData);
        }
    }
}