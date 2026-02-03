using Reflex.Core;
using UnityEngine;

public class SampleSceneInstaller : MonoBehaviour, IInstaller
{
    public void InstallBindings(ContainerBuilder builder)
    {
        builder.AddSingleton("World");
    }
}