using System.Text;
using BarafPaani.Gameplay.PowerUps;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace BarafPaani.UI
{
    /// <summary>
    /// Shows what the local player is carrying, and how long is left on
    /// anything running.
    ///
    /// Finds the holder rather than being wired to it, for the same reason
    /// MatchHud finds the match: characters are spawned, so nothing in the scene
    /// can point at them ahead of time.
    /// </summary>
    public class PowerUpHud : MonoBehaviour
    {
        [SerializeField]
        private Text _carriedLabel;

        [SerializeField]
        private Text _activeLabel;

        [SerializeField]
        [Tooltip("Marks the one that would be used.")]
        private Color _selected = new Color(1f, 0.85f, 0.3f);

        private readonly StringBuilder _line = new StringBuilder();
        private PowerUpHolder _holder;
        private PowerUpEffects _effects;

        private void Update()
        {
            Find();

            if (_holder == null)
            {
                Show(false);
                return;
            }

            Show(true);
            ShowCarried();
            ShowActive();
        }

        private void ShowCarried()
        {
            if (_carriedLabel == null)
            {
                return;
            }

            if (_holder.Count == 0)
            {
                _carriedLabel.text = "No power-ups";
                return;
            }

            _line.Clear();

            for (int slot = 0; slot < _holder.Count; slot++)
            {
                if (slot > 0)
                {
                    _line.Append("   ");
                }

                string name = Name(_holder.At(slot));

                // Rich text rather than a widget per slot: three names is not
                // worth a pool of icons yet, and this reads the same.
                _line.Append(slot == _holder.Selected
                    ? $"<color=#{ColorUtility.ToHtmlStringRGB(_selected)}>[{name}]</color>"
                    : name);
            }

            _carriedLabel.text = _line.ToString();
        }

        private void ShowActive()
        {
            if (_activeLabel == null)
            {
                return;
            }

            float remaining = _effects != null ? _effects.SpeedBoostRemaining : 0f;

            _activeLabel.enabled = remaining > 0f;

            if (remaining > 0f)
            {
                _activeLabel.text = $"Speed  {remaining:0.0}s";
            }
        }

        private static string Name(PowerUpKind kind)
        {
            switch (kind)
            {
                case PowerUpKind.SpeedBoost:
                    return "Speed";

                case PowerUpKind.Invisibility:
                    return "Invisible";

                case PowerUpKind.Clone:
                    return "Clone";

                default:
                    return "-";
            }
        }

        private void Find()
        {
            if (_holder != null)
            {
                return;
            }

            NetworkIdentity player = NetworkClient.localPlayer;

            if (player == null)
            {
                return;
            }

            _holder = player.GetComponent<PowerUpHolder>();
            _effects = player.GetComponent<PowerUpEffects>();
        }

        private void Show(bool visible)
        {
            if (_carriedLabel != null)
            {
                _carriedLabel.enabled = visible;
            }

            if (_activeLabel != null && !visible)
            {
                _activeLabel.enabled = false;
            }
        }
    }
}
