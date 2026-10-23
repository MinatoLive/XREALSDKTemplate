using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SurgicalLivePlatform
{
    /// <summary>
    /// Shared target classification for hand-ray UI presses.
    /// </summary>
    public static class UIPressTargetResolver
    {
        public static bool IsLocalPointInsideExpandedRect(Vector2 localPoint, Rect rect, float marginPercent)
        {
            var margin = Mathf.Max(0f, marginPercent);
            var marginX = rect.width * margin;
            var marginY = rect.height * margin;

            return localPoint.x >= rect.xMin - marginX &&
                   localPoint.x <= rect.xMax + marginX &&
                   localPoint.y >= rect.yMin - marginY &&
                   localPoint.y <= rect.yMax + marginY;
        }

        public static bool TryGetRayRectPoint(
            Vector3 origin,
            Vector3 direction,
            RectTransform rect,
            out Vector3 worldPoint,
            out Vector3 worldNormal)
        {
            worldPoint = default;
            worldNormal = default;

            if (rect == null || direction.sqrMagnitude < 0.000001f)
            {
                return false;
            }

            worldNormal = rect.forward;
            var plane = new Plane(worldNormal, rect.position);
            var ray = new Ray(origin, direction.normalized);
            if (!plane.Raycast(ray, out var distance) || distance < 0f)
            {
                return false;
            }

            worldPoint = ray.GetPoint(distance);
            return true;
        }

        public static bool IsWorldPointInsideExpandedRect(
            RectTransform rect,
            Vector3 worldPoint,
            float marginPercent)
        {
            if (rect == null)
            {
                return false;
            }

            var local = rect.InverseTransformPoint(worldPoint);
            return IsLocalPointInsideExpandedRect(new Vector2(local.x, local.y), rect.rect, marginPercent);
        }

        public static Button FindButton(GameObject hoverTarget)
        {
            return hoverTarget != null ? hoverTarget.GetComponentInParent<Button>() : null;
        }

        public static bool IsInsideScrollRect(Button button)
        {
            if (button == null)
            {
                return false;
            }

            var scrollRect = button.GetComponentInParent<ScrollRect>();
            return scrollRect != null && scrollRect.isActiveAndEnabled && scrollRect.content != null;
        }

        public static bool IsDraggable(Button button)
        {
            if (button == null)
            {
                return false;
            }

            if (button.GetComponentInParent<Slider>() != null)
            {
                return true;
            }

            // EventTrigger implements drag interfaces even when it has no drag entries.
            var eventTrigger = button.GetComponent<EventTrigger>();
            if (eventTrigger != null && eventTrigger.triggers != null)
            {
                for (var i = 0; i < eventTrigger.triggers.Count; i++)
                {
                    var eventId = eventTrigger.triggers[i].eventID;
                    if (eventId == EventTriggerType.BeginDrag ||
                        eventId == EventTriggerType.Drag ||
                        eventId == EventTriggerType.EndDrag ||
                        eventId == EventTriggerType.InitializePotentialDrag)
                    {
                        return true;
                    }
                }
            }

            var behaviours = button.GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour is EventTrigger)
                {
                    continue;
                }

                if (behaviour is IBeginDragHandler || behaviour is IDragHandler)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
