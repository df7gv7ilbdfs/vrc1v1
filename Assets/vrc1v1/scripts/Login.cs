
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Login : UdonSharpBehaviour
{
    [SerializeField] private GameObject_MainNetworkHandler mainNetworkHandler;

    private void OnEnable()
    {
        mainNetworkHandler.PlayerVerify();
    }

    public void Enter()
    {
        mainNetworkHandler.PlayerWaitAssign();
        mainNetworkHandler.AppendPlayer(Networking.LocalPlayer.playerId);
    }
}