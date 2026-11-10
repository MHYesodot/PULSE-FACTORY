using UnityEngine;

namespace PulseFactory.Presentation
{
    public enum BoardPresentationMode
    {
        ExistingUi,
        Mechanical3DLevel2
    }

    [CreateAssetMenu(menuName = "Pulse Factory/Mechanical Board Settings")]
    public sealed class MechanicalBoardSettings : ScriptableObject
    {
        public BoardPresentationMode Mode = BoardPresentationMode.Mechanical3DLevel2;
        public MechanicalBoardAssets Assets;
    }
}
