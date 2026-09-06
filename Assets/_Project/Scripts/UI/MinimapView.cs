using System.Collections.Generic;
using BarafPaani.Gameplay;
using BarafPaani.Gameplay.PowerUps;
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

        [Header("Map")]
        [SerializeField]
        [Tooltip("The camera drawing the map. Blips take their centre and scale from it, " +
                 "so the two cannot disagree about what the map is showing.")]
        private Camera _mapCamera;

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

            if (_mapCamera == null || local == null || !local.TryGetComponent(out PlayerRole viewer))
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
            // Never yourself: going invisible should not lose you your own blip
            // and leave you unable to read your own map.
            if (identity != viewer.netIdentity && PowerUpEffects.IsHidden(role))
            {
                // Not even your own team-mates, and not the catcher's otherwise
                // unlimited view of the runners. MapKnowledge owns the rule.
                world = Vector3.zero;
                return false;
            }

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
        /// World position to a point on the map.
        ///
        /// Centre and scale are read off the map camera rather than kept as
        /// their own numbers here. When they were separate, widening the camera
        /// left the blips mapping against the old extent and they drifted out of
        /// step with the streets underneath — worst at the edges. Taking both
        /// from the camera means they cannot disagree.
        ///
        /// Clamped to the disc rather than a square, since the map is round.
        /// Anything past the edge pins to the rim instead of disappearing, which
        /// is what keeps it useful once the map only shows a neighbourhood.
        /// </summary>
        private Vector2 ToMapPoint(Vector3 world)
        {
            Vector3 centre = _mapCamera.transform.position;
            float extent = _mapCamera.orthographicSize;

            Vector2 offset = new Vector2(
                (world.x - centre.x) / extent,
                (world.z - centre.z) / extent);

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

            // worldPositionStays false: the UI default preserves world position
            // instead, which rescales the clone against the canvas and would size
            // blips wrongly the moment the template stops sharing a parent.
            Image blip = Instantiate(_blipPrefab, _blipArea, false);
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
