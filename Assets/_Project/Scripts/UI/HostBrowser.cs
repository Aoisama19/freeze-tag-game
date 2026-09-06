using System.Collections.Generic;
using BarafPaani.Core;
using Mirror;
using Mirror.Discovery;
using UnityEngine;
using UnityEngine.UI;

namespace BarafPaani.UI
{
    /// <summary>
    /// Lists the matches being hosted on this network, and joins the one you
    /// pick.
    ///
    /// Hosts broadcast on their own UDP port and clients ask every few seconds;
    /// none of it touches the game's own connection, so a found address is
    /// simply the address you would otherwise have typed. Typing one is still
    /// here, and still the thing that always works — broadcast does not cross
    /// subnets, does not reach the internet, and is blocked outright on a fair
    /// number of university and office networks.
    /// </summary>
    public class HostBrowser : MonoBehaviour
    {
        [SerializeField]
        private NetworkDiscovery _discovery;

        [SerializeField]
        [Tooltip("Rows are added under here.")]
        private RectTransform _listRoot;

        [SerializeField]
        private Text _emptyLabel;

        [SerializeField]
        private InputField _addressField;

        [SerializeField]
        private Button _connectButton;

        [SerializeField]
        private ConnectingScreen _screen;

        [SerializeField]
        private MatchSetup _setup;

        [SerializeField]
        private Font _font;

        [SerializeField]
        [Tooltip("A host that has not answered for this long has gone.")]
        private float _forgetAfterSeconds = 10f;

        private readonly Dictionary<long, Host> _hosts = new Dictionary<long, Host>();
        private readonly List<GameObject> _rows = new List<GameObject>();

        private bool _dirty = true;
        private bool _joining;

        private readonly struct Host
        {
            public readonly string Address;
            public readonly float SeenAt;

            public Host(string address, float seenAt)
            {
                Address = address;
                SeenAt = seenAt;
            }
        }

        private void Start()
        {
            if (_addressField != null && _setup != null)
            {
                // Whatever was typed in the menu, so a player who knows the
                // address does not have to type it twice.
                _addressField.text = _setup.JoinAddress;
            }

            // Consumed here rather than by a launcher: arriving on this screen
            // is not a request to connect to anything yet.
            _setup?.ClearRequest();

            if (_connectButton != null)
            {
                _connectButton.onClick.RemoveAllListeners();
                _connectButton.onClick.AddListener(ConnectToTyped);
            }

            if (_discovery == null)
            {
                Debug.LogWarning("No NetworkDiscovery, so no hosts will be found.", this);
                return;
            }

            _discovery.OnServerFound.RemoveListener(Found);
            _discovery.OnServerFound.AddListener(Found);
            _discovery.StartDiscovery();
        }

        private void OnDestroy()
        {
            if (_discovery != null)
            {
                _discovery.OnServerFound.RemoveListener(Found);
                _discovery.StopDiscovery();
            }
        }

        private void Found(ServerResponse response)
        {
            // Keyed by the server's own id rather than by address: a host with
            // more than one network card answers once per card, and all of them
            // are the same match.
            _hosts[response.serverId] = new Host(response.uri.Host, Time.time);
            _dirty = true;
        }

        private void Update()
        {
            if (_joining)
            {
                return;
            }

            Forget();

            if (_dirty)
            {
                _dirty = false;
                Redraw();
            }
        }

        private void Forget()
        {
            List<long> gone = null;

            foreach (KeyValuePair<long, Host> entry in _hosts)
            {
                if (Time.time - entry.Value.SeenAt <= _forgetAfterSeconds)
                {
                    continue;
                }

                gone ??= new List<long>();
                gone.Add(entry.Key);
            }

            if (gone == null)
            {
                return;
            }

            foreach (long id in gone)
            {
                _hosts.Remove(id);
            }

            _dirty = true;
        }

        private void Redraw()
        {
            foreach (GameObject row in _rows)
            {
                Destroy(row);
            }

            _rows.Clear();

            if (_emptyLabel != null)
            {
                _emptyLabel.enabled = _hosts.Count == 0;

                if (_hosts.Count == 0)
                {
                    _emptyLabel.text =
                        "Looking for matches on this network...\nOr type an address below.";
                }
            }

            if (_listRoot == null)
            {
                return;
            }

            int index = 0;

            foreach (KeyValuePair<long, Host> entry in _hosts)
            {
                _rows.Add(BuildRow(entry.Value.Address, index));
                index++;
            }
        }

        private GameObject BuildRow(string address, int index)
        {
            GameObject row = new GameObject($"Host {index + 1}");
            row.transform.SetParent(_listRoot, false);

            Image plate = row.AddComponent<Image>();
            plate.color = new Color(1f, 1f, 1f, 0.10f);

            RectTransform rect = row.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -index * 56f);
            rect.sizeDelta = new Vector2(620f, 48f);

            Button button = row.AddComponent<Button>();
            button.targetGraphic = plate;

            // Captured by value, so every row joins its own host rather than
            // whichever one the loop finished on.
            string target = address;
            button.onClick.AddListener(() => Connect(target));

            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(row.transform, false);

            Text label = labelObject.AddComponent<Text>();
            label.font = _font != null
                ? _font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            label.fontSize = 22;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = $"Match at {address}";

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            return row;
        }

        private void ConnectToTyped()
        {
            string typed = _addressField != null ? _addressField.text : null;

            Connect(string.IsNullOrWhiteSpace(typed) ? "localhost" : typed.Trim());
        }

        private void Connect(string address)
        {
            if (_joining)
            {
                return;
            }

            _joining = true;

            // Stopped before connecting: there is no reason to keep shouting on
            // the network once we know where we are going.
            if (_discovery != null)
            {
                _discovery.StopDiscovery();
            }

            _screen?.AttemptStarted(address);

            if (NetworkManager.singleton is GameNetworkManager manager)
            {
                manager.JoinMultiplayer(address);
                return;
            }

            Debug.LogError("No GameNetworkManager here, so joining is not possible.", this);
        }
    }
}
