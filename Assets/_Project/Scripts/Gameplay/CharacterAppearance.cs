using Mirror;
using UnityEngine;

namespace BarafPaani.Gameplay
{
    /// <summary>
    /// How a character is coloured: which side it is on, and whether it is
    /// frozen.
    ///
    /// Split out from Freezable because the two questions have different owners.
    /// Freeze is a rule the server decides; a colour is only ever a way of
    /// showing it, and now that a character is a skinned model with more than
    /// one renderer, that showing is enough work to be worth its own component.
    ///
    /// Colour goes through a MaterialPropertyBlock rather than renderer.material,
    /// which would clone the shared material once per character and leak the
    /// clone. The block writes both the URP and the built-in colour property so
    /// this does not quietly stop working if a material changes shader.
    /// </summary>
    public class CharacterAppearance : NetworkBehaviour
    {
        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColour = Shader.PropertyToID("_Color");

        [SerializeField]
        [Tooltip("Every renderer that makes up the body.")]
        private Renderer[] _renderers;

        [SerializeField]
        private Color _catcherColour = new Color(0.85f, 0.27f, 0.22f);

        [SerializeField]
        private Color _runnerColour = new Color(0.83f, 0.84f, 0.86f);

        [SerializeField]
        private Color _frozenColour = new Color(0.55f, 0.8f, 1f);

        private PlayerRole _role;
        private MaterialPropertyBlock _block;
        private bool _frozen;

        private void Awake()
        {
            _role = GetComponent<PlayerRole>();
            _block = new MaterialPropertyBlock();

            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<Renderer>();
            }
        }

        // Roles are set before the character is spawned, so by the time either
        // of these runs the side is already known and correct.
        public override void OnStartServer()
        {
            Refresh();
        }

        public override void OnStartClient()
        {
            Refresh();
        }

        public void SetFrozen(bool frozen)
        {
            _frozen = frozen;
            Refresh();
        }

        private void Refresh()
        {
            if (_renderers == null || _block == null)
            {
                return;
            }

            Color colour = _frozen
                ? _frozenColour
                : _role != null && _role.Role == Role.Catcher
                    ? _catcherColour
                    : _runnerColour;

            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColour, colour);
                _block.SetColor(LegacyColour, colour);
                renderer.SetPropertyBlock(_block);
            }
        }
    }
}
