using UnityEngine;
using UnityEngine.SceneManagement;
using Reflex.Core;
using Reflex.Extensions;

public class ReflexSceneLoader : MonoBehaviour
{
    private void Start()
    {
        void InstallExtra(Scene scene, ContainerBuilder builder)
        {
            builder.AddSingleton("Beautiful");
        }
        var logger_scope = gameObject.scene.GetSceneContainer().Resolve<ILogger>();
        logger_scope.Log($"From logger_scope binding in scene {gameObject.scene.name}");
        // This way you can access ContainerBuilder of the scene that is currently building
        SceneScope.OnSceneContainerBuilding += InstallExtra;

        // If you are loading scenes without addressables
        SceneManager.LoadSceneAsync("ReflexDI").completed += operation =>
        {
            SceneScope.OnSceneContainerBuilding -= InstallExtra;
        };

        // If you are loading scenes with addressables
        // Addressables.LoadSceneAsync("Greet").Completed += operation =>
        // {
        //     SceneScope.OnSceneContainerBuilding -= InstallExtra;
        // };
    }
}