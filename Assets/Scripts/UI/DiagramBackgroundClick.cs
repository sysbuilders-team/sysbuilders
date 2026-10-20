using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UI
{
    /// <summary>
    /// Attach to the diagram scroll-rect background Image.
    /// Clicking the empty diagram area cancels an in-progress association.
    /// </summary>
    public class DiagramBackgroundClick : MonoBehaviour, IPointerClickHandler
    {
        public event Action OnClicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            OnClicked?.Invoke();
        }
    }
}
