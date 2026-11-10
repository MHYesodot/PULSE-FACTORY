using UnityEngine;

namespace PulseFactory.Presentation
{
    public sealed class MechanicalBoardAssets : ScriptableObject
    {
        public GameObject ConveyorModule;
        public GameObject DiverterAssembly;
        public GameObject RedPackage;
        public GameObject BluePackage;
        public GameObject SourceStation;
        public GameObject RedDestination;
        public GameObject BlueDestination;
        public Material BoardDeck;
        public Material ActiveRoute;
        public Material InactiveRoute;
        public Material PackageRed;
        public Material PackageBlue;
    }
}
