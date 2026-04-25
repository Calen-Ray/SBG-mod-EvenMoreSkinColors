using UnityEngine;

namespace EvenMoreSkinColors
{
    internal sealed class SkinToneFollower : MonoBehaviour
    {
        private PlayerCosmetics _cosmetics;
        private bool _hasLastApplied;
        private SkinToneSelection _lastApplied;

        internal void Initialize(PlayerCosmetics cosmetics)
        {
            _cosmetics = cosmetics;
        }

        private void Update()
        {
            if (_cosmetics == null || _cosmetics.GetComponent<PlayerCosmeticsSwitcher>() == null || _cosmetics.netId == 0)
            {
                return;
            }

            if (_cosmetics.isLocalPlayer)
            {
                SkinToneSelection local = SkinToneState.LocalSelection;
                if (!_hasLastApplied || !_lastApplied.Equals(local))
                {
                    _hasLastApplied = true;
                    _lastApplied = local;
                    SkinToneState.TryApplyFor(_cosmetics);
                }

                return;
            }

            if (SkinToneState.TryGetRemoteSelection(_cosmetics.netId, out SkinToneSelection remote))
            {
                if (!_hasLastApplied || !_lastApplied.Equals(remote))
                {
                    _hasLastApplied = true;
                    _lastApplied = remote;
                    SkinToneState.TryApplyFor(_cosmetics);
                }
            }
            else if (_hasLastApplied)
            {
                _hasLastApplied = false;
                SkinToneState.RevertToVanilla(_cosmetics.GetComponent<PlayerCosmeticsSwitcher>());
            }
        }
    }
}
