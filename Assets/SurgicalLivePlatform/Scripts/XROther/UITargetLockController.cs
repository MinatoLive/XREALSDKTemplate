using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SurgicalLivePlatform
{
    /// <summary>
    /// Holds the UI Button target for a hand-ray press so a short ray drift does not retarget the press.
    /// It does not freeze the ray, input actions, or any transform.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UITargetLockController : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float maxLockSeconds = 2f;

        private struct LockedTarget
        {
            public int pointerId;
            public Button button;
            public float lockedAt;
        }

        private readonly List<LockedTarget> _locks = new List<LockedTarget>(4);

        public void Lock(int pointerId, Button button)
        {
            if (button == null)
            {
                return;
            }

            for (var i = 0; i < _locks.Count; i++)
            {
                if (_locks[i].pointerId != pointerId)
                {
                    continue;
                }

                var existing = _locks[i];
                existing.button = button;
                existing.lockedAt = Time.unscaledTime;
                _locks[i] = existing;
                return;
            }

            _locks.Add(new LockedTarget
            {
                pointerId = pointerId,
                button = button,
                lockedAt = Time.unscaledTime
            });
        }

        public void Unlock(int pointerId)
        {
            for (var i = _locks.Count - 1; i >= 0; i--)
            {
                if (_locks[i].pointerId == pointerId)
                {
                    _locks.RemoveAt(i);
                }
            }
        }

        public bool TryGetLockedButton(int pointerId, out Button button)
        {
            for (var i = 0; i < _locks.Count; i++)
            {
                var locked = _locks[i];
                if (locked.pointerId != pointerId)
                {
                    continue;
                }

                if (locked.button != null && locked.button.IsActive())
                {
                    button = locked.button;
                    return true;
                }

                _locks.RemoveAt(i);
                break;
            }

            button = null;
            return false;
        }

        private void Update()
        {
            var now = Time.unscaledTime;
            for (var i = _locks.Count - 1; i >= 0; i--)
            {
                var locked = _locks[i];
                if (locked.button == null ||
                    !locked.button.IsActive() ||
                    now - locked.lockedAt > maxLockSeconds)
                {
                    _locks.RemoveAt(i);
                }
            }
        }

        private void OnDisable()
        {
            _locks.Clear();
        }
    }
}
