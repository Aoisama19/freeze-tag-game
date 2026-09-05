using System.Collections.Generic;
using BarafPaani.Gameplay;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace BarafPaani.UI
{
    /// <summary>
    /// Draws the minimap for whoever is playing on this client.
    ///
    /// Blips are asked for from MapKnowledge rather than decided here, so what
    /// the map shows and what the AI acts on come from one rule. The catcher's
    /// blip in particular is read from MapAwareness — the position the server
    /// decided this player has earned — and never from the catcher's actual
    /// transform, which is present on every client so the model can be drawn.
    /// Reading the transform would make the server gating decorative.
    /// </summary>
    public class MinimapView : MonoBehaviour
    {
        [SerializeField]
        private RectTransform _blipArea;

        [SerializeField]
        private Image _blipPrefab;

        [Header("Arena")]
        [SerializeField]
        private Vector3 _arenaCentre;

        [SerializeField]
        [Tooltip("Metres from the centre of the map to its edge. Must match what the " +
                 "minimap camera actually frames, or blips drift out of step with the map.")]
        private float _mapExtent = 69f;

        [Header("Colours")]
        [SerializeField]
        private Color _self = new Color(0.3f, 1f, 0.4f);

        [SerializeField]
        private Color _catcher = new Color(0.95f, 0.25f, 0.25f);

        [SerializeField]
        private Color _runner = new Color(0.35f, 0.6f, 1f);

        [SerializeField]
        [Tooltip("Frozen team-mates, so a rescue target reads at a glance.")]
        private Color _frozen = new Color(0.6f, 0.85f, 1f);

        [SerializeField]
        [Tooltip("Redraw rate. Blips do not need to move every frame.")]
        private float _refreshInterval = 0.1f;

        private readonly Dictionary<uint, Image> _blips = new Dictionary<uint, Image>();
        private readonly List<uint> _stale = new List<uint>();

        private float _nextRefresh;

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh)
            {
                return;
            }

            _nextRefresh = Time.unscaledTime + _refreshInterval;
            Redraw();
        }

        private void Redraw()
        {
            NetworkIdentity local = NetworkClient.localPlayer;

            if (local == null || !local.TryGetComponent(out PlayerRole viewer))
            {
                HideAll();
                return;
            }

            local.TryGetComponent(out MapAwareness awareness);

            foreach (KeyValuePair<uint, Image> existing in _blips)
            {
                existing.Value.enabled = false;
            }

            foreach (NetworkIdentity identity in NetworkClient.spawned.Values)
            {
                if (identity == null || !identity.TryGetComponent(out PlayerRole role))
                {
                    continue;
                }

                if (!TryGetDrawPosition(identity, role, viewer, awareness, out Vector3 world))
                {
                    continue;
                }

                Image blip = GetBlip(identity.netId);
                blip.enabled = true;
                blip.color = ColourFor(identity, role, local);
                blip.rectTransform.anchoredPosition = ToMapPoint(world);
            }

            PruneMissing();
        }

        /// <summary>
        /// Where to draw this character, or false if the viewer is not entitled
        /// to know. The catcher is the only character whose drawn position is
        /// not simply its transform.
        /// </summary>
        private bool TryGetDrawPosition(
            NetworkIdentity identity,
            PlayerRole role,
            PlayerRole viewer,
            MapAwareness awareness,
            out Vector3 world)
        {
            if (!MapKnowledge.NeedsLineOfSight(viewer.Role, role.Role))
            {
                world = identity.transform.position;
                return true;
            }

            // A runner looking for the catcher gets only what the server sent.
            if (awareness != null && awareness.CatcherKnown)
            {
                world = awareness.CatcherPosition;
                return true;
            }

            world = Vector3.zero;
            return false;
        }

        private Color ColourFor(NetworkIdentity identity, PlayerRole role, NetworkIdentity local)
        {
            if (identity == local)
            {
                return _self;
            }

            if (role.Role == Role.Catcher)
            {
                return _catcher;
            }

            bool frozen = identity.TryGetComponent(out Freezable freezable) && freezable.IsFrozen;
            return frozen ? _frozen : _runner;
        }

        /// <summary>
        /// World position to a point on the map. The map shows a fixed area at a
        /// fixed scale rather than scrolling, so this is a straight linear
        /// mapping — but clamped to the disc rather than to a square, since the
        /// map is round. Anything beyond the edge pins to the rim instead of
        /// disappearing, which is how it stays useful when someone runs wide.
        /// </summary>
        private Vector2 ToMapPoint(Vector3 world)
        {
            Vector2 offset = new Vector2(
                (world.x - _arenaCentre.x) / _mapExtent,
                (world.z - _arenaCentre.z) / _mapExtent);

            // Leave room for the blip's own radius so it sits inside the frame
            // rather than straddling it.
            const float rim = 0.92f;

            if (offset.sqrMagnitude > rim * rim)
            {
                offset = offset.normalized * rim;
            }

            Rect area = _blipArea.rect;
            float radius = Mathf.Min(area.width, area.height) * 0.5f;

            return offset * radius;
        }

        private Image GetBlip(uint netId)
        {
            if (_blips.TryGetValue(netId, out Image existing) && existing != null)
            {
                return existing;
            }

            Image blip = Instantiate(_blipPrefab, _blipArea);
            blip.gameObject.name = $"Blip {netId}";
            _blips[netId] = blip;
            return blip;
        }

        /// <summary>Drops blips for characters that have left the game.</summary>
        private void PruneMissing()
        {
            _stale.Clear();

            foreach (KeyValuePair<uint, Image> pair in _blips)
            {
                if (pair.Value == null || !NetworkClient.spawned.ContainsKey(pair.Key))
                {
                    _stale.Add(pair.Key);
                }
            }

            foreach (uint netId in _stale)
            {
                if (_blips.TryGetValue(netId, out Image blip) && blip != null)
                {
                    Destroy(blip.gameObject);
                }

                _blips.Remove(netId);
            }
        }

        private void HideAll()
        {
            foreach (KeyValuePair<uint, Image> pair in _blips)
            {
                if (pair.Value != null)
                {
                    pair.Value.enabled = false;
                }
            }
        }
    }
}
