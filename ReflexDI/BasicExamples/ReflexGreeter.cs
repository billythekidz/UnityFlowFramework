using Reflex.Attributes;
using System.Collections.Generic;
using Reflex.Extensions;
using UnityEngine;

public class ReflexGreeter : MonoBehaviour
{
    [Inject] private readonly IEnumerable<string> strings;
    [Inject] ILogger logger;
    [Inject] ILogger logger_transient;
    [Inject] private IEnumerable<ILogger> allLoggers;
    void Start()
    {
        logger.Log(string.Join(" ", strings));

        int i = 1;
        foreach (var logger in allLoggers)
        {
            logger.Log($"From IEnumerable<ILogger> binding #{i++}");
        }
        
        logger_transient.Log($"From logger_transient binding");
        
        var logger_scope = gameObject.scene.GetSceneContainer().Resolve<ILogger>();
        logger_scope.Log($"From logger_scope binding in scene {gameObject.scene.name}");
    }
}